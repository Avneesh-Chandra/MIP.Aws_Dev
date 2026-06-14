using MIP.Aws.Application.Abstractions.News;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources.PdfSelectorSuggestion;

public sealed record SuggestPdfSelectorsCommand(Guid NewsSourceId) : IRequest<IReadOnlyList<PdfSelectorSuggestionDto>>;

public sealed record GetPdfSelectorSuggestionsQuery(Guid NewsSourceId) : IRequest<IReadOnlyList<PdfSelectorSuggestionDto>>;

public sealed record TestPdfSelectorSuggestionCommand(Guid NewsSourceId, Guid SuggestionId) : IRequest<PdfSelectorSuggestionTestOutcome>;

public sealed record AcceptPdfSelectorSuggestionCommand(Guid NewsSourceId, Guid SuggestionId) : IRequest;

public sealed record RejectPdfSelectorSuggestionCommand(Guid NewsSourceId, Guid SuggestionId) : IRequest;
