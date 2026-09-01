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
- **A result row exists if and only if the task succeeded.** Every failure path
  leaves the result table alone, so `Failed` and `TimedOut` tasks are exactly the
  ones with no result and "did this work" stays a single query.
- **Ordering only reaches one direction; a transaction reaches both.** The first
  version wrote the result and then transitioned, which makes `Succeeded` the
  last thing that happens and cannot say anything about a worker dying in the
  gap. Both writes are one procedure and one transaction now — the result row and
  `Running → Succeeded` commit together or neither does.
- **Deduplication and atomicity turn out to be the same mechanism.** The guarded
  transition sits inside the completing transaction, so a duplicate that got as
  far as a composed report loses the transition, reads zero back, and takes its
  own result row down with the rollback — without needing to work out that it was
  a duplicate.
- **`Running → Succeeded` is not in the transition procedure's legal list.** The
  only path to `Succeeded` is the procedure that writes the result alongside it,
  which is what turns the invariant from a rule a worker follows into a shape the
  schema will not let you express. What it costs is that one transaction means
  one database.
- **The uncomfortable half.** A transient failure on the completing transaction
  is retried, and when the retries are exhausted a completed report is thrown
  away rather than written somewhere the invariant does not cover. The row stays
  `Running` — a database too sick to take that commit will not record a clean
  `Failed` either.
- **The card polls the task row; nothing is pushed to it.** A push would be a
  second way for the answer to arrive, carrying its own delivery problem — the
  client that was disconnected at the moment it fired. The row is already the
  source of truth, so the card asks the row. Same argument as kickoff being a
  skill, applied at the other end.
- **The model never carries the report forward.** The conversation keeps the task
  id; the payload is rendered to the user and nothing else. A user asking to see
  it again is a `get-task-result` skill call, so re-reading a finished report
  goes through the same dispatch controls as reading anything else, and a long
  report costs the context window nothing on later turns.
- **Classification floor and ceiling are an ordered enum, not strings.** Effective
  classification is the higher of the two. Sorted as text, `Public` lands after
  `Confidential`, so a public floor and a confidential ceiling resolve to public —
  silently, and in the direction that declassifies.
- **The envelope carries a version because the rows outlive the renderer.** A
  result row is written once and read for as long as the conversation exists, so
  the table is a permanent record of every payload shape ever emitted. Versioning
  costs a string; migrating a few million JSON payloads costs a weekend.

## Files

| File | What it is |
|---|---|
| [`header-ten-second-budget.png`](header-ten-second-budget.png) / [`.webp`](header-ten-second-budget.webp) | Header image, 1600x800. Authored HTML rendered through headless Chrome — no image model. Source in [`support-files/header-ten-second-budget.html`](support-files/header-ten-second-budget.html). |
| [`turn-budget.png`](turn-budget.png) / [`.webp`](turn-budget.webp) | The three bounds drawn to scale against the two-minute loop bound, with the ten-second budget drawn through them. Source in [`support-files/turn-budget.html`](support-files/turn-budget.html). |
| [`two-tiers.png`](two-tiers.png) / [`.webp`](two-tiers.webp) | The tiers labeled by failure model rather than happy path, and the single completing transaction that keeps the result table answerable. Source in [`support-files/two-tiers.html`](support-files/two-tiers.html). |
| [`code-samples/01-task-kickoff-skill.cs`](code-samples/01-task-kickoff-skill.cs) | The kickoff skill. The point of the file is how ordinary it is — no branch, no background-mode flag, nothing for the existing tool-surface controls to miss. The row is created `Pending` before the message is enqueued, and the order is load-bearing. |
| [`code-samples/02-usp-aitask-updatestatus.sql`](code-samples/02-usp-aitask-updatestatus.sql) | The state-transition procedure. One guarded `UPDATE`, `@@ROWCOUNT` as the verdict, no `THROW` on a losing transition — and no `Running → Succeeded` in its legal list, because that transition belongs to the procedure below. |
| [`code-samples/03-usp-aitask-completewithresult.sql`](code-samples/03-usp-aitask-completewithresult.sql) | Completion: the result row and the guarded move to `Succeeded` inside one transaction. A losing transition rolls its own result row back, which is the whole duplicate-handling story. |
| [`code-samples/04-structured-report-worker.cs`](code-samples/04-structured-report-worker.cs) | The Service Bus triggered worker: claim, compose under a linked wall-clock token, complete. Timeout is distinguished from host shutdown, which should be left to redeliver. |
| [`code-samples/05-task-result-envelope.cs`](code-samples/05-task-result-envelope.cs) | `{Kind, Version, Payload}`, and the report payload that carries its own classification floor and ceiling because the turn that produced it is gone by the time anyone opens the row. |

The `.cs` files are illustrative rather than a compiling set — they reference
interfaces (`ITaskStateStore`, `IReportComposer`, `ITaskQueue`) that belong to
the shared library rather than to this article.

## What's still open

A worker that dies after claiming leaves the task in `Running`, and the
redelivery finds nothing to claim, because `Running` is not `Pending`. The guard
that makes duplicate deliveries cheap is the same guard that makes an abandoned
row unreachable. Recovering those rows is a separate mechanism and is not shown
in these samples.

Collapsing the two writes into one transaction took the sharp edge off that. The
earlier version had to worry that a sweeper settling an abandoned `Running` row
to `Failed` was settling a task that had already written its result — the one
state the invariant forbids — and so the sweeper had to look before it decided.
That state is no longer reachable, because nothing writes a result without
moving the status in the same transaction.

What is left is a timing question rather than a consistency one, and it is
answered by deriving one number from another. The sweep settles rows that have
been `Running` longer than the worker's wall-clock budget plus a margin, so a
worker still composing is never settled out from under — the row cannot be old
enough to qualify while its worker is still allowed to run.

Sample 04 logs a warning when its completion loses the transition anyway. That
line should never print. If it does, the margin is wrong, and the visible
symptom is a report that was finished and thrown away.

## License

MIT — copy, adapt, ship.
