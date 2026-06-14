using MIP.Aws.Application.Features.SourceRecovery;
using MIP.Aws.Domain.Security;
using MIP.Aws.Shared.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Controllers;

[ApiController]
[Route("api/v1/source-recovery")]
[Authorize(Policy = AuthPolicies.SourceRecoveryView)]
public sealed class SourceRecoveryController(IMediator mediator) : ControllerBase
{
    [HttpPost("analyze/{downloadJobId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SourceRecoveryAnalysisDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SourceRecoveryAnalysisDto>>> AnalyzeAsync(
        Guid downloadJobId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AnalyzeSourceRecoveryCommand(downloadJobId), cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApiResponse<SourceRecoveryAnalysisDto>.Ok(result));
    }

    [HttpGet("attempts/{attemptId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SourceRecoveryAnalysisDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SourceRecoveryAnalysisDto>>> GetAttemptAsync(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSourceRecoveryAnalysisQuery(attemptId), cancellationToken)
            .ConfigureAwait(false);
        return result is null ? NotFound() : Ok(ApiResponse<SourceRecoveryAnalysisDto>.Ok(result));
    }

    [HttpGet("recovery-center")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SourceRecoveryCenterItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SourceRecoveryCenterItemDto>>>> GetRecoveryCenterAsync(
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRecoveryCenterFailuresQuery(date), cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<SourceRecoveryCenterItemDto>>.Ok(result));
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SourceRecoveryHistoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SourceRecoveryHistoryItemDto>>>> GetHistoryAsync(
        [FromQuery] int take = 50,
        [FromQuery] DateOnly? monitorDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetSourceRecoveryHistoryQuery(take, monitorDate), cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<SourceRecoveryHistoryItemDto>>.Ok(result));
    }

    [HttpPost("attempts/{attemptId:guid}/options/{optionIndex:int}/preview")]
    [Authorize(Policy = AuthPolicies.SourceRecoveryApply)]
    [ProducesResponseType(typeof(ApiResponse<SourceRecoveryPreviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SourceRecoveryPreviewDto>>> PreviewAsync(
        Guid attemptId,
        int optionIndex,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PreviewSourceRecoveryCommand(attemptId, optionIndex), cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApiResponse<SourceRecoveryPreviewDto>.Ok(result));
    }

    [HttpPost("attempts/{attemptId:guid}/options/{optionIndex:int}/apply")]
    [Authorize(Policy = AuthPolicies.SourceRecoveryApply)]
    [ProducesResponseType(typeof(ApiResponse<SourceRecoveryApplyResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SourceRecoveryApplyResultDto>>> ApplyAsync(
        Guid attemptId,
        int optionIndex,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ApplySourceRecoveryCommand(attemptId, optionIndex), cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApiResponse<SourceRecoveryApplyResultDto>.Ok(result));
    }

    [HttpPost("attempts/{attemptId:guid}/finalize")]
    [Authorize(Policy = AuthPolicies.SourceRecoveryApply)]
    [ProducesResponseType(typeof(ApiResponse<SourceRecoveryApplyResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SourceRecoveryApplyResultDto>>> FinalizeAsync(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new FinalizeSourceRecoveryAttemptCommand(attemptId), cancellationToken)
            .ConfigureAwait(false);
        return Ok(ApiResponse<SourceRecoveryApplyResultDto>.Ok(result));
    }

    [HttpPost("attempts/{attemptId:guid}/rollback")]
    [Authorize(Policy = AuthPolicies.SourceRecoveryAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RollbackAsync(
        Guid attemptId,
        [FromBody] RollbackSourceRecoveryRequest body,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new RollbackSourceRecoveryCommand(attemptId, body.Reason ?? "Manual rollback"),
            cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}

public sealed record RollbackSourceRecoveryRequest(string? Reason);
