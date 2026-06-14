using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Browser;

/// <summary>
/// Ensures the Playwright Chromium build required by <see cref="Microsoft.Playwright"/> is present on the host.
/// </summary>
public static class PlaywrightChromiumProvisioner
{
    public static async Task<bool> CanLaunchChromiumAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            }).ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> EnsureChromiumInstalledAsync(ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await CanLaunchChromiumAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.LogDebug("Playwright Chromium is available.");
            return true;
        }

        logger.LogInformation(
            "Playwright Chromium is not installed for this package version. Running: playwright install chromium");

        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            logger.LogWarning(
                "playwright install chromium exited with code {ExitCode}. Run infra/local/Install-PlaywrightChromium.ps1 manually.",
                exitCode);
            return false;
        }

        if (await CanLaunchChromiumAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.LogInformation("Playwright Chromium installed successfully.");
            return true;
        }

        logger.LogWarning(
            "Playwright Chromium install completed but launch still fails. Check API logs and run infra/local/Install-PlaywrightChromium.ps1.");
        return false;
    }
}
