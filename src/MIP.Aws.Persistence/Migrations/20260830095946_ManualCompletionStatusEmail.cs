using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManualCompletionStatusEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ManualCompletionStatusEmailSentAt",
                table: "DownloadMonitorBatchRuns",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualCompletionStatusEmailSentAt",
                table: "DownloadMonitorBatchRuns");
        }
    }
}
