using System.Text.Json;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Application.Features.SourceRecovery;

public static class SourceRecoveryJsonParser
{
    public static IReadOnlyList<SourceRecoveryOptionDto> ParseOptions(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (!TryGetProperty(root, "options", out var optionsEl) || optionsEl.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SourceRecoveryOptionDto>();
        }

        var list = new List<SourceRecoveryOptionDto>();
        var index = 0;
        foreach (var item in optionsEl.EnumerateArray())
        {
            list.Add(ParseOption(item, index++));
        }

        return list;
    }

    public static IReadOnlyList<string> ParseStringArray(JsonElement root, string property)
    {
        if (!TryGetProperty(root, property, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return el.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? string.Empty)
            .Where(x => x.Length > 0)
            .ToList();
    }

    private static SourceRecoveryOptionDto ParseOption(JsonElement item, int index)
    {
        TryGetProperty(item, "patch", out var patchEl);
        var patch = new SourceRecoveryConfigurationPatchDto(
            GetString(patchEl, "usernameSelector"),
            GetString(patchEl, "passwordSelector"),
            GetString(patchEl, "submitSelector"),
            GetString(patchEl, "downloadSelector"),
            GetString(patchEl, "loginIconSelector"),
            GetString(patchEl, "newspaperCanvasSelector"),
            GetString(patchEl, "contextMenuSelector"),
            GetString(patchEl, "downloadMenuItemSelector"),
            GetString(patchEl, "loginSuccessSelector"),
            GetString(patchEl, "successUrlPattern"),
            GetString(patchEl, "pdfDownloadSelector"),
            GetString(patchEl, "pdfLinkSelector"),
            GetString(patchEl, "baseUrl"),
            GetString(patchEl, "editionUrl"),
            GetString(patchEl, "pdfDiscoveryPageUrl"),
            GetInt(patchEl, "downloadWaitTimeoutSeconds"),
            GetBool(patchEl, "useHeadlessBrowser"));

        var selectors = new List<SelectorRecoveryCandidateDto>();
        if (TryGetProperty(item, "selectorCandidates", out var sc) && sc.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in sc.EnumerateArray())
            {
                selectors.Add(new SelectorRecoveryCandidateDto(
                    GetString(s, "selector") ?? string.Empty,
                    GetString(s, "source") ?? "ai",
                    GetInt(s, "confidence") ?? 70,
                    ParseStrategy(GetString(s, "strategy")),
                    GetString(s, "fieldName")));
            }
        }

        return new SourceRecoveryOptionDto(
            GetInt(item, "optionIndex") ?? index,
            GetString(item, "title") ?? $"Recovery option {index + 1}",
            GetString(item, "description") ?? string.Empty,
            GetString(item, "expectedFix") ?? string.Empty,
            GetInt(item, "confidenceScore") ?? 60,
            GetInt(item, "predictedSuccessPercent") ?? 60,
            ParseRisk(GetString(item, "riskLevel")),
            ParseStringArray(item, "affectedFields"),
            ParseStringArray(item, "affectedWorkflowSteps"),
            patch,
            selectors);
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        value = default;
        if (el.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var prop in el.EnumerateObject())
        {
            if (!prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = prop.Value;
            return true;
        }

        return false;
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!TryGetProperty(el, name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static int? GetInt(JsonElement el, string name)
    {
        if (!TryGetProperty(el, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Number when value.TryGetInt32(out var i) => i,
            _ => null
        };
    }

    private static bool? GetBool(JsonElement el, string name)
    {
        if (!TryGetProperty(el, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static SourceRecoveryRiskLevel ParseRisk(string? value) => value?.ToLowerInvariant() switch
    {
        "low" => SourceRecoveryRiskLevel.Low,
        "high" => SourceRecoveryRiskLevel.High,
        _ => SourceRecoveryRiskLevel.Medium
    };

    private static SelectorRecoveryStrategy ParseStrategy(string? value) => value?.ToLowerInvariant() switch
    {
        "xpath" => SelectorRecoveryStrategy.XPath,
        "role" => SelectorRecoveryStrategy.Role,
        "text" => SelectorRecoveryStrategy.Text,
        "aria" => SelectorRecoveryStrategy.Aria,
        "nth" or "nthelement" => SelectorRecoveryStrategy.NthElement,
        _ => SelectorRecoveryStrategy.Css
    };
}
