using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Features.NewsSources.PdfEdition;
using MIP.Aws.Application.Features.NewsSources.PdfSelectorSuggestion;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Domain.Security;
using MIP.Aws.Shared.Paging;
using MIP.Aws.Shared.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Controllers;

/// <summary>
/// Newspaper source catalog, licensed portal automation tests, validation, and download orchestration.
/// </summary>
[ApiController]
[Route("api/v1/news-sources")]
[Authorize(Policy = AuthPolicies.SourceManagementPolicy)]
public sealed class NewsSourcesController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Lists configured newspaper sources with pagination and optional text search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NewsSourceListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<NewsSourceListItemDto>>>> ListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetNewsSourcesQuery(page, pageSize, search), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<PagedResult<NewsSourceListItemDto>>.Ok(result, "News sources retrieved"));
    }

    /// <summary>
    /// Gets a single news source with schedule and portal automation metadata (password is never returned).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<NewsSourceDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NewsSourceDetailDto>>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var dto = await mediator.Send(new GetNewsSourceByIdQuery(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<NewsSourceDetailDto>.Ok(dto, "News source retrieved"));
    }

    /// <summary>
    /// Creates a news source with optional schedule and encrypted portal credentials.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateAsync(
        [FromBody] CreateNewsSourceRequest body,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(
            new CreateNewsSourceCommand(
                body.Name,
                body.BaseUrl,
                body.SourceType,
                body.AcquisitionMode,
                body.SourceAccessMode,
                body.RequiresLogin,
                body.LoginUrl,
                body.EditionUrl,
                body.LogoutUrl,
                body.PortalUsername,
                body.LoginMethod,
                body.UsernameSelector,
                body.PasswordSelector,
                body.SubmitSelector,
                body.DownloadSelector,
                body.LoginSuccessSelector,
                body.SuccessUrlPattern,
                body.RequiresCaptcha,
                body.IsDownloadAllowed,
                body.Notes,
                body.DefaultLanguage,
                body.Country,
                body.RequiresAuthentication,
                body.UseHeadlessBrowser,
                body.DownloadFrequencyMinutes,
                body.ConnectorKey,
                body.SourceCategoryId,
                body.IsEnabled,
                body.CronExpression,
                body.ScheduleTimeZoneId,
                body.ScheduleEnabled,
                body.CredentialUsername,
                body.CredentialPassword,
                body.RequiresMfa,
                body.RequiresOtp,
                body.ManualLoginRequired,
                body.OtpInstructions,
                body.AssistedSessionTimeoutMinutes,
                body.PdfDiscovery,
                body.Portal),
            cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<Guid>.Ok(id, "News source created"));
    }

    /// <summary>
    /// Updates an existing news source.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> UpdateAsync(
        Guid id,
        [FromBody] UpdateNewsSourceRequest body,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new UpdateNewsSourceCommand(
                id,
                body.Name,
                body.BaseUrl,
                body.SourceType,
                body.AcquisitionMode,
                body.SourceAccessMode,
                body.RequiresLogin,
                body.LoginUrl,
                body.EditionUrl,
                body.LogoutUrl,
                body.PortalUsername,
                body.LoginMethod,
                body.UsernameSelector,
                body.PasswordSelector,
                body.SubmitSelector,
                body.DownloadSelector,
                body.LoginSuccessSelector,
                body.SuccessUrlPattern,
                body.RequiresCaptcha,
                body.IsDownloadAllowed,
                body.Notes,
                body.DefaultLanguage,
                body.Country,
                body.RequiresAuthentication,
                body.UseHeadlessBrowser,
                body.DownloadFrequencyMinutes,
                body.ConnectorKey,
                body.SourceCategoryId,
                body.IsEnabled,
                body.CronExpression,
                body.ScheduleTimeZoneId,
                body.ScheduleEnabled,
                body.CredentialUsername,
                body.CredentialPassword,
                body.RequiresMfa,
                body.RequiresOtp,
                body.ManualLoginRequired,
                body.OtpInstructions,
                body.AssistedSessionTimeoutMinutes,
                body.PdfDiscovery,
                body.Portal),
            cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse.Ok("News source updated"));
    }

    /// <summary>
    /// Soft-deletes a news source.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteNewsSourceCommand(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse.Ok("News source deleted"));
    }

    /// <summary>
    /// Validates URL reachability and robots/compliance policy without persisting.
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(ApiResponse<NewsSourceConnectionTestResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NewsSourceConnectionTestResult>>> TestAsync(
        [FromBody] TestNewsSourceRequest body,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new TestNewsSourceConnectionCommand(body.BaseUrl, body.AcquisitionMode), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<NewsSourceConnectionTestResult>.Ok(result, result.Success ? "Connection test succeeded" : "Connection test reported issues"));
    }

    /// <summary>
    /// Runs a headless Playwright login probe for a WebPortalLogin source; captures screenshots/HTML on failure.
    /// </summary>
    [HttpPost("{id:guid}/test-login")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<NewsPortalLoginTestResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NewsPortalLoginTestResultDto>>> TestLoginAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new TestNewsSourceLoginCommand(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<NewsPortalLoginTestResultDto>.Ok(result, result.Success ? "Login test succeeded" : "Login test failed"));
    }

    /// <summary>
    /// Toggles whether a source is active (included in schedulers and download jobs).
    /// </summary>
    [HttpPatch("{id:guid}/enabled")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<bool>>> SetEnabledAsync(
        Guid id,
        [FromBody] SetNewsSourceEnabledRequest body,
        CancellationToken cancellationToken)
    {
        var enabled = await mediator.Send(new SetNewsSourceEnabledCommand(id, body.IsEnabled), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<bool>.Ok(enabled, enabled ? "Source is active" : "Source is inactive"));
    }

    /// <summary>
    /// Enqueues a Hangfire job to download a single source immediately (any supported source type).
    /// </summary>
    [HttpPost("{id:guid}/download-now")]
    [Authorize(Policy = AuthPolicies.AdminOrAnalyst)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> DownloadNowAsync(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new EnqueueNewsSourceDownloadCommand(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse.Ok("Download job enqueued"));
    }

    /// <summary>
    /// Enqueues licensed portal edition download (WebPortalLogin only; same worker pipeline as download-now).
    /// </summary>
    [HttpPost("{id:guid}/download-edition")]
    [Authorize(Policy = AuthPolicies.AdminOrAnalyst)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> DownloadEditionAsync(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DownloadNewsSourceEditionCommand(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse.Ok("Edition download job enqueued for WebPortalLogin source"));
    }

    /// <summary>
    /// Synchronous login + permitted edition download probe (PressReader context-menu Download); validates PDF bytes.
    /// </summary>
    [HttpPost("{id:guid}/test-download")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<NewsPortalDownloadTestResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NewsPortalDownloadTestResultDto>>> TestDownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new TestNewsSourcePortalDownloadCommand(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<NewsPortalDownloadTestResultDto>.Ok(result, result.Success ? "Download test succeeded" : "Download test failed"));
    }

    /// <summary>
    /// Signs out on the licensed portal (e.g. daralkhaleej: click subscriber id → Sign out) to release concurrent sessions.
    /// </summary>
    [HttpPost("{id:guid}/test-logout")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<NewsPortalLogoutTestResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NewsPortalLogoutTestResultDto>>> TestLogoutAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new TestNewsSourceLogoutCommand(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<NewsPortalLogoutTestResultDto>.Ok(result, result.Success ? "Logout test succeeded" : "Logout test failed"));
    }

    [HttpGet("{id:guid}/latest-portal-pdf")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfRead)]
    [ProducesResponseType(typeof(ApiResponse<PortalLatestEditionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PortalLatestEditionDto>>> GetLatestPortalPdfAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLatestPortalEditionQuery(id), cancellationToken).ConfigureAwait(false);
        return result is null
            ? NotFound(ApiResponse<PortalLatestEditionDto>.Fail("No portal PDF edition found"))
            : Ok(ApiResponse<PortalLatestEditionDto>.Ok(result, "Latest portal PDF retrieved"));
    }

    [HttpGet("{id:guid}/portal-pdf-history")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfAudit)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PortalEditionHistoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PortalEditionHistoryItemDto>>>> GetPortalPdfHistoryAsync(
        Guid id,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetPortalEditionHistoryQuery(id, take), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<PortalEditionHistoryItemDto>>.Ok(result, "Portal PDF history retrieved"));
    }

    /// <summary>
    /// Operational metrics for ingestion dashboards.
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(ApiResponse<DownloadMetricsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DownloadMetricsDto>>> MetricsAsync(CancellationToken cancellationToken)
    {
        var dto = await mediator.Send(new GetDownloadMetricsQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<DownloadMetricsDto>.Ok(dto, "Metrics retrieved"));
    }

    /// <summary>
    /// Lookup of source categories used to populate dropdowns on the admin UI.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SourceCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SourceCategoryDto>>>> CategoriesAsync(CancellationToken cancellationToken)
    {
        var list = await mediator.Send(new GetSourceCategoriesQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<SourceCategoryDto>>.Ok(list, "Categories retrieved"));
    }

    [HttpPost("{id:guid}/discover-pdf-edition")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfWrite)]
    [ProducesResponseType(typeof(ApiResponse<PdfEditionDownloadOutcome>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdfEditionDownloadOutcome>>> DiscoverPdfEditionAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DiscoverPdfEditionCommand(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<PdfEditionDownloadOutcome>.Ok(result, "PDF edition discovery completed"));
    }

    [HttpPost("{id:guid}/download-today-pdf")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfWrite)]
    [ProducesResponseType(typeof(ApiResponse<PdfEditionDownloadOutcome>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdfEditionDownloadOutcome>>> DownloadTodayPdfAsync(
        Guid id,
        [FromQuery] bool enqueueOcr = true,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new DownloadTodayPdfCommand(id, enqueueOcr), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<PdfEditionDownloadOutcome>.Ok(result, "PDF edition download completed"));
    }

    [HttpPost("{id:guid}/download-manual-pdf")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfWrite)]
    [ProducesResponseType(typeof(ApiResponse<PdfEditionDownloadOutcome>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdfEditionDownloadOutcome>>> DownloadManualPdfAsync(
        Guid id,
        [FromBody] DownloadManualPdfRequest body,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new DownloadManualPdfCommand(id, body.ManualUrl, body.SaveAsDiscoveryPageUrl, body.EnqueueOcr),
            cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<PdfEditionDownloadOutcome>.Ok(result, "Manual PDF download completed"));
    }

    [HttpGet("{id:guid}/latest-pdf")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfRead)]
    [ProducesResponseType(typeof(ApiResponse<PdfEditionDownloadOutcome>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdfEditionDownloadOutcome>>> GetLatestPdfAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLatestPdfEditionQuery(id), cancellationToken).ConfigureAwait(false);
        return result is null
            ? NotFound(ApiResponse<PdfEditionDownloadOutcome>.Fail("No downloaded PDF edition found"))
            : Ok(ApiResponse<PdfEditionDownloadOutcome>.Ok(result, "Latest PDF edition retrieved"));
    }

    [HttpGet("{id:guid}/pdf-history")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfAudit)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PdfEditionHistoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PdfEditionHistoryItemDto>>>> GetPdfHistoryAsync(
        Guid id,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetPdfEditionHistoryQuery(id, take), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<PdfEditionHistoryItemDto>>.Ok(result, "PDF edition history retrieved"));
    }

    [HttpGet("{id:guid}/pdf/{fileId:guid}")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StreamPdfAsync(
        Guid id,
        Guid fileId,
        [FromQuery] bool inline = true,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new StreamPdfEditionQuery(id, fileId, inline), cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return NotFound();
        }

        Response.Headers.ContentDisposition = inline
            ? $"inline; filename=\"{result.FileName}\""
            : $"attachment; filename=\"{result.FileName}\"";
        return File(result.Content, result.ContentType);
    }

    [HttpPost("{id:guid}/suggest-pdf-selectors")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfWrite)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PdfSelectorSuggestionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PdfSelectorSuggestionDto>>>> SuggestPdfSelectorsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SuggestPdfSelectorsCommand(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<PdfSelectorSuggestionDto>>.Ok(result, "AI selector suggestions generated"));
    }

    [HttpGet("{id:guid}/selector-suggestions")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfWrite)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PdfSelectorSuggestionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PdfSelectorSuggestionDto>>>> GetSelectorSuggestionsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPdfSelectorSuggestionsQuery(id), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyList<PdfSelectorSuggestionDto>>.Ok(result, "Selector suggestions retrieved"));
    }

    [HttpPost("{id:guid}/selector-suggestions/{suggestionId:guid}/test")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfWrite)]
    [ProducesResponseType(typeof(ApiResponse<PdfSelectorSuggestionTestOutcome>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PdfSelectorSuggestionTestOutcome>>> TestSelectorSuggestionAsync(
        Guid id,
        Guid suggestionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new TestPdfSelectorSuggestionCommand(id, suggestionId), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<PdfSelectorSuggestionTestOutcome>.Ok(result, "Selector suggestion tested"));
    }

    [HttpPost("{id:guid}/selector-suggestions/{suggestionId:guid}/accept")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfWrite)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> AcceptSelectorSuggestionAsync(
        Guid id,
        Guid suggestionId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new AcceptPdfSelectorSuggestionCommand(id, suggestionId), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse.Ok("Selector suggestion accepted"));
    }

    [HttpPost("{id:guid}/selector-suggestions/{suggestionId:guid}/reject")]
    [Authorize(Policy = AuthPolicies.NewsSourcePdfWrite)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> RejectSelectorSuggestionAsync(
        Guid id,
        Guid suggestionId,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectPdfSelectorSuggestionCommand(id, suggestionId), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse.Ok("Selector suggestion rejected"));
    }

}

public sealed record SetNewsSourceEnabledRequest(bool IsEnabled);

public sealed record CreateNewsSourceRequest(
    string Name,
    string BaseUrl,
    NewsSourceType SourceType,
    ContentAcquisitionMode AcquisitionMode,
    NewsSourceAccessMode SourceAccessMode,
    bool RequiresLogin,
    string? LoginUrl,
    string? EditionUrl,
    string? LogoutUrl,
    string? PortalUsername,
    PortalLoginMethod LoginMethod,
    string? UsernameSelector,
    string? PasswordSelector,
    string? SubmitSelector,
    string? DownloadSelector,
    string? LoginSuccessSelector,
    string? SuccessUrlPattern,
    bool RequiresCaptcha,
    bool IsDownloadAllowed,
    string? Notes,
    string? DefaultLanguage,
    string? Country,
    bool RequiresAuthentication,
    bool UseHeadlessBrowser,
    int? DownloadFrequencyMinutes,
    string? ConnectorKey,
    Guid? SourceCategoryId,
    bool IsEnabled,
    string? CronExpression,
    string? ScheduleTimeZoneId,
    bool ScheduleEnabled,
    string? CredentialUsername,
    string? CredentialPassword,
    bool RequiresMfa = false,
    bool RequiresOtp = false,
    bool ManualLoginRequired = false,
    string? OtpInstructions = null,
    int? AssistedSessionTimeoutMinutes = null,
    PdfDiscoveryFieldsDto? PdfDiscovery = null,
    PortalAutomationFieldsDto? Portal = null);

public sealed record UpdateNewsSourceRequest(
    string Name,
    string BaseUrl,
    NewsSourceType SourceType,
    ContentAcquisitionMode AcquisitionMode,
    NewsSourceAccessMode SourceAccessMode,
    bool RequiresLogin,
    string? LoginUrl,
    string? EditionUrl,
    string? LogoutUrl,
    string? PortalUsername,
    PortalLoginMethod LoginMethod,
    string? UsernameSelector,
    string? PasswordSelector,
    string? SubmitSelector,
    string? DownloadSelector,
    string? LoginSuccessSelector,
    string? SuccessUrlPattern,
    bool RequiresCaptcha,
    bool IsDownloadAllowed,
    string? Notes,
    string? DefaultLanguage,
    string? Country,
    bool RequiresAuthentication,
    bool UseHeadlessBrowser,
    int? DownloadFrequencyMinutes,
    string? ConnectorKey,
    Guid? SourceCategoryId,
    bool IsEnabled,
    string? CronExpression,
    string? ScheduleTimeZoneId,
    bool ScheduleEnabled,
    string? CredentialUsername,
    string? CredentialPassword,
    bool RequiresMfa = false,
    bool RequiresOtp = false,
    bool ManualLoginRequired = false,
    string? OtpInstructions = null,
    int? AssistedSessionTimeoutMinutes = null,
    PdfDiscoveryFieldsDto? PdfDiscovery = null,
    PortalAutomationFieldsDto? Portal = null);

public sealed record TestNewsSourceRequest(string BaseUrl, ContentAcquisitionMode AcquisitionMode);

public sealed record DownloadManualPdfRequest(
    string ManualUrl,
    bool SaveAsDiscoveryPageUrl = false,
    bool EnqueueOcr = true);
