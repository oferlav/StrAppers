using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <summary>
    /// Seeds the sentinel Metrics row (Id=0, "SprintSummary") that CacheMetrics summary rows reference:
    /// per-sprint summaries are stored as MetricId=0, and the course-level summary as MetricId=0 +
    /// SprintNumber=-1. The FK on CacheMetrics.MetricId requires the row to exist. Required=false keeps
    /// it out of assessment-config seeding/copying and the run-student-sprint batch loop; InstituteId is
    /// null so it belongs to no institute's catalog.
    /// </summary>
    public partial class SeedSummaryMetricRow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Metrics",
                columns: new[] { "Id", "Name", "Required", "Influence" },
                values: new object[] { 0, "SprintSummary", false, 3 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Metrics", keyColumn: "Id", keyValue: 0);
        }
    }
}
