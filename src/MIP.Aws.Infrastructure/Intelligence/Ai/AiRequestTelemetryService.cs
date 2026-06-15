using MIP.Aws.Application.Abstractions.Intelligence;

namespace MIP.Aws.Infrastructure.Intelligence.Ai;

public sealed class AiRequestTelemetryService : IAiRequestTelemetry
{
    private string? _status;
    private string? _error;
    private DateTimeOffset? _at;
    private string? _testStatus;
    private string? _testError;
    private DateTimeOffset? _testAt;

    public void RecordSuccess(string provider, string operation)
    {
        _status = $"{provider}:{operation}:Success";
        _error = null;
        _at = DateTimeOffset.UtcNow;
    }

    public void RecordFailure(string provider, string operation, string error)
    {
        _status = $"{provider}:{operation}:Failed";
        _error = error;
        _at = DateTimeOffset.UtcNow;
    }

    public void RecordTestSuccess(string provider)
    {
        _testStatus = "Success";
        _testError = null;
        _testAt = DateTimeOffset.UtcNow;
    }

    public void RecordTestFailure(string provider, string error)
    {
        _testStatus = "Failed";
        _testError = error;
        _testAt = DateTimeOffset.UtcNow;
    }

    public (string? Status, string? Error, DateTimeOffset? At) GetLastRequest() => (_status, _error, _at);

    public (string? Status, string? Error, DateTimeOffset? At) GetLastTest() => (_testStatus, _testError, _testAt);
}
