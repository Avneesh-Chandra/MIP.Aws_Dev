using MIP.Aws.Infrastructure.News.PdfEdition;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>Validates portal-downloaded bytes before persisting as a licensed edition PDF.</summary>
public static class PortalPdfFileValidator
{
    public static (bool Valid, string? FailureReason, string ContentType) Validate(
        byte[] bytes,
        bool requirePdfContent,
        int minimumSizeKb)
    {
        if (bytes.Length == 0)
        {
            return (false, "Downloaded file is empty.", "application/octet-stream");
        }

        if (LooksLikeHtml(bytes))
        {
            return (false, "Downloaded file appears to be HTML (login/error page).", "text/html");
        }

        if (!PdfEditionContentFetcher.IsPdf(bytes))
        {
            return requirePdfContent
                ? (false, "Missing PDF magic bytes (%PDF).", "application/octet-stream")
                : (false, "File is not a PDF.", "application/octet-stream");
        }

        if (bytes.Length < minimumSizeKb * 1024L)
        {
            return (false, $"PDF is smaller than minimum {minimumSizeKb} KB.", "application/pdf");
        }

        return (true, null, "application/pdf");
    }

    private static bool LooksLikeHtml(byte[] bytes)
    {
        var sample = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart();
        return sample.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
               || sample.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
               || sample.Contains("<head", StringComparison.OrdinalIgnoreCase);
    }
}
