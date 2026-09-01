-- Completing a task: the result row and the status change, in one transaction.
--
-- This is the only path to Succeeded. usp_AiTask_UpdateStatus owns every other
-- transition, and Running -> Succeeded is deliberately absent from its legal
-- list, so there is no way to mark a task Succeeded that does not write the
-- result in the same transaction. The invariant stops being a rule a worker has
-- to follow and becomes a shape the schema will not let you express.
--
-- The guard is the same guard, in the same place: the expected status sits in
-- the WHERE clause and @@ROWCOUNT is the verdict. It does two jobs here. It
-- answers "did I win," the way it does on the claim, and because the insert is
-- inside the same transaction, a worker that loses takes its own result row
-- down with the rollback. A duplicate that somehow got all the way to a
-- composed report does not have to detect that it is a duplicate - it calls
-- this, gets @Transitioned = 0, and leaves nothing behind.
--
-- Still no THROW. Losing is an expected outcome, not an error.

CREATE OR ALTER PROCEDURE dbo.usp_AiTask_CompleteWithResult
    @TaskId        UNIQUEIDENTIFIER,
    @ResultKind    VARCHAR(64),
    @ResultVersion INT,
    @Payload       NVARCHAR(MAX),          -- the envelope payload, serialized
    @WorkerId      NVARCHAR(100) = NULL,
    @Transitioned  BIT           OUTPUT,
    -- Handed back so the caller can tell the two zeroes apart: a task settled
    -- by the recovery sweep is worth an alert, and a retry of a call that had
    -- already committed is not.
    @CurrentStatus VARCHAR(16)   OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    -- Any error aborts the batch rather than the statement, so "committed the
    -- result but not the status" is not a state this procedure can produce.
    SET XACT_ABORT ON;
    SET @Transitioned = 0;

    BEGIN TRAN;

        -- Guarded rather than a bare INSERT. TaskId is the primary key, so a
        -- bare insert would raise on the one retry that matters: the call whose
        -- commit succeeded and whose connection dropped before the caller heard
        -- about it. The PK is still there underneath as the real guarantee -
        -- this only keeps the expected case from arriving as an exception.
        INSERT INTO dbo.AiTaskResults (TaskId, ResultKind, ResultVersion, Payload, CreatedUtc)
        SELECT @TaskId, @ResultKind, @ResultVersion, @Payload, SYSUTCDATETIME()
         WHERE NOT EXISTS (SELECT 1 FROM dbo.AiTaskResults WHERE TaskId = @TaskId);

        UPDATE dbo.AiTasks
           SET Status        = 'Succeeded',
               WorkerId      = COALESCE(@WorkerId, WorkerId),
               CompletedUtc  = SYSUTCDATETIME(),
               RowVersionUtc = SYSUTCDATETIME()
         WHERE TaskId = @TaskId
           AND Status = 'Running';

        IF @@ROWCOUNT = 1
        BEGIN
            SET @Transitioned = 1;
            COMMIT TRAN;
        END
        ELSE
        BEGIN
            -- The row was not Running. Another delivery completed it, or a
            -- sweep settled it, or this is the ambiguous-commit retry above.
            -- The INSERT goes back with the rollback either way.
            ROLLBACK TRAN;
        END

    -- A scalar subquery, not SELECT @var = col. With no matching row the
    -- assignment form leaves the variable holding whatever the caller passed in,
    -- and since this is an OUTPUT parameter that is usually the value from the
    -- previous call - so a missing task would report the last task's status.
    -- That is exactly the case this parameter was added to disambiguate.
    SET @CurrentStatus = (SELECT Status FROM dbo.AiTasks WHERE TaskId = @TaskId);
END
GO
