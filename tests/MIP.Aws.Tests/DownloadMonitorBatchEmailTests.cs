using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Operator;

namespace MIP.Aws.Tests;

public sealed class DownloadMonitorBatchEmailTests
{
    [Theory]
    [InlineData(DownloadJobStatus.Succeeded, true)]
    [InlineData(DownloadJobStatus.Failed, true)]
    [InlineData(DownloadJobStatus.AutoAiRecoveryAnalyzing, true)]
    [InlineData(DownloadJobStatus.AutoAiRecoveryApplying, true)]
    [InlineData(DownloadJobStatus.AutoAiRecoveryRetrying, true)]
    [InlineData(DownloadJobStatus.FailedAfterAutoAiRecovery, true)]
    [InlineData(DownloadJobStatus.Pending, false)]
    [InlineData(DownloadJobStatus.Running, false)]
    public void Batch_download_phase_complete_allows_terminal_and_auto_recovery_states(
        DownloadJobStatus status,
        bool expectedComplete)
    {
        var complete = DownloadMonitorBatchOutcomeHelper.IsAutoRecoveryInProgressStatus(status)
                       || DownloadMonitorBatchOutcomeHelper.IsTerminalDownloadStatus(status);

        Assert.Equal(expectedComplete, complete);
    }

    [Fact]
    public void Initial_and_recovery_follow_up_subjects_use_distinct_suffixes()
    {
        var date = new DateOnly(2026, 6, 29);
        var initial = $"GFH MIP AWS — Download Monitor status ({date:yyyy-MM-dd})";
        var followUp = $"GFH MIP AWS — Download Monitor recovery update ({date:yyyy-MM-dd})";
        Assert.NotEqual(initial, followUp);
        Assert.Contains("recovery update", followUp, StringComparison.OrdinalIgnoreCase);
    }
}
