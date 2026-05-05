using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoothOperator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialPublicKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicKey",
                table: "Credentials",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicKey",
                table: "Credentials");
        }
    }
}
