namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Abstraction over on-disk/blob file storage so Application handlers
/// (banner images, sponsor logos, bill uploads) don't depend on where files
/// physically live. Implemented by Infrastructure.Services.LocalFileStorageService
/// today; swap the implementation for Azure Blob Storage later without touching
/// any handler.</summary>
public interface IFileStorageService
{
    /// <summary>Saves the file under the given logical folder and returns a
    /// relative URL (e.g. "/uploads/festivals/12/banner_guid.jpg") suitable for
    /// storing on an entity and serving back via static files.</summary>
    Task<string> SaveAsync(byte[] content, string fileName, string folder, CancellationToken ct = default);
}
