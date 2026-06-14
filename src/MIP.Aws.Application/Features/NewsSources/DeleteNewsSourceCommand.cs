using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record DeleteNewsSourceCommand(Guid Id) : IRequest<Unit>;
