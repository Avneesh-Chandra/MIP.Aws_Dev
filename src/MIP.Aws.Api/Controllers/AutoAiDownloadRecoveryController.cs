using MIP.Aws.Application.Features.AutoAiRecovery;
using MIP.Aws.Domain.Security;
using MIP.Aws.Shared.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class AutoAiDownloadRecoveryController(IMediator mediator) : ControllerBase
{
    [HttpPost("operator/download-jobs/{id:guid}/auto-ai-recover")]
    [Authorize(Policy = AuthPolicies.OperatorDownloadMonitor)]
    [ProducesResponseType(typeof(ApiResponse<AutoAiRecoveryResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AutoAiRecoveryResultDto>>> TriggerAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorUserId();
        var result = await mediator.Send(new TriggerAutoAiRecoveryCommand(id, actorId), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<AutoAiRecoveryResultDto>.Ok(result));
    }

    [HttpGet("operator/download-jobs/{id:guid}/auto-ai-recovery-status")]
    [Authorize(Policy = AuthPolicies.OperatorDownloadMonitor)]
    [ProducesResponseType(typeof(ApiResponse<AutoAiRecoveryStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AutoAiRecoveryStatusDto>>> GetStatusAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAutoAiRecoveryStatusQuery(id), cancellationToken).ConfigureAwait(false);
        return result is null ? NotFound() : Ok(ApiResponse<AutoAiRecoveryStatusDto>.Ok(result));
    }

    [HttpGet("operator/download-jobs/{id:guid}/auto-ai-recovery-timeline")]
    [Authorize(Policy = AuthPolicies.OperatorDownloadMonitor)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AutoAiRecoveryTimelineStepDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AutoAiRecoveryTimelineStepDto>>>> GetTimelineAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAutoAiRecoveryTimelineQuery(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<AutoAiRecoveryTimelineStepDto>>.Ok(result));
    }

    [HttpGet("admin/auto-ai-recovery/settings")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<AutoAiDownloadRecoverySettingsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AutoAiDownloadRecoverySettingsDto>>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAutoAiDownloadRecoverySettingsQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<AutoAiDownloadRecoverySettingsDto>.Ok(result));
    }

    [HttpPut("admin/auto-ai-recovery/settings")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<AutoAiDownloadRecoverySettingsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AutoAiDownloadRecoverySettingsDto>>> UpdateSettingsAsync(
        [FromBody] AutoAiDownloadRecoverySettingsDto settings,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateAutoAiDownloadRecoverySettingsCommand(settings, GetActorUserId()),
            cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<AutoAiDownloadRecoverySettingsDto>.Ok(result));
    }

    [HttpPost("admin/sources/{id:guid}/auto-ai-recovery/enable")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    public async Task<IActionResult> EnableSourceAsync(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetSourceAutoAiRecoveryCommand(id, true, GetActorUserId()), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("admin/sources/{id:guid}/auto-ai-recovery/disable")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    public async Task<IActionResult> DisableSourceAsync(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetSourceAutoAiRecoveryCommand(id, false, GetActorUserId()), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private Guid GetActorUserId()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
