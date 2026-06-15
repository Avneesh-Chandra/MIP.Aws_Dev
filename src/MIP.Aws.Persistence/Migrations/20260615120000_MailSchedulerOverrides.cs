using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations;

/// <inheritdoc />
public partial class MailSchedulerOverrides : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "MailAutomationEnabled",
            table: "MailSettings",
            type: "bit",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "StatusEmailEnabled",
            table: "MailSettings",
            type: "bit",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StatusEmailRecipient",
            table: "MailSettings",
            type: "nvarchar(320)",
            maxLength: 320,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MailAutomationEnabled",
            table: "MailSettings");

        migrationBuilder.DropColumn(
            name: "StatusEmailEnabled",
            table: "MailSettings");

        migrationBuilder.DropColumn(
            name: "StatusEmailRecipient",
            table: "MailSettings");
    }
}
