namespace TrustFlow.Api.Services.Auth;

public interface IRefreshTokenService
{
    RefreshTokenCreationResult Create(
        Guid userId,
        Guid? familyId = null
    );

    string HashToken(string rawToken);
}