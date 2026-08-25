namespace SocietyManagement.Application.Common.Interfaces;

/// <summary>Abstraction over on-disk/blob file storage so Application handlers
/// (banner images, sponsor logos, bill uploads) don't depend on where files
/// physically live. Implemented by Infrastructure.Services.AzureBlobFileStorageService
/// when AzureBlobStorage:ConnectionString is configured, LocalFileStorageService
/// otherwise (see DependencyInjection.cs) — handlers never depend on which.</summary>
public interface IFileStorageService
{
    /// <summary>Saves the file under the given logical folder and returns the
    /// URL to store on the owning entity: a relative path for local disk (e.g.
    /// "/uploads/festivals/12/banner_guid.jpg") or an absolute blob URL for
    /// Azure — either way, render it through AssetUrlPipe on the frontend.</summary>
    Task<string> SaveAsync(byte[] content, string fileName, string folder, CancellationToken ct = default);
}
