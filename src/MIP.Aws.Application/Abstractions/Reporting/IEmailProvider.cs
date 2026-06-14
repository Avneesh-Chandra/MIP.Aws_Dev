using MIP.Aws.Application.Configuration;

namespace MIP.Aws.Application.Abstractions.Reporting;

/// <summary>Low-level email transport for a single mail provider.</summary>
public interface IEmailProvider
{
    MailActiveProvider ProviderType { get; }

    bool IsConfigured { get; }

    string ProviderName { get; }

    string? ResolvedFromAddress { get; }

    Task<EmailProviderSendResult> SendAsync(EmailProviderSendRequest request, CancellationToken cancellationToken);
}

public interface IEmailProviderFactory
{
    IEmailProvider GetProvider(MailActiveProvider provider);

    IEmailProvider GetActiveProvider(MailActiveProvider activeProvider);
}

public sealed record EmailProviderSendRequest(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc,
    string Subject,
    string HtmlBody,
    IReadOnlyList<EmailAttachment> Attachments);

public sealed record EmailProviderSendResult(
    bool Success,
    string FromAddress,
    string? MessageId,
    string? OperationId,
    string? ErrorMessage);
