using System.Diagnostics;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.Browser;

/// <summary>Container-safe Chromium launch options for Azure/Linux non-root runtimes.</summary>
public static class PlaywrightBrowserLaunch
{
    public static readonly string[] ChromiumArgs =
    [
        "--no-sandbox",
        "--disable-dev-shm-usage",
        "--disable-gpu",
        "--disable-software-rasterizer",
        "--disable-blink-features=AutomationControlled"
    ];

    public static BrowserTypeLaunchOptions ChromiumLaunchOptions(bool headless = true) => new()
    {
        Headless = headless,
        Args = ChromiumArgs
    };

    public static async Task<IPlaywright> CreatePlaywrightAsync()
    {
        EnsureDriverExecutables();
        return await Playwright.CreateAsync().ConfigureAwait(false);
    }

    public static Task<IBrowser> LaunchChromiumAsync(IPlaywright playwright, bool headless = true) =>
        playwright.Chromium.LaunchAsync(ChromiumLaunchOptions(headless));

    /// <summary>
    /// Launches Chromium headed when a display is available (existing DISPLAY or Xvfb),
    /// otherwise falls back to headless. Used for Al Ayam RPA-parity downloads.
    /// </summary>
    public static async Task<(IBrowser Browser, bool Headed)> LaunchChromiumPreferHeadedAsync(
        IPlaywright playwright)
    {
        var headed = PlaywrightVirtualDisplay.TryEnsureDisplay();
        var browser = await LaunchChromiumAsync(playwright, headless: !headed).ConfigureAwait(false);
        return (browser, headed);
    }

    internal static void EnsureDriverExecutables()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var playwrightDir = Path.Combine(AppContext.BaseDirectory, ".playwright");
        if (!Directory.Exists(playwrightDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(playwrightDir, "node", SearchOption.AllDirectories))
        {
            TryChmodExecutable(file);
        }

        foreach (var file in Directory.EnumerateFiles(playwrightDir, "*.sh", SearchOption.AllDirectories))
        {
            TryChmodExecutable(file);
        }
    }

    private static void TryChmodExecutable(string path)
    {
        try
        {
            using var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            chmod?.WaitForExit(5_000);
        }
        catch
        {
            // Best-effort; launch will surface a clear error if this fails.
        }
    }
}
