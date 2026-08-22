# Documents Are Never Instructions: Safety Walls That Don't Ask Who Wrote It

Companion code for the Medium article. A trust gradient sorts retrieved
documents by who wrote them, and the question it answers — how much to believe a
document — has nothing to do with whether the model may act on it. This is what
it took to stop conflating the two, and what turned up afterward when we checked
whether the tests proving it were testing anything.

**[Read the full article on Level Up Coding →](#)** *(link pending publish)*

## TL;DR

- **A procedure document cannot be reviewed clean.** An internal wiki page
  explaining how to force a repayment exists to contain imperative sentences and
  an example identifier. You cannot review the commands out of it without
  destroying it, so a trust gradient asks a reviewer to catch something the
  document has to keep containing.
- **Provenance answers how much to believe, never whether to obey.** Once fencing
  became unconditional, the field recording trust level gated nothing — and it
  was still called `trustLevel`, still holding a value called `trusted`, in the
  one place an engineer looks when handling retrieved content. The rename to
  `provenance` altered no behavior at all.
- **A confirmation step is not the wall.** Every write is a proposal that
  executes only through a separate endpoint, which defends against a model acting
  alone and has no answer for a model steered into asking. The confirmation would
  also have supplied exactly the context whose absence made an earlier bad delete
  fail with a `400`.
- **Two unconditional controls, and only one of them is structural.** Structural
  fencing wraps every chunk from every index whatever its provenance; doc-action
  separation latches on any retrieval and drops every write call for the
  remainder of the turn. The fence asks the model to respect a delimiter. The
  latch asks nothing.
- **Escape the markers like you'd escape SQL.** A document containing the literal
  closing token otherwise closes its own fence, and a filename can inject a
  second `provenance` attribute. The place the analogy breaks is the important
  one: a parameterized query removes the mixing entirely, and there is no
  parameterized prompt.
- **Never a silent drop.** A blocked write returns a policy error naming the
  reason. Silence leaves an emitted tool call with no paired result, which both
  major provider APIs reject on the next request — and leaves the model likely to
  report a payment it never made.
- **A memo field is a document that didn't look like one.** Customer-typed free
  text returned by structured data reads was reaching the model outside every
  injection defense, because all of them were scoped to document retrieval. Free
  text in results is now fenced, scanned and write-blocking, and write-skill
  arguments are locked to references with consequential values resolved
  server-side.
- **The block is deliberately wider than the proven risk.** *Approve the first
  one* selects positionally and never needed the memo. *Approve the invoice about
  the Q3 consulting* makes the memo the selector. You cannot separate those from
  outside the turn, so one rule covers both.
- **A green check is a claim about the code.** The adversarial test asserting that
  a poisoned document could not trigger a write was passing against a registry
  with zero write skills in it. It would have passed against an empty repository,
  and from the suite's point of view nothing was wrong.

## Files

| File | What it is |
|---|---|
| [`header-documents-are-never-instructions.png`](header-documents-are-never-instructions.png) / [`.webp`](header-documents-are-never-instructions.webp) | Header image, 1536x864. AI-generated with Z-Image Turbo. |
| [`write-block-coverage.png`](write-block-coverage.png) / [`.webp`](write-block-coverage.webp) | The coverage figure: which inbound paths crossed which wall, and the one that crossed neither. Source in [`support-files/write-block-coverage.html`](support-files/write-block-coverage.html). |
| [`code-samples/01-fence-assembly.cs`](code-samples/01-fence-assembly.cs) | The fence renderer, with the escaping that keeps a document from closing its own fence or injecting an attribute. No branch on provenance anywhere in the file, which is the point of it. |
| [`code-samples/02-write-block-dispatch.cs`](code-samples/02-write-block-dispatch.cs) | Doc-action separation applied immediately before dispatch, and the refusal that goes back for a blocked call. The latch is set through `AddRetrieved`, so `state.HasRetrieved = true` does not compile. |
| [`code-samples/03-fragment-retrieved-content.md`](code-samples/03-fragment-retrieved-content.md) | The shipped prompt fragment at `3.0.0`, with the version history that makes a major bump for a no-op rename the correct number. |

The `.cs` files compile as a set against `net10.0`.

Worth reading against [`shared-foundation/code-samples/02-turn-state.cs`](../shared-foundation/code-samples/02-turn-state.cs),
where the same record is committed as
`RetrievedChunk(string Text, TrustLevel TrustLevel, string Source)`. The rename
this article is about is visible as a diff between two files in this repo.

## What's still open

The positive control. Nothing in the injection suite establishes that the write
path is reachable, so nothing distinguishes *the block worked* from *the feature
is missing* — and that was exactly the state the suite was in when it was passing
against an empty registry.

## License

MIT — copy, adapt, ship.
