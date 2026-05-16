using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmoothOperator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrigramSearchIndexes : Migration
    {
        // Expression-based GIN trigram indexes. They match the LOWER(col) LIKE '%term%'
        // SQL that EF emits for the audit-log / user .ToLower().Contains() filters, so
        // those case-insensitive substring searches use an index instead of a sequential
        // scan. Defined as raw SQL because the LOWER(col) expression cannot be expressed
        // through the EF fluent API; they are therefore not tracked in the model snapshot.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_AuditLogs_Action_Lower_Trgm\" " +
                "ON \"AuditLogs\" USING gin (lower(\"Action\") gin_trgm_ops);");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Users_Email_Lower_Trgm\" " +
                "ON \"Users\" USING gin (lower(\"Email\") gin_trgm_ops);");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Users_Name_Lower_Trgm\" " +
                "ON \"Users\" USING gin (lower(\"Name\") gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_AuditLogs_Action_Lower_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Users_Email_Lower_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Users_Name_Lower_Trgm\";");
        }
    }
}
