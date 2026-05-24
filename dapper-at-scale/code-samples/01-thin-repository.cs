// A repository once you've standardized on IDapperBase + stored procedures.
// Notice what ISN'T here: no SQL strings, no connection management, no manual
// parameter binding, no try/catch-to-log boilerplate. Every method is one call.
//
// Trimmed from UpAllNight.Infrastructure.Repositories.RefreshTokenRepository.

public class RefreshTokenRepository(IDapperBase db) : IRefreshTokenRepository
{
    public async Task<RefreshToken> CreateAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        var dto = await db.GetRecordAsync<RefreshTokenDto>(
            new { token.TokenId, token.UserId, token.TokenHash, token.FamilyId, token.CodeChallenge, token.ExpiresAt },
            RefreshTokensStoredProcedures.Create,
            cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            throw new InvalidOperationException($"{RefreshTokensStoredProcedures.Create} did not return the inserted row.");
        }

        return MapToRefreshToken(dto);
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var dto = await db.GetRecordAsync<RefreshTokenDto>(
            new { TokenHash = tokenHash },
            RefreshTokensStoredProcedures.GetByTokenHash,
            cancellationToken).ConfigureAwait(false);

        return dto is null ? null : MapToRefreshToken(dto);
    }

    public async Task RevokeTokenAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var response = await db.ExecuteAsync(
            new { TokenId = tokenId },
            RefreshTokensStoredProcedures.Revoke,
            cancellationToken).ConfigureAwait(false);

        if (response.ErrorFound)
        {
            throw new InvalidOperationException($"{RefreshTokensStoredProcedures.Revoke} reported error: {response.ErrorMessage}");
        }
    }

    // Explicit mapping — no AutoMapper. The mapping is greppable and refactorable.
    private static RefreshToken MapToRefreshToken(RefreshTokenDto dto) => new()
    {
        TokenId = dto.TokenId,
        UserId = dto.UserId,
        TokenHash = dto.TokenHash,
        FamilyId = dto.FamilyId,
        CodeChallenge = dto.CodeChallenge,
        ExpiresAt = dto.ExpiresAt,
        RevokedAt = dto.RevokedAt,
        CreatedAt = dto.CreatedAt
    };
}
