using MIP.Aws.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MIP.Aws.Application.Features.Auth;

public sealed class GetMeQueryHandler(UserManager<ApplicationUser> userManager) : IRequestHandler<GetMeQuery, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        if (user is null || user.IsDeleted)
        {
            throw new UnauthorizedAccessException("User not found.");
        }

        var roles = (IReadOnlyList<string>)(await userManager.GetRolesAsync(user).ConfigureAwait(false)).ToArray();
        return new UserProfileDto(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName ?? string.Empty,
            user.DisplayName,
            roles,
            user.IsActive);
    }
}
