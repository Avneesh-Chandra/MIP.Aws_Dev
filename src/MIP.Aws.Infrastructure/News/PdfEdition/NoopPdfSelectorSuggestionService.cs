using MIP.Aws.Application.Abstractions.News;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

public sealed class NoopPdfSelectorSuggestionService : IPdfSelectorSuggestionService
{
    public Task<IReadOnlyList<PdfSelectorSuggestionDto>> RequestSuggestionsAsync(Guid newsSourceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PdfSelectorSuggestionDto>>([]);

    public Task<IReadOnlyList<PdfSelectorSuggestionDto>> GetSuggestionsAsync(Guid newsSourceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PdfSelectorSuggestionDto>>([]);

    public Task<PdfSelectorSuggestionTestOutcome> TestSuggestionAsync(Guid newsSourceId, Guid suggestionId, CancellationToken cancellationToken) =>
        Task.FromResult(new PdfSelectorSuggestionTestOutcome(false, null, "AI selector suggestions are disabled.", null, null));

    public Task AcceptSuggestionAsync(Guid newsSourceId, Guid suggestionId, Guid? reviewerUserId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task RejectSuggestionAsync(Guid newsSourceId, Guid suggestionId, Guid? reviewerUserId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
