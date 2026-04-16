using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricsAndCacheMetricsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Metrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CacheMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BoardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SprintNumber = table.Column<int>(type: "integer", nullable: false),
                    MetricId = table.Column<int>(type: "integer", nullable: false),
                    ReviewContent = table.Column<string>(type: "text", nullable: false),
                    Graph = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacheMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CacheMetrics_Metrics_MetricId",
                        column: x => x.MetricId,
                        principalTable: "Metrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CacheMetrics_ProjectBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "ProjectBoards",
                        principalColumn: "BoardId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CacheMetrics_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Metrics",
                columns: new[] { "Id", "Endpoint", "Name" },
                values: new object[,]
                {
                    { 1, null, "Adherence" },
                    { 2, null, "GapAnalysis" },
                    { 3, null, "Improvement" },
                    { 4, null, "Communication" },
                    { 5, null, "Attendance" },
                    { 6, null, "Strengths&weaknesses" },
                    { 7, null, "CustomerEngagement" }
                });

            migrationBuilder.Sql(
                """
                SELECT setval(
                    pg_get_serial_sequence('"Metrics"', 'Id'),
                    GREATEST((SELECT COALESCE(MAX("Id"), 1) FROM "Metrics"), 1));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CacheMetrics_BoardId",
                table: "CacheMetrics",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_CacheMetrics_MetricId",
                table: "CacheMetrics",
                column: "MetricId");

            migrationBuilder.CreateIndex(
                name: "IX_CacheMetrics_StudentId",
                table: "CacheMetrics",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CacheMetrics");

            migrationBuilder.DropTable(
                name: "Metrics");
        }
    }
}
