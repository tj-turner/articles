// The skill the model calls to start long work.
//
// The point of this file is how ordinary it is. There is no async dispatch
// path, no "background mode" flag on the turn, no second orchestrator. The
// model calls a skill, the skill returns, the turn ends.
//
// Be precise about what that inherits, because the dramatic answer is the wrong
// one. The write block, argument locking, impact levels and the proposal path
// are all WRITE controls, and a kickoff is a read - none of them fire here.
// What it actually inherits is the boring list, and the boring list is the
// argument: scope checks, classification, the generated argument schema, the
// startup pairing check that refuses to boot a skill with no description file,
// forensic logging, and the per-turn cap check.
//
// Category is TaskKickoff, not Read, and that distinction does work: a kickoff
// is not published into the catalog a worker composes from, so a report cannot
// start another report.
//
// No Description on the attribute. Per the content-as-code piece, a skill's
// description is a reviewed Markdown file with frontmatter bound to the skill
// id, and the service refuses to start if this class has no paired file.
//
// Ordering is load-bearing: the row is created Pending BEFORE the message is
// enqueued. Reverse it and a worker can pick up a kickoff for a task that does
// not exist yet, which turns a race into a support ticket.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Platform.Ai.Skills.Tasks;

public sealed record StartStructuredReportArgs(
    string UserQuery,
    IReadOnlyDictionary<string, string> ResolvedParameters);

public sealed record StartStructuredReportResult(Guid TaskId, string Status);

[Skill(id: "start-structured-report", category: SkillCategory.TaskKickoff)]
[SkillClassification(Classification.Internal)]
[SkillScopeRequired("reports.read")]
public sealed class StartStructuredReportSkill(
    ITaskStateStore tasks,
    ITaskQueue queue) : ISkill<StartStructuredReportArgs, StartStructuredReportResult>
{
    public async Task<StartStructuredReportResult> ExecuteAsync(
        StartStructuredReportArgs args,
        SkillContext context,
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
                Status: AiTaskStatus.Pending,
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
