// Per-domain stored-procedure name constants. One file per domain.
// Every repository call references a constant, never a string literal — so a
// rename is a compile error, not a runtime "could not find stored procedure".
//
// From UpAllNight.Infrastructure.Repositories.StoredProcedures.UsersStoredProcedures.

public static class UsersStoredProcedures
{
    public const string Create = "dbo.Users_Create";
    public const string GetByEmail = "dbo.Users_GetByEmail";
    public const string GetByFriendCode = "dbo.Users_GetByFriendCode";
    public const string GetById = "dbo.Users_GetById";
    public const string GetByUsername = "dbo.Users_GetByUsername";
    public const string GetPasswordHash = "dbo.Users_GetPasswordHash";
    public const string SetFriendCode = "dbo.Users_SetFriendCode";
    public const string UpdateLastLogin = "dbo.Users_UpdateLastLogin";
    public const string UpdatePasswordHash = "dbo.Users_UpdatePasswordHash";
    public const string AnonymizeForDeletion = "dbo.Users_AnonymizeForDeletion";
}
