using SocietyManagement.Application.Common.Interfaces;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Infrastructure.Services;

/// <summary>
/// Local-disk implementation of IFileStorageService — saves under
/// {AppContext.BaseDirectory}/wwwroot/uploads/{folder}/, which is exactly the
/// WebRootPath ASP.NET Core's static file middleware serves from by default
/// (see Program.cs's app.UseStaticFiles()), so the returned "/uploads/..."
/// URL resolves immediately with no extra wiring. Swap for an Azure Blob
/// Storage implementation at deploy time — the interface doesn't change.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private static readonly string[] AllowedExtensions =
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public Task<string> SaveAsync(byte[] content, string fileName, string folder, CancellationToken ct = default)
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
        var absoluteFolder = Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads", safeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var absolutePath = Path.Combine(absoluteFolder, uniqueFileName);
        File.WriteAllBytes(absolutePath, content);

        return Task.FromResult($"/uploads/{safeFolder}/{uniqueFileName}");
    }
}
