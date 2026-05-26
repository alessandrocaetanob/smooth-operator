using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoothOperator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecordingIncludeKeys",
                table: "Connections",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecordingOverride",
                table: "Connections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RecordingEnabled",
                table: "ConnectionGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecordingIncludeKeys",
                table: "ConnectionGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecordingRetentionDays",
                table: "ConnectionGroups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Recordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    StorageType = table.Column<int>(type: "integer", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    IncludeKeys = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recordings_Connections_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "Connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recordings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RecordingStorageSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageType = table.Column<int>(type: "integer", nullable: false),
                    LocalPath = table.Column<string>(type: "text", nullable: false),
                    S3Bucket = table.Column<string>(type: "text", nullable: true),
                    S3Region = table.Column<string>(type: "text", nullable: true),
                    S3Endpoint = table.Column<string>(type: "text", nullable: true),
                    S3AccessKeyId = table.Column<string>(type: "text", nullable: true),
                    EncryptedS3SecretAccessKey = table.Column<string>(type: "text", nullable: true),
                    S3PathPrefix = table.Column<string>(type: "text", nullable: true),
                    S3ForcePathStyle = table.Column<bool>(type: "boolean", nullable: false),
                    AzureAccountName = table.Column<string>(type: "text", nullable: true),
                    AzureContainerName = table.Column<string>(type: "text", nullable: true),
                    EncryptedAzureAccountKey = table.Column<string>(type: "text", nullable: true),
                    AzureBlobEndpoint = table.Column<string>(type: "text", nullable: true),
                    AzurePathPrefix = table.Column<string>(type: "text", nullable: true),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordingStorageSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_ConnectionId_StartedAt",
                table: "Recordings",
                columns: new[] { "ConnectionId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_SessionId",
                table: "Recordings",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_Status_EndedAt",
                table: "Recordings",
                columns: new[] { "Status", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_UserId",
                table: "Recordings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recordings");

            migrationBuilder.DropTable(
                name: "RecordingStorageSettings");

            migrationBuilder.DropColumn(
                name: "RecordingIncludeKeys",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "RecordingOverride",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "RecordingEnabled",
                table: "ConnectionGroups");

            migrationBuilder.DropColumn(
                name: "RecordingIncludeKeys",
                table: "ConnectionGroups");

            migrationBuilder.DropColumn(
                name: "RecordingRetentionDays",
                table: "ConnectionGroups");
        }
    }
}
