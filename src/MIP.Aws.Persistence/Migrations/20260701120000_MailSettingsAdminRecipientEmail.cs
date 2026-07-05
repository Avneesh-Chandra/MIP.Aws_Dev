using MIP.Aws.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(MediaIntelligenceDbContext))]
[Migration("20260701120000_MailSettingsAdminRecipientEmail")]
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
