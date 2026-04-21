using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public class AddInstituteTemplateNameAndRoleTemplateFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "InstituteTemplates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TemplateId",
                table: "InstituteRoles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstituteRoles_TemplateId",
                table: "InstituteRoles",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_InstituteRoles_InstituteTemplates_TemplateId",
                table: "InstituteRoles",
                column: "TemplateId",
                principalTable: "InstituteTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InstituteRoles_InstituteTemplates_TemplateId",
                table: "InstituteRoles");

            migrationBuilder.DropIndex(
                name: "IX_InstituteRoles_TemplateId",
                table: "InstituteRoles");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "InstituteRoles");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "InstituteTemplates");
        }
    }
}
