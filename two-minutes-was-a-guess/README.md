# Two Minutes Was a Guess: What a Month of Testing Did to a Published Budget

Companion code for the Medium article. The previous piece in this series
published a two-minute wall-clock budget for the task tier. A month of
user-acceptance testing met a business holding about eleven hundred payables,
and the budget went — along with the ten-minute ceiling we had assumed was the
platform's and had actually written ourselves.

**[Read the full article on Level Up Coding →](https://levelup.gitconnected.com/two-minutes-was-a-guess-what-a-month-of-testing-did-to-a-published-budget-e48d7c0b856e)**

Revises [`ten-second-budget/`](../ten-second-budget/), which is where the
two-minute budget and the host-timeout margin were published.

## TL;DR

- **A wall-clock budget does not degrade.** It is correct for every case under it
  and wrong for every case over it, with no middle where things get gradually
  slower and somebody notices. Three weeks of UAT showed nothing, because the
  businesses people picked fit. The review could not have caught it either —
  there was nothing on the page to catch.
- **I sized the budget against the steps, not the rows.** A handful of reads, a
  model pass, a result row: that sequence fits two minutes with room for a
  provider retry. Nothing in it is a function of how much data the caller asked
  for, which is the only variable that mattered.
- **Page size is the wrong dial, and the error message explains why.**
  `Resource ID : 1. The request limit for the database is 90 and has been
  reached.` Resource ID 1 is the *worker* limit; the message says "request" for
  backward compatibility from when one request meant one worker. You cannot
  compute a safe page size from how wide your fan-out is, because the thing being
  counted is not your requests — it is shared across every caller of that
  database, and provisioned per environment, so a larger tier fails at a larger
  page rather than not failing.
- **The failure was faster than the success.** A hundred and ten rows came back
  in 64 seconds; a hundred and twenty came back empty in 36. A timeout would have
  failed more slowly. Time was linear in the row count at roughly half a second a
  payable, which is not what a parallel fan-out costs — it is what a round trip
  per item costs.
- **An outage presented as "you owe nothing" is the one answer nobody should
  ship.** A catch around the whole projection returned an empty list upstream as
  HTTP 200 with no error flag, indistinguishable from a business with no
  payables. It ate `OperationCanceledException` too, in a tier whose terminal
  state depends on that exception arriving.
- **`catch (OperationCanceledException) when (ct.IsCancellationRequested)` is
  four words that separate two faults.** An `HttpClient` timeout surfaces as
  `TaskCanceledException`, which derives from the same base, so without the
  filter a stalled dependency is recorded as the task blowing its own wall clock.
- **Paging consistency was a separate defect from speed.** The page is cut inside
  a CTE and the outer query then joins and filters, so a payable can consume a
  slot and afterward be removed — a request for fifty returns fewer than fifty
  while more still exist, and a short page is the only end-of-set signal offered.
  The `ORDER BY` had no unique final tiebreaker either.
- **A tiebreaker fixes the order, not the set.** Sixteen calls spread over seven
  minutes of a data set being written to cannot produce an internally consistent
  report, however fast each call returns. De-duplication hides the overlap and
  cannot recover the gaps.
- **Claim completeness against a number, not against how the loop exited.** The
  first version let one de-duplicated row turn a truncated read into a whole
  answer; the second fixed that only when the source supplied a count. Both are
  in [`01-paged-gather.cs`](code-samples/01-paged-gather.cs), with the surviving
  version.
- **A truncated list is honest; a sum over a truncated list is not.** Fetching in
  installments and composing over whichever installment arrived made a report sum a
  third of a business's payables and present the figure as the total. The cap now
  bounds the answer rather than the fetching, and the report says on its face how
  much of the data it covers.
- **The ten-minute ceiling was ours.** An assertion in our own test suite capped
  the wall clock at ten on the grounds that ten is the Consumption plan's maximum
  — true of Consumption, false of the PremiumV3 plan these workers run on, whose
  default is thirty minutes. A report that needed 592 seconds spent 99% of a
  budget it never had to be given.
- **Four nested budgets, each exceeding the one above it:** a two-minute
  dependency call, a fifteen-minute invocation, a twenty-minute Service Bus lock
  renewal, a twenty-five-minute abandonment sweep. Two of the four end at
  `TimedOut` and are told apart by their reason, not their status — one says the
  work was slow, the other that a worker died, which sends an investigation to
  the right place.
- **The invocation budget can equal the host timeout, which revises the previous
  article.** That piece said the budget has to sit strictly below it, because both
  clocks otherwise trip together and nothing is left to record the timeout with.
  That holds only if the write that records the outcome runs on the request's
  cancellation token. When the host cancels, that token is already dead, so the
  worker opens a fresh one for the final write — ten seconds to mark the task
  `TimedOut`, thirty when there is a finished report to persist. A margin below
  the host timeout was one answer; the separate token is the one that survived a
  fifteen-minute report.
- **The constraint was never the clock.** Three queries per payable where a join
  would do is a projection problem, and the AI tier holds no connection to the
  operational databases by standard, so the join was a new endpoint on another
  team's backlog. The warehouse read was reachable that month, and it swapped
  sixteen calls into data being written to for a table that only changes when the
  refresh runs.
- **Render the high-water mark of the data, not the time the job finished.** A
  completion time claims more freshness than the rows have, by the job's own
  duration plus whatever the mirror was behind. On a report about money that is
  the difference between showing a payable that was already paid and not showing
  it.
- **A report with no sync state does not render, and neither does one stamped in
  the future.** The mark is written by another system's clock through a replica.
  The tolerance is four refresh intervals, and four is a number I made up —
  [`03-report-as-of.sql`](code-samples/03-report-as-of.sql) says so in a comment.
  It has not tripped. Two intervals, during maintenance on the sync itself, is the
  most we have ever been behind.
- **Two surfaces, two stores, both correct.** Ask in chat what you owe this week
  and the answer is live from the central API service; run the report and it comes
  from the warehouse, behind by a cadence, at a different grain. Unifying them
  sounds like tidiness and forces a choice about which surface gets to be quietly
  stale.

## Files

| File | What it is |
|---|---|
| [`nested-budgets.png`](nested-budgets.png) / [`.webp`](nested-budgets.webp) | The four windows drawn to scale against the twenty-five-minute sweep, with the fifteen-minute invocation budget drawn *through* the bars rather than beside them — two of the four extend past it on purpose. Source in [`support-files/nested-budgets.html`](support-files/nested-budgets.html). |
| [`page-size-walk.png`](page-size-walk.png) / [`.webp`](page-size-walk.webp) | Measured times for five page sizes against one test business, as bars drawn to the real times. The red bar is the point: a hundred and twenty came back empty in thirty-six seconds, visibly shorter than the hundred and ten that returned rows in sixty-four, which is not how a timeout behaves. Source in [`support-files/page-size-walk.html`](support-files/page-size-walk.html). |
| [`row-magnitudes.png`](row-magnitudes.png) / [`.webp`](row-magnitudes.webp) | Three row counts four orders of magnitude apart: fifty a page, about twelve hundred for a typical business, over five hundred thousand for the largest. The middle number is the one that decided it. Source in [`support-files/row-magnitudes.html`](support-files/row-magnitudes.html). |
| [`report-card-mock.png`](report-card-mock.png) / [`.webp`](report-card-mock.webp) | A mock of what comes back now: the summary and chart totaled over all 1,100 of the test business's payables, the coverage line, the as-of stamp, and the detail leaving as a PDF because 1,100 rows are past the cap. A mock rather than a screenshot — the real panel carries a business name, a task id and a tenant guid. Source in [`support-files/report-card-mock.html`](support-files/report-card-mock.html). |
| [`code-samples/01-paged-gather.cs`](code-samples/01-paged-gather.cs) | The paged gather, with the de-duplication keyed on the real grain and completeness claimed against a source count where one exists. Carries a `TimeProvider` and a gather deadline that deliberately excludes a composition reserve, and a per-page forecast that is labeled a forecast rather than a constant. |
| [`code-samples/02-unbounded-fanout.cs`](code-samples/02-unbounded-fanout.cs) | The shape that produced the empty success: a round trip per item, awaited all at once, behind a catch that returns an empty list. Then two repairs — a bounded, order-preserving projection, and the cancellation filter that stops a stalled dependency being recorded as a self-inflicted timeout. |
| [`code-samples/03-report-as-of.sql`](code-samples/03-report-as-of.sql) | The reporting read: one snapshot transaction, the row count in the same statement as the rows, the sync high-water mark rendered as the as-of stamp, and the tolerance check that refuses to render a stale or future-stamped report. `@MaxRows` is how many rows one read returns — the warehouse is fetched 500 at a time underneath it, and the caller keeps reading until it has the whole set — not a cap on the answer. |

The `.cs` files are illustrative rather than a compiling set — they reference
interfaces that belong to the shared library rather than to this article. The
`.sql` file follows the house output-parameter error contract (`@ErrorFound` /
`@ErrorMessage`) rather than `THROW`.

## What's still open

The worker has no deadline shorter than its budget, so it does not stop at
thirteen minutes to compose what it has. It works until the host cancels and
records that it was cut off — which means a report needing sixteen minutes
produces nothing at all, rather than a partial table carrying the coverage
warning the capped gather already knows how to write.

There is no token ceiling on a report. Eight model rounds carrying large tool
results can spend a lot inside the fifteen minutes, and what holds it down in
practice is the row summarization — more than five rows becomes a count and a
sample — rather than anything that would stop it. The per-round logging does not
cover this one either, because the report loop does not run through the
orchestrator that logging sits on. We are watching UAT to work out where a cap
belongs and whether a cap is the right shape for it.

The row cap is a limit on chat output, not on what can be read. Eight hundred
detail rows was what we would put in a chat card (`RowCeiling` in
[`01-paged-gather.cs`](code-samples/01-paged-gather.cs)); nobody measured it, and
one number stands in for three limits: rows held in memory, the size of the
serialized result, and what the results table stores. The summary is not capped —
one read returns two thousand rows (`@MaxRows` in
[`03-report-as-of.sql`](code-samples/03-report-as-of.sql)), the warehouse is
fetched five hundred at a time underneath that, and the caller keeps reading and
totaling until it has the set, so payables by status and the chart over it cover
every row the business has. That detail ceiling
is where the export came from: the biggest business holds over five hundred
thousand payables, which no chat window renders. The cap is a hundred now, and
anything above it comes back as a PDF with an Excel or CSV export option.

Read-your-own-writes is a case this design loses outright. Approve a payable, run
the report, and for up to fifteen minutes it is not there; a business onboarded
four minutes ago runs its first report and gets an empty table. That is the same
empty-success failure the article spends a month on, moved from the API layer to
the freshness layer. The as-of line tells that reader when the data was true. It
does not tell them the answer is wrong.

The export at half a million rows is still not built, and it cannot be the
synchronous job we run now. The candidates are a cursor, a stream straight into
the file, or the warehouse, where neither the page limit nor the fan-out exists.

## License

MIT — copy, adapt, ship.
