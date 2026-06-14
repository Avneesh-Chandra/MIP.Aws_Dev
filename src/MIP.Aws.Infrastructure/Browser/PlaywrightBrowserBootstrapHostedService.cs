using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Browser;

/// <summary>
/// On local development startup, installs Playwright Chromium when missing so PDF/portal captures work.
/// </summary>
public sealed class PlaywrightBrowserBootstrapHostedService(
    IHostEnvironment environment,
    ILogger<PlaywrightBrowserBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        await PlaywrightChromiumProvisioner.EnsureChromiumInstalledAsync(logger, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
