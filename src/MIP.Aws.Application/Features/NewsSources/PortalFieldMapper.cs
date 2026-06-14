using MIP.Aws.Application.Portal;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record PortalAutomationFieldsDto(
    string? PortalStrategyKey,
    string? LoginIconSelector,
    string? NewspaperCanvasSelector,
    string? ContextMenuSelector,
    string? DownloadMenuItemSelector,
    int DownloadWaitTimeoutSeconds);

public static class PortalFieldMapper
{
    public static PortalAutomationFieldsDto ToDto(NewsSource x) =>
        new(
            x.PortalStrategyKey,
            x.LoginIconSelector,
            x.NewspaperCanvasSelector,
            x.ContextMenuSelector,
            x.DownloadMenuItemSelector,
            x.DownloadWaitTimeoutSeconds <= 0 ? 180 : x.DownloadWaitTimeoutSeconds);

    public static void Apply(NewsSource entity, PortalAutomationFieldsDto dto)
    {
        entity.PortalStrategyKey = NormalizeStrategyKey(dto.PortalStrategyKey);
        entity.LoginIconSelector = dto.LoginIconSelector?.Trim();
        entity.NewspaperCanvasSelector = dto.NewspaperCanvasSelector?.Trim();
        entity.ContextMenuSelector = dto.ContextMenuSelector?.Trim();
        entity.DownloadMenuItemSelector = dto.DownloadMenuItemSelector?.Trim();
        entity.DownloadWaitTimeoutSeconds = dto.DownloadWaitTimeoutSeconds <= 0 ? 180 : Math.Min(dto.DownloadWaitTimeoutSeconds, 600);
    }

    public static string? NormalizeStrategyKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var trimmed = key.Trim();
        return trimmed.Equals(PortalStrategyKeys.PressReader, StringComparison.OrdinalIgnoreCase)
            ? PortalStrategyKeys.PressReader
            : trimmed.Equals(PortalStrategyKeys.Generic, StringComparison.OrdinalIgnoreCase)
                ? PortalStrategyKeys.Generic
                : trimmed;
    }
}
