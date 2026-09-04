using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Core;

/// <summary>Resolves which society a society-scoped API call should use —
/// mirrors current-society.service.ts's role on the web (a placeholder
/// pending true per-user multi-tenancy, per that file's own doc comment).
/// For Admin/Member/Watchman, AuthState.SocietyId (from the JWT's
/// society_id claim) already answers this directly. SuperAdmin has none —
/// not scoped to any single society — so the web app falls back to
/// getSocieties()[0] for shell branding; this does the same, caching the
/// result for the session so every screen isn't re-fetching the society
/// list on every navigation.</summary>
public class CurrentSocietyService
{
    private readonly AuthState _authState;
    private readonly SocietiesClient _societiesClient;
    private int? _resolvedSocietyId;

    public CurrentSocietyService(AuthState authState, SocietiesClient societiesClient)
    {
        _authState = authState;
        _societiesClient = societiesClient;
    }

    public async Task<int?> GetSocietyIdAsync()
    {
        if (_authState.SocietyId is int societyId) return societyId;

        if (_resolvedSocietyId is int cached) return cached;

        var response = await _societiesClient.SocietiesGETAsync();
        var firstSocietyId = response.Data?.FirstOrDefault()?.Id;
        _resolvedSocietyId = firstSocietyId;
        return firstSocietyId;
    }

    /// <summary>Cleared on logout so a different SuperAdmin session (or the
    /// same one after a society is added/removed) re-resolves instead of
    /// reusing a stale cached id.</summary>
    public void Reset() => _resolvedSocietyId = null;
}
