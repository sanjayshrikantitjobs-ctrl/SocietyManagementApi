using MediatR;
using Microsoft.Extensions.Logging;

namespace SocietyManagement.Application.Common.Behaviours;

/// <summary>Logs any exception that isn't already one of our expected AppException
/// types, then rethrows so GlobalExceptionMiddleware still maps it to HTTP 500.</summary>
public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> _logger;

    public UnhandledExceptionBehaviour(ILogger<UnhandledExceptionBehaviour<TRequest, TResponse>> logger) =>
        _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex) when (ex is not Shared.Exceptions.AppException)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogError(ex, "Unhandled exception for request {RequestName} {@Request}", requestName, request);
            throw;
        }
    }
}
