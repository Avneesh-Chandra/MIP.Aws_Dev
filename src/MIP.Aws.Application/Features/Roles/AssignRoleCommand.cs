using MediatR;

namespace MIP.Aws.Application.Features.Roles;

public sealed record AssignRoleCommand(Guid UserId, string RoleName) : IRequest<Unit>;
