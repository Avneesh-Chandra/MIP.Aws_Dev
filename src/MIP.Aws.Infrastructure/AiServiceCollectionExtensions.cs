using Amazon;
using Amazon.BedrockRuntime;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Infrastructure.Aws;
using MIP.Aws.Infrastructure.Intelligence.Ai;
using MIP.Aws.Infrastructure.Intelligence.Bedrock;
using MIP.Aws.Infrastructure.Intelligence.Recovery;
using MIP.Aws.Infrastructure.News.PdfEdition.SelectorSuggestion;
using MIP.Aws.Infrastructure.Operator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MIP.Aws.Infrastructure;

public static class AiServiceCollectionExtensions
{
    public static IServiceCollection AddMipAwsAi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        services.AddSingleton<IAiRequestTelemetry, AiRequestTelemetryService>();
        services.AddSingleton<IBedrockPromptAdapter, ClaudeBedrockPromptAdapter>();
        services.AddSingleton<IBedrockPromptAdapter, NovaBedrockPromptAdapter>();
        services.AddSingleton<BedrockPromptAdapterFactory>();
        services.AddSingleton<BedrockRuntimeClientFactory>();
        services.AddSingleton<MockAiProvider>();
        services.AddSingleton<AwsBedrockAiProvider>();
        services.AddSingleton<AiTextGenerationSelector>();

        var aws = configuration.GetSection(AwsOptions.SectionName).Get<AwsOptions>() ?? new AwsOptions();
        var ai = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        var useBedrock = ShouldRegisterBedrock(ai, environment);

        if (useBedrock)
        {
            services.AddSingleton<IAmazonBedrockRuntime>(sp => sp.GetRequiredService<BedrockRuntimeClientFactory>().Create());
        }

        services.AddScoped<BedrockSourceRecoveryService>();
        services.AddScoped<IAISourceRecoveryService>(sp => sp.GetRequiredService<BedrockSourceRecoveryService>());
        services.AddScoped<AiRecoveryProviderAdapter>();
        services.AddScoped<IAiRecoveryProvider>(sp => sp.GetRequiredService<AiRecoveryProviderAdapter>());
        services.AddScoped<IAiProviderFactory, AiProviderFactory>();
        services.AddScoped<IAiTextGenerationService>(sp => sp.GetRequiredService<AiTextGenerationSelector>().Resolve());
        services.AddScoped<IAiBedrockTestService, AiBedrockTestService>();
        services.AddScoped<IAiSelectorSuggestionService, BedrockAiSelectorSuggestionService>();
        services.AddScoped<IDownloadMonitorStatusSummaryService, DownloadMonitorStatusSummaryService>();

        return services;
    }

    private static bool ShouldRegisterBedrock(AiOptions ai, IHostEnvironment environment)
    {
        if (ai.MockMode)
        {
            return false;
        }

        var provider = ai.Provider?.Trim();
        if (string.IsNullOrWhiteSpace(provider))
        {
            return environment.IsProduction();
        }

        return string.Equals(provider, "AwsBedrock", StringComparison.OrdinalIgnoreCase);
    }
}
