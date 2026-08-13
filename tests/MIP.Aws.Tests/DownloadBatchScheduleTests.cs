using MIP.Aws.Application.Scheduling;

namespace MIP.Aws.Tests;

public sealed class DownloadBatchScheduleTests
{
    [Theory]
    [InlineData("04:30", DownloadBatchRecurrence.Daily, null, "30 4 * * *")]
    [InlineData("04:30", DownloadBatchRecurrence.Weekdays, null, "30 4 * * 1-5")]
    [InlineData("16:05", DownloadBatchRecurrence.CustomDays, "1,3,5", "5 16 * * 1,3,5")]
    public void ToUtcCron_builds_hangfire_expression(
        string time,
        DownloadBatchRecurrence recurrence,
        string? days,
        string expected)
    {
        Assert.Equal(expected, DownloadBatchSchedule.ToUtcCron(time, recurrence, days));
    }

    [Fact]
    public void Describe_includes_time_and_recurrence()
    {
        Assert.Equal(
            "Every day at 04:30 UTC",
            DownloadBatchSchedule.Describe("04:30", DownloadBatchRecurrence.Daily, null));
        Assert.Equal(
            "Weekdays at 04:30 UTC",
            DownloadBatchSchedule.Describe("04:30", DownloadBatchRecurrence.Weekdays, null));
        Assert.Contains("Monday", DownloadBatchSchedule.Describe("04:30", DownloadBatchRecurrence.CustomDays, "1"), StringComparison.Ordinal);
    }

    [Fact]
    public void TryNormalizeTimeUtc_rejects_invalid_values()
    {
        Assert.False(DownloadBatchSchedule.TryNormalizeTimeUtc("25:00", out _, out var error));
        Assert.Contains("HH:mm", error, StringComparison.OrdinalIgnoreCase);
        Assert.True(DownloadBatchSchedule.TryNormalizeTimeUtc("4:30", out var normalized, out _));
        Assert.Equal("04:30", normalized);
    }
}
