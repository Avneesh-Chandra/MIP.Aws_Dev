using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Tracks Azure Document Intelligence OCR for a single downloaded artifact (licensed content only).
/// </summary>
public class OcrProcessingJob : AuditableEntity
{
    public Guid DownloadedFileId { get; set; }

    public DownloadedFile DownloadedFile { get; set; } = null!;

    public OcrJobStatus Status { get; set; } = OcrJobStatus.Pending;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    /// <summary>Aggregate OCR / layout JSON relative path under the configured OCR storage folder.</summary>
    public string? ResultManifestRelativePath { get; set; }

    public double? AveragePageConfidence { get; set; }

    public string? CorrelationId { get; set; }

    public ICollection<OcrPageResult> Pages { get; set; } = new List<OcrPageResult>();
}
