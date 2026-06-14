using FluentValidation;

namespace MIP.Aws.Application.Features.Roles;

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(256);
    }
}
