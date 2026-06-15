using System.Text.Json;
using MIP.Aws.API.Controllers;
using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Infrastructure;
using MIP.Aws.Infrastructure.Aws;
using MIP.Aws.Infrastructure.Intelligence.Ai;
using MIP.Aws.Infrastructure.Intelligence.Bedrock;
using MIP.Aws.Infrastructure.Intelligence.Recovery;
using MIP.Aws.Infrastructure.Operator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Tests;

public sealed class AiDiResolutionTests
{
    [Fact]
    public void AddMipAwsAi_resolves_factory_and_bedrock_test_without_recursion()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Provider"] = "AwsBedrock",
                ["Ai:Enabled"] = "true",
                ["Ai:MockMode"] = "false",
                ["Aws:Region"] = "eu-north-1",
                ["Aws:Profile"] = "mip-dev",
                ["Aws:Bedrock:Enabled"] = "true",
                ["Aws:Bedrock:ModelId"] = "anthropic.claude-3-5-haiku-20241022-v1:0",
                ["Aws:Bedrock:Region"] = "eu-north-1",
                ["AiSourceRecovery:Enabled"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var env = new BedrockLocalConfigTests.HostEnvironment(isProduction: false);
        services.AddSingleton<IHostEnvironment>(env);
        services.AddMipAwsAi(config, env);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IAiProviderFactory>();
        var bedrockTest = scope.ServiceProvider.GetRequiredService<IAiBedrockTestService>();

        var status = factory.GetAdminStatus();
        Assert.Equal("AwsBedrock", status.Provider);
        Assert.NotNull(bedrockTest);
    }
}

public sealed class AzureOpenAiReferenceTests
{
    [Fact]
    public void Solution_has_no_AzureOpenAI_package_or_type_references()
    {
        var root = FindRepoRoot();
        var forbidden = new[]
        {
            "Azure.AI.OpenAI",
            "AzureOpenAIClient",
            "AzureOpenAiOptions",
            "AzureDocumentIntelligenceOptions",
            "\"AzureOpenAI\""
        };

        var sourceFiles = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                        || p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Where(p => p.Contains($"{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        || p.Contains($"{Path.DirectorySeparatorChar}infra{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        var hits = new List<string>();
        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file);
            foreach (var token in forbidden)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    hits.Add($"{file}: {token}");
                }
            }
        }

        Assert.Empty(hits);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "MIP.Aws.slnx")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate MIP.Aws.slnx from test output path.");
    }
}

public sealed class AiProviderSelectionTests
{
    [Fact]
    public void Development_defaults_to_Mock_provider()
    {
        var factory = CreateFactory(new AiOptions { Enabled = true }, isProduction: false);
        Assert.Equal("Mock", factory.GetStatus().ActiveProvider);
        Assert.True(factory.TextGeneration.IsEnabled);
    }

    [Fact]
    public void Production_defaults_to_AwsBedrock_provider()
    {
        var factory = CreateFactory(new AiOptions { Enabled = true }, isProduction: true);
        Assert.Equal("AwsBedrock", factory.GetStatus().ActiveProvider);
    }

    [Fact]
    public void MockMode_overrides_explicit_Bedrock_provider()
    {
        var factory = CreateFactory(new AiOptions { Provider = "AwsBedrock", MockMode = true }, isProduction: true);
        Assert.Equal("Mock", factory.GetStatus().ActiveProvider);
    }

    [Fact]
    public void Development_with_explicit_Bedrock_uses_AwsBedrock()
    {
        var factory = CreateFactory(new AiOptions { Provider = "AwsBedrock", Enabled = true }, isProduction: false);
        Assert.Equal("AwsBedrock", factory.GetStatus().ActiveProvider);
    }

