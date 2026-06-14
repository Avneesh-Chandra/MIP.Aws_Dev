namespace MIP.Aws.Application.Abstractions.Browser;

/// <summary>
/// Optional Playwright-backed HTML capture for JavaScript-heavy newspaper sites.
/// </summary>
public interface IHeadlessBrowserService
{
    /// <summary>
    /// Returns rendered HTML or null when headless capture is unavailable or fails.
    /// </summary>
    Task<string?> GetRenderedHtmlAsync(Uri url, TimeSpan timeout, CancellationToken cancellationToken);
}
