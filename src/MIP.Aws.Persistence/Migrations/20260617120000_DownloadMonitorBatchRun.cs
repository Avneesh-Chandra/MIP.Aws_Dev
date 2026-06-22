using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations;

/// <inheritdoc />
public partial class DownloadMonitorBatchRun : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DownloadMonitorBatchRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                TotalSources = table.Column<int>(type: "int", nullable: false),
                HangfireJobId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DownloadMonitorBatchRuns", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DownloadMonitorBatchRuns_StartedAt",
            table: "DownloadMonitorBatchRuns",
            column: "StartedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DownloadMonitorBatchRuns");
    }
}
