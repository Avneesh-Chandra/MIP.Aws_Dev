using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Infrastructure.Operator;

namespace MIP.Aws.Tests;

public sealed class DownloadMonitorEditionDateHelperTests
{
    [Theory]
    [InlineData("newspapers/Bahrain---Al-Ayam/2026-06-22/today-edition.pdf", "2026-06-22")]
    [InlineData(@"newspapers\PressReader\2026-06-21\pressreader-edition.pdf", "2026-06-21")]
    [InlineData("newspapers/source/edition.pdf", null)]
    public void TryParseEditionDateFromStoragePath_parses_date_folder(string path, string? expected)
    {
        var result = DownloadMonitorEditionDateHelper.TryParseEditionDateFromStoragePath(path);
        if (expected is null)
        {
            Assert.Null(result);
            return;
        }

        Assert.Equal(DateOnly.Parse(expected), result);
    }

    [Fact]
    public void ResolveLatestPdfEditionDate_prefers_downloaded_pdf_row()
    {
        var monitorDate = new DateOnly(2026, 6, 22);
        var downloadedForDay = new PdfEditionDownload
        {
            EditionDate = monitorDate,
            Status = PdfEditionStatus.Downloaded,
            DownloadedFileId = Guid.NewGuid()
        };

        var result = DownloadMonitorEditionDateHelper.ResolveLatestPdfEditionDate(
            downloadedForDay,
            [downloadedForDay],
            downloadedForDay.DownloadedFileId,
            null,
            new Dictionary<Guid, string>());

        Assert.Equal(monitorDate, result);
    }

    [Fact]
    public void ResolveLatestPdfEditionDate_parses_portal_blob_uri_when_no_pdf_row()
    {
        var jobId = Guid.NewGuid();
        var blobUri = "newspapers/Al-Watan/2026-06-22/pressreader-edition.pdf";

        var result = DownloadMonitorEditionDateHelper.ResolveLatestPdfEditionDate(
            null,
            [],
            Guid.NewGuid(),
            jobId,
            new Dictionary<Guid, string> { [jobId] = blobUri });

        Assert.Equal(new DateOnly(2026, 6, 22), result);
    }

    [Theory]
    [InlineData(true, "2026-06-22", "2026-06-22", true)]
    [InlineData(true, "2026-06-21", "2026-06-22", false)]
    [InlineData(true, null, "2026-06-22", false)]
    [InlineData(false, null, "2026-06-22", true)]
    public void EditionDateMatchesMonitor_evaluates_expected_cases(
        bool hasPdf,
        string? editionDate,
        string monitorDate,
        bool expected)
    {
        DateOnly? edition = editionDate is null ? null : DateOnly.Parse(editionDate);
        var monitor = DateOnly.Parse(monitorDate);

        Assert.Equal(
            expected,
            DownloadMonitorEditionDateHelper.EditionDateMatchesMonitor(monitor, edition, hasPdf));
    }
}
