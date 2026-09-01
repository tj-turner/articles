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
// The budget is a linked token, not a timer the composer is asked to respect.
// Nothing downstream has to cooperate for it to hold, which is the same reason
// the write block in the chat tier sits in front of dispatch instead of in the
// prompt.
//
// Known limit: a worker that dies after claiming leaves the task in Running,
// and the redelivery finds nothing to claim, because Running is not Pending.
// Recovering those rows is a separate mechanism and is not shown here.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Platform.Ai.Contracts.Tasks;

namespace Platform.Ai.Workers;

public sealed class StructuredReportWorker(
    ITaskStateStore tasks,
    IReportComposer composer,
    ILogger<StructuredReportWorker> log)
{
    // Strictly below the host's function timeout, and that margin is the whole
    // mechanism. A budget equal to the host timeout has no time left to record
    // TimedOut - both tokens trip together and the outcome is never written.
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(100);

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
                // but the status it came back with is worth reading. Failed or
                // TimedOut means a sweep settled a task this worker was still
                // working on, and the budget is not doing its job.
                log.LogWarning(
                    "Task {TaskId} was {Status} at completion; result discarded.",
                    kickoff.TaskId, completion.CurrentStatus);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host drain. Let it escape so Service Bus abandons the message and
            // redelivers it - swallowing this completes the message and destroys
            // the work. The task is left Running for the recovery sweep.
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
