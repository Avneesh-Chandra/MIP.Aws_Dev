using MIP.Aws.Application.Abstractions.Intelligence;
using MIP.Aws.Domain.Security;
using MIP.Aws.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Controllers;

[ApiController]
[Route("api/v1/admin/ai-provider")]
[Authorize(Policy = AuthPolicies.ContentAdminPolicy)]
public sealed class AiProviderStatusController(IAiProviderFactory providerFactory) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<AiProviderStatusDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<AiProviderStatusDto>> GetStatus()
    {
        return Ok(ApiResponse<AiProviderStatusDto>.Ok(providerFactory.GetStatus()));
    }
}
