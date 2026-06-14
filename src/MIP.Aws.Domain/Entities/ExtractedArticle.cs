using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Domain.Entities.Review;

namespace MIP.Aws.Domain.Entities;

public class ExtractedArticle : AuditableEntity
{
    public Guid? DownloadedFileId { get; set; }

    public DownloadedFile? DownloadedFile { get; set; }

    public ArticleIntelligenceStatus IntelligenceStatus { get; set; } = ArticleIntelligenceStatus.NotApplicable;

    public int? StartPage { get; set; }

    public int? EndPage { get; set; }

    public GfhRelevanceTier GfhRelevanceTier { get; set; } = GfhRelevanceTier.None;

    public double GfhRelevanceScore { get; set; }

    /// <summary>JSON payload describing GFH/subsidiary/person matches.</summary>
    public string? GfhSignalsJson { get; set; }

    public string? GfhContextExplanation { get; set; }

    public MarketImpactEstimate MarketImpactEstimate { get; set; } = MarketImpactEstimate.Unknown;

    public string? ExecutiveBrief { get; set; }

    public int AiProcessingAttempts { get; set; }

    /// <summary>Reason the last AI attempt failed; cleared on success or re-queue.</summary>
    public ArticleAiFailureReason? AiLastFailureReason { get; set; }

    /// <summary>Truncated error detail for analysts (not for automation).</summary>
    public string? AiLastFailureDetail { get; set; }

    public string Headline { get; set; } = string.Empty;

    /// <summary>Canonical article URL for deduplication when available.</summary>
    public string? CanonicalUrl { get; set; }

    /// <summary>SHA-256 fingerprint (canonical URL + normalized headline) for duplicate detection.</summary>
    public string? ContentFingerprint { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? Author { get; set; }

    public string RawContent { get; set; } = string.Empty;

    public string CleanedContent { get; set; } = string.Empty;

    public string? Section { get; set; }

    /// <summary>JSON array of tag strings.</summary>
    public string? TagsJson { get; set; }

    public string Language { get; set; } = "und";

    public string? OcrMetadataJson { get; set; }

    public ICollection<ArticleEntity> Entities { get; set; } = new List<ArticleEntity>();

    public ICollection<AiSummary> AiSummaries { get; set; } = new List<AiSummary>();

    public ICollection<ArticlePage> Pages { get; set; } = new List<ArticlePage>();

    public ICollection<ArticleClassification> Classifications { get; set; } = new List<ArticleClassification>();

    public ICollection<ArticleSentiment> Sentiments { get; set; } = new List<ArticleSentiment>();

    public ICollection<ArticleKeyword> Keywords { get; set; } = new List<ArticleKeyword>();

    // ─────────────── Analyst review workflow ───────────────

    /// <summary>Current state in the human-review workflow (independent of AI pipeline state).</summary>
    public ArticleReviewState ReviewState { get; set; } = ArticleReviewState.New;

    /// <summary>Identity user actively assigned to review this article (null when unassigned).</summary>
    public Guid? AssignedReviewerId { get; set; }

    /// <summary>Server-stamped SLA target — analysts must transition to Approved/Rejected/Escalated before this UTC.</summary>
    public DateTimeOffset? ReviewSlaDueAt { get; set; }

    /// <summary>Last analyst action timestamp (used for SLA dashboards).</summary>
    public DateTimeOffset? LastReviewActionAt { get; set; }

    /// <summary>Analyst-overridden headline (kept separately so original OCR/AI headline remains immutable).</summary>
    public string? AnalystHeadline { get; set; }

    /// <summary>Analyst-edited executive summary (overrides AI summary when present).</summary>
    public string? AnalystSummary { get; set; }

    /// <summary>Analyst-overridden sentiment polarity (string column to keep enum-agnostic).</summary>
    public string? AnalystSentiment { get; set; }

    /// <summary>Analyst-overridden relevance score 0..1; falls back to <see cref="GfhRelevanceScore"/> when null.</summary>
    public double? AnalystRelevanceScore { get; set; }

    /// <summary>Free-form manual tag list (JSON string array).</summary>
    public string? AnalystTagsJson { get; set; }

    public ICollection<ArticleReviewAction> ReviewActions { get; set; } = new List<ArticleReviewAction>();

    public ICollection<ArticleReviewComment> ReviewComments { get; set; } = new List<ArticleReviewComment>();

    public ICollection<ArticleAnnotation> Annotations { get; set; } = new List<ArticleAnnotation>();

    public ICollection<ReviewAssignment> Assignments { get; set; } = new List<ReviewAssignment>();

    public ExecutiveQueueItem? ExecutiveQueueItem { get; set; }
}
