namespace SocietyManagement.Mobile.Core.Auth;

/// <summary>Persists the access/refresh token pair between app launches.
/// Backed by platform secure storage (Keychain on iOS, Keystore-backed on
/// Android) — a stronger guarantee than the web app's own sessionStorage
/// choice, same intent: never plain-text-persist a bearer token.</summary>
public interface ITokenStorage
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task SaveTokensAsync(string accessToken, string refreshToken);
    Task ClearAsync();
}