    private static AiProviderFactory CreateFactory(AiOptions ai, bool isProduction, string? profile = null)
    {
        var env = new HostEnvironment(isProduction);
        var awsOptions = new AwsOptions
        {
            Region = "eu-north-1",
            Profile = profile ?? string.Empty,
            Bedrock = new AwsBedrockOptions { Enabled = true, ModelId = "anthropic.claude-3-5-haiku-20241022-v1:0", Region = "eu-north-1" }
        };
        var clientFactory = new BedrockRuntimeClientFactory(Options.Create(awsOptions));
        var telemetry = new AiRequestTelemetryService();
        var mock = new MockAiProvider(telemetry);
        var bedrock = new AwsBedrockAiProvider(clientFactory, new BedrockPromptAdapterFactory([]), Options.Create(awsOptions), telemetry, NullLogger<AwsBedrockAiProvider>.Instance);
        var selector = new AiTextGenerationSelector(Options.Create(ai), env, mock, bedrock);
        var recovery = new BedrockSourceRecoveryService(mock, Options.Create(ai), Options.Create(new AiSourceRecoveryOptions()), NullLogger<BedrockSourceRecoveryService>.Instance);

        return new AiProviderFactory(
            Options.Create(ai),
            Options.Create(awsOptions),
            selector,
            clientFactory,
            telemetry,
            recovery,
            new AiRecoveryProviderAdapter(recovery));
    }

