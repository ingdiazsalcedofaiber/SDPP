using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProtectionAndRiskFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "documents",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegritySignature",
                schema: "documents",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Labels",
                schema: "documents",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                schema: "documents",
                table: "Documents",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                schema: "documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IntegritySignature",
                schema: "documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Labels",
                schema: "documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                schema: "documents",
                table: "Documents");
        }
    }
}
