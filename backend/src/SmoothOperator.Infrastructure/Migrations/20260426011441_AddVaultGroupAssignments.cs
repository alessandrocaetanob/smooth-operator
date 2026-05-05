using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoothOperator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultGroupAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectionGroupUserGroup",
                columns: table => new
                {
                    GroupsId = table.Column<Guid>(type: "uuid", nullable: false),
                    VaultsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionGroupUserGroup", x => new { x.GroupsId, x.VaultsId });
                    table.ForeignKey(
                        name: "FK_ConnectionGroupUserGroup_ConnectionGroups_VaultsId",
                        column: x => x.VaultsId,
                        principalTable: "ConnectionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConnectionGroupUserGroup_UserGroups_GroupsId",
                        column: x => x.GroupsId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionGroupUserGroup_VaultsId",
                table: "ConnectionGroupUserGroup",
                column: "VaultsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionGroupUserGroup");
        }
    }
}
