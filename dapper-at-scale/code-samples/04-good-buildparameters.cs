// THE FIX. One behavioral change: let nulls flow through as DBNull so the proc
// receives every parameter it declares. We no longer rely on "= NULL" defaults
// existing in the database — the binder now satisfies the contract on its own.
//
// The only thing we still skip is an empty byte[], which Dapper would otherwise
// pass as a 0-length VARBINARY that sprocs typically reject.
//
// From UpAllNight.Infrastructure.Bases.DapperBase.

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

        // Skip empty byte[] (empty byte arrays pass through Dapper as
        // 0-length VARBINARY which sprocs typically reject). Nulls flow
        // through as DBNull so sprocs receive every declared parameter.
        if (value is byte[] bytes && bytes.Length == 0) continue;

        parameters.Add(prop.Name, value);
    }

    return parameters;
}
