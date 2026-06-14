using System.Text;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Infrastructure.News.PdfEdition.SelectorSuggestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class PdfDiscoveryPageCaptureService(
    IFileStorageService fileStorage,
    IOptions<StorageOptions> storageOptions,
    IOptions<AiSelectorSuggestionOptions> suggestionOptions,
    ILogger<PdfDiscoveryPageCaptureService> logger) : IPdfDiscoveryPageCaptureService
{
    public async Task<PdfDiscoveryPageCapture> CaptureAsync(string pageUrl, bool useHeadlessBrowser, CancellationToken cancellationToken)
    {
        if (!useHeadlessBrowser)
        {
            throw new InvalidOperationException("AI selector suggestion requires headless browser capture.");
        }

        using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
        var page = await browser.NewPageAsync().ConfigureAwait(false);
        await page.GotoAsync(pageUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 90_000 }).ConfigureAwait(false);

        var title = await page.TitleAsync().ConfigureAwait(false);
        var html = await page.ContentAsync().ConfigureAwait(false);
        var sanitized = PdfSelectorSuggestionSanitizer.SanitizeHtml(html);
        var candidates = PdfSelectorSuggestionSanitizer.ExtractCandidateElements(
            sanitized,
            suggestionOptions.Value.MaxCandidateElements);

        var fragment = sanitized.Length <= suggestionOptions.Value.MaxHtmlChars
            ? sanitized
            : sanitized[..suggestionOptions.Value.MaxHtmlChars];

        string? htmlPath = null;
        string? screenshotPath = null;
        try
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var folder = $"{storageOptions.Value.NewspapersRelativePath}/ai-selector-snapshots/{stamp}";
            var htmlBytes = Encoding.UTF8.GetBytes(sanitized);
            var htmlWrite = await fileStorage.WriteAsync($"{folder}/page.html", htmlBytes, cancellationToken).ConfigureAwait(false);
            htmlPath = htmlWrite.RelativeKey;

            var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true }).ConfigureAwait(false);
            var pngWrite = await fileStorage.WriteAsync($"{folder}/page.png", screenshot, cancellationToken).ConfigureAwait(false);
            screenshotPath = pngWrite.RelativeKey;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist AI selector capture artifacts for {Url}.", pageUrl);
        }

        return new PdfDiscoveryPageCapture(
            pageUrl,
            title ?? string.Empty,
            fragment,
            candidates,
            htmlPath,
            screenshotPath);
    }
}
