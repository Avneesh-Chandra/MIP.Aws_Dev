namespace MIP.Aws.Domain.Enums;

/// <summary>
/// Workflow states for the analyst review pipeline. Stored as an int column on
/// <c>ExtractedArticle.ReviewState</c>.
/// </summary>
public enum ArticleReviewState
{
    /// <summary>Article extracted; AI has not yet run.</summary>
    New = 0,

    /// <summary>AI pipeline finished; awaiting human analyst pickup.</summary>
    AiProcessed = 1,

    /// <summary>An analyst has claimed the article and is actively editing.</summary>
    UnderReview = 2,

    /// <summary>Analyst approved the article; eligible for downstream distribution.</summary>
    Approved = 3,

    /// <summary>Analyst rejected the article (off-topic, low quality, duplicate).</summary>
    Rejected = 4,

    /// <summary>Marked high priority and routed to the executive queue.</summary>
    Escalated = 5,

    /// <summary>Locked into the most recent executive brief.</summary>
    PublishedToExecutive = 6,

    /// <summary>Closed out — historical, no further action.</summary>
    Archived = 7
}
