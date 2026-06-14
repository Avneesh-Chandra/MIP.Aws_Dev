namespace MIP.Aws.Application.Features.NewsSources.PdfSelectorSuggestion;

public static class PdfSelectorSuggestionAuditEvents
{
    public const string Requested = "ai.selector.suggestion.requested";
    public const string Generated = "ai.selector.suggestion.generated";
    public const string Failed = "ai.selector.suggestion.failed";
    public const string Tested = "ai.selector.suggestion.tested";
    public const string Accepted = "ai.selector.suggestion.accepted";
    public const string Rejected = "ai.selector.suggestion.rejected";
}
