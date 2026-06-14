using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Page-level OCR output for auditing and downstream segmentation.
/// </summary>
public class OcrPageResult : AuditableEntity
{
    public Guid OcrProcessingJobId { get; set; }

    public OcrProcessingJob OcrProcessingJob { get; set; } = null!;

    public int PageNumber { get; set; }

    /// <summary>Concatenated line text for the page (UTF-8, Arabic-safe).</summary>
    public string PageText { get; set; } = string.Empty;

    public double? ConfidenceScore { get; set; }

    /// <summary>Optional path to page-level JSON snippet from Document Intelligence.</summary>
    public string? PageJsonRelativePath { get; set; }
}
