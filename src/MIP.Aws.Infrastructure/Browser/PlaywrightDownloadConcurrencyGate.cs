namespace MIP.Aws.Infrastructure.Browser;

/// <summary>
/// Serializes Playwright-heavy newspaper downloads within a single worker process.
/// Prevents two Chromium sessions competing for CPU/RAM on small Fargate tasks.
/// </summary>
internal static class PlaywrightDownloadConcurrencyGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task RunAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await work().ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }
}
