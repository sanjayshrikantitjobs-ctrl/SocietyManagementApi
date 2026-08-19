using MediatR;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Application.Features.Files;

/// <summary>Generic upload used by every banner/logo/bill-image picker across
/// the app (currently: festivals). IFileStorageService does the extension/size
/// validation and returns the relative URL to store on the owning entity.</summary>
public record UploadFileCommand(byte[] Content, string FileName, string Folder) : IRequest<string>;

public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, string>
{
    private readonly IFileStorageService _fileStorageService;

    public UploadFileCommandHandler(IFileStorageService fileStorageService) => _fileStorageService = fileStorageService;

    public Task<string> Handle(UploadFileCommand request, CancellationToken ct) =>
        _fileStorageService.SaveAsync(request.Content, request.FileName, request.Folder, ct);
}
