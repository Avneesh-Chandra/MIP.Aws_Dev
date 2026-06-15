using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Infrastructure.Aws;
using MIP.Aws.Infrastructure.Intelligence.Bedrock;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Intelligence.Ai;

public sealed class AwsBedrockAiProvider(
    BedrockRuntimeClientFactory clientFactory,
    BedrockPromptAdapterFactory adapterFactory,
    IOptions<AwsOptions> awsOptions,
    IAiRequestTelemetry telemetry,
    ILogger<AwsBedrockAiProvider> logger) : IAiTextGenerationService
{
    private readonly AwsBedrockOptions _bedrock = awsOptions.Value.Bedrock;
    private readonly object _clientLock = new();
    private IAmazonBedrockRuntime? _client;

    public string ProviderName => "AwsBedrock";

    public bool IsEnabled =>
        _bedrock.Enabled
        && !string.IsNullOrWhiteSpace(_bedrock.ModelId)
        && !string.IsNullOrWhiteSpace(clientFactory.ResolveRegion());

    public async Task<AiTextGenerationResult> GenerateAsync(
        AiTextGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new AiTextGenerationResult(
                false,
                null,
                "AWS Bedrock is not enabled. Set Aws:Bedrock:Enabled=true and configure ModelId/Region, or enable Ai:MockMode for local development.",
                ProviderName);
        }

        if (!clientFactory.TryValidateProfile(out var profileError))
        {
            telemetry.RecordFailure(ProviderName, "Generate", profileError!);
            return new AiTextGenerationResult(false, null, profileError, ProviderName);
        }

        try
        {
            var bedrock = GetOrCreateClient();
            var adapter = adapterFactory.Resolve(_bedrock.ModelId);
            var body = adapter.BuildRequestBody(
                request.SystemPrompt,
                request.UserPrompt,
                _bedrock.MaxTokens,
                _bedrock.Temperature,
                _bedrock.TopP);

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
            var invoke = new InvokeModelRequest
            {
                ModelId = _bedrock.ModelId,
                ContentType = "application/json",
                Accept = "application/json",
                Body = stream
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_bedrock.TimeoutSeconds > 0)
            {
                cts.CancelAfter(TimeSpan.FromSeconds(_bedrock.TimeoutSeconds));
            }

            var response = await bedrock.InvokeModelAsync(invoke, cts.Token).ConfigureAwait(false);
            using var reader = new StreamReader(response.Body);
            var responseJson = await reader.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            var text = ExtractText(responseJson);

            if (string.IsNullOrWhiteSpace(text))
            {
                telemetry.RecordFailure(ProviderName, "Generate", "Empty Bedrock response.");
                return new AiTextGenerationResult(false, null, "Bedrock returned an empty response.", ProviderName);
            }

            if (request.RequireJson && !AiRecoveryResponseParser.TryParse(text, out _, out var jsonError))
            {
                telemetry.RecordFailure(ProviderName, "Generate", jsonError ?? "Invalid JSON.");
                return new AiTextGenerationResult(false, null, jsonError ?? "Invalid JSON response.", ProviderName);
            }

            telemetry.RecordSuccess(ProviderName, "Generate");
            return new AiTextGenerationResult(true, text, null, ProviderName);
        }
        catch (Exception ex)
        {
            var message = BedrockErrorClassifier.Classify(ex);
            logger.LogWarning(ex, "Bedrock text generation failed for model {ModelId}.", _bedrock.ModelId);
            telemetry.RecordFailure(ProviderName, "Generate", message);
            return new AiTextGenerationResult(false, null, message, ProviderName);
        }
    }

    private IAmazonBedrockRuntime GetOrCreateClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        lock (_clientLock)
        {
            return _client ??= clientFactory.Create();
        }
    }

    private static string? ExtractText(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    return textEl.GetString();
                }
            }
        }

        if (root.TryGetProperty("output", out var output)
            && output.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var novaContent)
            && novaContent.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in novaContent.EnumerateArray())
            {
                if (block.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    return textEl.GetString();
                }
            }
        }

        if (root.TryGetProperty("generation", out var generation) && generation.ValueKind == JsonValueKind.String)
        {
            return generation.GetString();
        }

        return null;
    }
}
