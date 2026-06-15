namespace MIP.Aws.Application.Abstractions.Intelligence;

public interface IAiRequestTelemetry
{
    void RecordSuccess(string provider, string operation);

    void RecordFailure(string provider, string operation, string error);

    void RecordTestSuccess(string provider);

    void RecordTestFailure(string provider, string error);

    (string? Status, string? Error, DateTimeOffset? At) GetLastRequest();

    (string? Status, string? Error, DateTimeOffset? At) GetLastTest();
}
