using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LogicalDocuments_OwnerId",
                schema: "documents",
                table: "LogicalDocuments",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInstances_OwnerId_CreatedAtUtc",
                schema: "documents",
                table: "DocumentInstances",
                columns: new[] { "OwnerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversionJobs_CreatedAtUtc",
                schema: "documents",
                table: "ConversionJobs",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogicalDocuments_OwnerId",
                schema: "documents",
                table: "LogicalDocuments");

            migrationBuilder.DropIndex(
                name: "IX_DocumentInstances_OwnerId_CreatedAtUtc",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropIndex(
                name: "IX_ConversionJobs_CreatedAtUtc",
                schema: "documents",
                table: "ConversionJobs");
        }
    }
}
