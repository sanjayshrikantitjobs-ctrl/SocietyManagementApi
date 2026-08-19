using FluentValidation;
using MediatR;
using SocietyManagement.Shared.Exceptions;

namespace SocietyManagement.Application.Common.Behaviours;

/// <summary>Runs every registered FluentValidation validator for TRequest before the
/// handler executes; throws ValidationAppException (mapped to HTTP 400 by
/// GlobalExceptionMiddleware) on failure so handlers never see invalid input.</summary>
public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationAppException(failures);
        }

        return await next();
    }
}
