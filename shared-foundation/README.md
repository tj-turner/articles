# The Shared Foundation: Building an AI Library You'd Actually Reuse

Companion diagram and code for the Medium article. Writing the safety rule was
the easy part; deciding which repo owned it took the rest of the week and
mattered more. This is the case for putting the agent loop in one shared library
and keeping every consuming service thin enough that there's nothing left in it
to drift.

**[Read the full article on Level Up Coding →](https://levelup.gitconnected.com/the-shared-foundation-building-an-ai-library-youd-actually-reuse-4b156de92bd3)**

## TL;DR

- **A library, not a service.** An orchestrator service buys you forced upgrades
  and charges you a second streaming surface and a shared failure domain. The
  hop costs almost nothing in latency — a few milliseconds against a model call
  measured in seconds — so don't argue it on latency. Argue it on what you own.
- **Three groups go in the package:** orchestration (the loop), the safety
  primitives it composes, and the plumbing. The loop is the control flow of a
  turn; the harness is everything the loop runs inside. The library holds both.
- **The per-turn state object is the safety architecture** — not a diagram of
  it. Make its invariants structural: a latch with a private setter beats a
  public flag under a comment saying "never reset this."
- **Safety is an ordering, not a class.** Move the primitives around and they
  all still pass their unit tests while the system stops working.
- **Doc-action separation runs per batch of tool calls, not once per turn.** A
  model can emit a search and a delete in one response; a manifest filter built
  before either ran would have offered the write tool anyway. Filter the
  manifest *and* the batch — one removes the temptation, the other the
  possibility.
- **Consumers are adapters:** authorize, build options, call, translate. If a
  service does more than that, it owns a second loop and it will drift.

## Files

| File | What it is |
|---|---|
| [`support-files/package-boundary.html`](support-files/package-boundary.html) | Source for the "package boundary" figure. Hand-authored HTML/CSS rather than Mermaid on purpose — the argument is carried by *proportion* (thin consumers, thick library), and Mermaid lays out nodes without any notion of visual weight. Render with headless Chrome. |
| [`package-boundary.webp`](package-boundary.webp) | Rendered figure used in the article. |
| [`code-samples/01-agent-orchestrator-contract.cs`](code-samples/01-agent-orchestrator-contract.cs) | The loop behind one interface, plus the options record. The invariant worth stealing: every option may only *narrow* what a turn can do. |
| [`code-samples/02-turn-state.cs`](code-samples/02-turn-state.cs) | Per-turn state with a monotonic retrieval latch, and the doc-action separation filter applied to both the manifest and each dispatch batch. |
| [`code-samples/03-chat-adapter-endpoint.cs`](code-samples/03-chat-adapter-endpoint.cs) | An entire consuming service. Note the `Results.Empty` return — the observer has already written the SSE body — and the ownership check that deliberately stays out of the library. |

## What comes next

Prompts and skill definitions are the highest-leverage part of the harness — how
the loop learns what it can touch — and they belong neither in code nor in
config. [That's the next
piece](https://levelup.gitconnected.com/content-as-code-for-ai-prompts-and-skills-youd-actually-review-6b67401d52c7).
The argument for why a confirmation step isn't enough on its own, and what has to
sit in front of it, gets its own article too.

## License

MIT — copy, adapt, ship.
