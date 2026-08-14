using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIdentityAndFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-edited from the scaffolded output: EF's default diff (CLR type Document renamed
            // to DocumentInstance, table renamed to match) shows up as DropTable+CreateTable, which
            // would destroy every existing row. Renaming preserves the data (and, in SQL Server,
            // the FK from ConversionJobs keeps pointing at the same physical table automatically —
            // no need to drop/recreate it). Issued as raw SQL rather than migrationBuilder.RenameTable
            // specifically so it lands in its own batch/round-trip — SQL Server resolves object
            // names in a batch at parse time, so an ALTER TABLE referencing the *new* name later in
            // the same batch as the sp_rename fails with "cannot find the object", even though the
            // rename would have already run by then.
            migrationBuilder.Sql("EXEC sp_rename N'[documents].[Documents]', N'DocumentInstances';");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentVersionId",
                schema: "documents",
                table: "DocumentInstances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConvertedFromInstanceId",
                schema: "documents",
                table: "DocumentInstances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LogicalDocuments",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogicalDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LogicalDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ContentFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    StructuralSignature = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Classification = table.Column<byte>(type: "tinyint", nullable: false),
                    ClassificationSource = table.Column<byte>(type: "tinyint", nullable: false),
                    RiskScore = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Labels = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeTypeFromPrevious = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PreviousVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.Id);
                });

            // Backfill: every DocumentInstance that already existed before this migration becomes
            // its own brand-new LogicalDocument + a v1 DocumentVersion (ContentFingerprint left
            // NULL — it starts getting populated from the next real inspection/upload going
            // forward, not re-extracted retroactively; this is a dev environment with no
            // production documents to preserve fingerprint history for). Classification/RiskScore/
            // Category/Labels are copied from the instance's own already-set values, so nothing
            // about today's classifications changes.
            migrationBuilder.Sql(@"
                DECLARE @Backfill TABLE (InstanceId UNIQUEIDENTIFIER, LogicalId UNIQUEIDENTIFIER, VersionId UNIQUEIDENTIFIER);

                INSERT INTO @Backfill (InstanceId, LogicalId, VersionId)
                SELECT Id, NEWID(), NEWID() FROM [documents].[DocumentInstances];

                INSERT INTO [documents].[LogicalDocuments] (Id, OwnerId, CurrentVersionId, CreatedAtUtc)
                SELECT b.LogicalId, d.OwnerId, b.VersionId, d.CreatedAtUtc
                FROM @Backfill b
                JOIN [documents].[DocumentInstances] d ON d.Id = b.InstanceId;

                INSERT INTO [documents].[DocumentVersions]
                    (Id, LogicalDocumentId, VersionNumber, ContentFingerprint, StructuralSignature,
                     Classification, ClassificationSource, RiskScore, Category, Labels,
                     ChangeTypeFromPrevious, PreviousVersionId, CreatedByUserId, CreatedAtUtc)
                SELECT b.VersionId, b.LogicalId, 1, NULL, NULL,
                       d.Classification, d.ClassificationSource, d.RiskScore, d.Category, d.Labels,
                       'Initial', NULL, d.CreatedBy, d.CreatedAtUtc
                FROM @Backfill b
                JOIN [documents].[DocumentInstances] d ON d.Id = b.InstanceId;

                UPDATE d
                SET d.DocumentVersionId = b.VersionId
                FROM [documents].[DocumentInstances] d
                JOIN @Backfill b ON b.InstanceId = d.Id;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "DocumentVersionId",
                schema: "documents",
                table: "DocumentInstances",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInstances_ConvertedFromInstanceId",
                schema: "documents",
                table: "DocumentInstances",
                column: "ConvertedFromInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInstances_DocumentVersionId",
                schema: "documents",
                table: "DocumentInstances",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_ContentFingerprint",
                schema: "documents",
                table: "DocumentVersions",
                column: "ContentFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_LogicalDocumentId",
                schema: "documents",
                table: "DocumentVersions",
                column: "LogicalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_LogicalDocumentId_VersionNumber",
                schema: "documents",
                table: "DocumentVersions",
                columns: new[] { "LogicalDocumentId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentVersions",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "LogicalDocuments",
                schema: "documents");

            migrationBuilder.DropIndex(
                name: "IX_DocumentInstances_ConvertedFromInstanceId",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropIndex(
                name: "IX_DocumentInstances_DocumentVersionId",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "DocumentVersionId",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "ConvertedFromInstanceId",
                schema: "documents",
                table: "DocumentInstances");

            migrationBuilder.RenameTable(
                name: "DocumentInstances",
                schema: "documents",
                newName: "Documents");
        }
    }
}
