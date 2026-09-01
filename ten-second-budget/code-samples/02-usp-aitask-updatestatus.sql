-- The state-transition procedure for the task tier.
--
-- Every status change goes through this one procedure, with one exception:
--     Pending -> Running -> Failed | TimedOut
--
-- Running -> Succeeded is missing on purpose. Succeeding means writing a result
-- row and moving the status in a single transaction, which is a different
-- procedure - usp_AiTask_CompleteWithResult, sample 03. Leaving the transition
-- out of the table below is what makes "a result row exists if and only if the
-- task succeeded" unforgeable: there is no statement anywhere that can mark a
-- task Succeeded without writing the result alongside it.
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

    -- The guard above answers "is the row in the state I expected." It does not
    -- answer "is that a legal move," and without this table it isn't one: a
    -- caller can resurrect a terminal task with Succeeded -> Pending, skip
    -- Running entirely, or write a misspelled status that no @FromStatus will
    -- ever match again, stranding the row forever.
    --
    -- Still no THROW. An illegal transition returns @Transitioned = 0, which is
    -- the same answer a losing race gets, because both mean "the row did not
    -- move and you are not the one who moves it."
    IF NOT EXISTS (SELECT 1 FROM (VALUES
            ('Pending','Running'), ('Pending','TimedOut'),
            ('Running','Failed'),  ('Running','TimedOut')
        ) AS legal(f, t) WHERE f = @FromStatus AND t = @ToStatus)
    BEGIN
        RETURN;
    END

    UPDATE dbo.AiTasks
       SET Status        = @ToStatus,
           WorkerId      = COALESCE(@WorkerId, WorkerId),
           -- COALESCE, matching WorkerId above: a later transition that passes
           -- no reason must not erase the reason an earlier one recorded.
           FailureReason = COALESCE(@FailureReason, FailureReason),
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
