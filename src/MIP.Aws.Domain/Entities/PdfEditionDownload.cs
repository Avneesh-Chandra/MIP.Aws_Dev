using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Audit trail and metadata for a publicly discovered newspaper PDF edition.
/// </summary>
public class PdfEditionDownload : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource? NewsSource { get; set; }

    public Guid? DownloadJobId { get; set; }

    public DownloadJob? DownloadJob { get; set; }

    public Guid? DownloadedFileId { get; set; }

    public DownloadedFile? DownloadedFile { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string? SavedPath { get; set; }

    public string FileName { get; set; } = "today-edition.pdf";

    public long? FileSizeBytes { get; set; }

    public string? Sha256Hash { get; set; }

    public string ContentType { get; set; } = "application/pdf";

    public DateOnly EditionDate { get; set; }

    public double DiscoveryConfidence { get; set; }

    public PdfDiscoveryMethod DiscoveryMethod { get; set; }

    public PdfEditionStatus Status { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset? DiscoveredAt { get; set; }

    public DateTimeOffset? DownloadedAt { get; set; }
}
