# Dapper at Scale: The Stored-Procedure Lessons That Cost Me an Afternoon

Companion code samples for the Medium article.

**[Read the full article on Medium →](https://medium.com/@tnjturner/TODO-update-after-publish)**

## TL;DR

Generating a good stored procedure is one problem (that was the
[last article](https://medium.com/@tnjturner/dapper-llms-generating-stored-procedures-youd-actually-ship-e0f557a885e1)).
Living with a stored-procedure-only Dapper data layer across fourteen
repositories (the data-access pattern — `UserRepository`, `GameRepository`,
etc. — not Git repos) is a different one. This repo has the artifacts behind four
lessons: go SP-only and mean it, make every seed idempotent, give every proc
one error contract, and collapse the boilerplate into a single `IDapperBase` —
then watch out for the bug that taught me a pattern is the *rule plus the
constraints it implicitly relies on*.

## Files

| File | What it is |
|---|---|
| [`code-samples/01-thin-repository.cs`](code-samples/01-thin-repository.cs) | What a repository looks like once data access is standardized — every method is one call. |
| [`code-samples/02-idapperbase.cs`](code-samples/02-idapperbase.cs) | The single contract every repository talks to. |
| [`code-samples/03-bad-buildparameters.cs`](code-samples/03-bad-buildparameters.cs) | The ported parameter binder that skips nulls — and the production bug it caused. |
| [`code-samples/04-good-buildparameters.cs`](code-samples/04-good-buildparameters.cs) | The one-line fix: let nulls flow through as `DBNull`. Compare against `03`. |
| [`code-samples/05-error-trailer.sql`](code-samples/05-error-trailer.sql) | The `@ErrorMessage` / `@ErrorFound` output-param error contract every proc follows. |
| [`code-samples/06-sproc-constants.cs`](code-samples/06-sproc-constants.cs) | Per-domain sproc-name constants — a rename is a compile error, not a runtime miss. |
| [`code-samples/07-idempotent-seeds.sql`](code-samples/07-idempotent-seeds.sql) | The two idempotency guard shapes, and when to use each. |

## The one bug to take away

The `03` → `04` diff is the whole "port the constraint, not just the code"
lesson in two files. The binder in `03` was copied from a codebase where every
proc had `= NULL` defaults on its nullable parameters. Ours didn't. The first
request with a null property failed with:

> Procedure 'RefreshTokens_Create' expects parameter '@CodeChallenge', which was not supplied.

A pattern is the rule *plus* all the constraints it implicitly relies on. Port
the rule without the constraint and you've built a trap that won't fire until
production data hits it.

## License

MIT — copy, adapt, ship.
