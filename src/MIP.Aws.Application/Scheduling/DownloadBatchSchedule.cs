using System.Globalization;

namespace MIP.Aws.Application.Scheduling;

public enum DownloadBatchRecurrence
{
    Daily = 0,
    Weekdays = 1,
    CustomDays = 2
}

/// <summary>UTC Hangfire cron for the staggered download-monitor batch.</summary>
public static class DownloadBatchSchedule
{
    public const string RecurringJobId = "download-monitor-stagger-schedule";

    public static readonly IReadOnlyList<(int CronDay, string Label)> WeekDays =
    [
        (1, "Monday"),
        (2, "Tuesday"),
        (3, "Wednesday"),
        (4, "Thursday"),
        (5, "Friday"),
        (6, "Saturday"),
        (0, "Sunday")
    ];

    public static bool TryNormalizeTimeUtc(string? timeUtc, out string normalized, out string? error)
    {
        normalized = "04:30";
        error = null;
        if (string.IsNullOrWhiteSpace(timeUtc)
            || !TimeOnly.TryParse(timeUtc.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            error = "Batch time must be UTC in HH:mm (e.g. 04:30).";
            return false;
        }

        normalized = time.ToString("HH:mm", CultureInfo.InvariantCulture);
        return true;
    }

    public static IReadOnlyList<int> ParseDays(string? days)
    {
        if (string.IsNullOrWhiteSpace(days))
        {
            return [];
        }

        return days.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var day) ? day : -1)
            .Where(day => day is >= 0 and <= 6)
            .Distinct()
            .OrderBy(day => day == 0 ? 7 : day)
            .ToArray();
    }

    public static string FormatDays(IEnumerable<int> days) =>
        string.Join(',', days.Where(day => day is >= 0 and <= 6).Distinct().OrderBy(day => day == 0 ? 7 : day));

    public static string ToUtcCron(
        string timeUtc,
        DownloadBatchRecurrence recurrence,
        string? customDays)
    {
        if (!TryNormalizeTimeUtc(timeUtc, out var normalized, out _))
        {
            normalized = "04:30";
        }

        var time = TimeOnly.Parse(normalized, CultureInfo.InvariantCulture);
        var dayPart = recurrence switch
        {
            DownloadBatchRecurrence.Weekdays => "1-5",
            DownloadBatchRecurrence.CustomDays => ToCronDayPart(ParseDays(customDays)),
            _ => "*"
        };

        return $"{time.Minute} {time.Hour} * * {dayPart}";
    }

    public static string Describe(
        string timeUtc,
        DownloadBatchRecurrence recurrence,
        string? customDays)
    {
        if (!TryNormalizeTimeUtc(timeUtc, out var normalized, out _))
        {
            normalized = timeUtc?.Trim() ?? "04:30";
        }

        return recurrence switch
        {
            DownloadBatchRecurrence.Weekdays => $"Weekdays at {normalized} UTC",
            DownloadBatchRecurrence.CustomDays => $"{DescribeDays(ParseDays(customDays))} at {normalized} UTC",
            _ => $"Every day at {normalized} UTC"
        };
    }

    public static DownloadBatchRecurrence ParseRecurrence(string? value) =>
        Enum.TryParse<DownloadBatchRecurrence>(value, ignoreCase: true, out var parsed)
            ? parsed
            : DownloadBatchRecurrence.Daily;

    private static string ToCronDayPart(IReadOnlyList<int> days)
    {
        if (days.Count == 0)
        {
            return "*";
        }

        if (days.Count == 5 && days.SequenceEqual([1, 2, 3, 4, 5]))
        {
            return "1-5";
        }

        return string.Join(',', days);
    }

    private static string DescribeDays(IReadOnlyList<int> days)
    {
        if (days.Count == 0)
        {
            return "Every day";
        }

        var labels = WeekDays
            .Where(d => days.Contains(d.CronDay))
            .Select(d => d.Label)
            .ToArray();
        return labels.Length == 0 ? "Selected days" : string.Join(", ", labels);
    }
}
