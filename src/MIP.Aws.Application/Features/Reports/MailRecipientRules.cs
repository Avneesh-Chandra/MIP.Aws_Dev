namespace MIP.Aws.Application.Features.Reports;

public static class MailRecipientRules
{
    public static IReadOnlyList<string> Parse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(recipient => recipient.Trim().ToLowerInvariant())
                .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    public static string Normalize(string? value) => string.Join(';', Parse(value));

    public static IReadOnlyList<string> ResolveSelected(
        string? configuredRecipients,
        string? selectedRecipients)
    {
        var configured = Parse(configuredRecipients);
        if (selectedRecipients is null)
        {
            return configured;
        }

        var configuredSet = configured.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Parse(selectedRecipients)
            .Where(configuredSet.Contains)
            .ToList();
    }

    public static string SerializeSelected(
        string? configuredRecipients,
        IReadOnlyList<string>? selectedRecipients)
    {
        var configured = Parse(configuredRecipients);
        if (selectedRecipients is null)
        {
            return string.Join(';', configured);
        }

        var configuredSet = configured.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return string.Join(
            ';',
            selectedRecipients
                .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
                .Select(recipient => recipient.Trim().ToLowerInvariant())
                .Where(configuredSet.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
