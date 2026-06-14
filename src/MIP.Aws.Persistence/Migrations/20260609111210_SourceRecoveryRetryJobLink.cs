using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SourceRecoveryRetryJobLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RetryDownloadJobId",
                table: "SourceRecoveryAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceRecoveryAttempts_RetryDownloadJobId",
                table: "SourceRecoveryAttempts",
                column: "RetryDownloadJobId");

            migrationBuilder.AddForeignKey(
                name: "FK_SourceRecoveryAttempts_DownloadJobs_RetryDownloadJobId",
                table: "SourceRecoveryAttempts",
                column: "RetryDownloadJobId",
                principalTable: "DownloadJobs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SourceRecoveryAttempts_DownloadJobs_RetryDownloadJobId",
                table: "SourceRecoveryAttempts");

            migrationBuilder.DropIndex(
                name: "IX_SourceRecoveryAttempts_RetryDownloadJobId",
                table: "SourceRecoveryAttempts");

            migrationBuilder.DropColumn(
                name: "RetryDownloadJobId",
                table: "SourceRecoveryAttempts");
        }
    }
}
