using SocietyManagement.Mobile.Api.Generated;
using SocietyManagement.Mobile.Core.Auth;

namespace SocietyManagement.Mobile.Core;

/// <summary>Resolves which society a society-scoped API call should use, and
/// caches its full details (name/address) for the top bar — mirrors
/// current-society.service.ts's role on the web (a placeholder pending true
/// per-user multi-tenancy, per that file's own doc comment). For
/// Admin/Member/Watchman, AuthState.SocietyId (from the JWT's society_id
/// claim) picks the right entry out of the society list; SuperAdmin has
/// none, so the web app falls back to getSocieties()[0] — this does the
/// same. Society.View is granted to every role (DbSeeder.cs), so this call
/// never needs a permission check of its own.</summary>
public class CurrentSocietyService
{
    private readonly AuthState _authState;
    private readonly SocietiesClient _societiesClient;
    private SocietyDto? _cachedSociety;

    public CurrentSocietyService(AuthState authState, SocietiesClient societiesClient)
    {
        _authState = authState;
        _societiesClient = societiesClient;
    }

    public async Task<SocietyDto?> GetSocietyAsync()
    {
        if (_cachedSociety != null) return _cachedSociety;

        var response = await _societiesClient.SocietiesGETAsync();
        var societies = response.Data ?? new();
        _cachedSociety = _authState.SocietyId is int societyId
            ? societies.FirstOrDefault(s => s.Id == societyId)
            : societies.FirstOrDefault();
        return _cachedSociety;
    }

    public async Task<int?> GetSocietyIdAsync()
    {
        if (_authState.SocietyId is int societyId) return societyId;
        var society = await GetSocietyAsync();
        return society?.Id;
    }

    /// <summary>Cleared on logout so a different session (or the same one
    /// after a society is added/removed) re-resolves instead of reusing a
    /// stale cached value.</summary>
    public void Reset() => _cachedSociety = null;
}
