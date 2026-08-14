using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageBucket = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageObjectKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Classification = table.Column<byte>(type: "tinyint", nullable: false),
                    ClassificationSource = table.Column<byte>(type: "tinyint", nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: true),
                    RetentionIsPermanent = table.Column<bool>(type: "bit", nullable: true),
                    RetentionAction = table.Column<byte>(type: "tinyint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversionJobs",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    EngineUsed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    OutputDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ApprovalRequired = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversionJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversionJobs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "documents",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingRequestForms",
                schema: "documents",
                columns: table => new
                {
                    ConversionJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Project = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Process = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Client = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CaseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: true),
                    RetentionIsPermanent = table.Column<bool>(type: "bit", nullable: false),
                    RetentionAction = table.Column<byte>(type: "tinyint", nullable: false),
                    DeclaredClassification = table.Column<byte>(type: "tinyint", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingRequestForms", x => x.ConversionJobId);
                    table.ForeignKey(
                        name: "FK_ProcessingRequestForms_ConversionJobs_ConversionJobId",
                        column: x => x.ConversionJobId,
                        principalSchema: "documents",
                        principalTable: "ConversionJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversionJobs_DocumentId",
                schema: "documents",
                table: "ConversionJobs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256Hash",
                schema: "documents",
                table: "Documents",
                column: "Sha256Hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "ProcessingRequestForms",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "ConversionJobs",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "documents");
        }
    }
}
