using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MIP.Aws.Application.Abstractions.Reporting;
using MIP.Aws.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Reporting;

/// <summary>
/// Sends mail via Microsoft Graph <c>/users/{mailbox}/sendMail</c> using client-credentials OAuth.
/// </summary>
public sealed class MicrosoftGraphEmailSender : IReportEmailTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MicrosoftGraphOptions _graph;
    private readonly EmailOptions _email;
    private readonly ILogger<MicrosoftGraphEmailSender> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public MicrosoftGraphEmailSender(
        IHttpClientFactory httpClientFactory,
        IOptions<MicrosoftGraphOptions> graphOptions,
        IOptions<EmailOptions> emailOptions,
        ILogger<MicrosoftGraphEmailSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _graph = graphOptions.Value;
        _email = emailOptions.Value;
        _logger = logger;
    }

    public async Task<ReportEmailSendResult> SendAsync(ReportEmailMessage message, CancellationToken cancellationToken)
    {
        var from = ResolveFromEmail();
        if (!_graph.IsConfigured || string.IsNullOrWhiteSpace(from))
        {
            return Failed(from, message.To, EmailSendOutcome.SkippedConfigurationMissing,
                "Microsoft Graph is not configured. Set Email:Provider=MicrosoftGraph and MicrosoftGraph:Enabled=true with TenantId, ClientId, ClientSecret, and SenderMailbox.");
        }

        if (message.To.Count == 0)
        {
            return Failed(from, message.To, EmailSendOutcome.SkippedNoRecipients, "No recipients.");
        }

        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var client = _httpClientFactory.CreateClient("MicrosoftGraphMail");

            var mailbox = Uri.EscapeDataString(_graph.SenderMailbox.Trim());
            var payload = BuildPayload(message);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"users/{mailbox}/sendMail")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                // Graph sendMail returns 202 Accepted with empty body (no message id).
                _logger.LogInformation(
                    "Microsoft Graph email sent from {From} to {To} | {Subject}",
                    from, string.Join(',', message.To), message.Subject);

                return new ReportEmailSendResult(
                    true,
                    "microsoft-graph",
                    from,
                    message.To,
                    [],
                    null,
                    null,
                    null,
                    EmailSendOutcome.Sent,
                    DateTimeOffset.UtcNow);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var error = $"Graph sendMail failed ({(int)response.StatusCode}): {Truncate(body, 800)}";
            _logger.LogWarning("Microsoft Graph send failed for {To}: {Error}", string.Join(',', message.To), error);
            return Failed(from, message.To, EmailSendOutcome.Failed, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Microsoft Graph send failed for {To}", string.Join(',', message.To));
            return Failed(from, message.To, EmailSendOutcome.Failed, ex.Message);
        }
    }

    private string ResolveFromEmail()
    {
        if (!string.IsNullOrWhiteSpace(_email.FromEmail))
        {
            return _email.FromEmail.Trim();
        }

        return _graph.SenderMailbox?.Trim() ?? string.Empty;
    }

    private object BuildPayload(ReportEmailMessage message)
    {
        var attachments = message.Attachments.Count == 0
            ? null
            : message.Attachments.Select(a =>
            {
                var item = new Dictionary<string, object?>
                {
                    ["@odata.type"] = "#microsoft.graph.fileAttachment",
                    ["name"] = a.FileName,
                    ["contentType"] = string.IsNullOrWhiteSpace(a.ContentType) ? "application/octet-stream" : a.ContentType,
                    ["contentBytes"] = Convert.ToBase64String(a.Content)
                };
                if (a.IsInline && !string.IsNullOrWhiteSpace(a.ContentId))
                {
                    item["isInline"] = true;
                    item["contentId"] = a.ContentId;
                }

                return item;
            }).ToList();

        return new
        {
            message = new
            {
                subject = message.Subject,
                body = new
                {
                    contentType = "HTML",
                    content = message.HtmlBody ?? string.Empty
                },
                toRecipients = message.To.Select(ToRecipient).ToList(),
                ccRecipients = message.Cc is { Count: > 0 } ? message.Cc.Select(ToRecipient).ToList() : null,
                bccRecipients = message.Bcc is { Count: > 0 } ? message.Bcc.Select(ToRecipient).ToList() : null,
                attachments
            },
            saveToSentItems = _graph.SaveToSentItems
        };
    }

    private static object ToRecipient(string address) =>
        new { emailAddress = new { address } };

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _accessToken;
            }

            var client = _httpClientFactory.CreateClient("MicrosoftGraphToken");
            var tokenUrl = $"https://login.microsoftonline.com/{_graph.TenantId.Trim()}/oauth2/v2.0/token";
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _graph.ClientId.Trim(),
                ["client_secret"] = _graph.ClientSecret,
                ["scope"] = "https://graph.microsoft.com/.default"
            });

            using var response = await client.PostAsync(tokenUrl, form, cancellationToken).ConfigureAwait(false);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Microsoft Graph token request failed ({(int)response.StatusCode}): {Truncate(raw, 500)}");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var token = root.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Microsoft Graph token response missing access_token.");
            var expiresIn = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds)
                ? seconds
                : 3600;

            _accessToken = token;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";

    private static ReportEmailSendResult Failed(
        string from,
        IReadOnlyList<string> to,
        EmailSendOutcome outcome,
        string? error) =>
        new(false, "microsoft-graph", from, to, [], null, null, error, outcome);
}
