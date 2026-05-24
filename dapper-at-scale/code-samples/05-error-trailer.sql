-- The error contract every stored procedure in the codebase follows:
--   * SET NOCOUNT ON
--   * @ErrorMessage NVARCHAR(MAX) OUTPUT and @ErrorFound BIT OUTPUT trailers
--   * default to "no error", then BEGIN TRY / BEGIN CATCH that reports via the
--     output params instead of THROW
--
-- IDapperBase reads @ErrorFound / @ErrorMessage after every call, so the
-- repository layer never has to parse a raw SqlException to know what happened.

CREATE PROCEDURE [dbo].[RefreshTokens_Create]
    @TokenId       UNIQUEIDENTIFIER,
    @UserId        UNIQUEIDENTIFIER,
    @TokenHash     NVARCHAR(256),
    @FamilyId      UNIQUEIDENTIFIER,
    @CodeChallenge NVARCHAR(256),         -- note: no "= NULL" default. See article.
    @ExpiresAt     DATETIME2(7),
    @ErrorMessage  NVARCHAR(MAX) OUTPUT,
    @ErrorFound    BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ErrorFound = 0;
    SET @ErrorMessage = '';

    BEGIN TRY
        INSERT INTO RefreshTokens (TokenId, UserId, TokenHash, FamilyId, CodeChallenge, ExpiresAt, CreatedAt)
        OUTPUT INSERTED.TokenId, INSERTED.UserId, INSERTED.TokenHash, INSERTED.FamilyId, INSERTED.CodeChallenge, INSERTED.ExpiresAt, INSERTED.RevokedAt, INSERTED.CreatedAt
        VALUES (@TokenId, @UserId, @TokenHash, @FamilyId, @CodeChallenge, @ExpiresAt, SYSUTCDATETIME());
    END TRY
    BEGIN CATCH
        SET @ErrorFound = 1;
        SET @ErrorMessage = ERROR_MESSAGE();
    END CATCH
END
