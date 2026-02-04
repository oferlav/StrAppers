using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveToProjectCriterias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "ProjectCriterias",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Existing criteria remain visible in GET and classification until explicitly deactivated
            migrationBuilder.Sql("UPDATE \"ProjectCriterias\" SET \"Active\" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Active",
                table: "ProjectCriterias");
        }
    }
}
