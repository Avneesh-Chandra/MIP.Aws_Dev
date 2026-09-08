using System.Security.Claims;
using System.Text.Encodings.Web;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Domain.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MIP.Aws.API.Security;

public sealed class RpaApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<PublisherOutboundOptions> publisherOutbound)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AlAyamExternalRpaRules.ApiKeyHeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var provided = headerValues.ToString();
        var expected = publisherOutbound.Value.AlAyam.IngestApiKey;
        if (!AlAyamExternalRpaRules.ApiKeysMatch(expected, provided))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid RPA ingest API key."));
        }

        var identity = new ClaimsIdentity(Scheme.Name);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "alayam-rpa"));
        identity.AddClaim(new Claim(ClaimTypes.Name, "alayam-rpa"));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
