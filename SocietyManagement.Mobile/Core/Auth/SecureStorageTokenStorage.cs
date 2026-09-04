namespace SocietyManagement.Mobile.Core.Auth;

public class SecureStorageTokenStorage : ITokenStorage
{
    private const string AccessTokenKey = "sm_access_token";
    private const string RefreshTokenKey = "sm_refresh_token";

    public Task<string?> GetAccessTokenAsync() => SecureStorage.Default.GetAsync(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync() => SecureStorage.Default.GetAsync(RefreshTokenKey);

    public async Task SaveTokensAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        return Task.CompletedTask;
    }
}
