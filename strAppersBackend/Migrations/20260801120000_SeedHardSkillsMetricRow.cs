using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <summary>
    /// Seeds the sentinel Metrics row (Id=-1, "HardSkills") that CacheMetrics hard-skills rows
    /// reference: per-sprint hard skills assessments are stored as MetricId=-1. The FK on
    /// CacheMetrics.MetricId requires the row to exist. Required=false keeps it out of
    /// assessment-config seeding/copying and the run-student-sprint batch loop; InstituteId is
    /// null so it belongs to no institute's catalog. Mirrors SeedSummaryMetricRow (Id=0).
    /// </summary>
    public partial class SeedHardSkillsMetricRow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Metrics",
                columns: new[] { "Id", "Name", "Required", "Influence" },
                values: new object[] { -1, "HardSkills", false, 3 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Metrics", keyColumn: "Id", keyValue: -1);
        }
    }
}
