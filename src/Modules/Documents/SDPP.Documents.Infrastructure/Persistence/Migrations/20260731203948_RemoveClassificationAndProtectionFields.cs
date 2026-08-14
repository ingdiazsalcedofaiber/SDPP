using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClassificationAndProtectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentVersions_ContentFingerprint",
                schema: "documents",
                table: "DocumentVersions");

            // Named IX_Documents_Sha256Hash, not IX_DocumentInstances_Sha256Hash — this index
            // predates the Document -> DocumentInstance table rename earlier in the project
            // (sp_rename only renames the table object, never its indexes), so its physical name
            // in the live database never followed along.
            migrationBuilder.DropIndex(
                name: "IX_Documents_Sha256Hash",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "documents",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "Classification",
                schema: "documents",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "ClassificationSource",
                schema: "documents",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "ContentFingerprint",
                schema: "documents",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "Labels",
                schema: "documents",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                schema: "documents",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "StructuralSignature",
                schema: "documents",
                table: "DocumentVersions");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "Classification",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "ClassificationSource",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "IntegritySignature",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "Labels",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "ProtectionsApplied",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "Sha256Hash",
                schema: "documents",
                table: "DocumentInstances");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "documents",
                table: "DocumentVersions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Classification",
                schema: "documents",
                table: "DocumentVersions",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "ClassificationSource",
                schema: "documents",
                table: "DocumentVersions",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "ContentFingerprint",
                schema: "documents",
                table: "DocumentVersions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Labels",
                schema: "documents",
                table: "DocumentVersions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                schema: "documents",
                table: "DocumentVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StructuralSignature",
                schema: "documents",
                table: "DocumentVersions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "documents",
                table: "DocumentInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Classification",
                schema: "documents",
                table: "DocumentInstances",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "ClassificationSource",
                schema: "documents",
                table: "DocumentInstances",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "IntegritySignature",
                schema: "documents",
                table: "DocumentInstances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Labels",
                schema: "documents",
                table: "DocumentInstances",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProtectionsApplied",
                schema: "documents",
                table: "DocumentInstances",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                schema: "documents",
                table: "DocumentInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256Hash",
                schema: "documents",
                table: "DocumentInstances",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_ContentFingerprint",
                schema: "documents",
                table: "DocumentVersions",
                column: "ContentFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInstances_Sha256Hash",
                schema: "documents",
                table: "DocumentInstances",
                column: "Sha256Hash");
        }
    }
}
