using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.News;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources.PdfSelectorSuggestion;

public sealed class SuggestPdfSelectorsCommandHandler(IPdfSelectorSuggestionService service)
    : IRequestHandler<SuggestPdfSelectorsCommand, IReadOnlyList<PdfSelectorSuggestionDto>>
{
    public Task<IReadOnlyList<PdfSelectorSuggestionDto>> Handle(SuggestPdfSelectorsCommand request, CancellationToken cancellationToken) =>
        service.RequestSuggestionsAsync(request.NewsSourceId, cancellationToken);
}

public sealed class GetPdfSelectorSuggestionsQueryHandler(IPdfSelectorSuggestionService service)
    : IRequestHandler<GetPdfSelectorSuggestionsQuery, IReadOnlyList<PdfSelectorSuggestionDto>>
{
    public Task<IReadOnlyList<PdfSelectorSuggestionDto>> Handle(GetPdfSelectorSuggestionsQuery request, CancellationToken cancellationToken) =>
        service.GetSuggestionsAsync(request.NewsSourceId, cancellationToken);
}

public sealed class TestPdfSelectorSuggestionCommandHandler(IPdfSelectorSuggestionService service)
    : IRequestHandler<TestPdfSelectorSuggestionCommand, PdfSelectorSuggestionTestOutcome>
{
    public Task<PdfSelectorSuggestionTestOutcome> Handle(TestPdfSelectorSuggestionCommand request, CancellationToken cancellationToken) =>
        service.TestSuggestionAsync(request.NewsSourceId, request.SuggestionId, cancellationToken);
}

public sealed class AcceptPdfSelectorSuggestionCommandHandler(
    IPdfSelectorSuggestionService service,
    ICurrentUserContext currentUser)
    : IRequestHandler<AcceptPdfSelectorSuggestionCommand>
{
    public Task Handle(AcceptPdfSelectorSuggestionCommand request, CancellationToken cancellationToken) =>
        service.AcceptSuggestionAsync(request.NewsSourceId, request.SuggestionId, currentUser.UserId, cancellationToken);
}

public sealed class RejectPdfSelectorSuggestionCommandHandler(
    IPdfSelectorSuggestionService service,
    ICurrentUserContext currentUser)
    : IRequestHandler<RejectPdfSelectorSuggestionCommand>
{
    public Task Handle(RejectPdfSelectorSuggestionCommand request, CancellationToken cancellationToken) =>
        service.RejectSuggestionAsync(request.NewsSourceId, request.SuggestionId, currentUser.UserId, cancellationToken);
}
