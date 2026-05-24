// The single contract every repository talks to. One place that knows how to
// run a stored procedure, bind parameters, and read the standard output params.
//
// All sprocs in the codebase follow one convention:
//   - trailing @ErrorMessage NVARCHAR(MAX) OUTPUT and @ErrorFound BIT OUTPUT
//   - Insert/Update sprocs add @NewId OUTPUT (typed per sproc)
// Reads return ListResponse<T> for multi-row paths and T? for single-row paths
// (single-row throws on ErrorFound = 1). Writes return CommandResponse<TId>.
//
// Trimmed from UpAllNight.Infrastructure.Bases.IDapperBase (some overloads omitted).

public interface IDapperBase
{
    Task<CommandResponse<int>> ExecuteAsync(
        object request,
        string procedureName,
        CancellationToken cancellationToken = default,
        string outputErrorMessage = "ErrorMessage",
        string errorFound = "ErrorFound");

    Task<CommandResponse<TId>> ExecuteCommandAsync<TId>(
        object request,
        string procedureName,
        CancellationToken cancellationToken = default,
        string idParameter = "NewId",
        string outputErrorMessage = "ErrorMessage",
        string errorFound = "ErrorFound",
        int? commandTimeout = null);

    Task<T?> GetRecordAsync<T>(
        object request,
        string procedureName,
        CancellationToken cancellationToken = default,
        string outputErrorMessage = "ErrorMessage",
        string errorFound = "ErrorFound");

    Task<ListResponse<T>> GetRecordsAsync<T>(
        object request,
        string procedureName,
        CancellationToken cancellationToken = default,
        string outputErrorMessage = "ErrorMessage",
        string errorFound = "ErrorFound");

    Task<CommandResponse<TId>> InsertRecordAsync<TId>(
        object request,
        string procedureName,
        CancellationToken cancellationToken = default,
        string idParameter = "NewId",
        string outputErrorMessage = "ErrorMessage",
        string errorFound = "ErrorFound");
}
