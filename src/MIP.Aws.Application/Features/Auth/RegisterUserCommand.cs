using MediatR;

namespace MIP.Aws.Application.Features.Auth;

public sealed record RegisterUserCommand(string Email, string Password, string DisplayName, string RoleName) : IRequest<Guid>;
