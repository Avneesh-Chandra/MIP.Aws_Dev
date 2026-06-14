using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record TestNewsSourcePortalDownloadCommand(Guid NewsSourceId) : IRequest<NewsPortalDownloadTestResultDto>;

public sealed record GetLatestPortalEditionQuery(Guid NewsSourceId) : IRequest<PortalLatestEditionDto?>;

public sealed record GetPortalEditionHistoryQuery(Guid NewsSourceId, int Take = 20)
    : IRequest<IReadOnlyList<PortalEditionHistoryItemDto>>;

public sealed record GetPortalEditionDownloadProgressQuery(
    Guid NewsSourceId,
    DateTimeOffset? Since = null) : IRequest<PortalEditionDownloadProgressDto?>;

public sealed record PortalEditionDownloadProgressDto(
    int Percent,
    string Phase,
    bool IsComplete,
    string? Status,
    string? ErrorMessage);
