using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Domain.Security;
using MIP.Aws.Infrastructure.Intelligence.Recovery;

namespace MIP.Aws.Tests;

public sealed class AutoAiRecoveryTests
{
    [Fact]
    public void PatchValidator_allows_pressreader_operational_selectors()
    {
        var patch = DarAlKhaleejPressReaderBaseline.RecoveryPatch();

        Assert.True(AutoAiRecoveryPatchValidator.IsPatchSafe(patch, out var rejected));
        Assert.Empty(rejected);
    }

    [Fact]
    public void PatchValidator_rejects_credential_fields()
    {
        var patch = new SourceRecoveryConfigurationPatchDto(
            UsernameSelector: "input[name=user]",
            PasswordSelector: "input[name=pass]",
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

        Assert.False(AutoAiRecoveryPatchValidator.IsPatchSafe(patch, out var rejected));
        Assert.Contains(nameof(patch.UsernameSelector), rejected);
    }

    [Fact]
    public void PressReader_heuristic_is_safe_for_auto_apply()
    {
        var context = new SourceRecoveryAnalysisContext(
            Guid.NewGuid(),
            "UAE - Al Khaleej",
            Guid.NewGuid(),
            SourceRecoveryFailureTypes.DownloadButtonNotFound,
            "DownloadMenuNotOpen",
            "Page actions panel is not open on the edition reader.",
            "https://daralkhaleej.pressreader.com/al-khaleej-9aj7",
            "https://daralkhaleej.pressreader.com/al-khaleej-9aj7",
            null,
            0,
            DateTimeOffset.UtcNow,
            "{}",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            []);

        var options = SourceRecoveryHeuristicBuilder.BuildOptions(context);
        Assert.Single(options);
        Assert.Equal(SourceRecoveryRiskLevel.Low, options[0].RiskLevel);

        var settings = new AutoAiDownloadRecoveryOptions
        {
            MinimumConfidence = 0.70,
            MaximumRiskAllowed = "Medium"
        };

        Assert.True(
            AutoAiRecoveryPatchValidator.IsOptionSafeForAutoApply(options[0], settings, out var reason),
            reason);
    }

    [Fact]
    public void Publisher_baseline_retains_pressreader_low_risk_patch_after_failed_retry()
    {
        var source = new NewsSource
        {
            ConnectorKey = DarAlKhaleejPressReaderBaseline.ConnectorKey,
            PortalStrategyKey = DarAlKhaleejPressReaderBaseline.PortalStrategyKey,
            EditionUrl = "https://daralkhaleej.pressreader.com/al-khaleej-9aj7",
            BaseUrl = "https://daralkhaleej.pressreader.com"
        };

        var option = SourceRecoveryHeuristicBuilder.BuildOptions(
            new SourceRecoveryAnalysisContext(
                source.Id,
                "UAE - Al Khaleej",
                Guid.NewGuid(),
                SourceRecoveryFailureTypes.DownloadButtonNotFound,
                "DownloadMenuNotOpen",
                "Click the newspaper spread to open the actions menu.",
                source.EditionUrl,
                source.EditionUrl,
                null,
                0,
                DateTimeOffset.UtcNow,
                "{}",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                [],
                []))[0];

        Assert.True(PublisherRecoveryBaseline.ShouldRetainConfigAfterFailedRetry(source, option));
    }

    [Fact]
    public void Ranker_prefers_low_risk_pressreader_fix()
    {
        var settings = new AutoAiDownloadRecoveryOptions
        {
            MinimumConfidence = 0.70,
            MaximumRiskAllowed = "Medium",
            MaxSuggestionsToTry = 3
        };

        var pressReader = SourceRecoveryHeuristicBuilder.BuildOptions(
            new SourceRecoveryAnalysisContext(
                Guid.NewGuid(),
                "UAE - Al Khaleej",
                Guid.NewGuid(),
                SourceRecoveryFailureTypes.DownloadButtonNotFound,
                "DownloadMenuNotOpen",
                "Page actions panel is not open.",
                "https://daralkhaleej.pressreader.com/al-khaleej-9aj7",
                "https://daralkhaleej.pressreader.com/al-khaleej-9aj7",
                null,
                0,
                DateTimeOffset.UtcNow,
                "{}",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                [],
                []));

        var generic = SourceRecoveryHeuristicBuilder.BuildOptions(
            new SourceRecoveryAnalysisContext(
                Guid.NewGuid(),
                "Other portal",
                Guid.NewGuid(),
                SourceRecoveryFailureTypes.DownloadButtonNotFound,
                "DownloadFailed",
                "Download failed.",
                "https://example.com",
                "https://example.com",
                null,
                0,
                DateTimeOffset.UtcNow,
                "{}",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                [],
                []));

        var ranker = new AiRecoverySuggestionRanker();
        var ranked = ranker.RankForAutoRecovery([generic[0], pressReader[0]], settings);

        Assert.NotEmpty(ranked);
        Assert.Contains("newspaper spread", ranked[0].Title, StringComparison.OrdinalIgnoreCase);
    }
}
