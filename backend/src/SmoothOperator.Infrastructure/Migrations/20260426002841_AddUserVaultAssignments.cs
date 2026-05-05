using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoothOperator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVaultAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectionGroupUser",
                columns: table => new
                {
                    ConnectionGroupsId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsersId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionGroupUser", x => new { x.ConnectionGroupsId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_ConnectionGroupUser_ConnectionGroups_ConnectionGroupsId",
                        column: x => x.ConnectionGroupsId,
                        principalTable: "ConnectionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConnectionGroupUser_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionGroupUser_UsersId",
                table: "ConnectionGroupUser",
                column: "UsersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionGroupUser");
        }
    }
}
