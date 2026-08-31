namespace SocietyManagement.Shared.Exceptions;

/// <summary>Base for all handled application exceptions; caught by
/// GlobalExceptionMiddleware and mapped to a proper HTTP status + ApiResponse.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.") { }

    public NotFoundException(string message) : base(message) { }
}

public class ValidationAppException : AppException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationAppException() : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationAppException(IEnumerable<FluentValidation.Results.ValidationFailure> failures) : this()
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }
}

public class ForbiddenAccessException : AppException
{
    public ForbiddenAccessException() : base("You do not have permission to perform this action.") { }
    public ForbiddenAccessException(string message) : base(message) { }
}

public class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message = "Invalid credentials.") : base(message) { }
}

public class BadRequestAppException : AppException
{
    public BadRequestAppException(string message) : base(message) { }
}

public class ConflictAppException : AppException
{
    public ConflictAppException(string message) : base(message) { }
}

/// <summary>Thrown by SubscriptionActiveFilter when a society's subscription
/// has lapsed. Mapped to HTTP 402 (not 403) so the frontend can distinguish
/// "your trial ended" from a generic permission error and show a dedicated
/// lockout screen.</summary>
public class SubscriptionExpiredException : AppException
{
    public SubscriptionExpiredException()
        : base("Your society's subscription has expired. Please contact your platform administrator.") { }
}
