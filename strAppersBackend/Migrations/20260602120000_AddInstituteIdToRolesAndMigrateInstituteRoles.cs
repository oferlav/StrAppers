using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddInstituteIdToRolesAndMigrateInstituteRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Step 1: Add new columns to Roles ─────────────────────────────────

            migrationBuilder.AddColumn<int>(
                name: "InstituteId",
                table: "Roles",
                type: "integer",
                nullable: false,
                defaultValue: 1);          // All existing B2C rows get InstituteId = 1

            migrationBuilder.AddColumn<int>(
                name: "SquadId",
                table: "Roles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTechnical",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Competencies",
                table: "Roles",
                type: "text",
                nullable: true);

            // ── Step 2: Indexes ───────────────────────────────────────────────────

            migrationBuilder.CreateIndex(
                name: "IX_Roles_InstituteId",
                table: "Roles",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_SquadId",
                table: "Roles",
                column: "SquadId");

            // ── Step 3: Foreign key constraints ──────────────────────────────────

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Institutes_InstituteId",
                table: "Roles",
                column: "InstituteId",
                principalTable: "Institutes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_InstituteSquads_SquadId",
                table: "Roles",
                column: "SquadId",
                principalTable: "InstituteSquads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── Step 4: Copy InstituteRoles (InstituteId <> 1) into Roles ─────────
            //
            // These are institute-specific base roles (TemplateId is always null
            // in production — confirmed). SquadId is set to NULL because these are
            // base institute roles, not squad-scoped.
            //
            // The new Roles rows get fresh auto-increment Ids; the original
            // InstituteRoles rows are left untouched until Phase 9 drops the table.

            migrationBuilder.Sql(@"
                INSERT INTO ""Roles"" (
                    ""Name"",
                    ""Description"",
                    ""Competencies"",
                    ""Category"",
                    ""Type"",
                    ""SkillId"",
                    ""CustomerEngagement"",
                    ""IsTechnical"",
                    ""IsActive"",
                    ""InstituteId"",
                    ""SquadId"",
                    ""CreatedAt"",
                    ""UpdatedAt""
                )
                SELECT
                    ir.""Name"",
                    ir.""Description"",
                    ir.""Competencies"",
                    ir.""Category"",
                    ir.""Type"",
                    ir.""SkillId"",
                    ir.""CustomerEngagement"",
                    ir.""IsTechnical"",
                    ir.""IsActive"",
                    ir.""InstituteId"",
                    NULL,               -- SquadId: base institute roles have no squad
                    ir.""CreatedAt"",
                    ir.""UpdatedAt""
                FROM ""InstituteRoles"" ir
                WHERE ir.""InstituteId"" <> 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove FK constraints first
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Institutes_InstituteId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_InstituteSquads_SquadId",
                table: "Roles");

            // Remove indexes
            migrationBuilder.DropIndex(
                name: "IX_Roles_InstituteId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_SquadId",
                table: "Roles");

            // Remove migrated rows (rows that came from InstituteRoles — InstituteId <> 1)
            migrationBuilder.Sql(@"
                DELETE FROM ""Roles"" WHERE ""InstituteId"" <> 1;
            ");

            // Drop new columns
            migrationBuilder.DropColumn(name: "InstituteId", table: "Roles");
            migrationBuilder.DropColumn(name: "SquadId",      table: "Roles");
            migrationBuilder.DropColumn(name: "IsTechnical",  table: "Roles");
            migrationBuilder.DropColumn(name: "Competencies", table: "Roles");
        }
    }
}
