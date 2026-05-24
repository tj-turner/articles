// THE TRAP. Ported almost verbatim from a known-good internal codebase.
// It reflects over the request object and adds every property as a parameter —
// but it SKIPS nulls.
//
// That worked in the original codebase because every stored procedure there
// declared its nullable parameters with "= NULL" defaults, so a skipped
// parameter simply fell back to its default. The code relied on a convention
// that lived in the *database*, not in this method.
//
// Our procs don't have "= NULL" defaults. So the first time a request carried a
// null property (a non-PKCE refresh token with a null @CodeChallenge), the
// parameter was omitted entirely and SQL Server rejected the call:
//
//   Procedure 'RefreshTokens_Create' expects parameter '@CodeChallenge',
//   which was not supplied.

private static DynamicParameters BuildParameters(object? request)
{
    var parameters = new DynamicParameters();
    if (request is null)
    {
        return parameters;
    }

    foreach (var prop in request.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        var value = prop.GetValue(request);

        if (value is null) continue;   // <-- the trap: a constraint we didn't port

        parameters.Add(prop.Name, value);
    }

    return parameters;
}
