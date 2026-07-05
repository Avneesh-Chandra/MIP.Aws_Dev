using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.AutoAiRecovery;

public sealed record TriedSuggestionEntry(int OptionIndex, string Title, DateTimeOffset LastAttemptAt);

/// <summary>Parses and formats AI suggestion attempts across automatic recovery runs (manual attempts are excluded).</summary>
public static class AutoAiRecoverySuggestionHistory
{
    private static readonly Regex SuggestionAppliedStep = new(
        @"^Suggestion (\d+) applied$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SuggestionAlreadyTriedStep = new(
        @"^Suggestion (\d+) already tried$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TriedSuggestionEntry> ParseAppliedSuggestions(string? timelineJson)
    {
        if (string.IsNullOrWhiteSpace(timelineJson))
        {
            return [];
        }

        var entries = new List<TriedSuggestionEntry>();
        foreach (var step in AutoAiRecoveryTimelineJson.Deserialize(timelineJson))
        {
            var applied = SuggestionAppliedStep.Match(step.Step ?? string.Empty);
            var alreadyTried = SuggestionAlreadyTriedStep.Match(step.Step ?? string.Empty);
            if (!applied.Success && !alreadyTried.Success)
            {
                continue;
            }

            if (!int.TryParse((applied.Success ? applied : alreadyTried).Groups[1].Value, out var optionIndex))
            {
                continue;
            }

            var title = ExtractTitleFromDetail(step.Detail);
            entries.Add(new TriedSuggestionEntry(optionIndex, title, step.Timestamp));
        }

        return entries;
    }

    public static IReadOnlyList<TriedSuggestionEntry> AggregateLatestByOptionIndex(
        IEnumerable<(string? TimelineJson, IReadOnlyList<SourceRecoveryOptionDto> Options)> runs)
    {
        var latest = new Dictionary<int, TriedSuggestionEntry>();
        foreach (var (timelineJson, options) in runs)
        {
            foreach (var entry in ParseAppliedSuggestions(timelineJson))
            {
                var title = ResolveTitle(entry.OptionIndex, entry.Title, options);
                var normalized = entry with { Title = title };
                if (!latest.TryGetValue(entry.OptionIndex, out var existing)
                    || normalized.LastAttemptAt > existing.LastAttemptAt)
                {
                    latest[entry.OptionIndex] = normalized;
                }
            }
        }

        return latest.Values.OrderBy(e => e.OptionIndex).ToList();
    }

    public static async Task<DateTimeOffset?> ResolveBatchStartedAtForFailedJobAsync(
        IApplicationDbContext db,
        Guid failedDownloadJobId,
        CancellationToken cancellationToken)
    {
        var jobCreatedAt = await db.DownloadJobs.AsNoTracking()
            .Where(j => !j.IsDeleted && j.Id == failedDownloadJobId)
            .Select(j => (DateTimeOffset?)j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (jobCreatedAt is null)
        {
            return null;
        }

        return await db.DownloadMonitorBatchRuns.AsNoTracking()
            .Where(b => !b.IsDeleted && b.StartedAt <= jobCreatedAt.Value)
            .OrderByDescending(b => b.StartedAt)
            .Select(b => (DateTimeOffset?)b.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<HashSet<int>> LoadTriedOptionIndicesForSourceTodayAsync(
        IApplicationDbContext db,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var entries = await LoadTriedEntriesForSourceTodayAsync(db, sourceId, cancellationToken)
            .ConfigureAwait(false);
        return entries.Select(e => e.OptionIndex).ToHashSet();
    }

    public static async Task<HashSet<int>> LoadTriedOptionIndicesForSourceBatchAsync(
        IApplicationDbContext db,
        Guid sourceId,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken,
        Guid? excludeRunId = null)
    {
        var entries = await LoadTriedEntriesForSourceBatchAsync(
                db,
                sourceId,
                batchStartedAt,
                cancellationToken,
                excludeRunId)
            .ConfigureAwait(false);
        return entries.Select(e => e.OptionIndex).ToHashSet();
    }

    public static Task<IReadOnlyList<TriedSuggestionEntry>> LoadTriedEntriesForSourceBatchAsync(
        IApplicationDbContext db,
        Guid sourceId,
        DateTimeOffset batchStartedAt,
        CancellationToken cancellationToken,
        Guid? excludeRunId = null,
        DateTimeOffset? beforeCreatedAt = null)
    {
        var notBefore = batchStartedAt.AddMinutes(-1);
        return LoadTriedEntriesForSourceSinceAsync(
            db,
            sourceId,
            notBefore,
            cancellationToken,
            excludeRunId,
            beforeCreatedAt);
    }

    public static async Task<IReadOnlyList<TriedSuggestionEntry>> LoadTriedEntriesForSourceTodayAsync(
        IApplicationDbContext db,
        Guid sourceId,
        CancellationToken cancellationToken,
        Guid? excludeRunId = null,
        DateTimeOffset? beforeCreatedAt = null)
    {
        var dayStart = (beforeCreatedAt ?? DateTimeOffset.UtcNow).UtcDateTime.Date;
        var dayStartOffset = new DateTimeOffset(dayStart, TimeSpan.Zero);
        var dayEndOffset = dayStartOffset.AddDays(1);

        return await LoadTriedEntriesForSourceSinceAsync(
                db,
                sourceId,
                dayStartOffset,
                cancellationToken,
                excludeRunId,
                beforeCreatedAt,
                notAfter: dayEndOffset)
            .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<TriedSuggestionEntry>> LoadTriedEntriesForSourceSinceAsync(
        IApplicationDbContext db,
        Guid sourceId,
        DateTimeOffset notBefore,
        CancellationToken cancellationToken,
        Guid? excludeRunId = null,
        DateTimeOffset? beforeCreatedAt = null,
        DateTimeOffset? notAfter = null)
    {
        var runs = await db.AutoAiRecoveryRuns.AsNoTracking()
            .Where(r => !r.IsDeleted
                        && r.NewsSourceId == sourceId
                        && r.CreatedAt >= notBefore
                        && (notAfter == null || r.CreatedAt < notAfter)
                        && (excludeRunId == null || r.Id != excludeRunId)
                        && (beforeCreatedAt == null || r.CreatedAt < beforeCreatedAt))
            .OrderBy(r => r.CreatedAt)
            .Select(r => new { r.TimelineJson, r.SourceRecoveryAttemptId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var attemptIds = runs
            .Where(r => r.SourceRecoveryAttemptId is not null)
            .Select(r => r.SourceRecoveryAttemptId!.Value)
            .Distinct()
            .ToList();

        var analysisByAttempt = attemptIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<SourceRecoveryOptionDto>>()
            : await db.SourceRecoveryAttempts.AsNoTracking()
                .Where(a => attemptIds.Contains(a.Id))
                .Select(a => new { a.Id, a.AnalysisJson })
                .ToDictionaryAsync(
                    a => a.Id,
                    a => ParseOptions(a.AnalysisJson),
                    cancellationToken)
                .ConfigureAwait(false);

        var runPayloads = runs.Select(r =>
        {
            IReadOnlyList<SourceRecoveryOptionDto> options = [];
            if (r.SourceRecoveryAttemptId is Guid attemptId
                && analysisByAttempt.TryGetValue(attemptId, out var parsed))
            {
                options = parsed;
            }

            return (r.TimelineJson, options);
        });

        var aggregated = AggregateLatestByOptionIndex(runPayloads);

        var attemptIndices = await db.SourceRecoveryAttempts.AsNoTracking()
            .Where(a => !a.IsDeleted
                        && a.NewsSourceId == sourceId
                        && a.IsAutomatic
                        && a.CreatedAt >= notBefore
                        && (notAfter == null || a.CreatedAt < notAfter)
                        && a.SelectedOptionIndex >= 0
                        && (beforeCreatedAt == null || a.CreatedAt < beforeCreatedAt))
            .Select(a => new { a.SelectedOptionIndex, a.AppliedAt, a.CreatedAt, a.AnalysisJson })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var map = aggregated.ToDictionary(e => e.OptionIndex);
        foreach (var attempt in attemptIndices)
        {
            var when = attempt.AppliedAt ?? attempt.CreatedAt;
            var options = ParseOptions(attempt.AnalysisJson);
            var title = ResolveTitle(attempt.SelectedOptionIndex, null, options);
            if (!map.TryGetValue(attempt.SelectedOptionIndex, out var existing) || when > existing.LastAttemptAt)
            {
                map[attempt.SelectedOptionIndex] = new TriedSuggestionEntry(
                    attempt.SelectedOptionIndex,
                    title,
                    when);
            }
        }

        return map.Values.OrderBy(e => e.OptionIndex).ToList();
    }

    public static string FormatExhaustedSummary(
        IReadOnlyList<TriedSuggestionEntry> entries,
        bool forCurrentBatch = false)
    {
        if (entries.Count == 0)
        {
            return forCurrentBatch
                ? "No eligible AI recovery suggestions remain for this download batch."
                : "No eligible AI recovery suggestions remain for today.";
        }

        var parts = entries
            .OrderByDescending(e => e.LastAttemptAt)
            .Select(e => $"{e.Title} (option {e.OptionIndex}, last {e.LastAttemptAt:yyyy-MM-dd HH:mm} UTC)");
        var scope = forCurrentBatch ? "in this download batch" : "today";
        return $"All eligible suggestions already tried {scope} without success: " + string.Join("; ", parts) + ".";
    }

    public static (string? SuggestionTitle, DateTimeOffset? LastAttemptAt, string? ResultSummary) ResolveHistoryDisplay(
        SourceRecoveryAttempt attempt,
        AutoAiRecoveryRun? run,
        IReadOnlyList<SourceRecoveryOptionDto> options,
        IReadOnlyList<TriedSuggestionEntry> triedBeforeThisRun,
        bool triedEntriesAreBatchScoped = false)
    {
        if (run is not null)
        {
            var appliedInRun = ParseAppliedSuggestions(run.TimelineJson);
            if (appliedInRun.Count > 0)
            {
                var last = appliedInRun[^1];
                var title = ResolveTitle(last.OptionIndex, last.Title, options);
                return (title, last.LastAttemptAt, run.ResultSummary ?? attempt.ResultSummary);
            }

            if (run.Status is AutoAiRecoveryRunStatus.SkippedNoSuggestions
                or AutoAiRecoveryRunStatus.SkippedRepeatedBaseline)
            {
                var prior = triedBeforeThisRun;
                if (prior.Count == 0)
                {
                    return (
                        run.Status == AutoAiRecoveryRunStatus.SkippedRepeatedBaseline
                            ? "Skipped — baseline already active"
                            : "Skipped — no eligible suggestions",
                        null,
                        run.ResultSummary);
                }

                var latest = prior.MaxBy(e => e.LastAttemptAt);
                var headline = triedEntriesAreBatchScoped
                    ? $"Skipped — {prior.Count} suggestion(s) already tried in this download batch"
                    : $"Skipped — {prior.Count} suggestion(s) already tried today";
                var detail = FormatExhaustedSummary(prior, triedEntriesAreBatchScoped);
                return (headline, latest?.LastAttemptAt, detail);
            }

            if (!string.IsNullOrWhiteSpace(run.SuccessfulOptionTitle))
            {
                return (run.SuccessfulOptionTitle, run.CompletedAt ?? attempt.AppliedAt, run.ResultSummary);
            }
        }

        if (attempt.SelectedOptionIndex >= 0)
        {
            var title = options.FirstOrDefault(o => o.OptionIndex == attempt.SelectedOptionIndex)?.Title
                        ?? $"Suggestion {attempt.SelectedOptionIndex}";
            return (title, attempt.AppliedAt ?? attempt.CreatedAt, attempt.ResultSummary);
        }

        return (options.FirstOrDefault()?.Title, null, attempt.ResultSummary);
    }

    private static IReadOnlyList<SourceRecoveryOptionDto> ParseOptions(string? analysisJson)
    {
        if (string.IsNullOrWhiteSpace(analysisJson))
        {
            return [];
        }

        try
        {
            return SourceRecoveryJsonParser.ParseOptions(analysisJson);
        }
        catch
        {
            return [];
        }
    }

    private static string ResolveTitle(
        int optionIndex,
        string? titleFromTimeline,
        IReadOnlyList<SourceRecoveryOptionDto> options)
    {
        if (!string.IsNullOrWhiteSpace(titleFromTimeline))
        {
            return titleFromTimeline.Trim();
        }

        return options.FirstOrDefault(o => o.OptionIndex == optionIndex)?.Title
               ?? $"Suggestion {optionIndex}";
    }

    private static string ExtractTitleFromDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return string.Empty;
        }

        var trimmed = detail.Trim();
        var lastAttemptIdx = trimmed.IndexOf("(last attempt", StringComparison.OrdinalIgnoreCase);
        if (lastAttemptIdx > 0)
        {
            return trimmed[..lastAttemptIdx].Trim();
        }

        return trimmed;
    }
}
