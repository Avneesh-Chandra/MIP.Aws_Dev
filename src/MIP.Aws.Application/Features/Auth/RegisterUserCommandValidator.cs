using FluentValidation;

namespace MIP.Aws.Application.Features.Auth;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.RoleName).NotEmpty();
    }
}
