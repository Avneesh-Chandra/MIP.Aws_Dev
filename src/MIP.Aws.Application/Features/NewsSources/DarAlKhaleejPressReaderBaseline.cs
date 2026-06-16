using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Application.Features.SourceRecovery;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>
/// Known-good Dar Al Khaleej PressReader selectors: click spread → page actions → تنزيل → issue PDF.
/// </summary>
public static class DarAlKhaleejPressReaderBaseline
{
    public const string ConnectorKey = "news.pressreader";
    public const string PortalStrategyKey = "PressReader";

    public const string NewspaperCanvasSelector =
        "[class*='issue-page'], [class*='page-image'], #reader";

    public const string ContextMenuSelector =
        "[class*='page-actions'] button, [class*='toolbar'] button[class*='more']";

    public const string DownloadMenuItemSelector =
        "li:has-text('تنزيل'), [role='menuitem']:has-text('تنزيل'), button:has-text('تنزيل')";

    public const int DownloadWaitTimeoutSeconds = 300;

    public static bool IsPressReaderSource(string? connectorKey, string? portalStrategyKey, string? editionUrl, string? baseUrl) =>
        string.Equals(connectorKey, ConnectorKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(portalStrategyKey, PortalStrategyKey, StringComparison.OrdinalIgnoreCase)
        || ContainsPressReaderHost(editionUrl)
        || ContainsPressReaderHost(baseUrl);

    public static bool IsPressReaderAnalysisContext(SourceRecoveryAnalysisContext context) =>
        context.SourceName.Contains("Al Khaleej", StringComparison.OrdinalIgnoreCase)
        || ContainsPressReaderHost(context.EditionUrl)
        || ContainsPressReaderHost(context.SourceUrl)
        || ContainsPressReaderHost(context.LoginUrl);

    public static SourceRecoveryConfigurationPatchDto RecoveryPatch() => new(
        null,
        null,
        null,
        null,
        null,
        NewspaperCanvasSelector,
        ContextMenuSelector,
        DownloadMenuItemSelector,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        DownloadWaitTimeoutSeconds,
        null);

    private static bool ContainsPressReaderHost(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.Contains("pressreader.com", StringComparison.OrdinalIgnoreCase);
}
