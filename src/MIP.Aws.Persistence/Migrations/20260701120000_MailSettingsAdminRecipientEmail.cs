using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations;

/// <inheritdoc />
public partial class MailSettingsAdminRecipientEmail : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AdminRecipientEmail",
            table: "MailSettings",
            type: "nvarchar(320)",
            maxLength: 320,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AdminRecipientEmail",
            table: "MailSettings");
    }
}
