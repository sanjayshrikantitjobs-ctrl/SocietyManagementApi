using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Tests.Fakes;

/// <summary>Never touches disk/blob storage — just returns a deterministic
/// fake URL so handler tests can assert an image was (or wasn't) "saved"
/// without any real I/O.</summary>
public class FakeFileStorageService : IFileStorageService
{
    public int SaveCallCount { get; private set; }

    public Task<string> SaveAsync(byte[] content, string fileName, string folder, CancellationToken ct = default)
    {
        SaveCallCount++;
        return Task.FromResult($"https://fake-storage.test/{folder}/{fileName}");
    }
}
