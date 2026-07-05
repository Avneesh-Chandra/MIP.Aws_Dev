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
    public void AlAyam_heuristic_is_safe_for_auto_apply_when_blocked()
    {
        var context = new SourceRecoveryAnalysisContext(
            Guid.NewGuid(),
            AlAyamPublicPdfBaseline.SourceName,
            Guid.NewGuid(),
            SourceRecoveryFailureTypes.AccessDenied,
            "AccessBlocked",
            "Publisher blocked automated access (Cloudflare/bot protection) on the e-paper page.",
            AlAyamPublicPdfBaseline.EpaperUrl,
            AlAyamPublicPdfBaseline.EpaperUrl,
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

        var options = SourceRecoveryHeuristicBuilder.MergePublisherHeuristics(
            context,
            []);
        Assert.Equal(2, options.Count);
        Assert.True(options[0].Patch.UseHeadlessBrowser);
        Assert.Equal(AlAyamPublicPdfBaseline.PdfLinkSelector, options[0].Patch.PdfLinkSelector);

        var settings = new AutoAiDownloadRecoveryOptions
        {
            MinimumConfidence = 0.70,
            MaximumRiskAllowed = "Medium"
        };

        Assert.True(
            AutoAiRecoveryPatchValidator.IsOptionSafeForAutoApply(options[0], settings, out var reason),
            reason);
        Assert.True(
            PublisherRecoveryBaseline.ShouldRetainConfigAfterFailedRetry(
                new NewsSource { ConnectorKey = AlAyamPublicPdfBaseline.ConnectorKey },
                options[0]));
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
    public void ShouldRunForTrigger_allows_scheduled_when_RunAfterScheduledFailure_enabled()
    {
        var settings = new AutoAiDownloadRecoveryOptions
        {
            RunAfterScheduledFailure = true,
            RunAfterManualFailure = false
        };

        Assert.True(AutoAiRecoveryEligibility.ShouldRunForTrigger(DownloadJobTrigger.Scheduled, settings));
        Assert.False(AutoAiRecoveryEligibility.ShouldRunForTrigger(DownloadJobTrigger.Manual, settings));
    }

    [Fact]
    public void ShouldRunForTrigger_allows_manual_by_default()
    {
        var settings = new AutoAiDownloadRecoveryOptions { RunAfterScheduledFailure = true };

        Assert.True(AutoAiRecoveryEligibility.ShouldRunForTrigger(DownloadJobTrigger.Manual, settings));
    }

    [Fact]
    public void IsJobEligibleForAutoRecovery_requires_failed_status_and_non_recovery_trigger()
    {
        var eligible = new DownloadJob
        {
            Status = DownloadJobStatus.Failed,
            Trigger = DownloadJobTrigger.Scheduled,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

        Assert.True(AutoAiRecoveryEligibility.IsJobEligibleForAutoRecovery(eligible));

        eligible.Trigger = DownloadJobTrigger.Manual;
        Assert.True(AutoAiRecoveryEligibility.IsJobEligibleForAutoRecovery(eligible));

        eligible.Trigger = DownloadJobTrigger.AutoAiRecovery;
        Assert.False(AutoAiRecoveryEligibility.IsJobEligibleForAutoRecovery(eligible));

        eligible.Trigger = DownloadJobTrigger.Scheduled;
        eligible.CorrelationId = "recovery:abc";
        Assert.False(AutoAiRecoveryEligibility.IsJobEligibleForAutoRecovery(eligible));
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

    [Fact]
    public void Does_not_skip_AlAyam_baseline_when_config_already_active_baseline_guard_pre_applies()
    {
        var source = new NewsSource
        {
            Name = AlAyamPublicPdfBaseline.SourceName,
            ConnectorKey = AlAyamPublicPdfBaseline.ConnectorKey,
            UseHeadlessBrowser = true,
            BaseUrl = AlAyamPublicPdfBaseline.EpaperUrl,
            EditionUrl = AlAyamPublicPdfBaseline.EpaperUrl,
            PdfDiscoveryPageUrl = AlAyamPublicPdfBaseline.EpaperUrl,
            PdfLinkSelector = AlAyamPublicPdfBaseline.PdfLinkSelector
        };

        Assert.True(AlAyamPublicPdfBaseline.HasKnownGoodConfiguration(source));
        Assert.False(PublisherRepeatedRecoveryGuard.ShouldSkipRepeatedBaselineRecovery(
            source,
            "PdfValidationFailed",
            "Response appears to be HTML instead of a PDF."));
    }

    [Fact]
    public void Does_not_filter_redundant_AlAyam_baseline_option_for_auto_recovery()
    {
        var source = new NewsSource
        {
            ConnectorKey = AlAyamPublicPdfBaseline.ConnectorKey,
            UseHeadlessBrowser = true,
            BaseUrl = AlAyamPublicPdfBaseline.EpaperUrl,
            EditionUrl = AlAyamPublicPdfBaseline.EpaperUrl,
            PdfDiscoveryPageUrl = AlAyamPublicPdfBaseline.EpaperUrl,
            PdfLinkSelector = AlAyamPublicPdfBaseline.PdfLinkSelector
        };

        var options = SourceRecoveryHeuristicBuilder.MergePublisherHeuristics(
            new SourceRecoveryAnalysisContext(
                source.Id,
                AlAyamPublicPdfBaseline.SourceName,
                Guid.NewGuid(),
                SourceRecoveryFailureTypes.PdfValidationFailed,
                "PdfValidationFailed",
                "Response appears to be HTML instead of a PDF.",
                AlAyamPublicPdfBaseline.EpaperUrl,
                AlAyamPublicPdfBaseline.EpaperUrl,
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
                []),
            []);
        Assert.Equal(2, options.Count);
        Assert.False(PublisherRepeatedRecoveryGuard.IsRedundantBaselineOption(source, options[0]));
        Assert.Equal(AlAyamPublicPdfBaseline.InafDirectPdfLinkSelector, options[1].Patch.PdfLinkSelector);
    }

    [Fact]
    public void IsNonRetriablePublisherFailure_detects_datacenter_block_messages()
    {
        Assert.True(PublisherRepeatedRecoveryGuard.IsNonRetriablePublisherFailure(
            null,
            "Publisher blocked automated access (Cloudflare/bot protection) on the e-paper page."));
    }

    [Fact]
    public void AlAyam_second_heuristic_is_safe_for_auto_apply_with_extended_inaf_wait()
    {
        var context = new SourceRecoveryAnalysisContext(
            Guid.NewGuid(),
            AlAyamPublicPdfBaseline.SourceName,
            Guid.NewGuid(),
            SourceRecoveryFailureTypes.PdfLinkNotFound,
            "PdfLinkNotFound",
            "Al Ayam all-pages click path could not download a PDF from i.alayam.com.",
            AlAyamPublicPdfBaseline.EpaperUrl,
            AlAyamPublicPdfBaseline.EpaperUrl,
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
        Assert.Equal(2, options.Count);
        Assert.Equal(1, options[1].OptionIndex);
        Assert.Contains("INAF", options[1].Title, StringComparison.OrdinalIgnoreCase);

        var settings = new AutoAiDownloadRecoveryOptions
        {
            MinimumConfidence = 0.70,
            MaximumRiskAllowed = "Medium"
        };

        Assert.True(
            AutoAiRecoveryPatchValidator.IsOptionSafeForAutoApply(options[1], settings, out var reason),
            reason);
    }

    [Fact]
    public void SuggestionHistory_formats_exhausted_summary_with_titles_and_timestamps()
    {
        var when = new DateTimeOffset(2026, 7, 1, 7, 22, 0, TimeSpan.Zero);
        var entries = new[]
        {
            new TriedSuggestionEntry(0, "Restore Al Ayam e-paper PDF link selector and page URL", when),
            new TriedSuggestionEntry(1, "Wait longer for Al Ayam INAF PDF link on e-paper", when.AddMinutes(8))
        };

        var summary = AutoAiRecoverySuggestionHistory.FormatExhaustedSummary(entries);

        Assert.Contains("already tried today", summary, StringComparison.Ordinal);
        Assert.Contains("Restore Al Ayam", summary, StringComparison.Ordinal);
        Assert.Contains("INAF", summary, StringComparison.Ordinal);
        Assert.Contains("2026-07-01 07:22", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SuggestionHistory_formats_exhausted_summary_for_current_batch()
    {
        var when = new DateTimeOffset(2026, 7, 2, 7, 36, 0, TimeSpan.Zero);
        var entries = new[]
        {
            new TriedSuggestionEntry(0, "Restore Al Ayam e-paper PDF link selector and page URL", when),
            new TriedSuggestionEntry(1, "Wait longer for Al Ayam INAF PDF link on e-paper", when.AddMinutes(8))
        };

        var summary = AutoAiRecoverySuggestionHistory.FormatExhaustedSummary(entries, forCurrentBatch: true);

        Assert.Contains("already tried in this download batch", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("already tried today", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SuggestionHistory_parses_applied_steps_from_timeline()
    {
        var timeline = AutoAiRecoveryTimelineJson.Serialize([
            new AutoAiRecoveryTimelineStepDto(1, "Suggestion 1 applied", DateTimeOffset.Parse("2026-07-01T07:30:00Z"),
                "Wait longer for Al Ayam INAF PDF link on e-paper", true)
        ]);

        var parsed = AutoAiRecoverySuggestionHistory.ParseAppliedSuggestions(timeline);

        Assert.Single(parsed);
        Assert.Equal(1, parsed[0].OptionIndex);
        Assert.Contains("INAF", parsed[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public void TriedIndexAdjuster_removes_manual_succeeded_indices_from_tried_set()
    {
        var tried = new HashSet<int> { 0, 1, 2 };
        AutoAiRecoveryTriedIndexAdjuster.RemoveManualSucceededFromTried(tried, [0, 2]);
        Assert.Equal([1], tried.OrderBy(i => i));
    }

    [Fact]
    public void TriedIndexAdjuster_allows_AlAyam_baseline_retry_after_publisher_timing_gap()
    {
        var tried = new HashSet<int> { 0, 1 };
        var now = new DateTimeOffset(2026, 7, 5, 11, 11, 0, TimeSpan.Zero);
        var entries = new[]
        {
            new TriedSuggestionEntry(0, "Restore Al Ayam e-paper PDF link selector and page URL", now.AddMinutes(-45)),
            new TriedSuggestionEntry(1, "Wait longer for Al Ayam INAF PDF link on e-paper", now.AddMinutes(-5))
        };

        AutoAiRecoveryTriedIndexAdjuster.RemoveTimingRetriableFromTried(
            tried,
            entries,
            TimeSpan.FromMinutes(30),
            now);

        Assert.Equal([1], tried.OrderBy(i => i));
    }

    [Fact]
    public void TriedIndexAdjuster_keeps_recent_AlAyam_attempts_in_tried_set()
    {
        var tried = new HashSet<int> { 0 };
        var now = new DateTimeOffset(2026, 7, 5, 11, 11, 0, TimeSpan.Zero);
        var entries = new[]
        {
            new TriedSuggestionEntry(0, "Restore Al Ayam e-paper PDF link selector and page URL", now.AddMinutes(-10))
        };

        AutoAiRecoveryTriedIndexAdjuster.RemoveTimingRetriableFromTried(
            tried,
            entries,
            TimeSpan.FromMinutes(30),
            now);

        Assert.Equal([0], tried);
    }

    [Fact]
    public void Ranker_skips_previously_tried_option_indices()
    {
        var settings = new AutoAiDownloadRecoveryOptions
        {
            MinimumConfidence = 0.60,
            MaximumRiskAllowed = "High",
            MaxSuggestionsToTry = 5
        };

        var options = new[]
        {
            new SourceRecoveryOptionDto(
                0,
                "Suggestion A",
                "A",
                "A",
                95,
                90,
                SourceRecoveryRiskLevel.Low,
                [],
                [],
                new SourceRecoveryConfigurationPatchDto(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true),
                []),
            new SourceRecoveryOptionDto(
                1,
                "Suggestion B",
                "B",
                "B",
                90,
                85,
                SourceRecoveryRiskLevel.Low,
                [],
                [],
                new SourceRecoveryConfigurationPatchDto(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true),
                [])
        };

        var ranker = new AiRecoverySuggestionRanker();
        var ranked = ranker.RankForAutoRecovery(options, settings, new HashSet<int> { 0 });

        Assert.Single(ranked);
        Assert.Equal(1, ranked[0].OptionIndex);
        Assert.Equal("Suggestion B", ranked[0].Title);
    }
}
