using Microsoft.Extensions.Configuration;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Infrastructure.Services;

public class AppUrlService : IAppUrlService
{
    private readonly IConfiguration _configuration;

    public AppUrlService(IConfiguration configuration) => _configuration = configuration;

    public string? BuildAbsoluteUrl(string relativePath)
    {
        var baseUrl = _configuration["App:PublicBaseUrl"];
        return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }
}
