using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Classification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDlpWeightLabelsCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionCategory",
                schema: "classification",
                table: "PolicyRules",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessCategory",
                schema: "classification",
                table: "InspectionResults",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Labels",
                schema: "classification",
                table: "InspectionResults",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                schema: "classification",
                table: "InspectionResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BusinessCategory",
                schema: "classification",
                table: "Findings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Labels",
                schema: "classification",
                table: "Findings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Weight",
                schema: "classification",
                table: "Findings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BusinessCategory",
                schema: "classification",
                table: "DlpRules",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Labels",
                schema: "classification",
                table: "DlpRules",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Weight",
                schema: "classification",
                table: "DlpRules",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionCategory",
                schema: "classification",
                table: "PolicyRules");

            migrationBuilder.DropColumn(
                name: "BusinessCategory",
                schema: "classification",
                table: "InspectionResults");

            migrationBuilder.DropColumn(
                name: "Labels",
                schema: "classification",
                table: "InspectionResults");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                schema: "classification",
                table: "InspectionResults");

            migrationBuilder.DropColumn(
                name: "BusinessCategory",
                schema: "classification",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "Labels",
                schema: "classification",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "Weight",
                schema: "classification",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "BusinessCategory",
                schema: "classification",
                table: "DlpRules");

            migrationBuilder.DropColumn(
                name: "Labels",
                schema: "classification",
                table: "DlpRules");

            migrationBuilder.DropColumn(
                name: "Weight",
                schema: "classification",
                table: "DlpRules");
        }
    }
}
