using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.News.PdfEdition;

namespace MIP.Aws.Infrastructure.Intelligence.Recovery;

/// <summary>Builds the tried-index set used to exclude options from automatic recovery ranking.</summary>
internal static class AutoAiRecoveryRetriableSuggestions
{
    public static async Task<HashSet<int>> LoadExcludedIndicesForAutoRecoveryAsync(
        IApplicationDbContext db,
        NewsSource source,
        Guid sourceId,
        DateTimeOffset? batchStartedAt,
        CancellationToken cancellationToken,
        Guid? excludeRunId = null)
    {
        var entries = batchStartedAt is DateTimeOffset batchStart
            ? await AutoAiRecoverySuggestionHistory.LoadTriedEntriesForSourceBatchAsync(
                    db,
                    sourceId,
                    batchStart,
                    cancellationToken,
                    excludeRunId)
                .ConfigureAwait(false)
            : await AutoAiRecoverySuggestionHistory.LoadTriedEntriesForSourceTodayAsync(
                    db,
                    sourceId,
                    cancellationToken,
                    excludeRunId)
                .ConfigureAwait(false);

        var tried = entries.Select(e => e.OptionIndex).ToHashSet();

        var manualSucceeded = await AutoAiRecoverySuggestionHistory
            .LoadManualSucceededOptionIndicesSinceAsync(db, sourceId, batchStartedAt, cancellationToken)
            .ConfigureAwait(false);
        AutoAiRecoveryTriedIndexAdjuster.RemoveManualSucceededFromTried(tried, manualSucceeded);

        ApplyPublisherTimingRetries(source, tried, entries);

        return tried;
    }

    private static void ApplyPublisherTimingRetries(
        NewsSource source,
        HashSet<int> tried,
        IReadOnlyList<TriedSuggestionEntry> entries)
    {
        if (!AlAyamPublisherTiming.IsAlAyamSource(source))
        {
            return;
        }

        AutoAiRecoveryTriedIndexAdjuster.RemoveTimingRetriableFromTried(
            tried,
            entries,
            AlAyamPublisherTiming.DeferredDownloadDelays[0],
            DateTimeOffset.UtcNow);
    }
}
