using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class MakeRolesInstituteIdNullableForGlobalRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the existing FK (was NOT NULL, references Institutes.Id)
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Institutes_InstituteId",
                table: "Roles");

            // Make InstituteId nullable — global/B2C roles will use NULL
            migrationBuilder.AlterColumn<int>(
                name: "InstituteId",
                table: "Roles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: false,
                oldDefaultValue: 1);

            // All existing InstituteId=1 rows are global/B2C roles → set to NULL.
            // Institute-1-specific rows don't exist yet (no one has saved institute-1
            // customisations) so this is safe to run unconditionally.
            migrationBuilder.Sql(@"UPDATE ""Roles"" SET ""InstituteId"" = NULL WHERE ""InstituteId"" = 1;");

            // Re-add FK as optional (null = no parent = global role)
            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Institutes_InstituteId",
                table: "Roles",
                column: "InstituteId",
                principalTable: "Institutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Institutes_InstituteId",
                table: "Roles");

            // Restore NULL → 1 before making NOT NULL
            migrationBuilder.Sql(@"UPDATE ""Roles"" SET ""InstituteId"" = 1 WHERE ""InstituteId"" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "InstituteId",
                table: "Roles",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Institutes_InstituteId",
                table: "Roles",
                column: "InstituteId",
                principalTable: "Institutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
