using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Infrastructure.Intelligence.Ai;

public sealed class MockAiProvider(IAiRequestTelemetry telemetry) : IAiTextGenerationService
{
    public string ProviderName => "Mock";

    public bool IsEnabled => true;

    public Task<AiTextGenerationResult> GenerateAsync(
        AiTextGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RequireJson)
        {
            var mockJson = """
                {
                  "summary": "Mock AI: download failed because PDF selector no longer matches.",
                  "suggestions": [{
                    "title": "Retry with Playwright headless",
                    "description": "Use headless browser and wait for network idle before PDF capture.",
                    "confidence": 0.85,
                    "predictedSuccess": 0.85,
                    "risk": "Low",
                    "allowedPatch": { "useHeadlessBrowser": true, "downloadWaitTimeoutSeconds": 240 },
                    "blockedPatch": {},
                    "reason": "Improves dynamic page load reliability."
                  }]
                }
                """;
            telemetry.RecordSuccess(ProviderName, "Generate");
            return Task.FromResult(new AiTextGenerationResult(true, mockJson, null, ProviderName));
        }

        var text = request.SystemPrompt.Contains("executive summary", StringComparison.OrdinalIgnoreCase)
            ? "Mock summary: daily download monitor completed. Review failed sources in the portal."
            : "Mock AI response for development.";

        telemetry.RecordSuccess(ProviderName, "Generate");
        return Task.FromResult(new AiTextGenerationResult(true, text, null, ProviderName));
    }
}
