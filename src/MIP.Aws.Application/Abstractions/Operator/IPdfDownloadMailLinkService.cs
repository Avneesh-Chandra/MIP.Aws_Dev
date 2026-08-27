namespace MIP.Aws.Application.Abstractions.Operator;

public sealed record PdfDownloadMailLinkFile(
    Guid SourceId,
    Guid FileId,
    string FileName);

public sealed record PdfDownloadMailLinkPayload(
    IReadOnlyList<PdfDownloadMailLinkFile> Files);

public interface IPdfDownloadMailLinkService
{
    string CreateToken(
        IReadOnlyList<PdfDownloadMailLinkFile> files,
        DateTimeOffset expiresAt);

    bool TryReadToken(
        string token,
        out PdfDownloadMailLinkPayload? payload);
}
