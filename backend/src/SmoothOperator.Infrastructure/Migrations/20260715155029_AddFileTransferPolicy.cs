using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoothOperator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileTransferPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FileTransferPolicyOverride",
                table: "Connections",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FileTransferPolicy",
                table: "ConnectionGroups",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileTransferPolicyOverride",
                table: "Connections");

            migrationBuilder.DropColumn(
                name: "FileTransferPolicy",
                table: "ConnectionGroups");
        }
    }
}
