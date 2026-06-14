namespace MIP.Aws.Application.Abstractions.News;

public sealed record PdfDiscoveryPageCapture(
    string PageUrl,
    string PageTitle,
    string SanitizedHtml,
    IReadOnlyList<Intelligence.PdfSelectorCandidateElement> CandidateElements,
    string? HtmlSnapshotPath,
    string? ScreenshotPath);

public interface IPdfDiscoveryPageCaptureService
{
    Task<PdfDiscoveryPageCapture> CaptureAsync(string pageUrl, bool useHeadlessBrowser, CancellationToken cancellationToken);
}

public sealed record PdfSelectorSuggestionTestOutcome(
    bool Passed,
    string? ResolvedUrl,
    string? FailureReason,
    long? SizeBytes,
    string? ContentType);

public interface IPdfSelectorSuggestionTestService
{
    Task<PdfSelectorSuggestionTestOutcome> TestAsync(
        string pageUrl,
        string selector,
        string expectedAction,
        bool useHeadlessBrowser,
        bool requirePdfContent,
        int minimumSizeKb,
        CancellationToken cancellationToken);
}

public interface IPdfSelectorSuggestionService
{
    Task<IReadOnlyList<PdfSelectorSuggestionDto>> RequestSuggestionsAsync(Guid newsSourceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PdfSelectorSuggestionDto>> GetSuggestionsAsync(Guid newsSourceId, CancellationToken cancellationToken);

    Task<PdfSelectorSuggestionTestOutcome> TestSuggestionAsync(Guid newsSourceId, Guid suggestionId, CancellationToken cancellationToken);

    Task AcceptSuggestionAsync(Guid newsSourceId, Guid suggestionId, Guid? reviewerUserId, CancellationToken cancellationToken);

    Task RejectSuggestionAsync(Guid newsSourceId, Guid suggestionId, Guid? reviewerUserId, CancellationToken cancellationToken);
}

public sealed record PdfSelectorSuggestionDto(
    Guid Id,
    string Url,
    string SuggestedSelector,
    string SelectorType,
    string Purpose,
    double Confidence,
    string? Reason,
    string ExpectedAction,
    string Status,
    string? TestFailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);
