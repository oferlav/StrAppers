using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <summary>
    /// Widens Mission and OneLiner from varchar(100) to varchar(250) for databases that applied
    /// AddMissionAndOneLinerToProjects before it was updated to 250, or as a no-op widen on fresh DBs.
    /// </summary>
    public partial class AlterProjectMissionOneLinerToVarchar250 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Mission",
                table: "Projects",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OneLiner",
                table: "Projects",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Mission",
                table: "Projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OneLiner",
                table: "Projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
