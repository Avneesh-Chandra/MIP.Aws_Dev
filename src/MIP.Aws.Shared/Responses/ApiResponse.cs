namespace MIP.Aws.Shared.Responses;

/// <summary>
/// Standard envelope for API responses.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data, Errors = Array.Empty<string>() };

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null) =>
        new() { Success = false, Message = message, Data = default, Errors = errors ?? Array.Empty<string>() };
}

/// <summary>
/// Non-generic success/failure responses.
/// </summary>
public sealed class ApiResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ApiResponse Ok(string message = "Success") =>
        new() { Success = true, Message = message, Errors = Array.Empty<string>() };

    public static ApiResponse Fail(string message, IReadOnlyList<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors ?? Array.Empty<string>() };
}
