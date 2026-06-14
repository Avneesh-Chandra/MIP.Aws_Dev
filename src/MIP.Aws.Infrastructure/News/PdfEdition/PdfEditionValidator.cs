using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Validates PDF candidates via HTTP or Playwright fetch and magic-byte checks.
/// </summary>
public sealed class PdfEditionValidator(PdfEditionContentFetcher contentFetcher)
{
    public async Task<PdfEditionValidationResult> ValidateAsync(
        Uri url,
        bool requirePdfContent,
        int minimumSizeKb,
        bool useHeadlessBrowser,
        Uri? warmUpUrl,
        CancellationToken cancellationToken,
        NewsSource? source = null)
    {
        try
        {
            var bytes = await contentFetcher.FetchAsync(url, useHeadlessBrowser, warmUpUrl, source, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return new PdfEditionValidationResult(false, null, null, "Empty response when fetching PDF candidate.");
            }

            if (LooksLikeHtml(bytes))
            {
                return new PdfEditionValidationResult(false, "text/html", bytes.Length, "Response appears to be HTML (login/error page).");
            }

            if (!PdfEditionContentFetcher.IsPdf(bytes))
            {
                return new PdfEditionValidationResult(
                    false,
                    null,
                    bytes.Length,
                    requirePdfContent ? "Missing PDF magic bytes (%PDF)." : "Not a PDF file.");
            }

            if (bytes.Length < minimumSizeKb * 1024L)
            {
                return new PdfEditionValidationResult(false, "application/pdf", bytes.Length, $"File smaller than minimum {minimumSizeKb} KB.");
            }

            return new PdfEditionValidationResult(true, "application/pdf", bytes.Length, null, bytes);
        }
        catch (Exception ex)
        {
            return new PdfEditionValidationResult(false, null, null, ex.Message);
        }
    }

    private static bool LooksLikeHtml(byte[] bytes)
    {
        var sample = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart();
        return sample.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
               || sample.Contains("<head", StringComparison.OrdinalIgnoreCase);
    }
}
