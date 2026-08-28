-- The single state-transition procedure for the task tier.
--
-- Every status change in the task lifecycle goes through this one procedure:
--     Pending -> Running -> Succeeded | Failed | TimedOut
--
-- The guard is the whole design. @FromStatus is in the WHERE clause, so the
-- transition either matches the row's current state or it changes nothing, and
-- @@ROWCOUNT is the answer. A worker asks to move a task from Pending to
-- Running and finds out whether it won.
--
-- That makes deduplication a property of the row rather than a job the worker
-- has to remember to do. Service Bus redelivers on its own schedule; a second
-- delivery calls this with @FromStatus = 'Pending' against a row that is
-- already Running, matches nothing, and gets @Transitioned = 0 back.
--
-- No THROW on a losing transition. Losing is an expected outcome here, not an
-- error - it is what a duplicate delivery is supposed to look like.

CREATE OR ALTER PROCEDURE dbo.usp_AiTask_UpdateStatus
    @TaskId        UNIQUEIDENTIFIER,
    @FromStatus    VARCHAR(16),
    @ToStatus      VARCHAR(16),
    @WorkerId      NVARCHAR(100)  = NULL,
    @FailureReason NVARCHAR(1000) = NULL,
    @Transitioned  BIT            OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Transitioned = 0;

    UPDATE dbo.AiTasks
       SET Status        = @ToStatus,
           WorkerId      = COALESCE(@WorkerId, WorkerId),
           FailureReason = @FailureReason,
           ClaimedUtc    = CASE WHEN @ToStatus = 'Running'
                                THEN SYSUTCDATETIME() ELSE ClaimedUtc END,
           CompletedUtc  = CASE WHEN @ToStatus IN ('Succeeded', 'Failed', 'TimedOut')
                                THEN SYSUTCDATETIME() ELSE NULL END,
           RowVersionUtc = SYSUTCDATETIME()
     WHERE TaskId = @TaskId
       AND Status = @FromStatus;

    SET @Transitioned = CASE @@ROWCOUNT WHEN 1 THEN 1 ELSE 0 END;
END
GO
