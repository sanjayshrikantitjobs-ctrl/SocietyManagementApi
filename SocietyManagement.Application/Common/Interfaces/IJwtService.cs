using System.Security.Claims;
using SocietyManagement.Domain.Entities;

namespace SocietyManagement.Application.Common.Interfaces;

public interface IJwtService
{
    /// <summary>Short-lived (see AppConstants.AccessTokenExpiryMinutes) signed JWT
    /// carrying sub, role and a "perm" claim per permission code.</summary>
    string GenerateAccessToken(User user, IEnumerable<string> permissions);

    /// <summary>Extracts claims from an access token even if it has already expired
    /// (signature is still validated) — used by the refresh-token flow.</summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
