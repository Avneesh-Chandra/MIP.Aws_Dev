using System.Text.Json;
using MIP.Aws.Application.Abstractions.Security;
using Microsoft.AspNetCore.DataProtection;

namespace MIP.Aws.Infrastructure.Security;

public sealed class NewsCredentialProtector(IDataProtectionProvider provider) : INewsCredentialProtector
{
    private const string Purpose = "MIP.Aws.NewsSource.BasicAuth.v1";

    public string Protect(string username, string password)
    {
        var protector = provider.CreateProtector(Purpose);
        var payload = JsonSerializer.Serialize(new CredentialPayload(username, password));
        return protector.Protect(payload);
    }

    public (string Username, string Password)? Unprotect(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var protector = provider.CreateProtector(Purpose);
            var json = protector.Unprotect(payload);
            var model = JsonSerializer.Deserialize<CredentialPayload>(json);
            return model is null ? null : (model.Username, model.Password);
        }
        catch
        {
            return null;
        }
    }

    private sealed record CredentialPayload(string Username, string Password);
}
