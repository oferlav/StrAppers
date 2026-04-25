using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillsAndRoleSkillColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.AddColumn<bool>(
                name: "CustomerEngagement",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SkillId",
                table: "Roles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CustomerEngagement",
                table: "InstituteRoles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SkillId",
                table: "InstituteRoles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_SkillId",
                table: "Roles",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteRoles_SkillId",
                table: "InstituteRoles",
                column: "SkillId");

            migrationBuilder.AddForeignKey(
                name: "FK_InstituteRoles_Skills_SkillId",
                table: "InstituteRoles",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Skills_SkillId",
                table: "Roles",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstituteRoles_Skills_SkillId",
                table: "InstituteRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Skills_SkillId",
                table: "Roles");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Roles_SkillId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_InstituteRoles_SkillId",
                table: "InstituteRoles");

            migrationBuilder.DropColumn(
                name: "CustomerEngagement",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "SkillId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "CustomerEngagement",
                table: "InstituteRoles");

            migrationBuilder.DropColumn(
                name: "SkillId",
                table: "InstituteRoles");
        }
    }
}
