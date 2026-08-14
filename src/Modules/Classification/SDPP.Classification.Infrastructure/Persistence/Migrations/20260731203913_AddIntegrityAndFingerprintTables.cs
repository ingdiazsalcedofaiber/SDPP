using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Classification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrityAndFingerprintTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentIntegrityRecords",
                schema: "classification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IntegritySignature = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProtectionsApplied = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentIntegrityRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersionFingerprints",
                schema: "classification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LogicalDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StructuralSignature = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Classification = table.Column<byte>(type: "tinyint", nullable: false),
                    ClassificationSource = table.Column<byte>(type: "tinyint", nullable: false),
                    RiskScore = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Labels = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersionFingerprints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIntegrityRecords_DocumentId",
                schema: "classification",
                table: "DocumentIntegrityRecords",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIntegrityRecords_DocumentVersionId",
                schema: "classification",
                table: "DocumentIntegrityRecords",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIntegrityRecords_Sha256Hash",
                schema: "classification",
                table: "DocumentIntegrityRecords",
                column: "Sha256Hash");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersionFingerprints_ContentFingerprint",
                schema: "classification",
                table: "DocumentVersionFingerprints",
                column: "ContentFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersionFingerprints_DocumentVersionId",
                schema: "classification",
                table: "DocumentVersionFingerprints",
                column: "DocumentVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersionFingerprints_LogicalDocumentId",
                schema: "classification",
                table: "DocumentVersionFingerprints",
                column: "LogicalDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentIntegrityRecords",
                schema: "classification");

            migrationBuilder.DropTable(
                name: "DocumentVersionFingerprints",
                schema: "classification");
        }
    }
}
