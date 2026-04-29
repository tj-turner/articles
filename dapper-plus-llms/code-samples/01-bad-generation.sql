-- What you get from a vague prompt to Codex / Claude / Copilot:
-- "Write me a SQL Server stored procedure to insert a grant program
--  and one to get it by id. We use Dapper."
--
-- This is reasonable T-SQL. It is the wrong contract for a codebase whose
-- DapperBase expects @ErrorFound / @ErrorMessage / @NewId output params and
-- procs that never THROW. See the article for the full grade.

CREATE PROCEDURE GrantProgram_Insert
    @TenantId       INT,
    @Name           NVARCHAR(300),
    @Slug           NVARCHAR(200),
    @Description    NVARCHAR(MAX),
    @FundingSource  NVARCHAR(50),
    @CreatedBy      NVARCHAR(100),
    @CreatedDate    DATETIMEOFFSET            -- caller shouldn't be sending this
AS
BEGIN
    BEGIN TRY
        INSERT INTO GrantProgram (TenantId, Name, Slug, Description, FundingSource, CreatedBy, CreatedDate)
        VALUES (@TenantId, @Name, @Slug, @Description, @FundingSource, @CreatedBy, @CreatedDate);

        SELECT SCOPE_IDENTITY() AS NewId;     -- result set, not an OUTPUT param
    END TRY
    BEGIN CATCH
        THROW;                                -- breaks DapperBase's error contract
    END CATCH
END
GO

CREATE PROCEDURE GrantProgram_GetById
    @ProgID INT                               -- won't bind to GrantProgramId property
AS
BEGIN
    SELECT *                                  -- explicit columns, please
    FROM GrantProgram WITH (NOLOCK)           -- cargo-culted onto a PK lookup
    WHERE GrantProgramId = @ProgID
END
GO
