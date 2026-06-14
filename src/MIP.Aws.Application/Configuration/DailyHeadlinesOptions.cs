namespace MIP.Aws.Application.Configuration;

/// <summary>
/// How many cross-source headline stories to surface on executive views and digests.
/// </summary>
public sealed class DailyHeadlinesOptions
{
    public const string SectionName = "DailyHeadlines";

    /// <summary>Target number of major stories (typically 4–5).</summary>
    public int MaxItems { get; set; } = 5;

    /// <summary>Minimum GFH-relevant slots when available (remainder filled with general news).</summary>
    public int MinGfhSlots { get; set; } = 1;

    /// <summary>Maximum stories from the same newspaper source.</summary>
    public int MaxPerSource { get; set; } = 2;

    /// <summary>Hours to look back when UTC-day window has few articles.</summary>
    public int LookbackHours { get; set; } = 36;
}
