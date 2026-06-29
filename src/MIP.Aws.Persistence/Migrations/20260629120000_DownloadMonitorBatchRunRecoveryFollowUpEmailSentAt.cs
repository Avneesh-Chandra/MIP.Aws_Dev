using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations;

/// <inheritdoc />
public partial class DownloadMonitorBatchRunRecoveryFollowUpEmailSentAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RecoveryFollowUpEmailSentAt",
            table: "DownloadMonitorBatchRuns",
            type: "datetimeoffset",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RecoveryFollowUpEmailSentAt",
            table: "DownloadMonitorBatchRuns");
    }
}
