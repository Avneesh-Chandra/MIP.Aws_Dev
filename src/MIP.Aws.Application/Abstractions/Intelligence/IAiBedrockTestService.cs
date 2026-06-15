namespace MIP.Aws.Application.Abstractions.Intelligence;

public sealed record BedrockTestRequest(string Prompt);

public sealed record BedrockTestResult(
    bool Success,
    string Provider,
    string? Region,
    string? ModelId,
    string? Output,
    long LatencyMs,
    string? Error);

public interface IAiBedrockTestService
{
    Task<BedrockTestResult> TestAsync(BedrockTestRequest request, CancellationToken cancellationToken = default);
}
