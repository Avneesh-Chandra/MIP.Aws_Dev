using MIP.Aws.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(MediaIntelligenceDbContext))]
[Migration("20260629120000_DownloadMonitorBatchRunRecoveryFollowUpEmailSentAt")]
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
