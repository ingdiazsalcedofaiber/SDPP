using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Classification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "classification");

            migrationBuilder.CreateTable(
                name: "ClassificationPolicies",
                schema: "classification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScopeValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DlpRules",
                schema: "classification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DetectorType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PatternOrConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DefaultSeverity = table.Column<byte>(type: "tinyint", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DlpRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InspectionResults",
                schema: "classification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggeredBy = table.Column<byte>(type: "tinyint", nullable: false),
                    SuggestedClassification = table.Column<byte>(type: "tinyint", nullable: false),
                    FinalClassification = table.Column<byte>(type: "tinyint", nullable: false),
                    RequiresManualReview = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "classification",
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
                name: "PolicyRules",
                schema: "classification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConditionClassification = table.Column<byte>(type: "tinyint", nullable: true),
                    ConditionOperationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ConditionAreaEquals = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Effect = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ClassificationPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyRules_ClassificationPolicies_ClassificationPolicyId",
                        column: x => x.ClassificationPolicyId,
                        principalSchema: "classification",
                        principalTable: "ClassificationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Findings",
                schema: "classification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DetectorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Severity = table.Column<byte>(type: "tinyint", nullable: false),
                    MatchCount = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RuleVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InspectionResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_InspectionResults_InspectionResultId",
                        column: x => x.InspectionResultId,
                        principalSchema: "classification",
                        principalTable: "InspectionResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Findings_InspectionResultId",
                schema: "classification",
                table: "Findings",
                column: "InspectionResultId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_DocumentId",
                schema: "classification",
                table: "InspectionResults",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyRules_ClassificationPolicyId",
                schema: "classification",
                table: "PolicyRules",
                column: "ClassificationPolicyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DlpRules",
                schema: "classification");

            migrationBuilder.DropTable(
                name: "Findings",
                schema: "classification");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "classification");

            migrationBuilder.DropTable(
                name: "PolicyRules",
                schema: "classification");

            migrationBuilder.DropTable(
                name: "InspectionResults",
                schema: "classification");

            migrationBuilder.DropTable(
                name: "ClassificationPolicies",
                schema: "classification");
        }
    }
}
