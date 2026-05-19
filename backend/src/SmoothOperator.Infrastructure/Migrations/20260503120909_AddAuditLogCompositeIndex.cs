using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoothOperator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogCompositeIndex : Migration
    {
        private static readonly string[] _compositeIndexColumns = ["UserId", "Action", "ResourceId", "Timestamp"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Action_ResourceId_Timestamp",
                table: "AuditLogs",
                columns: _compositeIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId_Action_ResourceId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");
        }
    }
}
