using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoothOperator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGroupMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "UserGroups",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "UserGroups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_OwnerUserId",
                table: "UserGroups",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserGroups_Users_OwnerUserId",
                table: "UserGroups",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserGroups_Users_OwnerUserId",
                table: "UserGroups");

            migrationBuilder.DropIndex(
                name: "IX_UserGroups_OwnerUserId",
                table: "UserGroups");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "UserGroups");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "UserGroups");
        }
    }
}
