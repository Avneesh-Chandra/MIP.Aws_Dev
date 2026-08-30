namespace MIP.Aws.Application.Features.NewsSources.PdfEdition;

public static class PdfManualUploadRules
{
    public const long MaxUploadBytes = 150L * 1024L * 1024L;

    public static string? Validate(string fileName, ReadOnlySpan<byte> content, int minimumSizeKb)
    {
        var normalizedFileName = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        if (!normalizedFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "Only PDF files can be uploaded.";
        }

        if (content.Length == 0)
        {
            return "The uploaded file is empty.";
        }

        if (content.Length > MaxUploadBytes)
        {
            return "The uploaded PDF exceeds the 150 MB limit.";
        }

        if (content.Length < Math.Max(0, minimumSizeKb) * 1024L)
        {
            return $"The uploaded PDF is smaller than the configured minimum of {minimumSizeKb} KB.";
        }

        if (content.Length < 5
            || content[0] != (byte)'%'
            || content[1] != (byte)'P'
            || content[2] != (byte)'D'
            || content[3] != (byte)'F'
            || content[4] != (byte)'-')
        {
            return "The uploaded file is not a valid PDF.";
        }

        return null;
    }

    public static string NormalizeFileName(string fileName)
    {
        var normalized = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/')).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "manual-upload.pdf";
        }

        return normalized.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{normalized}.pdf";
    }
}
