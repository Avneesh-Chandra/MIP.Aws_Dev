namespace MIP.Aws.Application.Abstractions.Intelligence;

public sealed record AiProviderStatusDto(
    string ActiveProvider,
    bool AiEnabled,
    bool MockMode,
    string? BedrockRegion,
    string? BedrockModelId,
    bool BedrockEnabled,
    string? AwsProfile,
    string? LastRequestStatus,
    string? LastError,
    DateTimeOffset? LastRequestAt,
    string? LastTestStatus);

/// <summary>Admin-facing AI status (safe fields only — no secrets).</summary>
public sealed record AiAdminStatusDto(
    string Provider,
    bool Enabled,
    bool MockMode,
    string? Region,
    string? ModelId,
    string? AwsProfile,
    string? LastTestStatus,
    string? LastError,
    DateTimeOffset? LastRequestAt);

public interface IAiProviderFactory
{
    IAiTextGenerationService TextGeneration { get; }

    IAiRecoveryProvider RecoveryProvider { get; }

    IAISourceRecoveryService SourceRecovery { get; }

    AiProviderStatusDto GetStatus();

    AiAdminStatusDto GetAdminStatus();
}
