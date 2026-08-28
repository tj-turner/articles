# The Ten-Second Budget: Where Long Work Goes When Chat Can't Hold It

Companion code for the Medium article. A structured report takes about
forty-five seconds and a chat turn has about ten, so the work moved to a task
tier. What that bought was a second failure model — a chat turn that dies is a
retry someone asked for, and a worker that dies is a Service Bus redelivery
nobody asked for — and this is the set of invariants that survive it.

**[Read the full article on Level Up Coding →](#)** *(link pending publish)*

## TL;DR

- **Ten seconds is not a performance target.** It is the point past which the
  surface is lying about being a conversation. Streaming moves that line without
  removing it: tokens buy patience for a long answer and nothing at all for a
  long silence, and a turn spending forty seconds inside sequential tool calls
  has produced no tokens to stream.
- **A slow turn is a thing you tune. This isn't one.** No amount of tuning gets
  five sequential model round trips under ten seconds, which makes it a tier
  boundary rather than a performance backlog item.
- **Kickoff is a skill, not a second dispatch path.** Every control the tool
  surface has — the write block, argument locking, impact levels, the proposal
  path — is attached to skill dispatch. A separate pipeline is a second place all
  of it has to exist and a second place it drifts.
- **The classifier was the worse half of that idea.** Deciding up front whether a
  question is expensive means predicting tool-call depth before any tool is
  called. It is also wrong in the direction that hurts: the misprediction you
  notice is a cheap question sent to a worker; the one you don't is the expensive
  question kept in the turn.
- **The transition is the deduplication.** `@FromStatus` sits in the `WHERE`
  clause, so a status change either matches the row's current state or changes
  nothing, and `@@ROWCOUNT` is the whole answer. A redelivered message asks for
  `Pending → Running` against a row already `Running`, gets zero back, and
  returns. Losing that race is not an error and does not `THROW`.
- **A result row exists if and only if the task succeeded.** The result is
  written first and the status is marked `Succeeded` second, so `Succeeded` is
  never a promise a row is on its way. Every failure path leaves the result table
  alone, which keeps "did this work" a single query.
- **That invariant is a convention, not a database guarantee.** Two tables in two
  transactions means the ordering holds it up, plus two rules: a `TaskId` PK
  conflict is treated as already-written rather than as an error, and nothing
  after a successful result write may terminalize the task as `Failed`. If both
  tables live in one database, a single procedure holding both writes is
  strictly better than the ordering discipline.
- **The uncomfortable half.** A transient failure on the result write is retried,
  and when the retries are exhausted a completed report is thrown away rather
  than written somewhere the invariant does not cover. The row stays `Running` —
  a database too sick to take the result write will not record a clean `Failed`
  either.
- **The envelope carries a version because the rows outlive the renderer.** A
  result row is written once and read for as long as the conversation exists, so
  the table is a permanent record of every payload shape ever emitted. Versioning
  costs a string; migrating a few million JSON payloads costs a weekend.

## Files

| File | What it is |
|---|---|
| [`header-ten-second-budget.png`](header-ten-second-budget.png) / [`.webp`](header-ten-second-budget.webp) | Header image, 1600x800. Authored HTML rendered through headless Chrome — no image model. Source in [`support-files/header-ten-second-budget.html`](support-files/header-ten-second-budget.html). |
| [`turn-budget.png`](turn-budget.png) / [`.webp`](turn-budget.webp) | The three bounds drawn to scale against the two-minute loop bound, with the ten-second budget drawn through them. Source in [`support-files/turn-budget.html`](support-files/turn-budget.html). |
| [`two-tiers.png`](two-tiers.png) / [`.webp`](two-tiers.webp) | The tiers labeled by failure model rather than happy path, and the write ordering that keeps the result table answerable. Source in [`support-files/two-tiers.html`](support-files/two-tiers.html). |
| [`code-samples/01-task-kickoff-skill.cs`](code-samples/01-task-kickoff-skill.cs) | The kickoff skill. The point of the file is how ordinary it is — no branch, no background-mode flag, nothing for the existing tool-surface controls to miss. The row is created `Pending` before the message is enqueued, and the order is load-bearing. |
| [`code-samples/02-usp-aitask-updatestatus.sql`](code-samples/02-usp-aitask-updatestatus.sql) | The single state-transition procedure. One guarded `UPDATE`, `@@ROWCOUNT` as the verdict, and no `THROW` on a losing transition. |
| [`code-samples/03-structured-report-worker.cs`](code-samples/03-structured-report-worker.cs) | The Service Bus triggered worker: claim, compose under a linked wall-clock token, write the result, then mark `Succeeded`. Timeout is distinguished from host shutdown, which should be left to redeliver. |
| [`code-samples/04-task-result-envelope.cs`](code-samples/04-task-result-envelope.cs) | `{Kind, Version, Payload}`, and the report payload that carries its own classification floor and ceiling because the turn that produced it is gone by the time anyone opens the row. |

The `.cs` files are illustrative rather than a compiling set — they reference
interfaces (`ITaskStateStore`, `IReportComposer`, `ITaskQueue`) that belong to
the shared library rather than to this article.

## What's still open

A worker that dies after claiming leaves the task in `Running`, and the
redelivery finds nothing to claim, because `Running` is not `Pending`. The guard
that makes duplicate deliveries cheap is the same guard that makes an abandoned
row unreachable. Recovering those rows is a separate mechanism and is not shown
in these samples.

That mechanism is load-bearing for the headline invariant, which is why leaving
it out is a real gap rather than a tidy scope boundary. `succeeded → row exists`
is guaranteed by the write ordering. `row exists → succeeded` is not: a worker
can die between the two writes, and a sweeper that settles abandoned `Running`
rows to `Failed` without first checking for a result row creates precisely the
state the invariant forbids. The sweeper has to look before it decides.

## License

MIT — copy, adapt, ship.
