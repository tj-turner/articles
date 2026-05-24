-- Post-deploy seed scripts re-run on EVERY deployment. They must be idempotent,
-- or the second deploy collides with the rows the first one wrote.
--
-- Two guard shapes — and the choice between them is the point. Match the guard
-- to whether the data is a fixed set or a growing collection.

-------------------------------------------------------------------------------
-- 1. ALL-OR-NOTHING BLOCK GUARD
-- For reference data that arrives as one complete set (the 54 card definitions).
-- If the table has anything at all, skip the whole block.
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [dbo].[Cards])
BEGIN
    INSERT INTO [dbo].[Cards] ([CardId], [Rank], [Suit], [PointValue], [IsWild], [IsJoker]) VALUES (1, 'Ace', 'Hearts', 20, 0, 0);
    INSERT INTO [dbo].[Cards] ([CardId], [Rank], [Suit], [PointValue], [IsWild], [IsJoker]) VALUES (2, 'Two', 'Hearts', 20, 1, 0);
    -- ... 52 more ...
END
GO

-------------------------------------------------------------------------------
-- 2. PER-ROW GUARD
-- For a gallery that grows over time (avatars, tables, card designs). Each row
-- defends itself, so a later batch can add new rows without re-checking the
-- whole table. A block guard here would be WRONG: it would silently skip every
-- new avatar the moment the table held a single row.
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM [dbo].[Avatars] WHERE [AvatarId] = 'A0000001-0000-0000-0000-000000000001')
    INSERT INTO [dbo].[Avatars] ([AvatarId], [Name], [Description], [ImagePath], [IsDefault])
    VALUES ('A0000001-0000-0000-0000-000000000001', 'Male Default', 'Default male player avatar.', 'images/avatars/male-default.svg', 1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Avatars] WHERE [AvatarId] = 'A0000001-0000-0000-0000-000000000002')
    INSERT INTO [dbo].[Avatars] ([AvatarId], [Name], [Description], [ImagePath], [IsDefault])
    VALUES ('A0000001-0000-0000-0000-000000000002', 'Female Default', 'Default female player avatar.', 'images/avatars/female-default.svg', 1);
GO
