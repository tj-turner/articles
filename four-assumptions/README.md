# Building AI Infrastructure: Four Backend Assumptions to Rewire

Companion diagram and code for the Medium article — the first of several pieces
on building real infrastructure behind an AI product. Four assumptions a decade
of backend work taught you to trust, and how each one inverts the moment a
language model is in the loop.

**[Read the full article on Level Up Coding →](#)** *(link pending publish)*

## TL;DR

A slide caption we wrote ourselves — *"delete my data"* — got retrieved, read by
the model, and treated as an instruction. No attacker, no injection payload, no
exception. That one turn breaks four backend assumptions at once:

1. **Content is inert.** No — a model reads instructions and data in the same
   token stream, so *any* retrieved document is a potential instruction source.
   Documents are never sources of instructions, regardless of who authored them.
2. **Failures announce themselves.** No — an AI safety failure is a `200` with a
   fluent, wrong answer. Detection has to be an active discipline, not an alert.
3. **Cost is roughly fixed per request.** No — per-turn cost varies 10–100×, so
   spend governance moves *into* the turn (a cap-check before the expensive part).
4. **Retrying is safe.** No — a model call isn't idempotent, and a re-driven
   action is a second action. Reads retry; writes get idempotency instead.

## Files

| File | What it is |
|---|---|
| [`one-turn-blast-radius.mmd`](one-turn-blast-radius.mmd) | Mermaid source for the "one turn's blast radius" diagram — the real path of a single turn, with the four assumption-failures pinned to the hops where they bite. |
| [`one-turn-blast-radius.webp`](one-turn-blast-radius.webp) | Rendered diagram used in the article. |
| [`code-samples/01-retry-reads-vs-writes.cs`](code-samples/01-retry-reads-vs-writes.cs) | The retry split from Assumption 4 — reads/model calls get bounded backoff on `429`; writes carry an idempotency key and never retry. The point is the split, not the Polly config. |

## What comes next

Each of these four inversions gets its own piece of infrastructure in future
articles — a shared foundation library, content-as-code for prompts and skills,
safety by composition, the async plane, and RAG as infrastructure. Every article
stands alone; read them in any order.

## License

MIT — copy, adapt, ship.
