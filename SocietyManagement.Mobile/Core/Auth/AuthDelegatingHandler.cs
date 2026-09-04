using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SocietyManagement.Mobile.Api.Generated;

namespace SocietyManagement.Mobile.Core.Auth;

/// <summary>Attached to every generated API client's HttpClient
/// (ApiClientsRegistration) — mirrors the Angular app's jwt.interceptor.ts:
/// attaches the bearer token to every request, and on a 401 does a
/// single-flight refresh-and-retry-once before giving up. Deliberately does
/// NOT depend on the generated AuthClient — AuthClient's own HttpClient goes
/// through this same handler, so injecting AuthClient here would be a
/// circular DI dependency at handler-construction time. Instead the refresh
/// call is a hand-built request against a bare HttpClient (no handler
/// chain), reusing only the generated DTOs (RefreshTokenCommand,
/// LoginResponseDtoApiResponse) for correct request/response shapes.</summary>
public class AuthDelegatingHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private readonly ITokenStorage _tokenStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthState _authState;

    public AuthDelegatingHandler(ITokenStorage tokenStorage, IHttpClientFactory httpClientFactory, AuthState authState)
    {
        _tokenStorage = tokenStorage;
        _httpClientFactory = httpClientFactory;
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isRefreshEndpoint = request.RequestUri?.AbsolutePath.Contains("/auth/refresh-token", StringComparison.OrdinalIgnoreCase) == true;

        var accessToken = await _tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // The refresh endpoint itself is [AllowAnonymous] — a 401 from it means
        // the refresh token is dead, not that we need to refresh-and-retry it.
        if (response.StatusCode != HttpStatusCode.Unauthorized || isRefreshEndpoint)
        {
            return response;
        }

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            // Someone else may have already refreshed while we waited — if the
            // stored token changed since we attached ours, just retry with it.
            var latestToken = await _tokenStorage.GetAccessTokenAsync();
            var refreshed = !string.IsNullOrEmpty(latestToken) && latestToken != accessToken;

            if (!refreshed)
            {
                refreshed = await TryRefreshTokenAsync(cancellationToken);
                latestToken = await _tokenStorage.GetAccessTokenAsync();
            }

            if (!refreshed || string.IsNullOrEmpty(latestToken))
            {
                await _tokenStorage.ClearAsync();
                _authState.Clear();
                return response;
            }

            response.Dispose();
            var retryRequest = await CloneRequestAsync(request);
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", latestToken);
            return await base.SendAsync(retryRequest, cancellationToken);
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken)) return false;

        try
        {
            // The named "raw" client — same base address and platform HTTP
            // handler as every other client, but deliberately without
            // AuthDelegatingHandler in its pipeline (see
            // ApiClientsRegistration.AddSocietyApiClients), so this call can
            // never recurse back into itself.
            var rawClient = _httpClientFactory.CreateClient(ApiClientsRegistration.RawClientName);

            var command = new RefreshTokenCommand { AccessToken = string.Empty, RefreshToken = refreshToken, IpAddress = null };
            using var httpResponse = await rawClient.PostAsync(
                "api/Auth/refresh-token",
                new StringContent(JsonSerializer.Serialize(command), System.Text.Encoding.UTF8, "application/json"),
                cancellationToken);

            if (!httpResponse.IsSuccessStatusCode) return false;

            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<LoginResponseDtoApiResponse>(body);
            if (result?.Data?.AccessToken is null || result.Data.RefreshToken is null) return false;

            await _tokenStorage.SaveTokensAsync(result.Data.AccessToken, result.Data.RefreshToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        if (original.Content != null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }
}
