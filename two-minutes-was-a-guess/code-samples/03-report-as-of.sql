-- The warehouse report, and the timestamp that keeps it honest.
--
-- Written in the Azure SQL dialect, as the data-owner service reads it. A
-- warehouse engine's T-SQL surface differs — keys there are NOT ENFORCED, so the
-- refresh has to be a MERGE or duplicates make the as-of non-deterministic on the
-- one table whose job is to be authoritative about freshness, and time zone
-- conversion belongs in the renderer rather than here.
--
-- The reporting tables are built by a scheduled job, so a report off them is
-- behind the transactional source. The mitigation is not to chase the lag; it is
-- to state it, and to refuse rather than render a page of numbers carrying no
-- claim about when they were true.
--
-- MIT licensed, like everything in this folder.

-- The reporting table, at payable x recipient grain. Amounts at this grain
-- partition and sum; a payable-level amount would repeat across a payable's
-- recipients and overstate.
--
-- Clustered on the way it is read, not on its identity. Every query here filters
-- a business and orders by due date; a GUID primary key as the clustered index
-- gives a full scan per report and scatters the refresh's writes across the whole
-- tree. On a warehouse engine this is a clustered columnstore instead, which is
-- what "columnar" has to mean if an article claims it.
CREATE TABLE dbo.PayablesByRecipient
(
    BusinessId      UNIQUEIDENTIFIER NOT NULL,
    DueUtc          DATETIME2(3)     NOT NULL,
    PayableId       UNIQUEIDENTIFIER NOT NULL,
    RecipientId     UNIQUEIDENTIFIER NOT NULL,
    RecipientAmount DECIMAL(19, 4)   NOT NULL,
    CurrencyCode    CHAR(3)          NOT NULL,   -- summing across currencies is the oldest money bug there is
    CONSTRAINT PK_PayablesByRecipient PRIMARY KEY NONCLUSTERED (PayableId, RecipientId)
);
GO

CREATE CLUSTERED INDEX CX_PayablesByRecipient
    ON dbo.PayablesByRecipient (BusinessId, DueUtc, PayableId, RecipientId);
GO

-- One row per reporting table. Written by the refresh job, read by every report.
CREATE TABLE dbo.ReportSyncState
(
    TableName              VARCHAR(128) NOT NULL CONSTRAINT PK_ReportSyncState PRIMARY KEY,
    -- The high-water mark of the source data this refresh actually covered, not the
    -- time the job finished. A completion time claims more freshness than the rows
    -- have, by the job's own duration plus the replication lag behind it. Where a
    -- table is built from several sources, this is the minimum across them: several
    -- databases have no common consistency point.
    CoveredThroughUtc      DATETIME2(3) NOT NULL,
    RefreshIntervalSeconds INT          NOT NULL,   -- the cadence, not the pipe's latency
    RefreshRunId           UNIQUEIDENTIFIER NULL    -- which run produced these numbers
);
GO

CREATE OR ALTER PROCEDURE dbo.usp_Report_PayablesByRecipient
    @BusinessId    UNIQUEIDENTIFIER,
    -- A page size for the detail read, not a cap on the answer. The caller loops
    -- pages and totals as it goes, so the summary section — payables by status, and
    -- the chart over it — covers every row the business has, while only the detail
    -- table is capped (800 rows when this was written, 100 now, and an export beyond
    -- that). Each call is its own snapshot, so the as-of can move between pages: the
    -- loop has to decide what to do about that, because this proc cannot.
    @MaxRows       INT = 2000,
    @ErrorFound    BIT           = 0 OUTPUT,
    @ErrorMessage  NVARCHAR(400) = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ErrorFound = 0;
    SET @ErrorMessage = NULL;   -- or a healthy call carries the last failure's text

    -- One snapshot for the as-of and the rows, or a refresh committing between them
    -- renders rows that disagree with their own timestamp. The proc exists to be
    -- authoritative about which moment the reader is looking at; two reads at read
    -- committed cannot be.
    SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
    BEGIN TRAN;

    DECLARE @AsOfUtc DATETIME2(3), @IntervalSeconds INT;

    SELECT @AsOfUtc         = CoveredThroughUtc,
           @IntervalSeconds = RefreshIntervalSeconds
      FROM dbo.ReportSyncState
     WHERE TableName = 'PayablesByRecipient';

    -- No sync state means nobody can say when this data was true, so the report does
    -- not render. A report with no as-of line is worse than no report: it looks live.
    IF @AsOfUtc IS NULL
    BEGIN
        SET @ErrorFound = 1;
        SET @ErrorMessage = N'No sync state for PayablesByRecipient; refusing to render without an as-of.';
        ROLLBACK TRAN;
        RETURN;
    END

    -- Stale in either direction. Behind, because the likelier failure is not missing
    -- sync state but a refresh that stopped while the row stayed put, every report
    -- afterwards carrying a confident, recent-looking, hours-old as-of. Ahead,
    -- because the mark is written by another system's clock through a replica, and a
    -- future as-of claims more freshness than any real data could have.
    --
    -- Four intervals is a placeholder, and it has not tripped. Two is the most we
    -- have ever been behind, during maintenance on the sync itself, and the same
    -- pattern runs for several processes in Fabric — so four behind points at the
    -- syncing rather than at this report. The multiple at which refusal should start
    -- is still undecided, and refusing is not obviously right either: it turns one
    -- stalled refresh into an outage for every tenant, where a loud stale banner
    -- would not.
    IF @AsOfUtc > SYSUTCDATETIME()
        OR DATEDIFF(SECOND, @AsOfUtc, SYSUTCDATETIME()) > @IntervalSeconds * 4
    BEGIN
        SET @ErrorFound = 1;
        SET @ErrorMessage = N'Sync state for PayablesByRecipient is outside tolerance; refusing to render.';
        ROLLBACK TRAN;
        RETURN;
    END

    -- The count comes from the same statement as the rows, so the header cannot
    -- disagree with what shipped, and the denominator is this business's rather than
    -- the table's. A table-wide count beside rows filtered to one business is wrong
    -- by the number of tenants.
    --
    -- Staleness is derived at render, never stored: a result row is read for as long
    -- as the conversation exists, and a stored "fifteen minutes ago" is wrong the
    -- moment it is written. AsOfUtc survives storage.
    SELECT TOP (@MaxRows)
           AsOfUtc          = @AsOfUtc,
           BusinessRowCount = COUNT(*) OVER (),
           r.PayableId,
           r.RecipientId,
           r.RecipientAmount,
           r.CurrencyCode,
           r.DueUtc
      FROM dbo.PayablesByRecipient AS r
     WHERE r.BusinessId = @BusinessId
     -- Deterministic, which an OFFSET walk over the transactional source never was.
     -- The tiebreaker is not what repaired that, though: what repaired it is that
     -- this is one read rather than sixteen over a moving set.
     ORDER BY r.DueUtc, r.PayableId, r.RecipientId;

    COMMIT TRAN;
END
GO
