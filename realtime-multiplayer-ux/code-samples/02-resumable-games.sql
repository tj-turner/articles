-- The "door back in." When a player reconnects (closed tab, killed debug
-- session, swapped devices), the lobby calls this to find the games they're
-- still seated at. Without it, a running InProgress game the player is still
-- a member of is invisible to them — the server knows they belong, the UI
-- offers no way to return.
--
-- Note the status filter: both Waiting AND InProgress are resumable. The
-- StaleGameSweepService (see 03) only reaps Waiting orphans; an InProgress
-- game needs the opposite of reaping — a way back in, not a cleanup.
--
-- Carries the same @ErrorMessage / @ErrorFound output contract every proc in
-- this codebase uses (see the previous article in the series).
--
-- Trimmed from UpAllNight.Database StoredProcedures/Games_GetResumableByUserId.sql.

CREATE PROCEDURE [dbo].[Games_GetResumableByUserId]
    @UserId       UNIQUEIDENTIFIER,
    @ErrorMessage NVARCHAR(MAX) OUTPUT,
    @ErrorFound   BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ErrorFound = 0;
    SET @ErrorMessage = '';

    BEGIN TRY
        SELECT g.GameId, g.Status, g.RoundNumber, g.PlayerCount, g.DealerPlayerId,
               g.CurrentTurnPlayerId, g.HasDrawnThisTurn, g.JoinCode, g.IsPrivate,
               g.CreatedAt, g.CompletedAt
        FROM Games g
        INNER JOIN GamePlayers gp ON gp.GameId = g.GameId
        WHERE gp.UserId = @UserId
          AND g.Status IN ('Waiting', 'InProgress')   -- both are resumable
        ORDER BY g.CreatedAt DESC;
    END TRY
    BEGIN CATCH
        SET @ErrorFound = 1;
        SET @ErrorMessage = ERROR_MESSAGE();
    END CATCH
END
