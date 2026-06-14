using System.Text.Json;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Application.Features.SourceRecovery;

/// <summary>Safe, AI-patchable subset of <see cref="NewsSource"/> configuration.</summary>
public sealed record SourceRecoveryConfigurationSnapshot(
    string? UsernameSelector,
    string? PasswordSelector,
    string? SubmitSelector,
    string? DownloadSelector,
    string? LoginIconSelector,
    string? NewspaperCanvasSelector,
    string? ContextMenuSelector,
    string? DownloadMenuItemSelector,
    string? LoginSuccessSelector,
    string? SuccessUrlPattern,
    string? PdfDownloadSelector,
    string? PdfLinkSelector,
    string? BaseUrl,
    string? EditionUrl,
    string? PdfDiscoveryPageUrl,
    int DownloadWaitTimeoutSeconds,
    bool UseHeadlessBrowser)
{
  public static SourceRecoveryConfigurationSnapshot FromEntity(NewsSource source) => new(
        source.UsernameSelector,
        source.PasswordSelector,
        source.SubmitSelector,
        source.DownloadSelector,
        source.LoginIconSelector,
        source.NewspaperCanvasSelector,
        source.ContextMenuSelector,
        source.DownloadMenuItemSelector,
        source.LoginSuccessSelector,
        source.SuccessUrlPattern,
        source.PdfDownloadSelector,
        source.PdfLinkSelector,
        source.BaseUrl,
        source.EditionUrl,
        source.PdfDiscoveryPageUrl,
        source.DownloadWaitTimeoutSeconds,
        source.UseHeadlessBrowser);

    public string ToJson() => JsonSerializer.Serialize(this);

    public static SourceRecoveryConfigurationSnapshot? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SourceRecoveryConfigurationSnapshot>(json);
    }

    public SourceRecoveryConfigurationPatchDto ToPatch() => new(
        UsernameSelector,
        PasswordSelector,
        SubmitSelector,
        DownloadSelector,
        LoginIconSelector,
        NewspaperCanvasSelector,
        ContextMenuSelector,
        DownloadMenuItemSelector,
        LoginSuccessSelector,
        SuccessUrlPattern,
        PdfDownloadSelector,
        PdfLinkSelector,
        BaseUrl,
        EditionUrl,
        PdfDiscoveryPageUrl,
        DownloadWaitTimeoutSeconds,
        UseHeadlessBrowser);

    public void ApplyPatch(SourceRecoveryConfigurationPatchDto patch, NewsSource target)
    {
        if (patch.UsernameSelector is not null) target.UsernameSelector = patch.UsernameSelector;
        if (patch.PasswordSelector is not null) target.PasswordSelector = patch.PasswordSelector;
        if (patch.SubmitSelector is not null) target.SubmitSelector = patch.SubmitSelector;
        if (patch.DownloadSelector is not null) target.DownloadSelector = patch.DownloadSelector;
        if (patch.LoginIconSelector is not null) target.LoginIconSelector = patch.LoginIconSelector;
        if (patch.NewspaperCanvasSelector is not null) target.NewspaperCanvasSelector = patch.NewspaperCanvasSelector;
        if (patch.ContextMenuSelector is not null) target.ContextMenuSelector = patch.ContextMenuSelector;
        if (patch.DownloadMenuItemSelector is not null) target.DownloadMenuItemSelector = patch.DownloadMenuItemSelector;
        if (patch.LoginSuccessSelector is not null) target.LoginSuccessSelector = patch.LoginSuccessSelector;
        if (patch.SuccessUrlPattern is not null) target.SuccessUrlPattern = patch.SuccessUrlPattern;
        if (patch.PdfDownloadSelector is not null) target.PdfDownloadSelector = patch.PdfDownloadSelector;
        if (patch.PdfLinkSelector is not null) target.PdfLinkSelector = patch.PdfLinkSelector;
        if (patch.BaseUrl is not null) target.BaseUrl = patch.BaseUrl;
        if (patch.EditionUrl is not null) target.EditionUrl = patch.EditionUrl;
        if (patch.PdfDiscoveryPageUrl is not null) target.PdfDiscoveryPageUrl = patch.PdfDiscoveryPageUrl;
        if (patch.DownloadWaitTimeoutSeconds is int wait) target.DownloadWaitTimeoutSeconds = wait;
        if (patch.UseHeadlessBrowser is bool headless) target.UseHeadlessBrowser = headless;
    }
}