    private sealed class HostEnvironment(bool isProduction) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isProduction ? Environments.Production : Environments.Development;
        public string ApplicationName { get; set; } = "MIP.Aws.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public sealed class MockAiProviderTests
{
    [Fact]
    public async Task Mock_provider_returns_valid_recovery_json()
    {
        var provider = new MockAiProvider(new AiRequestTelemetryService());
        var result = await provider.GenerateAsync(
            new Application.Abstractions.Intelligence.AiTextGenerationRequest("system", "user", RequireJson: true));

        Assert.True(result.Success);
        Assert.True(AiRecoveryResponseParser.TryParse(result.Text!, out var parsed, out _));
        Assert.NotEmpty(parsed.Options);
    }
}

public sealed class BedrockPromptAdapterTests
{
    [Fact]
    public void Claude_adapter_builds_valid_messages_payload()
    {
        var adapter = new ClaudeBedrockPromptAdapter();
        Assert.True(adapter.CanHandle("anthropic.claude-3-5-haiku-20241022-v1:0"));

        var body = adapter.BuildRequestBody("sys", "user", 1200, 0.2, 0.9);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("bedrock-2023-05-31", doc.RootElement.GetProperty("anthropic_version").GetString());
        Assert.Equal(1200, doc.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("sys", doc.RootElement.GetProperty("system").GetString());
    }

    [Fact]
    public void Nova_adapter_builds_valid_inference_payload()
    {
        var adapter = new NovaBedrockPromptAdapter();
        Assert.True(adapter.CanHandle("amazon.nova-lite-v1:0"));

        var body = adapter.BuildRequestBody("sys", "user", 800, 0.1, 0.95);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(800, doc.RootElement.GetProperty("inferenceConfig").GetProperty("maxTokens").GetInt32());
        Assert.True(doc.RootElement.GetProperty("messages")[0].GetProperty("content")[0].TryGetProperty("text", out _));
    }
}

public sealed class AiRecoverySafetyTests
{
    [Fact]
    public void Unsafe_recovery_patch_with_credentials_is_rejected()
    {
        const string json = """
            {
              "summary": "bad",
              "suggestions": [{
                "title": "bad",
                "description": "bad",
                "confidence": 0.9,
                "predictedSuccess": 0.9,
                "risk": "Low",
                "allowedPatch": { "passwordSelector": "#pwd" },
                "blockedPatch": {},
                "reason": "nope"
              }]
            }
            """;

        Assert.False(AiRecoveryResponseValidator.TryValidateRawJson(json, out var error));
        Assert.Contains("Unsafe", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Invalid_json_is_rejected()
    {
        Assert.False(AiRecoveryResponseValidator.TryValidateRawJson("not json", out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Auto_apply_rejects_username_selector_in_patch()
    {
        var patch = new SourceRecoveryConfigurationPatchDto(
            UsernameSelector: "#user",
            PasswordSelector: null,
            SubmitSelector: null,
            DownloadSelector: null,
            LoginIconSelector: null,
            NewspaperCanvasSelector: null,
            ContextMenuSelector: null,
            DownloadMenuItemSelector: null,
            LoginSuccessSelector: null,
            SuccessUrlPattern: null,
            PdfDownloadSelector: null,
            PdfLinkSelector: null,
            BaseUrl: null,
            EditionUrl: null,
            PdfDiscoveryPageUrl: null,
            DownloadWaitTimeoutSeconds: null,
            UseHeadlessBrowser: null);

        Assert.False(AutoAiRecoveryPatchValidator.IsPatchSafe(patch, out var rejected));
        Assert.Contains(nameof(SourceRecoveryConfigurationPatchDto.UsernameSelector), rejected);
    }
}

public sealed class StatusEmailSummaryTests
{
    [Fact]
    public void Disabled_ai_uses_deterministic_summary()
    {
        var monitor = new DownloadMonitorDto(
            DateOnly.FromDateTime(DateTime.UtcNow),
            new DownloadMonitorSummaryDto(5, 4, 1, 0, 4, 0, []),
            []);

        var summary = DownloadMonitorStatusSummaryService.BuildDeterministicSummary(monitor);
        Assert.Contains("4 of 5 sources succeeded", summary);
    }

    [Fact]
    public async Task Mock_provider_summary_is_used_when_ai_enabled()
    {
        var service = new DownloadMonitorStatusSummaryService(
            new MockAiProvider(new AiRequestTelemetryService()),
            Options.Create(new AiOptions { Enabled = true }));

        var monitor = new DownloadMonitorDto(
            DateOnly.FromDateTime(DateTime.UtcNow),
            new DownloadMonitorSummaryDto(2, 1, 1, 0, 1, 0, []),
            []);

        var summary = await service.BuildSummaryAsync(monitor, CancellationToken.None);
        Assert.Contains("Mock summary", summary);
    }
}

public sealed class BedrockLocalConfigTests
{
    [Fact]
    public void Missing_profile_returns_clear_error()
    {
        var factory = new BedrockRuntimeClientFactory(Options.Create(new AwsOptions { Profile = "nonexistent-profile-xyz" }));
        Assert.False(factory.TryValidateProfile(out var error));
        Assert.Contains("nonexistent-profile-xyz", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aws configure", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Aws_profile_env_var_takes_precedence_over_config()
    {
        var previous = Environment.GetEnvironmentVariable("AWS_PROFILE");
        try
        {
            Environment.SetEnvironmentVariable("AWS_PROFILE", "env-profile-test");
            var factory = new BedrockRuntimeClientFactory(Options.Create(new AwsOptions { Profile = "mip-dev" }));
            Assert.Equal("env-profile-test", factory.ResolveProfile());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_PROFILE", previous);
        }
    }

    [Fact]
    public void Admin_status_includes_configured_profile()
    {
        var factory = new AiProviderFactory(
            Options.Create(new AiOptions { Provider = "AwsBedrock", Enabled = true }),
            Options.Create(new AwsOptions { Region = "eu-north-1", Profile = "mip-dev", Bedrock = new AwsBedrockOptions { Enabled = true, ModelId = "anthropic.claude-3-5-haiku-20241022-v1:0", Region = "eu-north-1" } }),
            new AiTextGenerationSelector(
                Options.Create(new AiOptions { Provider = "AwsBedrock", Enabled = true }),
                new HostEnvironment(false),
                new MockAiProvider(new AiRequestTelemetryService()),
                new AwsBedrockAiProvider(
                    new BedrockRuntimeClientFactory(Options.Create(new AwsOptions { Profile = "mip-dev" })),
                    new BedrockPromptAdapterFactory([]),
                    Options.Create(new AwsOptions { Profile = "mip-dev", Bedrock = new AwsBedrockOptions { Enabled = true, ModelId = "anthropic.claude-3-5-haiku-20241022-v1:0" } }),
                    new AiRequestTelemetryService(),
                    NullLogger<AwsBedrockAiProvider>.Instance)),
            new BedrockRuntimeClientFactory(Options.Create(new AwsOptions { Profile = "mip-dev" })),
            new AiRequestTelemetryService(),
            new BedrockSourceRecoveryService(new MockAiProvider(new AiRequestTelemetryService()), Options.Create(new AiOptions()), Options.Create(new AiSourceRecoveryOptions()), NullLogger<BedrockSourceRecoveryService>.Instance),
            new AiRecoveryProviderAdapter(new BedrockSourceRecoveryService(new MockAiProvider(new AiRequestTelemetryService()), Options.Create(new AiOptions()), Options.Create(new AiSourceRecoveryOptions()), NullLogger<BedrockSourceRecoveryService>.Instance)));

        var status = factory.GetAdminStatus();
        Assert.Equal("AwsBedrock", status.Provider);
        Assert.Equal("mip-dev", status.AwsProfile);
        Assert.Equal("eu-north-1", status.Region);
    }

    internal sealed class HostEnvironment(bool isProduction) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isProduction ? Environments.Production : Environments.Development;
        public string ApplicationName { get; set; } = "MIP.Aws.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public sealed class AiAdminControllerAuthTests
{
    [Fact]
    public void Bedrock_test_endpoint_requires_SuperAdminOnly()
    {
        var method = typeof(AiAdminController).GetMethod(nameof(AiAdminController.TestBedrockAsync));
        Assert.NotNull(method);
        var authorize = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();
        Assert.NotNull(authorize);
        Assert.Equal(MIP.Aws.Domain.Security.AuthPolicies.SuperAdminOnly, authorize!.Policy);
    }

    [Fact]
    public async Task Mock_mode_bedrock_test_returns_clear_error()
    {
        var service = new AiBedrockTestService(
            CreateFactory(new AiOptions { Provider = "AwsBedrock", MockMode = true }),
            new BedrockRuntimeClientFactory(Options.Create(new AwsOptions())),
            Options.Create(new AwsOptions { Bedrock = new AwsBedrockOptions { Enabled = true, ModelId = "anthropic.claude-3-5-haiku-20241022-v1:0" } }),
            Options.Create(new AiOptions { MockMode = true }),
            new AiRequestTelemetryService());

        var result = await service.TestAsync(new BedrockTestRequest("test"), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("MockMode", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static AiProviderFactory CreateFactory(AiOptions ai)
    {
        var aws = new AwsOptions { Region = "eu-north-1", Bedrock = new AwsBedrockOptions { Enabled = true, ModelId = "anthropic.claude-3-5-haiku-20241022-v1:0" } };
        var telemetry = new AiRequestTelemetryService();
        var mock = new MockAiProvider(telemetry);
        var recovery = new BedrockSourceRecoveryService(mock, Options.Create(ai), Options.Create(new AiSourceRecoveryOptions()), NullLogger<BedrockSourceRecoveryService>.Instance);
        var selector = new AiTextGenerationSelector(
            Options.Create(ai),
            new BedrockLocalConfigTests.HostEnvironment(false),
            mock,
            new AwsBedrockAiProvider(new BedrockRuntimeClientFactory(Options.Create(aws)), new BedrockPromptAdapterFactory([]), Options.Create(aws), telemetry, NullLogger<AwsBedrockAiProvider>.Instance));
        return new AiProviderFactory(
            Options.Create(ai),
            Options.Create(aws),
            selector,
            new BedrockRuntimeClientFactory(Options.Create(aws)),
            telemetry,
            recovery,
            new AiRecoveryProviderAdapter(recovery));
    }
}
