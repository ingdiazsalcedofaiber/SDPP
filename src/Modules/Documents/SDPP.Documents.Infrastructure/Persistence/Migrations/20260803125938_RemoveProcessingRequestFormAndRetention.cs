using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProcessingRequestFormAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessingRequestForms",
                schema: "documents");

            migrationBuilder.DropColumn(
                name: "RetentionAction",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "RetentionDays",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "RetentionIsPermanent",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "ApprovalRequired",
                schema: "documents",
                table: "ConversionJobs");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "documents",
                table: "ConversionJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "RetentionAction",
                schema: "documents",
                table: "DocumentInstances",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionDays",
                schema: "documents",
                table: "DocumentInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionIsPermanent",
                schema: "documents",
                table: "DocumentInstances",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApprovalRequired",
                schema: "documents",
                table: "ConversionJobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                schema: "documents",
                table: "ConversionJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessingRequestForms",
                schema: "documents",
                columns: table => new
                {
                    ConversionJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CaseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Client = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeclaredClassification = table.Column<byte>(type: "tinyint", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Process = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Project = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RetentionAction = table.Column<byte>(type: "tinyint", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: true),
                    RetentionIsPermanent = table.Column<bool>(type: "bit", nullable: false)
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
        }
    }
}
