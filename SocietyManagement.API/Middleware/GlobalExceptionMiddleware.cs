using System.Net;
using System.Text.Json;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Shared.Wrappers;

namespace SocietyManagement.API.Middleware;

/// <summary>
/// Single place every unhandled/expected exception is converted into the
/// standard {success,message,data,errors} envelope (spec: "Error Handling").
/// Sits outermost in the pipeline (see Program.cs) so it also catches
/// exceptions raised by later middleware, not just controller actions.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, response) = exception switch
        {
            ValidationAppException validationEx => (
                (int)HttpStatusCode.BadRequest,
                ApiResponse.FailureResponse(
                    "Validation failed.",
                    validationEx.Errors.SelectMany(e => e.Value).ToList())),

            NotFoundException notFoundEx => (
                (int)HttpStatusCode.NotFound,
                ApiResponse.FailureResponse(notFoundEx.Message)),

            UnauthorizedAppException unauthorizedEx => (
                (int)HttpStatusCode.Unauthorized,
                ApiResponse.FailureResponse(unauthorizedEx.Message)),

            ForbiddenAccessException forbiddenEx => (
                (int)HttpStatusCode.Forbidden,
                ApiResponse.FailureResponse(forbiddenEx.Message)),

            ConflictAppException conflictEx => (
                (int)HttpStatusCode.Conflict,
                ApiResponse.FailureResponse(conflictEx.Message)),

            BadRequestAppException badRequestEx => (
                (int)HttpStatusCode.BadRequest,
                ApiResponse.FailureResponse(badRequestEx.Message)),

            _ => (
                (int)HttpStatusCode.InternalServerError,
                ApiResponse.FailureResponse(
                    _env.IsDevelopment() ? exception.Message : "An unexpected error occurred. Please try again later.",
                    _env.IsDevelopment() ? new List<string> { exception.StackTrace ?? string.Empty } : null))
        };

        if (statusCode == (int)HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Handled exception ({StatusCode}) processing {Method} {Path}: {Message}",
                statusCode, context.Request.Method, context.Request.Path, exception.Message);
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
