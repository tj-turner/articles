-- Output from the prompt in 03-prompt-template.md, with the conventions doc
-- and a sibling Insert proc as the exemplar. Compare against 01-bad-generation.sql.
--
-- Note what changed:
--   * @NewId / @ErrorMessage / @ErrorFound output params — matches DapperBase
--   * outputs initialized before BEGIN TRY
--   * CATCH sets @ErrorFound and @ErrorMessage, never THROWs
--   * parameter names line up with CreateGrantProgramParams
--   * audit timestamp set inside the proc, not accepted as a parameter
--   * bracketed identifiers, explicit column list, no SELECT *
--   * @DefaultCurrencyCode defaulted to N'USD'

-- Creates a new grant program within a tenant. Budget + window fields are optional
-- (programs often start in Draft with just a name/description).
-- Slug uniqueness within tenant is enforced by UX_GrantProgram_TenantId_Slug.
CREATE PROCEDURE [dbo].[GrantProgram_Insert]
    @TenantId                INT,
    @Name                    NVARCHAR(300),
    @Slug                    NVARCHAR(200),
    @Description             NVARCHAR(MAX),
    @FundingSource           NVARCHAR(50),
    @ProgramGuidelines       NVARCHAR(MAX)   = NULL,
    @CFDA                    NVARCHAR(20)    = NULL,
    @DefaultCurrencyCode     NVARCHAR(3)     = N'USD',
    @FiscalYear              INT             = NULL,
    @TotalBudget             DECIMAL(18,2)   = NULL,
    @MinAwardAmount          DECIMAL(18,2)   = NULL,
    @MaxAwardAmount          DECIMAL(18,2)   = NULL,
    @MatchRequired           BIT             = 0,
    @MatchPercentage         DECIMAL(5,2)    = NULL,
    @ApplicationOpenDate     DATETIMEOFFSET  = NULL,
    @ApplicationCloseDate    DATETIMEOFFSET  = NULL,
    @IsRollingDeadline       BIT             = 0,
    @AllowLateSubmissions    BIT             = 0,
    @MaxApplicationsPerOrg   INT             = NULL,
    @RequiresLOI             BIT             = 0,
    @ProgramStatus           NVARCHAR(50)    = N'Draft',
    @PortalVisibility        NVARCHAR(50)    = N'Public',
    @CreatedBy               NVARCHAR(100),
    @NewId                   INT             OUTPUT,
    @ErrorMessage            NVARCHAR(MAX)   OUTPUT,
    @ErrorFound              BIT             OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ErrorFound   = 0;
    SET @ErrorMessage = NULL;
    SET @NewId        = 0;

    BEGIN TRY
        INSERT INTO [dbo].[GrantProgram] (
            [TenantId], [Name], [Slug], [Description], [ProgramGuidelines],
            [FundingSource], [CFDA], [DefaultCurrencyCode], [FiscalYear],
            [TotalBudget], [AllocatedAmount], [RemainingAmount],
            [MinAwardAmount], [MaxAwardAmount],
            [MatchRequired], [MatchPercentage],
            [ApplicationOpenDate], [ApplicationCloseDate], [IsRollingDeadline], [AllowLateSubmissions],
            [MaxApplicationsPerOrg], [RequiresLOI],
            [ProgramStatus], [PortalVisibility],
            [CreatedBy], [CreatedDate]
        )
        VALUES (
            @TenantId, @Name, @Slug, @Description, @ProgramGuidelines,
            @FundingSource, @CFDA, @DefaultCurrencyCode, @FiscalYear,
            @TotalBudget, 0, @TotalBudget,
            @MinAwardAmount, @MaxAwardAmount,
            @MatchRequired, @MatchPercentage,
            @ApplicationOpenDate, @ApplicationCloseDate, @IsRollingDeadline, @AllowLateSubmissions,
            @MaxApplicationsPerOrg, @RequiresLOI,
            @ProgramStatus, @PortalVisibility,
            @CreatedBy, SYSDATETIMEOFFSET()
        );

        SET @NewId = CAST(SCOPE_IDENTITY() AS INT);
    END TRY
    BEGIN CATCH
        SET @ErrorFound   = 1;
        SET @ErrorMessage = ERROR_MESSAGE();
    END CATCH
END
GO
