using MediatR;

namespace MIP.Aws.Application.Features.Auth;

public sealed record GetMeQuery(Guid UserId) : IRequest<UserProfileDto>;
