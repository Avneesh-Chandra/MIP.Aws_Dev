using System.Diagnostics;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Infrastructure.Aws;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Intelligence.Ai;

public sealed class AiBedrockTestService(
    IAiProviderFactory providerFactory,
    BedrockRuntimeClientFactory clientFactory,
    IOptions<AwsOptions> awsOptions,
    IOptions<AiOptions> aiOptions,
    IAiRequestTelemetry telemetry) : IAiBedrockTestService
{
    public async Task<BedrockTestResult> TestAsync(BedrockTestRequest request, CancellationToken cancellationToken = default)
    {
        var status = providerFactory.GetAdminStatus();
        var bedrock = awsOptions.Value.Bedrock;
        var region = clientFactory.ResolveRegion();
        var modelId = bedrock.ModelId;

        if (aiOptions.Value.MockMode)
        {
            const string error = "Ai:MockMode is true. Set Ai:MockMode=false to test live Bedrock.";
            telemetry.RecordTestFailure("AwsBedrock", error);
            return new BedrockTestResult(false, "Mock", region, modelId, null, 0, error);
        }

        if (!string.Equals(status.Provider, "AwsBedrock", StringComparison.OrdinalIgnoreCase))
        {
            const string error = "Ai:Provider must be AwsBedrock for Bedrock testing.";
            telemetry.RecordTestFailure("AwsBedrock", error);
            return new BedrockTestResult(false, status.Provider, region, modelId, null, 0, error);
        }

        if (!bedrock.Enabled)
        {
            const string error = "Aws:Bedrock:Enabled is false.";
            telemetry.RecordTestFailure("AwsBedrock", error);
            return new BedrockTestResult(false, "AwsBedrock", region, modelId, null, 0, error);
        }

        if (!clientFactory.TryValidateProfile(out var profileError))
        {
            telemetry.RecordTestFailure("AwsBedrock", profileError!);
            return new BedrockTestResult(false, "AwsBedrock", region, modelId, null, 0, profileError);
        }

        try
        {
            _ = clientFactory.Create();
        }
        catch (Exception ex)
        {
            var message = BedrockErrorClassifier.Classify(ex);
            telemetry.RecordTestFailure("AwsBedrock", message);
            return new BedrockTestResult(false, "AwsBedrock", region, modelId, null, 0, message);
        }

        var prompt = string.IsNullOrWhiteSpace(request.Prompt)
            ? "Reply with one sentence confirming Bedrock connectivity for MIP.Aws."
            : request.Prompt.Trim();

        var sw = Stopwatch.StartNew();
        var result = await providerFactory.TextGeneration.GenerateAsync(
            new AiTextGenerationRequest(
                "You are a helpful assistant for GFH Media Intelligence operators. Be concise and factual.",
                prompt),
            cancellationToken).ConfigureAwait(false);
        sw.Stop();

        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            var error = BedrockErrorClassifier.Classify(new Exception(result.Error ?? "Bedrock test failed."));
            telemetry.RecordTestFailure("AwsBedrock", error);
            return new BedrockTestResult(false, "AwsBedrock", region, modelId, null, sw.ElapsedMilliseconds, error);
        }

        telemetry.RecordTestSuccess("AwsBedrock");
        return new BedrockTestResult(true, "AwsBedrock", region, modelId, result.Text.Trim(), sw.ElapsedMilliseconds, null);
    }
}
