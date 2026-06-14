using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MIP.Aws.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketDataIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketDataImportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    ProviderConfigName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowsAttempted = table.Column<int>(type: "int", nullable: false),
                    RowsImported = table.Column<int>(type: "int", nullable: false),
                    RowsSkipped = table.Column<int>(type: "int", nullable: false),
                    RowsFailed = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StoredArtifactKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    InitiatedByEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    InitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RowErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MarketDataImportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AuthSecretReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Schedule = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFailureAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFailureMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_MarketDataProviderConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketInstruments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Exchange = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    InstrumentType = table.Column<int>(type: "int", nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsFeaturedForExecutiveReport = table.Column<bool>(type: "bit", nullable: false),
                    IsGfhStock = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_MarketInstruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketMovementSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InstrumentsTracked = table.Column<int>(type: "int", nullable: false),
                    InstrumentsWithData = table.Column<int>(type: "int", nullable: false),
                    Gainers = table.Column<int>(type: "int", nullable: false),
                    Losers = table.Column<int>(type: "int", nullable: false),
                    Unchanged = table.Column<int>(type: "int", nullable: false),
                    AverageChangePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    MaxGainPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    MaxGainSymbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MaxLossPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    MaxLossSymbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    VolatileFlags = table.Column<int>(type: "int", nullable: false),
                    MissingDataCount = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Commentary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
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
                    table.PrimaryKey("PK_MarketMovementSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketPriceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Open = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    High = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Low = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Close = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PreviousClose = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    Change = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ChangePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    SourceProvider = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ImportJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsVolatile = table.Column<bool>(type: "bit", nullable: false),
                    AnalystNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_MarketPriceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketPriceSnapshots_MarketDataImportJobs_ImportJobId",
                        column: x => x.ImportJobId,
                        principalTable: "MarketDataImportJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarketPriceSnapshots_MarketInstruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "MarketInstruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataImportJobs_Provider_StartedAt",
                table: "MarketDataImportJobs",
                columns: new[] { "Provider", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataImportJobs_Status",
                table: "MarketDataImportJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataProviderConfigs_Name",
                table: "MarketDataProviderConfigs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataProviderConfigs_Provider_IsActive",
                table: "MarketDataProviderConfigs",
                columns: new[] { "Provider", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketInstruments_IsActive_DisplayOrder",
                table: "MarketInstruments",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketInstruments_IsFeaturedForExecutiveReport_IsActive",
                table: "MarketInstruments",
                columns: new[] { "IsFeaturedForExecutiveReport", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketInstruments_Symbol_Exchange",
                table: "MarketInstruments",
                columns: new[] { "Symbol", "Exchange" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketMovementSummaries_TradeDate_Scope",
                table: "MarketMovementSummaries",
                columns: new[] { "TradeDate", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSnapshots_ImportJobId",
                table: "MarketPriceSnapshots",
                column: "ImportJobId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSnapshots_InstrumentId_TradeDate_SourceProvider",
                table: "MarketPriceSnapshots",
                columns: new[] { "InstrumentId", "TradeDate", "SourceProvider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSnapshots_IsVolatile_TradeDate",
                table: "MarketPriceSnapshots",
                columns: new[] { "IsVolatile", "TradeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceSnapshots_TradeDate",
                table: "MarketPriceSnapshots",
                column: "TradeDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketDataProviderConfigs");

            migrationBuilder.DropTable(
                name: "MarketMovementSummaries");

            migrationBuilder.DropTable(
                name: "MarketPriceSnapshots");

            migrationBuilder.DropTable(
                name: "MarketDataImportJobs");

            migrationBuilder.DropTable(
                name: "MarketInstruments");
        }
    }
}
