using MIP.Aws.Application.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Infrastructure.Security;

/// <summary>
/// Stamps enterprise security headers on every response (CSP, HSTS, X-Frame-Options, etc.). The
/// concrete values come from <see cref="SecurityHeaderOptions"/> so they remain configurable per
/// environment without redeploying.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeaderOptions _options;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
    {
        _next = next;
        _options = options.Value.Headers;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_options.Enabled)
        {
            ApplyHeaders(context.Response.Headers);
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Stamps the configured response headers. Exposed so unit tests can verify the policy without
    /// running the full HTTP pipeline.
    /// </summary>
    public void ApplyHeaders(IHeaderDictionary headers)
    {
        Set(headers, "Content-Security-Policy", _options.ContentSecurityPolicy);
        Set(headers, "Referrer-Policy", _options.ReferrerPolicy);
        Set(headers, "Permissions-Policy", _options.PermissionsPolicy);
        Set(headers, "X-Frame-Options", _options.FrameOptions);
        Set(headers, "X-XSS-Protection", _options.XssProtection);
        Set(headers, "X-Content-Type-Options", _options.ContentTypeOptions);
        Set(headers, "Cross-Origin-Opener-Policy", _options.CrossOriginOpenerPolicy);

        if (_options.HstsMaxAgeSeconds > 0)
        {
            Set(headers, "Strict-Transport-Security", $"max-age={_options.HstsMaxAgeSeconds}; includeSubDomains");
        }

        if (_options.RemoveServerHeader)
        {
            headers.Remove("Server");
        }
    }

    private static void Set(IHeaderDictionary headers, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !headers.ContainsKey(name))
        {
            headers[name] = value;
        }
    }
}
