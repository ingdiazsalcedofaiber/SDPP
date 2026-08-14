using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProtectionsAppliedField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectionsApplied",
                schema: "documents",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectionsApplied",
                schema: "documents",
                table: "Documents");
        }
    }
}
