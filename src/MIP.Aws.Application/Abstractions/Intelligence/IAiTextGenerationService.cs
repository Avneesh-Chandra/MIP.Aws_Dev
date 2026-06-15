namespace MIP.Aws.Application.Abstractions.Intelligence;

public sealed record AiTextGenerationRequest(
    string SystemPrompt,
    string UserPrompt,
    bool RequireJson = false);

public sealed record AiTextGenerationResult(
    bool Success,
    string? Text,
    string? Error,
    string ProviderName);

public interface IAiTextGenerationService
{
    string ProviderName { get; }

    bool IsEnabled { get; }

    Task<AiTextGenerationResult> GenerateAsync(
        AiTextGenerationRequest request,
        CancellationToken cancellationToken = default);
}
