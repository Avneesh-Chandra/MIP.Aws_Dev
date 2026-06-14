using MediatR;

namespace MIP.Aws.Application.Features.Roles;

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleListItemDto>>;
