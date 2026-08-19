namespace SocietyManagement.Shared.Wrappers;

/// <summary>
/// Every API endpoint returns this envelope (per spec API Standards), so the
/// Angular HttpInterceptor can unwrap `data` uniformly and surface `message` /
/// `errors` in toast notifications without each service knowing the endpoint's
/// raw shape.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public List<string>? Errors { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Request successful") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> FailureResponse(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors, Data = default };
}

/// <summary>Non-generic variant for endpoints that only return a status (e.g. Delete).</summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse SuccessResponse(string message = "Request successful") =>
        new() { Success = true, Message = message };

    public new static ApiResponse FailureResponse(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}
