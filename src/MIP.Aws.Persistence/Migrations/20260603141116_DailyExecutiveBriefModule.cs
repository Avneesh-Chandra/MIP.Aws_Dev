using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyExecutiveBriefModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyExecutiveBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PdfStoragePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    HtmlStoragePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    LastFailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyExecutiveBriefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyExecutiveBriefSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToRecipients = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcRecipients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BccRecipients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SendTimeLocal = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    GenerateTimeLocal = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequireApprovalBeforeSend = table.Column<bool>(type: "bit", nullable: false),
                    AutoSendApproved = table.Column<bool>(type: "bit", nullable: false),
                    IncludePdfAttachment = table.Column<bool>(type: "bit", nullable: false),
                    IncludeExcelAttachment = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyExecutiveBriefSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyExecutiveBriefEmailLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyExecutiveBriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyExecutiveBriefEmailLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyExecutiveBriefEmailLogs_DailyExecutiveBriefs_DailyExecutiveBriefId",
                        column: x => x.DailyExecutiveBriefId,
                        principalTable: "DailyExecutiveBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyExecutiveBriefItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyExecutiveBriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractedArticleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SectionType = table.Column<int>(type: "int", nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ImportanceScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsIncluded = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyExecutiveBriefItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyExecutiveBriefItems_DailyExecutiveBriefs_DailyExecutiveBriefId",
                        column: x => x.DailyExecutiveBriefId,
                        principalTable: "DailyExecutiveBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyExecutiveBriefMarketSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyExecutiveBriefId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Market = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Ticker = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ClosingPrice = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PreviousClosingPrice = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ChangePercent = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    VolumeTraded = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyExecutiveBriefMarketSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyExecutiveBriefMarketSnapshots_DailyExecutiveBriefs_DailyExecutiveBriefId",
                        column: x => x.DailyExecutiveBriefId,
                        principalTable: "DailyExecutiveBriefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyExecutiveBriefEmailLogs_DailyExecutiveBriefId",
                table: "DailyExecutiveBriefEmailLogs",
                column: "DailyExecutiveBriefId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyExecutiveBriefItems_DailyExecutiveBriefId_SectionType_DisplayOrder",
                table: "DailyExecutiveBriefItems",
                columns: new[] { "DailyExecutiveBriefId", "SectionType", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyExecutiveBriefMarketSnapshots_DailyExecutiveBriefId_DisplayOrder",
                table: "DailyExecutiveBriefMarketSnapshots",
                columns: new[] { "DailyExecutiveBriefId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyExecutiveBriefs_BriefDate",
                table: "DailyExecutiveBriefs",
                column: "BriefDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyExecutiveBriefs_Status_BriefDate",
                table: "DailyExecutiveBriefs",
                columns: new[] { "Status", "BriefDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyExecutiveBriefEmailLogs");

            migrationBuilder.DropTable(
                name: "DailyExecutiveBriefItems");

            migrationBuilder.DropTable(
                name: "DailyExecutiveBriefMarketSnapshots");

            migrationBuilder.DropTable(
                name: "DailyExecutiveBriefSettings");

            migrationBuilder.DropTable(
                name: "DailyExecutiveBriefs");
        }
    }
}
