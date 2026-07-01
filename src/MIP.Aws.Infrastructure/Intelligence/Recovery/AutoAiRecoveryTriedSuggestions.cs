using System.Text.Json;
using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Features.AutoAiRecovery;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

/// <summary>Tracks which AI recovery option indices were already applied for a source today.</summary>
internal static class AutoAiRecoveryTriedSuggestions
{
    private static readonly Regex SuggestionAppliedStep = new(
        @"^Suggestion (\d+) applied$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlySet<int> FromTimelineJson(string? timelineJson)
    {
        var tried = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(timelineJson))
        {
            return tried;
        }

        try
        {
            foreach (var step in AutoAiRecoveryTimelineJson.Deserialize(timelineJson))
            {
                var match = SuggestionAppliedStep.Match(step.Step ?? string.Empty);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var optionIndex))
                {
                    tried.Add(optionIndex);
                }
            }
        }
        catch (JsonException)
        {
            // ignore corrupt timeline payloads
        }

        return tried;
    }

    public static async Task<HashSet<int>> LoadForSourceTodayAsync(
        IApplicationDbContext db,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var dayStart = DateTimeOffset.UtcNow.Date;
        var tried = new HashSet<int>();

        var timelines = await db.AutoAiRecoveryRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && r.NewsSourceId == sourceId && r.CreatedAt >= dayStart)
            .Select(r => r.TimelineJson)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var timeline in timelines)
        {
            tried.UnionWith(FromTimelineJson(timeline));
        }

        var attemptIndices = await db.SourceRecoveryAttempts.AsNoTracking()
            .Where(a => !a.IsDeleted
                        && a.NewsSourceId == sourceId
                        && a.IsAutomatic
                        && a.CreatedAt >= dayStart
                        && a.SelectedOptionIndex >= 0)
            .Select(a => a.SelectedOptionIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var index in attemptIndices)
        {
            tried.Add(index);
        }

        return tried;
    }
}
