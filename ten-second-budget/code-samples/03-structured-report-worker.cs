// The worker. Service Bus triggered, one task per message.
//
// Read the order of the last two calls in the happy path: the result row is
// written first, and only then is the task marked Succeeded. That direction is
// what makes "a result row exists if and only if the task succeeded" true from
// the reader's side. Succeeded is the last thing that happens, so a status of
// Succeeded is never a promise the result is on its way.
//
// Be precise about what that buys, because it is a convention and not a
// database guarantee. Two tables are written in two transactions, so the
// invariant holds because of the ordering plus the two rules below - the PK
// conflict is treated as already-written, and nothing after a successful result
// write is allowed to terminalize the task as Failed. If the two tables share a
// database, one procedure holding both writes is strictly better and this file
// is the wrong shape.
//
// The budget is a linked token, not a timer the composer is asked to respect.
// Nothing downstream has to cooperate for it to hold, which is the same reason
// the write block in the chat tier sits in front of dispatch instead of in the
// prompt.
//
// Known limit: a worker that dies after claiming leaves the task in Running,
// and the redelivery finds nothing to claim, because Running is not Pending.
// Recovering those rows is a separate mechanism and is not shown here. Note
// what that mechanism has to do - it must check for a result row before it
// settles an abandoned task, or it will mark a task Failed that has a result,
// which is the one state the invariant forbids.

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
    ITaskResultStore results,
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
            // Two different situations, and only one of them is routine.
            // Distinguishing them needs the row's current status back from the
            // transition; logged apart because the second one wants an alert.
            log.LogInformation(
                "Task {TaskId} was not Pending; dropping delivery.", kickoff.TaskId);
            return;
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(Budget);

        try
        {
            var payload = await composer.ComposeAsync(kickoff, budget.Token);

            // CancellationToken.None from here down. Everything below is what
            // makes the invariant true, and none of it may be abandoned partway
            // because the host started draining.
            await WriteResultAsync(kickoff.TaskId, payload);

            await tasks.TryTransitionAsync(
                kickoff.TaskId, AiTaskStatus.Running, AiTaskStatus.Succeeded,
                reason: null, CancellationToken.None);
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

    // A PK conflict means a previous attempt already wrote this exact result.
    // That is the retry succeeding, not the retry failing, and treating it as an
    // error is what would put a result row on a Failed task.
    private async Task WriteResultAsync(Guid taskId, StructuredReportPayload payload)
    {
        try
        {
            await results.WriteAsync(
                new TaskResultEnvelope<StructuredReportPayload>(
                    Kind: "StructuredReport",
                    Version: 1,
                    Payload: payload),
                taskId,
                CancellationToken.None);
        }
        catch (DuplicateTaskResultException)
        {
            log.LogInformation("Result for {TaskId} was already written.", taskId);
        }
    }

    // CancellationToken.None on purpose: the terminal status has to be recorded
    // even when the reason for recording it is that everything else was
    // canceled. Retried by the store's transient policy, and if that is
    // exhausted the row stays Running rather than lying about the outcome - a
    // database too sick to take the result write will not take this one either.
    private Task MarkAsync(Guid taskId, AiTaskStatus status, string reason) =>
        tasks.TryTransitionAsync(taskId, AiTaskStatus.Running, status, reason, CancellationToken.None);
}
