# What Outlives the Plan

Companion code samples for the Medium article (Part 4 of *Lessons from Building UpAllNight*).

**[Read the full article on Level Up Coding →](https://levelup.gitconnected.com/what-outlives-the-plan-10c14f67839e)**

## TL;DR

The 1,200-line plan I wrote on day one was the most useful document on the
project. The parts of it that tried to hold current state went stale the
moment I shipped. The parts that only held *decisions* — the project-reference
rules in `CLAUDE.md`, the same rules enforced by the `.csproj` graph, past
corrections in Claude Code project memory, paid external accounts on the
calendar — outlived three feature reorganizations. They all share one shape:
they live somewhere the code can't reach to silently drift away from.

This repo has the four artifacts behind those lessons. Three shelves and a
counterexample.

## Files

| File | What it is |
|---|---|
| [`code-samples/01-claude-md-architecture-rules.md`](code-samples/01-claude-md-architecture-rules.md) | The relevant slice of the project `CLAUDE.md` — solution layout, the one-sentence hard rule, data-access conventions. The *why* shelf. |
| [`code-samples/02-claude-memory-example.md`](code-samples/02-claude-memory-example.md) | Two real Claude Code project memories — a feedback memory (the seed-script rule) and a project memory (an intentional deferred decision) — plus the `MEMORY.md` index that loads them. |
| [`code-samples/03-provisioning-checklist.md`](code-samples/03-provisioning-checklist.md) | A copy-paste checklist for the day-one paid external accounts a project depends on. Filled-in example for a mobile game with IAP, plus a blank template. |
| [`code-samples/04-project-references.xml`](code-samples/04-project-references.xml) | The `Web`, `App`, and `Api` `.csproj` excerpts that prove the layer rule is mechanical: `Web` *cannot* reference `Api` because the project graph doesn't allow it. The *what* shelf. |

## Three shelves and a counterexample

The article's central frame: four day-one decisions outlived a stale plan
because they sat on shelves the code couldn't reach to drift away from.

- **Shelf 1 — `CLAUDE.md`.** Holds the *why* of the layer rules in one
  sentence. Sample `01`.
- **Shelf 2 — the `.csproj` graph.** Holds the *what* — the rule is
  mechanically unenforceable to violate at the call site. Sample `04`.
- **Shelf 3 — Claude Code project memory.** Holds past corrections so the
  next session opens with them already loaded. Sample `02`.
- **Counterexample — the calendar.** The lesson I'm paying the cost of in
  real time, because the provisioning sentence in `plan.md` was never on
  a shelf. Sample `03` is what I should have written on day one.

## License

MIT — copy, adapt, ship.
