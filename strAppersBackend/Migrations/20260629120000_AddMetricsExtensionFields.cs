using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricsExtensionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Metrics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Metrics",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Required",
                table: "Metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Influence",
                table: "Metrics",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "Skill",
                table: "Metrics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AIExpertise",
                table: "Metrics",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstituteId",
                table: "Metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metrics_InstituteId",
                table: "Metrics",
                column: "InstituteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Metrics_Institutes_InstituteId",
                table: "Metrics",
                column: "InstituteId",
                principalTable: "Institutes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Metrics_Institutes_InstituteId",
                table: "Metrics");

            migrationBuilder.DropIndex(
                name: "IX_Metrics_InstituteId",
                table: "Metrics");

            migrationBuilder.DropColumn(name: "Description",  table: "Metrics");
            migrationBuilder.DropColumn(name: "Category",     table: "Metrics");
            migrationBuilder.DropColumn(name: "Required",     table: "Metrics");
            migrationBuilder.DropColumn(name: "Influence",    table: "Metrics");
            migrationBuilder.DropColumn(name: "Skill",        table: "Metrics");
            migrationBuilder.DropColumn(name: "AIExpertise",  table: "Metrics");
            migrationBuilder.DropColumn(name: "InstituteId",  table: "Metrics");
        }
    }
}
