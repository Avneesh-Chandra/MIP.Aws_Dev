namespace MIP.Aws.Application.Features.AutoAiRecovery;

/// <summary>Adjusts batch-scoped tried suggestion indices before auto recovery ranks options.</summary>
public static class AutoAiRecoveryTriedIndexAdjuster
{
    public static void RemoveManualSucceededFromTried(HashSet<int> tried, IEnumerable<int> manualSucceeded) =>
        tried.ExceptWith(manualSucceeded);

    /// <summary>
    /// Allows the same option to be retried after a publisher timing gap when an earlier attempt likely failed
    /// because the edition was not published yet (not because the configuration was wrong).
    /// </summary>
    public static void RemoveTimingRetriableFromTried(
        HashSet<int> tried,
        IReadOnlyList<TriedSuggestionEntry> entries,
        TimeSpan minGapSinceLastAttempt,
        DateTimeOffset utcNow)
    {
        foreach (var entry in entries)
        {
            if (utcNow - entry.LastAttemptAt >= minGapSinceLastAttempt)
            {
                tried.Remove(entry.OptionIndex);
            }
        }
    }
}
