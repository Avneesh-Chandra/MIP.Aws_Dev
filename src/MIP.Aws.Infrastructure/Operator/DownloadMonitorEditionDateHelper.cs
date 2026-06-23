using System.Globalization;
using System.Text.RegularExpressions;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Operator;

public static partial class DownloadMonitorEditionDateHelper
{
    [GeneratedRegex(@"/(\d{4}-\d{2}-\d{2})(?:/|$)", RegexOptions.CultureInvariant)]
    private static partial Regex StoragePathDateRegex();

    public static DateOnly? TryParseEditionDateFromStoragePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var match = StoragePathDateRegex().Match(path.Replace('\\', '/'));
        if (!match.Success)
        {
            return null;
        }

        return DateOnly.TryParseExact(
            match.Groups[1].Value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var editionDate)
            ? editionDate
            : null;
    }

    public static DateOnly? ResolveLatestPdfEditionDate(
        PdfEditionDownload? downloadedForDay,
        IReadOnlyList<PdfEditionDownload> sourcePdfs,
        Guid? latestFileId,
        Guid? latestJobId,
        IReadOnlyDictionary<Guid, string> blobUriByJobId)
    {
        if (downloadedForDay is { Status: PdfEditionStatus.Downloaded or PdfEditionStatus.Validated or PdfEditionStatus.SkippedDuplicate })
        {
            return downloadedForDay.EditionDate;
        }

        if (latestFileId is Guid fileId)
        {
            var pdfByFile = sourcePdfs.FirstOrDefault(p =>
                p.DownloadedFileId == fileId
                && p.Status is PdfEditionStatus.Downloaded or PdfEditionStatus.Validated or PdfEditionStatus.SkippedDuplicate);
            if (pdfByFile is not null)
            {
                return pdfByFile.EditionDate;
            }
        }

        if (latestJobId is Guid jobId && blobUriByJobId.TryGetValue(jobId, out var blobUri))
        {
            return TryParseEditionDateFromStoragePath(blobUri);
        }

        var latestSuccessfulPdf = sourcePdfs.FirstOrDefault(p =>
            p.Status is PdfEditionStatus.Downloaded or PdfEditionStatus.Validated or PdfEditionStatus.SkippedDuplicate);
        if (latestSuccessfulPdf is not null)
        {
            return latestSuccessfulPdf.EditionDate;
        }

        return null;
    }

    public static bool EditionDateMatchesMonitor(DateOnly monitorDate, DateOnly? editionDate, bool hasPdf) =>
        !hasPdf || (editionDate is not null && editionDate == monitorDate);

    public static (string Issue, string Action) BuildMismatchAttention(DateOnly monitorDate, DateOnly? editionDate)
    {
        if (editionDate is null)
        {
            return (
                "PDF is available but the edition date could not be verified against the monitor date.",
                "Open the PDF and confirm it is today's newspaper; re-run download if needed.");
        }

        return (
            $"PDF edition date is {editionDate:yyyy-MM-dd}, not monitor date {monitorDate:yyyy-MM-dd}.",
            "Verify the downloaded file is today's newspaper; re-run download or supply a manual URL.");
    }
}
