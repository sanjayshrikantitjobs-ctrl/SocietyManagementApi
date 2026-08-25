using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage implementation of IFileStorageService — swaps out
/// LocalFileStorageService's on-disk saves so uploaded images (visitor
/// photos, festival banners, sponsor logos, ...) survive app restarts/
/// redeploys and are reachable from any client, not just whichever API
/// instance's local disk happened to receive the upload. Returns the
/// blob's full public URL; AssetUrlPipe on the frontend already passes
/// absolute URLs through unchanged (see asset-url.pipe.ts), so no
/// frontend change was needed for this swap.
/// </summary>
public class AzureBlobFileStorageService : IFileStorageService
{
    private static readonly string[] AllowedExtensions =
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    // Guards CreateIfNotExistsAsync so it only round-trips once per process
    // instead of on every upload — the service is registered Scoped (one
    // instance per request), so this state has to live outside the instance.
    private static volatile bool _containerEnsured;
    private static readonly SemaphoreSlim EnsureLock = new(1, 1);

    private readonly BlobContainerClient _containerClient;

    public AzureBlobFileStorageService(BlobServiceClient blobServiceClient, string containerName)
    {
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> SaveAsync(byte[] content, string fileName, string folder, CancellationToken ct = default)
    {
        if (content.Length == 0 || content.Length > MaxFileSizeBytes)
        {
            throw new BadRequestAppException("File is empty or exceeds the 10 MB upload limit.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new BadRequestAppException($"File type '{extension}' is not allowed.");
        }

        var safeFolder = string.Join('/', folder.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => string.Concat(segment.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'))));

        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var blobName = $"{safeFolder}/{uniqueFileName}";

        await EnsureContainerAsync(ct);

        var blobClient = _containerClient.GetBlobClient(blobName);
        await using var stream = new MemoryStream(content);
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = GetContentType(extension) } },
            ct);

        return blobClient.Uri.ToString();
    }

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerEnsured) return;
        await EnsureLock.WaitAsync(ct);
        try
        {
            if (_containerEnsured) return;
            // PublicAccessType.Blob: anonymous read of individual blobs by URL,
            // but no container listing/browsing — matches the trust level the
            // old local /uploads static-file path already had (no auth check).
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
            _containerEnsured = true;
        }
        finally
        {
            EnsureLock.Release();
        }
    }

    private static string GetContentType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}
