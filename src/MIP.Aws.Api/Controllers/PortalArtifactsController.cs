using MIP.Aws.Application.Abstractions.Storage;
using MIP.Aws.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Controllers;

/// <summary>
/// Streams login/download diagnostic artifacts (screenshots, HTML snapshots) captured by the
/// Playwright portal automation. Restricted to operators with failure-details access.
/// </summary>
[ApiController]
[Route("api/v1/portal-artifacts")]
[Authorize(Policy = AuthPolicies.OperatorFailureDetails)]
public sealed class PortalArtifactsController(IFileStorageService storage) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync([FromQuery] string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest("Missing 'key' query parameter.");
        }

        if (key.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(key))
        {
            return BadRequest("Invalid storage key.");
        }

        var bytes = await storage.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return NotFound();
        }

        var contentType = GuessContentType(key);
        Response.Headers.CacheControl = "no-store";
        return File(bytes, contentType);
    }

    private static string GuessContentType(string key)
    {
        var ext = Path.GetExtension(key).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream"
        };
    }
}
