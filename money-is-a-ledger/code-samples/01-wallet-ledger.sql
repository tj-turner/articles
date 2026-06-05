-- The wallet adjustment SP. Every economic event in UpAllNight — game wins,
-- daily logins, item purchases, real-money grants — goes through this proc.
--
-- Three things matter and they all live in one transaction:
--   1. UPDLOCK + ROWLOCK on the wallet row, so two concurrent credits can't
--      read-modify-write over each other.
--   2. BalanceAfter snapshotted into the ledger row, so every transaction
--      carries the running balance it produced (no need to re-derive it
--      from a sum of deltas after the fact).
--   3. Lifetime rollups updated in the same UPDATE, branched by Reason.
--      One write, one truth — the rollups cannot drift from the ledger
--      because they ARE the ledger, totalled in the same statement.
--
-- The ordering inside the transaction is intentional: lock, compute new
-- balance, UPDATE the wallet cache, then INSERT the ledger row with
-- BalanceAfter = the value we just wrote. The ledger row is a witness to
-- what happened, not a request for what's about to.
--
-- Reason enum (lives in C# as PaymentReason; the CASE expression below is
-- the second copy of the bucket-assignment rule — see article §3):
--   1=SignupGrant, 2=GameWin, 3=GameLoss, 4=FirstWinOfDay, 5=DailyLogin,
--   6=RealMoneyPurchase, 7=ItemPurchase, 8=BundlePurchase, 9=Refund,
--   10=RefundClawback, 11=AdminAdjustment, 12=BackfillGrant.
-- Codes 9 and 10 are intentionally outside every rollup bucket today.
--
-- From UpAllNight.Database/StoredProcedures/Wallet_AdjustWithLedger.sql.

CREATE PROCEDURE [dbo].[Wallet_AdjustWithLedger]
    @UserId          UNIQUEIDENTIFIER,
    @Delta           BIGINT,
    @Reason          TINYINT,
    @RelatedEntityId UNIQUEIDENTIFIER = NULL,
    @Notes           NVARCHAR(500)   = NULL,
    @ErrorMessage    NVARCHAR(MAX)   OUTPUT,
    @ErrorFound      BIT             OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ErrorFound = 0;
    SET @ErrorMessage = NULL;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @WalletId UNIQUEIDENTIFIER;
        DECLARE @NewBalance BIGINT;

        -- Row lock held for the duration of the tran. Concurrent adjusts
        -- for the same wallet serialize here.
        SELECT @WalletId = WalletId, @NewBalance = ChipBalance
        FROM Wallets WITH (UPDLOCK, ROWLOCK)
        WHERE UserId = @UserId;

        IF @WalletId IS NULL
        BEGIN
            -- Auto-create on first credit so callers don't need to pre-ensure
            -- a wallet exists. Trade-off: a typo'd @UserId silently mints a
            -- wallet instead of failing loud. The FK to Users catches genuinely
            -- bad ids; we accepted that for caller ergonomics.
            INSERT INTO Wallets (UserId) VALUES (@UserId);
            SELECT @WalletId = WalletId, @NewBalance = ChipBalance
            FROM Wallets WHERE UserId = @UserId;
        END

        SET @NewBalance = @NewBalance + @Delta;

        -- Balance cache + lifetime rollups in one UPDATE. The CASE expression
        -- is the place new Reason codes silently skip the rollup if you forget
        -- to update it (the trap the article calls out in §3).
        UPDATE Wallets
        SET ChipBalance       = @NewBalance,
            LifetimeEarned    = LifetimeEarned    + CASE WHEN @Delta > 0 AND @Reason IN (1,2,3,4,5,11,12) THEN @Delta ELSE 0 END,
            LifetimePurchased = LifetimePurchased + CASE WHEN @Delta > 0 AND @Reason = 6                  THEN @Delta ELSE 0 END,
            LifetimeSpent     = LifetimeSpent     + CASE WHEN @Delta < 0 AND @Reason IN (7,8)             THEN -@Delta ELSE 0 END,
            UpdatedAt         = SYSUTCDATETIME()
        WHERE WalletId = @WalletId;

        DECLARE @TxId UNIQUEIDENTIFIER = NEWID();

        -- The append-only ledger row. BalanceAfter is the value we just wrote
        -- to the wallet, frozen here so any later audit can answer
        -- "what was the balance the instant after this event?" without
        -- re-running every prior delta.
        INSERT INTO WalletTransactions (WalletTransactionId, WalletId, Delta, BalanceAfter, Reason, RelatedEntityId, Notes)
        VALUES (@TxId, @WalletId, @Delta, @NewBalance, @Reason, @RelatedEntityId, @Notes);

        COMMIT TRAN;

        SELECT @TxId AS WalletTransactionId, @WalletId AS WalletId, @NewBalance AS BalanceAfter;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        SET @ErrorFound = 1;
        SET @ErrorMessage = ERROR_MESSAGE();
    END CATCH
END
