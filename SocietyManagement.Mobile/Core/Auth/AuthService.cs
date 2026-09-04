using System.Text.Json;
using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core;

namespace SocietyManagement.Mobile.Core.Auth;

public class AuthService : IAuthService
{
    private readonly AuthClient _authClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly AuthState _authState;
    private readonly CurrentSocietyService _currentSocietyService;

    public AuthService(AuthClient authClient, ITokenStorage tokenStorage, AuthState authState, CurrentSocietyService currentSocietyService)
    {
        _authClient = authClient;
        _tokenStorage = tokenStorage;
        _authState = authState;
        _currentSocietyService = currentSocietyService;
    }

    public async Task<AuthResult> LoginAsync(string identifier, string password, string? societyCode)
    {
        try
        {
            var response = await _authClient.LoginPOSTAsync(new LoginCommand
            {
                Identifier = identifier,
                Password = password,
                SocietyCode = string.IsNullOrWhiteSpace(societyCode) ? null : societyCode,
                IpAddress = null
            });

            if (response.Data?.AccessToken is null || response.Data.RefreshToken is null || response.Data.User is null)
            {
                return new AuthResult(false, "Unexpected response from server.");
            }

            await _tokenStorage.SaveTokensAsync(response.Data.AccessToken, response.Data.RefreshToken);
            _authState.SetUser(response.Data.User);
            return new AuthResult(true, null);
        }
        catch (AuthApiException ex)
        {
            return new AuthResult(false, ExtractMessage(ex.Response) ?? "Login failed. Check your credentials and try again.");
        }
        catch (Exception ex)
        {
            // TEMPORARY: surfacing the real exception while diagnosing a
            // "couldn't reach the server" report — this hid whether it was
            // actually a network failure, a TLS issue, or something else
            // (e.g. SecureStorage) entirely. Revert to a plain friendly
            // message once the root cause here is confirmed fixed.
            return new AuthResult(false, $"Couldn't reach the server ({ex.GetType().Name}: {ex.Message})");
        }
    }

    public async Task<bool> RestoreSessionAsync()
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync();
        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        try
        {
            // Goes through AuthDelegatingHandler, which transparently refreshes
            // an expired access token before this call would ever fail with 401.
            var response = await _authClient.MeAsync();
            if (response.Data is null)
            {
                await _tokenStorage.ClearAsync();
                return false;
            }

            _authState.SetUser(response.Data);
            return true;
        }
        catch
        {
            await _tokenStorage.ClearAsync();
            _authState.Clear();
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                await _authClient.LogoutAsync(new LogoutCommand { RefreshToken = refreshToken });
            }
            catch
            {
                // Best-effort server-side revoke — clearing local state below is
                // what actually signs the user out on this device either way.
            }
        }

        await _tokenStorage.ClearAsync();
        _authState.Clear();
        _currentSocietyService.Reset();
    }

    private static string? ExtractMessage(string? rawJsonBody)
    {
        if (string.IsNullOrWhiteSpace(rawJsonBody)) return null;
        try
        {
            using var doc = JsonDocument.Parse(rawJsonBody);
            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
