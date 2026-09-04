namespace SocietyManagement.Mobile.Core.Auth;

public record AuthResult(bool Success, string? ErrorMessage);

/// <summary>Orchestrates login/logout/session-restore against AuthClient —
/// mirrors AuthService.ts's role on the Angular side. ViewModels talk to
/// this, never to AuthClient directly.</summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(string identifier, string password, string? societyCode);

    /// <summary>Called once at app startup — if a refresh token is stored,
    /// validates it against GET /api/auth/me (which transparently refreshes
    /// an expired access token via AuthDelegatingHandler) and repopulates
    /// AuthState. Mirrors app.config.ts's provideAppInitializer restoring
    /// the session before the UI renders.</summary>
    Task<bool> RestoreSessionAsync();

    Task LogoutAsync();
}
