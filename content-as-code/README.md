# Content-as-Code for AI: Prompts and Skills You'd Actually Review

Companion diagram and code for the Medium article. Prompt text is the
highest-leverage thing in an AI system and the least reviewable thing in the
repo. This is the case for making it a source file — embedded in the build,
diffed in a pull request, and checked by the compiler where a human reader
can't.

**[Read the full article on Level Up Coding →](#)** *(link pending publish)*

## TL;DR

- **String literals aren't just ugly, they're unreviewable.** A reflowed
  paragraph and a rewritten safety rule produce diffs that look the same, the
  reviewer you actually want doesn't read C#, and CI has no way to tell a prompt
  change from a rename — so the adversarial evaluation suite never fires.
- **The question isn't where the bytes live, it's when the instruction set stops
  being able to change.** Serious prompt registries have versions, approvals and
  publish events, so the usual "a database row has no history" argument is
  false. The property worth having is that a running process cannot be talked
  into a different set of instructions — by an admin screen, an env var, a flag,
  or a support engineer at 2am.
- **Runtime may narrow, never author.** Kill switches, flags and choosing among
  shipped variants are configuration. Composing a new sentence the model will
  treat as an instruction is authoring, and authoring belongs in a pull request.
- **Instructions ship in the build; values arrive at runtime, and values are
  data.** A substitution slot is a channel into the trust region — a
  customer-editable display name spliced raw into a system prompt is an edit to
  the most trusted text in the process, made from outside the company.
- **Prompts are Markdown files with YAML frontmatter, embedded as resources.**
  A source generator emits a typed id per prompt and binds `{{context.…}}`
  placeholders to a context record, so a missing prompt and a mistyped
  placeholder are both compile errors. The failure mode this replaces is
  silence: an absent prompt yields an empty system prompt, and an assistant with
  no rules doesn't throw.
- **No control flow in prompt files.** The moment a prompt can branch, reading
  the file stops telling you what the model saw. Composition happens in an
  assembler class, in code, under review.
- **A skill is two files.** The companion Markdown carries the prose the model
  reads; C# attributes carry the metadata the loop enforces. Prose is for humans
  to review, metadata is for the compiler to hold.
- **The registry pairs both halves at startup and refuses to boot if it can't.**
  A class with no description, a description with no class, a classification
  that disagrees between them, or a skill that declares itself a read and also
  carries `[SkillWrite]` — all four stop the service.
- **`safetyCritical` in the frontmatter is what the red-team suite keys off.**
  Adversarial runs go against the assembled prompt, and a scheduled release
  waits for a clean one. The expedited hotfix lane is the honest gap: it's the
  path built for the worst moment and the one least likely to have that
  coverage.

## Files

| File | What it is |
|---|---|
| [`header-content-as-code.png`](header-content-as-code.png) / [`.webp`](header-content-as-code.webp) | Header image, 1536×864. AI-generated with Microsoft Copilot; the source render was cropped to remove the provider's watermark. |
| [`pairing-check.html`](pairing-check.html) | Source for the startup pairing figure. Hand-authored HTML/CSS rather than Mermaid: the figure needs two authored columns of equal weight converging on one gate, and the attribute syntax and frontmatter keys have to be character-correct in an article arguing for correctness. Render with headless Chrome at `deviceScaleFactor: 2`. |
| [`pairing-check.webp`](pairing-check.webp) | Rendered figure used in the article. |
| [`code-samples/01-agent-chat-tenant.md`](code-samples/01-agent-chat-tenant.md) | A real prompt file: frontmatter, placeholders, and prose. This is the artifact the whole article is arguing for. |
| [`code-samples/02-prompts.cs`](code-samples/02-prompts.cs) | The typed `PromptId`, the frontmatter model, and the embedded-resource store. Note that there is no reload path and no setter — the only way to change a prompt is to ship a build. |
| [`code-samples/03-prompt-assembler.cs`](code-samples/03-prompt-assembler.cs) | Composition in code: fixed fragment order, and a template renderer that substitutes and nothing else. |
| [`code-samples/04-list-invoices.md`](code-samples/04-list-invoices.md) | The companion description for a skill — the prose half, which is what the model actually reads when deciding whether to call it. |
| [`code-samples/05-skill-authoring.cs`](code-samples/05-skill-authoring.cs) | The metadata half: the attributes, the `SkillContext` record, and one real skill. Worth noting what `SkillContext` deliberately omits. |
| [`code-samples/06-startup-pairing-check.cs`](code-samples/06-startup-pairing-check.cs) | The registry build that pairs classes with descriptions and throws on all three mismatches at once. |

The `.cs` files compile as a set against `net10.0`. The source generator is not
included — `PromptId` and its members are written out by hand to show the shape
of what a generator emits.

## What comes next

The safety controls this article keeps referring to — propose-confirm-execute,
structural fencing on retrieved content, doc-action separation — get their own
piece, including the argument for applying them to trusted documents too.

## License

MIT — copy, adapt, ship.
