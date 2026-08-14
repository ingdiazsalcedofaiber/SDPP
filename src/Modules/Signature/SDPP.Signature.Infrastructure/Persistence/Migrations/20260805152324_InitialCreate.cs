using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDPP.Signature.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "signature");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "signature",
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
                name: "SavedSignatures",
                schema: "signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    AspectRatio = table.Column<double>(type: "float", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSignatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignatureEnvelopes",
                schema: "signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SigningMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginalSha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FinalDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalDocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalSha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureEnvelopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignerAccessChallenges",
                schema: "signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LinkExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OtpCodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OtpExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpFailedAttempts = table.Column<int>(type: "int", nullable: false),
                    SessionTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SessionTokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignerAccessChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnvelopeFields",
                schema: "signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    PositionX = table.Column<double>(type: "float", nullable: false),
                    PositionY = table.Column<double>(type: "float", nullable: false),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SignatureImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SignatureMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FilledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvelopeFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvelopeFields_SignatureEnvelopes_EnvelopeId",
                        column: x => x.EnvelopeId,
                        principalSchema: "signature",
                        principalTable: "SignatureEnvelopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvelopeRecipients",
                schema: "signature",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MatchedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ViewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeclinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeclineReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvelopeRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvelopeRecipients_SignatureEnvelopes_EnvelopeId",
                        column: x => x.EnvelopeId,
                        principalSchema: "signature",
                        principalTable: "SignatureEnvelopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnvelopeFields_EnvelopeId",
                schema: "signature",
                table: "EnvelopeFields",
                column: "EnvelopeId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvelopeFields_RecipientId",
                schema: "signature",
                table: "EnvelopeFields",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvelopeRecipients_EnvelopeId",
                schema: "signature",
                table: "EnvelopeRecipients",
                column: "EnvelopeId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvelopeRecipients_MatchedUserId",
                schema: "signature",
                table: "EnvelopeRecipients",
                column: "MatchedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSignatures_UserId",
                schema: "signature",
                table: "SavedSignatures",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureEnvelopes_CreatedByUserId",
                schema: "signature",
                table: "SignatureEnvelopes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureEnvelopes_DueDateUtc",
                schema: "signature",
                table: "SignatureEnvelopes",
                column: "DueDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureEnvelopes_Status",
                schema: "signature",
                table: "SignatureEnvelopes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SignerAccessChallenges_RecipientId",
                schema: "signature",
                table: "SignerAccessChallenges",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_SignerAccessChallenges_TokenHash",
                schema: "signature",
                table: "SignerAccessChallenges",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnvelopeFields",
                schema: "signature");

            migrationBuilder.DropTable(
                name: "EnvelopeRecipients",
                schema: "signature");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "signature");

            migrationBuilder.DropTable(
                name: "SavedSignatures",
                schema: "signature");

            migrationBuilder.DropTable(
                name: "SignerAccessChallenges",
                schema: "signature");

            migrationBuilder.DropTable(
                name: "SignatureEnvelopes",
                schema: "signature");
        }
    }
}
