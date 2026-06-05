-- The grant SP that turns "a webhook fired" into "the player has chips,"
-- and the unique index that lets it run twice without granting twice.
--
-- This is the seed-script idempotency lesson (Part 1 §4) applied where the
-- stakes are real: webhooks re-deliver. Apple re-notifies. Google's billing
-- client re-posts on app resume. Without the (Provider, ProviderTransactionId)
-- key and a Status check, a re-delivered event would mint a second grant.
--
-- The Status code the SP returns is what tells the caller whether this call
-- actually moved the wallet (1 = Granted this call) or just observed an
-- existing one (2 = AlreadyGranted). Both are success; only 1 sends a
-- "you got chips" notification to the client.
--
-- From UpAllNight.Database/StoredProcedures/ChipPackPurchase_GrantChips.sql
-- and the unique index on Tables/ChipPackPurchases.sql.

-- The constraint that makes the whole pattern work.
CREATE UNIQUE INDEX [UX_ChipPackPurchases_Provider_TransactionId]
    ON [dbo].[ChipPackPurchases] ([Provider], [ProviderTransactionId]);
GO

CREATE PROCEDURE [dbo].[ChipPackPurchase_GrantChips]
    @Provider               TINYINT,
    @ProviderTransactionId  NVARCHAR(200),
    @ProviderReceiptPayload NVARCHAR(MAX) = NULL,
    @ErrorMessage           NVARCHAR(MAX) OUTPUT,
    @ErrorFound             BIT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @ErrorFound = 0;
    SET @ErrorMessage = NULL;

    -- Idempotent grant. Looks up the pending row by (Provider, ProviderTransactionId),
    -- credits the wallet, links the WalletTransaction id, marks Granted.
    -- Re-running is a no-op (returns the existing row).
    --
    -- Result shape: (Status, BalanceAfter, ChipsGranted)
    -- Status codes: 1=Granted (this call), 2=AlreadyGranted, 3=NotFound, 4=Refunded

    BEGIN TRY
        DECLARE @PurchaseId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER,
                @ChipPackId UNIQUEIDENTIFIER, @ExistingStatus TINYINT,
                @ExistingTxId UNIQUEIDENTIFIER;

        SELECT @PurchaseId    = ChipPackPurchaseId,
               @UserId        = UserId,
               @ChipPackId    = ChipPackId,
               @ExistingStatus= Status,
               @ExistingTxId  = WalletTransactionId
        FROM ChipPackPurchases
        WHERE Provider = @Provider AND ProviderTransactionId = @ProviderTransactionId;

        IF @PurchaseId IS NULL
        BEGIN
            -- Webhook arrived before BeginPurchase wrote the pending row.
            -- Bail without creating one — the pending row is the only place
            -- the user/sku binding is known.
            SELECT CAST(3 AS TINYINT) AS Status,
                   CAST(0 AS BIGINT)  AS BalanceAfter,
                   0                  AS ChipsGranted;
            RETURN;
        END

        IF @ExistingStatus = 4  -- Refunded — refund landed before the grant
                                -- finished idempotently somehow. Do not grant.
        BEGIN
            SELECT CAST(4 AS TINYINT) AS Status,
                   CAST(0 AS BIGINT)  AS BalanceAfter,
                   0                  AS ChipsGranted;
            RETURN;
        END

        IF @ExistingStatus = 3  -- Already Granted — re-delivered webhook.
                                -- Return the balance from the original grant
                                -- so the caller can ack and move on.
        BEGIN
            DECLARE @ExistingChips INT;
            SELECT @ExistingChips = cp.ChipsGranted
            FROM ChipPacks cp
            WHERE cp.ChipPackId = @ChipPackId;

            DECLARE @ExistingBalance BIGINT;
            SELECT @ExistingBalance = w.ChipBalance
            FROM Wallets w
            WHERE w.UserId = @UserId;

            SELECT CAST(2 AS TINYINT) AS Status,
                   ISNULL(@ExistingBalance, 0) AS BalanceAfter,
                   ISNULL(@ExistingChips, 0)   AS ChipsGranted;
            RETURN;
        END

        -- Pending → Granted: do the actual wallet credit. Same lock + ledger
        -- pattern as 01-wallet-ledger.sql; inlined here because we also want
        -- to update the purchase row in the same transaction.
        DECLARE @ChipsToGrant INT;
        SELECT @ChipsToGrant = ChipsGranted
        FROM ChipPacks
        WHERE ChipPackId = @ChipPackId;

        BEGIN TRAN;

        DECLARE @WalletId UNIQUEIDENTIFIER, @CurBalance BIGINT;
        SELECT @WalletId = WalletId, @CurBalance = ChipBalance
        FROM Wallets WITH (UPDLOCK, ROWLOCK)
        WHERE UserId = @UserId;

        IF @WalletId IS NULL
        BEGIN
            INSERT INTO Wallets (UserId) VALUES (@UserId);
            SELECT @WalletId = WalletId, @CurBalance = ChipBalance
            FROM Wallets WHERE UserId = @UserId;
        END

        DECLARE @NewBalance BIGINT = @CurBalance + @ChipsToGrant;
        DECLARE @TxId UNIQUEIDENTIFIER = NEWID();

        UPDATE Wallets
        SET ChipBalance       = @NewBalance,
            LifetimePurchased = LifetimePurchased + @ChipsToGrant,
            UpdatedAt         = SYSUTCDATETIME()
        WHERE WalletId = @WalletId;

        -- Reason 6 = RealMoneyPurchase. RelatedEntityId points back at the
        -- ChipPackPurchases row, so the ledger row carries a hard link to
        -- the receipt that produced it.
        INSERT INTO WalletTransactions (
            WalletTransactionId, WalletId, Delta, BalanceAfter,
            Reason, RelatedEntityId, Notes)
        VALUES (@TxId, @WalletId, @ChipsToGrant, @NewBalance, 6, @PurchaseId, NULL);

        UPDATE ChipPackPurchases
        SET Status                 = 3,  -- Granted
            ValidatedAt            = ISNULL(ValidatedAt, SYSUTCDATETIME()),
            GrantedAt              = SYSUTCDATETIME(),
            WalletTransactionId    = @TxId,
            ProviderReceiptPayload = ISNULL(@ProviderReceiptPayload, ProviderReceiptPayload)
        WHERE ChipPackPurchaseId = @PurchaseId;

        COMMIT TRAN;

        SELECT CAST(1 AS TINYINT) AS Status,
               @NewBalance        AS BalanceAfter,
               @ChipsToGrant      AS ChipsGranted;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        SET @ErrorFound   = 1;
        SET @ErrorMessage = ERROR_MESSAGE();
    END CATCH
END
