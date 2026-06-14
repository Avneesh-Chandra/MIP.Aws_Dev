using System.Security.Claims;
using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Application.Features.NewsSources.PdfEdition;
using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Domain.Security;
using MIP.Aws.Shared.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Controllers;

[ApiController]
[Route("api/v1/operator")]
[Authorize(Policy = AuthPolicies.OperatorDownloadMonitor)]
public sealed class OperatorDownloadMonitorController(IMediator mediator) : ControllerBase
{
    [HttpGet("download-monitor")]
    [ProducesResponseType(typeof(ApiResponse<DownloadMonitorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DownloadMonitorDto>>> GetMonitorAsync(
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetDownloadMonitorQuery(date), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<DownloadMonitorDto>.Ok(result));
    }

    [HttpPost("download-monitor/execute-batch")]
    [ProducesResponseType(typeof(ApiResponse<DownloadMonitorBatchRunResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DownloadMonitorBatchRunResult>>> ExecuteBatchAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new ExecuteDownloadMonitorBatchCommand(GetActorUserId()),
                cancellationToken).ConfigureAwait(false);
            return Ok(ApiResponse<DownloadMonitorBatchRunResult>.Ok(result, "PDF download batch started."));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<DownloadMonitorBatchRunResult>.Fail(ex.Message));
        }
    }

    [HttpGet("download-monitor/batch-progress")]
    [ProducesResponseType(typeof(ApiResponse<DownloadMonitorBatchProgressResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DownloadMonitorBatchProgressResult>>> GetBatchProgressAsync(
        [FromQuery] DateTimeOffset? batchStartedAt,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetDownloadMonitorBatchProgressQuery(batchStartedAt),
            cancellationToken).ConfigureAwait(false);
        return result is null
            ? NotFound(ApiResponse<DownloadMonitorBatchProgressResult>.Fail("No active PDF download batch."))
            : Ok(ApiResponse<DownloadMonitorBatchProgressResult>.Ok(result));
    }

    [HttpGet("sources/{id:guid}/download-status")]
    [ProducesResponseType(typeof(ApiResponse<SourceDownloadStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SourceDownloadStatusDto>>> GetSourceStatusAsync(
        Guid id,
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSourceDownloadStatusQuery(id, date), cancellationToken).ConfigureAwait(false);
        return result is null ? NotFound() : Ok(ApiResponse<SourceDownloadStatusDto>.Ok(result));
    }

    [HttpGet("sources/{id:guid}/latest-pdf")]
    [Authorize(Policy = AuthPolicies.OperatorLatestPdf)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestPdfAsync(
        Guid id,
        [FromQuery] bool inline = false,
        CancellationToken cancellationToken = default)
    {
        var link = await mediator.Send(new GetOperatorLatestPdfQuery(id), cancellationToken).ConfigureAwait(false);
        if (link?.Available != true || link.FileId is null)
        {
            return NotFound();
        }

        var stream = await mediator.Send(new StreamPdfEditionQuery(id, link.FileId.Value, inline), cancellationToken)
            .ConfigureAwait(false);
        if (stream is null)
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers.ContentDisposition = inline
            ? $"inline; filename=\"{stream.FileName}\""
            : $"attachment; filename=\"{stream.FileName}\"";
        return File(stream.Content, stream.ContentType);
    }

    [HttpGet("download-jobs/{jobId:guid}/failure-details")]
    [Authorize(Policy = AuthPolicies.OperatorFailureDetails)]
    [ProducesResponseType(typeof(ApiResponse<DownloadFailureDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DownloadFailureDetailsDto>>> GetFailureDetailsAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetDownloadFailureDetailsQuery(jobId), cancellationToken).ConfigureAwait(false);
        return result is null ? NotFound() : Ok(ApiResponse<DownloadFailureDetailsDto>.Ok(result));
    }

    [HttpGet("download-jobs/{jobId:guid}/ai-recovery-details")]
    [Authorize(Policy = AuthPolicies.SourceRecoveryView)]
    [ProducesResponseType(typeof(ApiResponse<AiRecoverySuccessDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AiRecoverySuccessDetailsDto>>> GetAiRecoveryDetailsAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAiRecoverySuccessDetailsQuery(jobId), cancellationToken)
            .ConfigureAwait(false);
        return result is null ? NotFound() : Ok(ApiResponse<AiRecoverySuccessDetailsDto>.Ok(result));
    }

    [HttpPost("download-jobs/{jobId:guid}/notes")]
    [Authorize(Policy = AuthPolicies.OperatorNotes)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Guid>>> AddNoteAsync(
        Guid jobId,
        [FromBody] OperatorNoteRequest request,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new AddDownloadOperatorNoteCommand(jobId, request.Note), cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApiResponse<Guid>.Ok(id, "Operator note saved."));
    }

    [HttpPost("download-jobs/{jobId:guid}/inform-admin")]
    [Authorize(Policy = AuthPolicies.OperatorInformAdmin)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Guid>>> InformAdminAsync(
        Guid jobId,
        [FromBody] InformAdminRequest? request,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new InformAdminCommand(jobId, request?.OperatorNote), cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApiResponse<Guid>.Ok(id, "Admin informed."));
    }

    public sealed record OperatorNoteRequest(string Note);

    public sealed record InformAdminRequest(string? OperatorNote);

    private Guid GetActorUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId) ? userId : Guid.Empty;
    }
}
