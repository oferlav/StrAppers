using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTestResultsToBoardStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastTestStatus",
                table: "BoardStates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastTestOutput",
                table: "BoardStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTestRunDate",
                table: "BoardStates",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTestStatus",
                table: "BoardStates");

            migrationBuilder.DropColumn(
                name: "LastTestOutput",
                table: "BoardStates");

            migrationBuilder.DropColumn(
                name: "LastTestRunDate",
                table: "BoardStates");
        }
    }
}
