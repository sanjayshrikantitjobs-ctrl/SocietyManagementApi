using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using SocietyManagement.Application.Common.Interfaces;

namespace SocietyManagement.Application.Common.Behaviours;

/// <summary>Structured start/end log per request plus a slow-request warning
/// (&gt;500ms) — cheap, Serilog-friendly observability without an APM dependency.</summary>
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger, ICurrentUserService currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Handling {RequestName} for UserId {UserId}", requestName, _currentUser.UserId);

        var response = await next();

        sw.Stop();
        if (sw.ElapsedMilliseconds > 500)
        {
            _logger.LogWarning(
                "Long running request: {RequestName} ({ElapsedMilliseconds} ms) for UserId {UserId}",
                requestName, sw.ElapsedMilliseconds, _currentUser.UserId);
        }

        return response;
    }
}
