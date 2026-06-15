using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Infrastructure.Aws;
using MIP.Aws.Infrastructure.Intelligence.Recovery;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Intelligence.Ai;

public sealed class AiProviderFactory(
    IOptions<AiOptions> aiOptions,
    IOptions<AwsOptions> awsOptions,
    AiTextGenerationSelector textGenerationSelector,
    BedrockRuntimeClientFactory clientFactory,
    IAiRequestTelemetry telemetry,
    BedrockSourceRecoveryService sourceRecovery,
    AiRecoveryProviderAdapter recoveryProvider) : IAiProviderFactory
{
    public IAiTextGenerationService TextGeneration => textGenerationSelector.Resolve();

    public IAiRecoveryProvider RecoveryProvider => recoveryProvider;

    public IAISourceRecoveryService SourceRecovery => sourceRecovery;

    public AiProviderStatusDto GetStatus()
    {
        var ai = aiOptions.Value;
        var bedrock = awsOptions.Value.Bedrock;
        var active = textGenerationSelector.ResolveProviderName();
        var (status, error, at) = telemetry.GetLastRequest();
        var (testStatus, testError, _) = telemetry.GetLastTest();
        var profile = clientFactory.ResolveProfile();

        return new AiProviderStatusDto(
            active,
            ai.Enabled,
            ai.MockMode || string.Equals(active, "Mock", StringComparison.OrdinalIgnoreCase),
            clientFactory.ResolveRegion(),
            bedrock.ModelId,
            bedrock.Enabled,
            string.IsNullOrWhiteSpace(profile) ? null : profile,
            status,
            error ?? testError,
            at,
            testStatus);
    }

    public AiAdminStatusDto GetAdminStatus()
    {
        var status = GetStatus();
        return new AiAdminStatusDto(
            status.ActiveProvider,
            status.AiEnabled,
            status.MockMode,
            status.BedrockRegion,
            status.BedrockModelId,
            status.AwsProfile,
            status.LastTestStatus,
            status.LastError,
            status.LastRequestAt);
    }

}
