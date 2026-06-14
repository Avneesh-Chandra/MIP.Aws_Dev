namespace MIP.Aws.Application.Abstractions;

public interface ICurrentUserContext
{
    Guid? UserId { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }

    bool IsInRole(string role);
}
