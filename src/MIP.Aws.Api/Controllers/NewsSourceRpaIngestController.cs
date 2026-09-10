using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Features.NewsSources.PdfEdition;
using MIP.Aws.Domain.Security;
using MIP.Aws.Shared.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MIP.Aws.API.Controllers;

/// <summary>
/// Machine ingest for Al Ayam RPA. Operators continue to use the Upload PDF page.
/// </summary>
[ApiController]
[Route("api/v1/news-sources")]
[Authorize(Policy = AuthPolicies.RpaPdfIngest)]
[IgnoreAntiforgeryToken]
public sealed class NewsSourceRpaIngestController(
    IMediator mediator,
    IOptions<PublisherOutboundOptions> publisherOutbound) : ControllerBase
{
    [HttpPost("{id:guid}/upload-today-pdf")]
    [RequestSizeLimit(PdfManualUploadRules.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PdfManualUploadRules.MaxUploadBytes)]
    [ProducesResponseType(typeof(ApiResponse<PdfEditionDownloadOutcome>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PdfEditionDownloadOutcome>>> UploadTodayPdfAsync(
        Guid id,
        [FromForm(Name = "file")] IFormFile? file,
        [FromQuery] bool enqueueOcr = true,
        CancellationToken cancellationToken = default)
    {
        var alAyam = publisherOutbound.Value.AlAyam;
        if (!AlAyamExternalRpaRules.IsAllowedSource(id, alAyam.IngestAllowedSourceId))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<PdfEditionDownloadOutcome>.Fail("RPA ingest is not allowed for this news source."));
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse<PdfEditionDownloadOutcome>.Fail("A PDF file is required."));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);

            var result = await mediator.Send(
                    new UploadTodayPdfCommand(id, file.FileName, memory.ToArray(), enqueueOcr, FromExternalRpa: true),
                    cancellationToken)
                .ConfigureAwait(false);

            return Ok(ApiResponse<PdfEditionDownloadOutcome>.Ok(result, "PDF edition uploaded"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PdfEditionDownloadOutcome>.Fail(ex.Message));
        }
    }
}
