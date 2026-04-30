# Dapper + LLMs: Generating Stored Procedures You'd Actually Ship

Companion code samples for the Medium article.

**[Read the full article on Medium →](https://medium.com/@tnjturner/TODO-update-after-publish)**

## TL;DR

LLMs generate the *median* stored procedure from their training data. Your
codebase has a specific shape — naming conventions, error contracts, how the
data-access layer binds to procs. Shipping-quality generation is a context
problem, not a model problem.

This repo has the four artifacts referenced in the article: a bad generation,
a good generation, the conventions file that gets you from one to the other,
and the prompt template that ties it together. The diff between
`01-bad-generation.sql` and `04-good-generation.sql` is the whole article in
two files.

## Files

| File | What it is |
|---|---|
| [`code-samples/01-bad-generation.sql`](code-samples/01-bad-generation.sql) | The kind of stored proc you get from a vague prompt to Codex / Claude / Copilot. Don't ship this. |
| [`code-samples/02-conventions.md`](code-samples/02-conventions.md) | The conventions file you drop in your repo and point your AI assistant at. |
| [`code-samples/03-prompt-template.md`](code-samples/03-prompt-template.md) | Fill-in-the-blanks prompt for generating a new proc with full context. |
| [`code-samples/04-good-generation.sql`](code-samples/04-good-generation.sql) | The proc the prompt produces. Compare against `01`. Same model, same task — only the context changed. |

## How to use these

1. Copy `02-conventions.md` into your database project (e.g. as `sql/conventions.md`).
2. Adjust the rules to match your actual codebase. The error-contract pattern
   in the article is what matters; specific verbs, types, and identifier
   conventions may differ in your shop.
3. Add a one-liner to your `CLAUDE.md` / `.cursorrules` / Copilot instructions
   so every developer's AI assistant reads the conventions automatically:

   > When generating or modifying stored procedures, follow `sql/conventions.md`
   > and use the existing procs in `<your-database-project>/Stored Procedures/`
   > as style exemplars.

4. When you need a new proc, use `03-prompt-template.md` as a starting point.
   Drop in the DTO, two or three sibling procs, the schema slice, and the
   repository call site.
5. Run the six-item review checklist from the article before merging.

## A note on the contract

The good generation in this repo assumes a `DapperBase` wrapper that binds to
output parameters — `@NewId`, `@ErrorMessage`, `@ErrorFound` — and procs that
report errors via output params instead of `THROW`. If your codebase uses a
different contract (raw `SqlConnection`, `THROW` for errors, return values
for new ids, etc.), the structural lesson still applies: tell the model what
the contract is. Don't make it guess.

## License

MIT — copy, adapt, ship.
