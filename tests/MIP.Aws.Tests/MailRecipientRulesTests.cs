using MIP.Aws.Application.Features.Reports;

namespace MIP.Aws.Tests;

public sealed class MailRecipientRulesTests
{
    [Fact]
    public void ResolveSelected_defaults_legacy_null_selection_to_all_configured_recipients()
    {
        var selected = MailRecipientRules.ResolveSelected(
            " First@example.com;second@example.com, FIRST@example.com ",
            null);

        Assert.Equal(["first@example.com", "second@example.com"], selected);
    }

    [Fact]
    public void SerializeSelected_keeps_only_configured_unique_recipients()
    {
        var serialized = MailRecipientRules.SerializeSelected(
            "first@example.com;second@example.com",
            ["SECOND@example.com", "unknown@example.com", "second@example.com"]);

        Assert.Equal("second@example.com", serialized);
    }
}
