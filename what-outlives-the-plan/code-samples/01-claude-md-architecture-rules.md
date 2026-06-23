# `CLAUDE.md` — architecture rules that outlived the plan

This is the relevant slice of the `CLAUDE.md` from the **UpAllNight** repo. The
project layout has reorganized three times since this file was written; these
rules haven't moved. They sit at the root of the repo and load into every
Claude Code session.

The point is the *shape*: one paragraph of project layout, one *hard rule*
sentence below it, and a short list of data-access conventions. All decisions,
no current state. The C# can iterate freely underneath them.

```markdown
## Solution Layout (`src/UpAllNight.slnx`)

Clean-architecture .NET 10 solution. Project reference rules are strict —
do not cross layers.

- **UpAllNight.Api** — ASP.NET Core Web API. Entry point (`Program.cs`),
  controllers under `Controllers/`, SignalR hub at `Hubs/GameHub.cs`.
- **UpAllNight.Services** — Domain models, business rules, and both
  *service* interfaces (`Interfaces/Services`) and *repository*
  interfaces (`Interfaces/Repositories`). No Dapper, no DTOs.
- **UpAllNight.Infrastructure** — Dapper repository implementations,
  `ConnectionFactory`, DB DTOs. Implements the interfaces defined in
  `Services` — `Services` has zero knowledge of `Infrastructure`.
- **UpAllNight.Contracts** — HTTP request/response + SignalR payload
  types only. Shared between `Api` and the Blazor client (`App`).
- **UpAllNight.App** — Razor Class Library containing all shared Blazor
  pages, components, and client services. **Pages live here, not in
  Web/Mobile.**
- **UpAllNight.Web** — Thin Blazor WebAssembly host. References `App`
  + `Contracts`. No pages of its own.
- **UpAllNight.Mobile** — Thin .NET MAUI Blazor Hybrid host for
  iOS/Android. References `App` + `Contracts`.

**Hard rule:** `Web` and `Mobile` talk to `Api` via HTTP/SignalR only —
no project reference to `Api`, `Services`, or `Infrastructure`.

## Data Access Conventions

- Dapper only — **no Entity Framework**.
- All database access goes through **stored procedures**. Do not add
  inline SQL in new repositories.
- Write **explicit mappings** between DB DTOs and domain models.
  **Never use AutoMapper.**
- Pass `CancellationToken` to every async method.
- Use primary constructors for DI.
```

## Why this shape outlives the plan

Three observations from a year of watching this file *not change*:

**Nothing in it describes current state.** Notice there are no file paths
beyond the project roots, no class names, no method signatures. The plan
that sat alongside this had all of those, and they're all stale now.
`CLAUDE.md` doesn't, so there's nothing in here for the code to drift away
from.

**The negative rules carry their weight.** *"No Entity Framework. Never use
AutoMapper. No project reference to Api/Services/Infrastructure."* Negative
rules are durable — adding EF would require deleting "no EF" first, and
nobody does that quietly. Positive rules drift; negative rules block.

**One sentence is the hard rule.** Everything else is context. When I quote
this file in PR review, I quote *that* sentence. It hasn't moved in a year
and at this point I'm not sure it ever will.
