using MIP.Aws.Infrastructure.Operator;

namespace MIP.Aws.Tests;

public sealed class DownloadMonitorStatusEmailGuardTests
{
    [Fact]
    public void Recovery_follow_up_enqueue_claim_matches_batch_start()
    {
        var batchStartedAt = new DateTimeOffset(2026, 7, 5, 4, 30, 0, TimeSpan.Zero);

        Assert.True(DownloadMonitorStatusEmailGuard.IsRecoveryFollowUpEnqueueClaim(batchStartedAt, batchStartedAt));
        Assert.False(DownloadMonitorStatusEmailGuard.IsRecoveryFollowUpEnqueueClaim(
            batchStartedAt.AddHours(1),
            batchStartedAt));
    }
}
