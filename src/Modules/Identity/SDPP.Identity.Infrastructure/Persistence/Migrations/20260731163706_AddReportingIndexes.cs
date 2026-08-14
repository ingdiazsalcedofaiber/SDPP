using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAtUtc",
                schema: "identity",
                table: "Users",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CreatedAtUtc",
                schema: "identity",
                table: "Users");
        }
    }
}
