namespace MIP.Aws.Application.Abstractions.Security;

/// <summary>
/// Protects optional basic-auth style credentials at rest using host data protection.
/// </summary>
public interface INewsCredentialProtector
{
    string Protect(string username, string password);

    (string Username, string Password)? Unprotect(string? payload);
}
