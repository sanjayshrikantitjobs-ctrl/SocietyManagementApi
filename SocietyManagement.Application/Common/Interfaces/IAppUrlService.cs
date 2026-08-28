namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Builds absolute links back into this app for things embedded in
/// outbound messages (WhatsApp, email) that must work from any device — e.g.
/// a visitor-approval link opened straight from WhatsApp, with no session.</summary>
public interface IAppUrlService
{
    /// <summary>Returns null if App:PublicBaseUrl isn't configured — callers
    /// should skip embedding a link rather than send a broken one.</summary>
    string? BuildAbsoluteUrl(string relativePath);
}
