# Prompt Template — Generating a Stored Procedure

Drop the five ingredients into the placeholders. Send to Codex / Claude / Copilot.

---

You are writing a SQL Server stored procedure for an existing .NET 10 codebase
that uses Dapper through a `DapperBase` wrapper. The wrapper expects a specific
output-parameter contract — procs report errors via output params and never
`THROW`. Match the conventions exactly.

## Conventions

<!-- Paste the contents of 02-conventions.md here. -->

## Exemplar procs (style — match these)

<!-- Paste 2–3 sibling procs from the same project. Pick:
     - one Insert (so the model sees @NewId + SCOPE_IDENTITY pattern)
     - one GetById (so it sees the read-side error contract)
     - one Update (only if you're following up with an Update) -->

```sql
-- {Entity}_Insert.sql
…
```

```sql
-- {Entity}_GetById.sql
…
```

## Target table

<!-- Paste only the CREATE TABLE for the target table (and FK referents if relevant).
     Do NOT paste the full schema. -->

```sql
CREATE TABLE [dbo].[GrantProgram] ( … );
```

## C# request DTO

<!-- The exact record/class the repository will pass to the proc.
     Parameter names will be derived from these property names. -->

```csharp
public record CreateGrantProgramParams( … );
```

## Repository call site

<!-- The single line of C# that has to work against the proc you generate.
     This pins the proc name, the PK type, and the DapperBase method. -->

```csharp
public Task<CommandResponse<int>> CreateAsync(
    CreateGrantProgramParams parameters,
    CancellationToken cancellationToken = default) =>
    db.InsertRecordAsync<int>(parameters, "dbo.GrantProgram_Insert", cancellationToken);
```

## Task

Generate `dbo.GrantProgram_Insert.sql`. Match the exemplar procs in style,
indentation, and parameter alignment. Output only the SQL file contents.
