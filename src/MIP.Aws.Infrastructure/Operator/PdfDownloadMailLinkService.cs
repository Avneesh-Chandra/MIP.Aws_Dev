using System.Text.Json;
using MIP.Aws.Application.Abstractions.Operator;
using Microsoft.AspNetCore.DataProtection;

namespace MIP.Aws.Infrastructure.Operator;

public sealed class PdfDownloadMailLinkService : IPdfDownloadMailLinkService
{
    private const string ProtectorPurpose = "MIP.Aws.PdfDownloadMail.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ITimeLimitedDataProtector protector;

    public PdfDownloadMailLinkService(IDataProtectionProvider provider)
    {
        protector = provider
            .CreateProtector(ProtectorPurpose)
            .ToTimeLimitedDataProtector();
    }

    public string CreateToken(
        IReadOnlyList<PdfDownloadMailLinkFile> files,
        DateTimeOffset expiresAt)
    {
        if (files is not { Count: > 0 and <= 100 })
        {
            throw new ArgumentException("At least one PDF is required.", nameof(files));
        }

        var lifetime = expiresAt - DateTimeOffset.UtcNow;
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "The link expiry must be in the future.");
        }

        var normalized = files
            .Select(file => new PdfDownloadMailLinkFile(
                file.SourceId,
                file.FileId,
                string.IsNullOrWhiteSpace(file.FileName) ? "today-edition.pdf" : file.FileName.Trim()))
            .ToList();
        var json = JsonSerializer.Serialize(new PdfDownloadMailLinkPayload(normalized), JsonOptions);
        return protector.Protect(json, lifetime);
    }

    public bool TryReadToken(
        string token,
        out PdfDownloadMailLinkPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var json = protector.Unprotect(token.Trim(), out _);
            var candidate = JsonSerializer.Deserialize<PdfDownloadMailLinkPayload>(json, JsonOptions);
            if (candidate?.Files is not { Count: > 0 and <= 100 }
                || candidate.Files.Any(file => file.SourceId == Guid.Empty || file.FileId == Guid.Empty)
                || candidate.Files.Select(file => file.FileId).Distinct().Count() != candidate.Files.Count)
            {
                return false;
            }

            payload = candidate;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
