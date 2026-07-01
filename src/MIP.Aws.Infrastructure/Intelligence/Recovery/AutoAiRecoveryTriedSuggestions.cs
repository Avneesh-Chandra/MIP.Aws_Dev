using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Features.AutoAiRecovery;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

/// <summary>Tracks which AI recovery option indices were already applied for a source today.</summary>
internal static class AutoAiRecoveryTriedSuggestions
{
    public static IReadOnlySet<int> FromTimelineJson(string? timelineJson) =>
        AutoAiRecoverySuggestionHistory.ParseAppliedSuggestions(timelineJson)
            .Select(e => e.OptionIndex)
            .ToHashSet();

    public static Task<HashSet<int>> LoadForSourceTodayAsync(
        IApplicationDbContext db,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        AutoAiRecoverySuggestionHistory.LoadTriedOptionIndicesForSourceTodayAsync(db, sourceId, cancellationToken);
}
