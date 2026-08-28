// The skill the model calls to start long work.
//
// The point of this file is how ordinary it is. There is no async dispatch
// path, no "background mode" flag on the turn, no second orchestrator. The
// model calls a skill, the skill returns, the turn ends. Everything the tool
// surface already does to skills - the write block, argument locking, impact
// levels - applies to this one without being extended, because there is nothing
// here for it to miss.
//
// Ordering is load-bearing: the row is created Pending BEFORE the message is
// enqueued. Reverse it and a worker can pick up a kickoff for a task that does
// not exist yet, which turns a race into a support ticket.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Platform.Ai.Skills.Tasks;

public sealed record StartStructuredReportArgs(
    string UserQuery,
    IReadOnlyDictionary<string, string> ResolvedParameters);

public sealed record StartStructuredReportResult(Guid TaskId, string Status);

[Skill(
    Name = "start_structured_report",
    Impact = SkillImpact.Read,
    Description =
        "Start a structured report for a question that needs several lookups " +
        "composed into one answer. Returns immediately with a task id; the " +
        "report is delivered to the conversation when it finishes.")]
public sealed class StartStructuredReportSkill(
    ITaskStateStore tasks,
    ITaskQueue queue) : ISkill<StartStructuredReportArgs, StartStructuredReportResult>
{
    public async Task<StartStructuredReportResult> InvokeAsync(
        SkillContext context,
        StartStructuredReportArgs args,
        CancellationToken cancellationToken)
    {
        var taskId = Guid.CreateVersion7();

        // Sequential, not concurrent, and not in the other order. The task has
        // to be resolvable by the time anything can read the queue.
        await tasks.CreateAsync(
            new TaskRecord(
                TaskId: taskId,
                Kind: TaskKind.StructuredReport,
                TenantId: context.TenantId,
                UserId: context.UserId,
                ConversationId: context.ConversationId,
                Status: TaskStatus.Pending,
                CreatedUtc: DateTimeOffset.UtcNow),
            cancellationToken);

        await queue.EnqueueAsync(
            new TaskKickoff(
                TaskId: taskId,
                Kind: TaskKind.StructuredReport,
                TenantId: context.TenantId,
                UserId: context.UserId,
                ConversationId: context.ConversationId,
                MessageId: context.MessageId,
                UserQuery: args.UserQuery,
                ResolvedParameters: args.ResolvedParameters,
                // Carried on the kickoff rather than re-derived in the worker.
                // The worker has no user session to ask.
                ClassificationFloor: context.ClassificationFloor),
            cancellationToken);

        return new StartStructuredReportResult(taskId, "queued");
    }
}
