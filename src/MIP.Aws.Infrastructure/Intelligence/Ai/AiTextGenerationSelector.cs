using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Intelligence.Ai;

/// <summary>Resolves the active text-generation provider without depending on <see cref="IAiProviderFactory"/>.</summary>
public sealed class AiTextGenerationSelector(
    IOptions<AiOptions> aiOptions,
    IHostEnvironment hostEnvironment,
    MockAiProvider mockProvider,
    AwsBedrockAiProvider bedrockProvider)
{
    public string ResolveProviderName()
    {
        var ai = aiOptions.Value;
        if (ai.MockMode)
        {
            return "Mock";
        }

        var provider = ai.Provider?.Trim();
        if (string.IsNullOrWhiteSpace(provider))
        {
            return hostEnvironment.IsProduction() ? "AwsBedrock" : "Mock";
        }

        return provider;
    }

    public IAiTextGenerationService Resolve()
    {
        return ResolveProviderName() switch
        {
            "AwsBedrock" => bedrockProvider,
            "Mock" => mockProvider,
            _ => throw new InvalidOperationException(
                $"Unsupported Ai:Provider '{aiOptions.Value.Provider}'. Use Mock or AwsBedrock.")
        };
    }
}
