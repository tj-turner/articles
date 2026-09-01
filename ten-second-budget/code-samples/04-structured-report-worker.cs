// The worker. Service Bus triggered, one task per message.
//
// The happy path is three calls: claim the row, compose the report, complete.
// Completion is one call because it is one transaction - the result row and the
// move to Succeeded commit together or neither of them happens. That is what
// makes "a result row exists if and only if the task succeeded" true in both
// directions rather than only one.
//
// An earlier version wrote the result and then transitioned separately, in that
// order. Ordering buys the direction that matters most - Succeeded is never a
// promise a row is on its way - and cannot buy the other one. A worker that
// died between the two writes left a result row on a task still marked Running,
// which the recovery sweep then had to know about. One transaction retires that
// whole class of problem, at the cost of requiring both tables to share a
// database.
//
// Be careful about what the budget is worth. A linked token is cooperative by
// definition: a composer that never checks it runs as long as it likes, and the
// completion below would then write a result for work that overran the bound.
// What the token does buy is that the composer cannot EXTEND the budget - it is
// handed a clock that is already counting down rather than asked to start one.
// The recheck after ComposeAsync is what turns an overrun into a TimedOut
// instead of a late success, and it is the difference between this and the
// write block in the chat tier, which sits in front of dispatch and cannot be
// declined at all.
//
// Known limit: a worker that dies after claiming leaves the task in Running,
// and the redelivery finds nothing to claim, because Running is not Pending.
// Recovering those rows is a separate mechanism and is not shown here.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SharedAi.Contracts.Tasks;

namespace SharedAi.Workers;

public sealed class StructuredReportWorker(
    ITaskStateStore tasks,
    IReportComposer composer,
    ILogger<StructuredReportWorker> log)
{
    // The host's function timeout has to sit strictly above this, and the margin
    // between the two is a real mechanism rather than slack. A budget equal to
    // the host timeout leaves no time to record TimedOut - both tokens trip
    // together and the outcome is never written.
    //
    // Provider retries live inside this budget, not beside it. A 429 carrying a
    // twenty-second Retry-After spends twenty seconds of the two minutes, and
    // the composer gets no say in that, because the token it was handed is the
    // one already counting down.
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(120);

    [Function(nameof(StructuredReportWorker))]
    public async Task RunAsync(
        [ServiceBusTrigger("%Tasks:StructuredReportQueue%", Connection = "Tasks:Bus")]
        TaskKickoff kickoff,
        CancellationToken cancellationToken)
    {
        var claimed = await tasks.TryTransitionAsync(
            kickoff.TaskId, AiTaskStatus.Pending, AiTaskStatus.Running, cancellationToken);

        if (!claimed)
        {
            // The routine case: a duplicate delivery arriving at a row that is
            // no longer Pending. Nothing to undo, because nothing was done.
            log.LogInformation(
                "Task {TaskId} was not Pending; dropping delivery.", kickoff.TaskId);
            return;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Budget);

        try
        {
            var payload = await composer.ComposeAsync(kickoff, budget.Token);

            // A composer that never checked the token can return here having
            // overrun the budget. Without this line the overrun lands as a
            // Succeeded task, which is a quieter lie than a TimedOut one.
            budget.Token.ThrowIfCancellationRequested();

            // CancellationToken.None on purpose. This call is what makes the
            // invariant true and it may not be abandoned partway because the
            // host started draining.
            var completion = await tasks.CompleteWithResultAsync(
                kickoff.TaskId,
                new TaskResultEnvelope<StructuredReportPayload>(
                    Kind: "StructuredReport",
                    Version: 1,
                    Payload: payload),
                CancellationToken.None);

            if (!completion.Transitioned)
            {
                // The row was not Running by the time the report was finished.
                // The transaction rolled back, so there is nothing to clean up -
                // but the two reasons want different log levels. Succeeded means
                // a previous attempt of this same call committed and the reply
                // never got back to us, which is the case the guarded INSERT in
                // the procedure exists to make boring. Anything else means a
                // sweep settled a task this worker was still working on, and the
                // margin between the budget and the sweep is wrong.
                if (completion.CurrentStatus is AiTaskStatus.Succeeded)
                {
                    log.LogInformation(
                        "Task {TaskId} was already complete; this delivery's result was discarded.",
                        kickoff.TaskId);
                }
                else
                {
                    log.LogWarning(
                        "Task {TaskId} was {Status} at completion; finished report discarded.",
                        kickoff.TaskId, completion.CurrentStatus);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host drain. Let it escape rather than record a terminal status we
            // cannot stand behind - the work is genuinely in an unknown state.
            //
            // Be honest about what that buys, because it is less than it looks.
            // Service Bus abandons the message and redelivers it, and the
            // redelivery asks for Pending -> Running against a row this worker
            // already moved to Running, so it is dropped by the claim guard. The
            // guard cannot tell a duplicate from the same message retrying
            // legitimately. Recovery is the sweep's job, not the queue's.
            throw;
        }
        catch (OperationCanceledException) when (budget.IsCancellationRequested)
        {
            await MarkAsync(kickoff.TaskId, AiTaskStatus.TimedOut, "budget exceeded");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Structured report task {TaskId} failed.", kickoff.TaskId);
            await MarkAsync(kickoff.TaskId, AiTaskStatus.Failed, ex.GetType().Name);
        }
    }

    // CancellationToken.None on purpose: the terminal status has to be recorded
    // even when the reason for recording it is that everything else was
    // canceled. Retried by the store's transient policy, and if that is
    // exhausted the row stays Running rather than lying about the outcome - a
    // database too sick to take the completing transaction will not take this
    // one either.
    private Task MarkAsync(Guid taskId, AiTaskStatus status, string reason) =>
        tasks.TryTransitionAsync(taskId, AiTaskStatus.Running, status, reason, CancellationToken.None);
}
