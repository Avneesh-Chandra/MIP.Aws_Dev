using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutoAiDownloadRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AutoAiRecoveryRunId",
                table: "SourceRecoveryAttempts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutomatic",
                table: "SourceRecoveryAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoAiRecoveryEnabled",
                table: "NewsSources",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AutoAiRecoveryRunId",
                table: "DownloadJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Trigger",
                table: "DownloadJobs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AutoAiDownloadRecoverySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    RunAfterScheduledFailure = table.Column<bool>(type: "bit", nullable: false),
                    RunAfterManualFailure = table.Column<bool>(type: "bit", nullable: false),
                    MaxSuggestionsToTry = table.Column<int>(type: "int", nullable: false),
                    MinimumConfidence = table.Column<double>(type: "float", nullable: false),
                    MaximumRiskAllowed = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequireHumanApprovalForMediumRisk = table.Column<bool>(type: "bit", nullable: false),
                    CooldownMinutesPerSource = table.Column<int>(type: "int", nullable: false),
                    MaxAutoRecoveryAttemptsPerDayPerSource = table.Column<int>(type: "int", nullable: false),
                    ActivateSuccessfulCandidateAutomatically = table.Column<bool>(type: "bit", nullable: false),
                    RollbackOnFailure = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AutoAiDownloadRecoverySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoAiRecoveryRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FailedDownloadJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceRecoveryAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetryDownloadJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuccessfulCandidateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuggestionsTried = table.Column<int>(type: "int", nullable: false),
                    SuccessfulOptionIndex = table.Column<int>(type: "int", nullable: true),
                    SuccessfulOptionTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TimelineJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
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
                    table.PrimaryKey("PK_AutoAiRecoveryRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoAiRecoveryRuns_DownloadJobs_FailedDownloadJobId",
                        column: x => x.FailedDownloadJobId,
                        principalTable: "DownloadJobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AutoAiRecoveryRuns_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutoAiRecoveryRuns_SourceRecoveryAttempts_SourceRecoveryAttemptId",
                        column: x => x.SourceRecoveryAttemptId,
                        principalTable: "SourceRecoveryAttempts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceRecoveryAttempts_AutoAiRecoveryRunId",
                table: "SourceRecoveryAttempts",
                column: "AutoAiRecoveryRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoAiRecoveryRuns_FailedDownloadJobId",
                table: "AutoAiRecoveryRuns",
                column: "FailedDownloadJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoAiRecoveryRuns_NewsSourceId_CreatedAt",
                table: "AutoAiRecoveryRuns",
                columns: new[] { "NewsSourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoAiRecoveryRuns_SourceRecoveryAttemptId",
                table: "AutoAiRecoveryRuns",
                column: "SourceRecoveryAttemptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoAiDownloadRecoverySettings");

            migrationBuilder.DropTable(
                name: "AutoAiRecoveryRuns");

            migrationBuilder.DropIndex(
                name: "IX_SourceRecoveryAttempts_AutoAiRecoveryRunId",
                table: "SourceRecoveryAttempts");

            migrationBuilder.DropColumn(
                name: "AutoAiRecoveryRunId",
                table: "SourceRecoveryAttempts");

            migrationBuilder.DropColumn(
                name: "IsAutomatic",
                table: "SourceRecoveryAttempts");

            migrationBuilder.DropColumn(
                name: "AutoAiRecoveryEnabled",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "AutoAiRecoveryRunId",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "Trigger",
                table: "DownloadJobs");
        }
    }
}
