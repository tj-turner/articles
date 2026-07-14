# Agents That Learn From Rejection

Companion code samples for the Medium article on building a KDP book-generation
system with Claude CLI — and the three-part memory loop that lets it improve
permanently every time reality corrects it.

**[Read the full article on Medium →](#)** *(link pending publish)*

## TL;DR

KDP rejected a book cover for spine text too close to the trim edge — even
though the geometry said it fit. Instead of nudging that one cover, I wrote the
lesson into the *agent*, at three altitudes: a `memory/` note that records the
reason, the shared `kdp-spec.md` that mandates the rule, and the generated
`build.py` that enforces it. One rejection, three files, most-general to
most-specific. Every future book inherits the fix. This folder has all three
forms side by side so you can see the same rule change vocabularies as it flows
downstream.

## Files

| File | What it is |
|---|---|
| [`code-samples/01-agent-spec-frontmatter.md`](code-samples/01-agent-spec-frontmatter.md) | Top of `puzzlebook.md` — a Claude Code subagent spec. The frontmatter is the routing/capability contract; the body is domain expertise as imperatives. The spec *is* the source code. |
| [`code-samples/02-shared-spec-spine-rule.md`](code-samples/02-shared-spec-spine-rule.md) | **The law.** The spine section of the shared `kdp-spec.md` that every agent Reads before building a cover, with the rejection encoded as a rule. |
| [`code-samples/03-memory-file.md`](code-samples/03-memory-file.md) | **The diary.** A `memory/` note with the rigid `rule / Why / How to apply` shape. The *Why* is what keeps the rule from being optimized away. |
| [`code-samples/04-build-py-spine-enforcement.py`](code-samples/04-build-py-spine-enforcement.py) | **The enforcement.** The generated `build.py` drawing an empty spine, reason preserved in a comment. |

## The three forms, one rule

The article's central frame: in a Claude CLI agent system, durable knowledge
lives at three altitudes, and pushing every lesson to the *most general form it
can live in* is what makes the system compound.

- **The memory note (`01` → sample `03`).** Cross-session. Records what
  happened and *why*, so the reason survives even when the rule looks
  inconvenient later.
- **The shared spec (sample `02`).** The single source of truth all three
  agents defer to. Change it once, every agent obeys.
- **The generated code (sample `04`).** What actually runs, with the reason
  carried into a comment for whoever reads the script next.

This is the deliberate inverse of
[*The Rule Lives in Three Places*](https://levelup.gitconnected.com/the-rule-lives-in-three-places-7690081dc542):
there, three copies of a rule were a drift *liability*. Here, the three forms
are a propagation *pipeline* — the specific is always downstream of the general.

## License

MIT — copy, adapt, ship.
