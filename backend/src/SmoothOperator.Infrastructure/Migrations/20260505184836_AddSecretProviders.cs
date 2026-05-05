using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoothOperator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecretProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalSecretName",
                table: "Credentials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSecretVersion",
                table: "Credentials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecretProviderId",
                table: "Credentials",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageMode",
                table: "Credentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SecretProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptedConfigJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecretProviders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Credentials_SecretProviderId",
                table: "Credentials",
                column: "SecretProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Credentials_SecretProviders_SecretProviderId",
                table: "Credentials",
                column: "SecretProviderId",
                principalTable: "SecretProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Credentials_SecretProviders_SecretProviderId",
                table: "Credentials");

            migrationBuilder.DropTable(
                name: "SecretProviders");

            migrationBuilder.DropIndex(
                name: "IX_Credentials_SecretProviderId",
                table: "Credentials");

            migrationBuilder.DropColumn(
                name: "ExternalSecretName",
                table: "Credentials");

            migrationBuilder.DropColumn(
                name: "ExternalSecretVersion",
                table: "Credentials");

            migrationBuilder.DropColumn(
                name: "SecretProviderId",
                table: "Credentials");

            migrationBuilder.DropColumn(
                name: "StorageMode",
                table: "Credentials");
        }
    }
}
