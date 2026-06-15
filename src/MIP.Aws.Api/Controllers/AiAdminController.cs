using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Domain.Security;
using MIP.Aws.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Controllers;

[ApiController]
[Route("api/v1/admin/ai")]
public sealed class AiAdminController(
    IAiProviderFactory providerFactory,
    IAiBedrockTestService bedrockTestService) : ControllerBase
{
    [HttpGet("status")]
    [Authorize(Policy = AuthPolicies.ContentAdminPolicy)]
    [ProducesResponseType(typeof(ApiResponse<AiAdminStatusDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<AiAdminStatusDto>> GetStatus()
    {
        return Ok(ApiResponse<AiAdminStatusDto>.Ok(providerFactory.GetAdminStatus()));
    }

    [HttpPost("bedrock/test")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<BedrockTestResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BedrockTestResult>>> TestBedrockAsync(
        [FromBody] BedrockTestRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await bedrockTestService.TestAsync(
            request ?? new BedrockTestRequest("Suggest a safe PDF selector recovery for a failed newspaper download."),
            cancellationToken).ConfigureAwait(false);

        return Ok(ApiResponse<BedrockTestResult>.Ok(result));
    }
}
