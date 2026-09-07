namespace MIP.Aws.Application.Abstractions.Reporting;

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content,
    string? ContentId = null,
    bool IsInline = false);
