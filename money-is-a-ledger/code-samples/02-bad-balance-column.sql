-- The version of the wallet I almost shipped — and what's wrong with it.
--
-- This is NOT in the codebase. It's the counter-example to 01-wallet-ledger.sql,
-- written here so the lesson is visible side-by-side. Everything below "works"
-- in the narrowest sense (the integer goes up, the integer goes down). It also
-- fails every question a player will eventually ask.

CREATE TABLE [dbo].[Wallets_Bad]
(
    [WalletId]    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [UserId]      UNIQUEIDENTIFIER NOT NULL,
    [ChipBalance] BIGINT           NOT NULL DEFAULT 0,

    CONSTRAINT [PK_Wallets_Bad] PRIMARY KEY CLUSTERED ([WalletId]),
    CONSTRAINT [UQ_Wallets_Bad_UserId] UNIQUE ([UserId])
);
GO

CREATE PROCEDURE [dbo].[Wallet_Adjust_Bad]
    @UserId UNIQUEIDENTIFIER,
    @Delta  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Problem 1: No row lock, no transaction. Two concurrent credits can
    -- read the same balance, each add their delta to it, and both write back —
    -- one of the credits silently vanishes.
    UPDATE Wallets_Bad
    SET ChipBalance = ChipBalance + @Delta
    WHERE UserId = @UserId;

    -- Problem 2: No history. When a player asks "where did my chips go,"
    -- the only answer this table can give is "you have X chips right now."
    -- The chip that left at 14:32 is gone. Application logs are not the
    -- place you reconstruct player trust from.

    -- Problem 3: No reason code, no related entity. Even if you DID have a
    -- ledger table, this proc couldn't populate it — the caller would have
    -- to remember to log the reason every time, and one missed call site
    -- is a hole in the audit trail forever.

    -- Problem 4: No lifetime rollups, and no place to bolt them on without
    -- introducing new sync bugs. Lifetime counters fall out of the ledger
    -- write in 01-wallet-ledger.sql precisely because they live with the
    -- same statement that caused them.
END
GO

-- The takeaway: every problem above is solved by the same change — make the
-- truth an append-only ledger, and make this `ChipBalance` column a cache of
-- the ledger's tail rather than the source of truth itself. See
-- 01-wallet-ledger.sql for the version that ships.
