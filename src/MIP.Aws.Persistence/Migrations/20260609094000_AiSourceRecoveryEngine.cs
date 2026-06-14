using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiSourceRecoveryEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceRecoveryKnowledgeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FailureType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PortalStrategyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ConnectorKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FieldName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OldSelector = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    NewSelector = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Strategy = table.Column<int>(type: "int", nullable: false),
                    SuccessCount = table.Column<int>(type: "int", nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceRecoveryAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_SourceRecoveryKnowledgeEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceConfigurationVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    JsonConfiguration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceRecoveryAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_SourceConfigurationVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceConfigurationVersions_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceRecoveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewsSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DownloadJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AnalysisJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedOptionIndex = table.Column<int>(type: "int", nullable: false),
                    CandidateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RollbackVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AppliedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResultSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PredictedSuccessPercent = table.Column<int>(type: "int", nullable: true),
                    ActualSuccessPercent = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_SourceRecoveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceRecoveryAttempts_DownloadJobs_DownloadJobId",
                        column: x => x.DownloadJobId,
                        principalTable: "DownloadJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SourceRecoveryAttempts_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SourceRecoveryAttempts_SourceConfigurationVersions_CandidateVersionId",
                        column: x => x.CandidateVersionId,
                        principalTable: "SourceConfigurationVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SourceRecoveryAttempts_SourceConfigurationVersions_RollbackVersionId",
                        column: x => x.RollbackVersionId,
                        principalTable: "SourceConfigurationVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceConfigurationVersions_NewsSourceId_Status",
                table: "SourceConfigurationVersions",
                columns: new[] { "NewsSourceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceConfigurationVersions_NewsSourceId_VersionNumber",
                table: "SourceConfigurationVersions",
                columns: new[] { "NewsSourceId", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceRecoveryAttempts_CandidateVersionId",
                table: "SourceRecoveryAttempts",
                column: "CandidateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceRecoveryAttempts_DownloadJobId_CreatedAt",
                table: "SourceRecoveryAttempts",
                columns: new[] { "DownloadJobId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceRecoveryAttempts_NewsSourceId_CreatedAt",
                table: "SourceRecoveryAttempts",
                columns: new[] { "NewsSourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceRecoveryAttempts_RollbackVersionId",
                table: "SourceRecoveryAttempts",
                column: "RollbackVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceRecoveryKnowledgeEntries_FailureType_PortalStrategyKey_FieldName",
                table: "SourceRecoveryKnowledgeEntries",
                columns: new[] { "FailureType", "PortalStrategyKey", "FieldName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceRecoveryKnowledgeEntries");

            migrationBuilder.DropTable(
                name: "SourceRecoveryAttempts");

            migrationBuilder.DropTable(
                name: "SourceConfigurationVersions");
        }
    }
}
