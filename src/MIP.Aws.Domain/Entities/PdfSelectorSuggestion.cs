using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// AI-advisory CSS selector suggestion captured after PDF discovery failure for admin review.
/// </summary>
public class PdfSelectorSuggestion : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource? NewsSource { get; set; }

    public string Url { get; set; } = string.Empty;

    public string? HtmlSnapshotPath { get; set; }

    public string? ScreenshotPath { get; set; }

    public string SuggestedSelector { get; set; } = string.Empty;

    public PdfSelectorType SelectorType { get; set; } = PdfSelectorType.Css;

    public PdfSelectorPurpose Purpose { get; set; }

    public double Confidence { get; set; }

    public string? Reason { get; set; }

    public PdfSelectorExpectedAction ExpectedAction { get; set; }

    public PdfSelectorSuggestionStatus Status { get; set; } = PdfSelectorSuggestionStatus.Suggested;

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? TestFailureReason { get; set; }
}
