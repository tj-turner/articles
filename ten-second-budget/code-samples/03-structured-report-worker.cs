// The worker. Service Bus triggered, one task per message.
//
// Read the order of the last two calls in the happy path: the result row is
// written first, and only then is the task marked Succeeded. That direction is
// what makes "a result row exists if and only if the task succeeded" true from
// the reader's side. Succeeded is the last thing that happens, so a status of
// Succeeded is never a promise the result is on its way.
//
// The wall clock is a linked token, not a timer the composer is asked to
// respect. Nothing downstream has to cooperate for the budget to hold, which is
// the same reason the write block in the chat tier sits in front of dispatch
// instead of in the prompt.
//
// Known limit of this sample: a worker that dies after claiming leaves the task
// in Running, and the redelivery finds nothing to claim, because Running is not
// Pending. Recovering those rows is a separate mechanism and is not shown here.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Platform.Ai.Workers;

public sealed class StructuredReportWorker(
    ITaskStateStore tasks,
    IReportComposer composer,
    ITaskResultStore results,
    ILogger<StructuredReportWorker> log)
{
    private static readonly TimeSpan WallClock = TimeSpan.FromMinutes(2);

    [Function(nameof(StructuredReportWorker))]
    public async Task RunAsync(
        [ServiceBusTrigger("%Tasks:StructuredReportQueue%", Connection = "Tasks:Bus")]
        TaskKickoff kickoff,
        CancellationToken cancellationToken)
    {
        var claimed = await tasks.TryTransitionAsync(
            kickoff.TaskId, TaskStatus.Pending, TaskStatus.Running, cancellationToken);

        if (!claimed)
        {
            // Not an error. Service Bus redelivered a message some other
            // delivery already won, and the row said so.
            log.LogInformation(
                "Task {TaskId} was not Pending; dropping duplicate delivery.", kickoff.TaskId);
            return;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(WallClock);

        try
        {
            var payload = await composer.ComposeAsync(kickoff, budget.Token);

            await results.WriteAsync(
                new TaskResultEnvelope<StructuredReportPayload>(
                    Kind: "StructuredReport",
                    Version: "1",
                    Payload: payload),
                kickoff.TaskId,
                cancellationToken);

            await tasks.TryTransitionAsync(
                kickoff.TaskId, TaskStatus.Running, TaskStatus.Succeeded, cancellationToken);
        }
        catch (OperationCanceledException) when (budget.IsCancellationRequested
                                                 && !cancellationToken.IsCancellationRequested)
        {
            // The two minutes ran out. Distinguished from host shutdown, which
            // cancels the outer token and should be left to redeliver.
            await MarkAsync(kickoff.TaskId, TaskStatus.TimedOut, "wall clock exceeded");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Structured report task {TaskId} failed.", kickoff.TaskId);
            await MarkAsync(kickoff.TaskId, TaskStatus.Failed, ex.GetType().Name);
        }
    }

    // CancellationToken.None on purpose: the terminal status has to be recorded
    // even when the reason for recording it is that everything else was
    // canceled. Retried by the store's transient policy, and if that is
    // exhausted the row stays Running rather than lying about the outcome.
    private Task MarkAsync(Guid taskId, TaskStatus status, string reason) =>
        tasks.TryTransitionAsync(taskId, TaskStatus.Running, status, reason, CancellationToken.None);
}
