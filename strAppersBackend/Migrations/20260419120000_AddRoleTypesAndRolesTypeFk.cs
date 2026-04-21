using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleTypesAndRolesTypeFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RoleTypes",
                columns: new[] { "Id", "Description" },
                values: new object[,]
                {
                    { 0, "Default" },
                    { 1, "bundle" },
                    { 2, "bundle" },
                    { 3, "Required" },
                    { 4, "leadership" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Type",
                table: "Roles",
                column: "Type");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_RoleTypes_Type",
                table: "Roles",
                column: "Type",
                principalTable: "RoleTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_RoleTypes_Type",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Type",
                table: "Roles");

            migrationBuilder.DropTable(
                name: "RoleTypes");
        }
    }
}
