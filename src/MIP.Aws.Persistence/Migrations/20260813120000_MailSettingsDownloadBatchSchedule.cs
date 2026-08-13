using MIP.Aws.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations;

[DbContext(typeof(MediaIntelligenceDbContext))]
[Migration("20260813120000_MailSettingsDownloadBatchSchedule")]
public partial class MailSettingsDownloadBatchSchedule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BatchScheduleTimeUtc",
            table: "MailSettings",
            type: "nvarchar(5)",
            maxLength: 5,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BatchRecurrence",
            table: "MailSettings",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BatchRecurrenceDays",
            table: "MailSettings",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BatchScheduleTimeUtc", table: "MailSettings");
        migrationBuilder.DropColumn(name: "BatchRecurrence", table: "MailSettings");
        migrationBuilder.DropColumn(name: "BatchRecurrenceDays", table: "MailSettings");
    }
}
