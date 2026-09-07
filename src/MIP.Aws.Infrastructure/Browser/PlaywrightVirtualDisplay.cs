using System.Diagnostics;

namespace MIP.Aws.Infrastructure.Browser;

/// <summary>
/// Ensures a DISPLAY exists for headed Chromium (RPA-parity). On Linux, starts Xvfb when missing.
/// Returns false when headed mode is not possible (caller should use headless).
/// </summary>
internal static class PlaywrightVirtualDisplay
{
    private const string DefaultDisplay = ":99";
    private static readonly object Gate = new();
    private static Process? _xvfb;
    private static bool _attempted;

    /// <summary>True when Chromium can run headed (Windows desktop, existing DISPLAY, or Xvfb started).</summary>
    public static bool TryEnsureDisplay()
    {
        if (!OperatingSystem.IsLinux())
        {
            // Local Windows / IIS: real desktop session — headed Chromium is fine.
            return true;
        }

        var existing = Environment.GetEnvironmentVariable("DISPLAY");
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return true;
        }

        lock (Gate)
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            {
                return true;
            }

            if (_xvfb is { HasExited: false })
            {
                Environment.SetEnvironmentVariable("DISPLAY", DefaultDisplay);
                return true;
            }

            if (_attempted && _xvfb is null)
            {
                return false;
            }

            _attempted = true;
            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = "Xvfb",
                    Arguments = $"{DefaultDisplay} -screen 0 1440x900x24 -ac",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                _xvfb = Process.Start(start);
                if (_xvfb is null)
                {
                    return false;
                }

                // Give Xvfb a moment to bind the display.
                Thread.Sleep(500);
                if (_xvfb.HasExited)
                {
                    _xvfb.Dispose();
                    _xvfb = null;
                    return false;
                }

                Environment.SetEnvironmentVariable("DISPLAY", DefaultDisplay);
                return true;
            }
            catch
            {
                _xvfb = null;
                return false;
            }
        }
    }
}
