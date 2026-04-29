# Stored Procedure Conventions

Short on purpose. If a rule isn't here, the exemplar procs cover it.

## Error contract (non-negotiable)

- Every proc has these output params, in this order, at the end of the param list:
  ```
  @NewId         <pk-type>     OUTPUT,   -- inserts only, name must be exactly NewId
  @ErrorMessage  NVARCHAR(MAX) OUTPUT,
  @ErrorFound    BIT           OUTPUT
  ```
- Procs **never** `THROW` or `RAISERROR`. Errors are reported by setting
  `@ErrorFound = 1` and `@ErrorMessage = ERROR_MESSAGE()` inside `BEGIN CATCH`.
- Initialize every output at the top, before `BEGIN TRY`:
  ```
  SET NOCOUNT ON;
  SET @ErrorFound   = 0;
  SET @ErrorMessage = NULL;
  SET @NewId        = 0;   -- inserts only
  ```

## Naming

- Procs: `dbo.{Entity}_{Verb}` — e.g. `GrantProgram_Insert`, `GrantProgram_GetById`,
  `GrantProgram_ListByTenant`, `GrantProgram_UpdateStatus`.
- Parameters match the C# DTO property names exactly. `GrantProgramId`, not `Id`
  or `ProgramId`.
- Bracket all identifiers: `[dbo].[GrantProgram]`, `[GrantProgramId]`.

## Body

- `SELECT` lists are explicit. Never `SELECT *`.
- Reads do not use `WITH (NOLOCK)`. The default isolation level is correct.
- Audit columns: `CreatedBy` and `ModifiedBy` come in as `NVARCHAR(100)` parameters.
  `CreatedDate` / `ModifiedDate` are set by the proc using `SYSDATETIMEOFFSET()`.
- New keys come from `SCOPE_IDENTITY()` for `INT`/`BIGINT` identity columns,
  or `NEWSEQUENTIALID()` / a generated value for `UNIQUEIDENTIFIER`.
- Defaults on optional parameters: `= NULL` for nullable columns; literal default
  (e.g. `= N'USD'`, `= 0`) for non-null columns with a known default.

## What not to do

- No `EXEC(@sql)`, no string-built `ORDER BY`. Whitelist sort columns in a `CASE`.
- No `MERGE` for upserts. Use `IF EXISTS … UPDATE … ELSE INSERT` inside the `TRY`.
- No `THROW;` or `RAISERROR` anywhere. The error contract is the output params.

## File layout

- `GrantTrace.Database/dbo/Stored Procedures/{Entity}_{Verb}.sql`, one proc per file.
- Top-of-file comment describing intent and any non-obvious behavior.
