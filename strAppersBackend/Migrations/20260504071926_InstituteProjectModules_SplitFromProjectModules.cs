using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class InstituteProjectModules_SplitFromProjectModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstituteProjectModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstituteProjectId = table.Column<int>(type: "integer", nullable: false),
                    ModuleType = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: true),
                    OriginalModuleId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstituteProjectModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstituteProjectModules_InstituteProjects_InstituteProjectId",
                        column: x => x.InstituteProjectId,
                        principalTable: "InstituteProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstituteProjectModules_ModuleTypes_ModuleType",
                        column: x => x.ModuleType,
                        principalTable: "ModuleTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstituteProjectModules_InstituteProjectId",
                table: "InstituteProjectModules",
                column: "InstituteProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteProjectModules_InstituteProjectId_OriginalModuleId",
                table: "InstituteProjectModules",
                columns: new[] { "InstituteProjectId", "OriginalModuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_InstituteProjectModules_ModuleType",
                table: "InstituteProjectModules",
                column: "ModuleType");

            migrationBuilder.CreateIndex(
                name: "IX_InstituteProjectModules_Sequence",
                table: "InstituteProjectModules",
                column: "Sequence");

            // Preserve module Ids so TrelloBoardJson ModuleId references stay valid.
            migrationBuilder.Sql(
                """
                INSERT INTO "InstituteProjectModules" ("Id", "InstituteProjectId", "ModuleType", "Title", "Description", "Sequence", "OriginalModuleId")
                SELECT "Id", "InstituteProjectId", "ModuleType", "Title", "Description", "Sequence", "OriginalModuleId"
                FROM "ProjectModules"
                WHERE "InstituteProjectId" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                SELECT setval(
                  pg_get_serial_sequence('"InstituteProjectModules"', 'Id'),
                  GREATEST((SELECT COALESCE(MAX("Id"), 0) FROM "InstituteProjectModules"), 1)
                );
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "ProjectModules" WHERE "InstituteProjectId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectModules_InstituteProjects_InstituteProjectId",
                table: "ProjectModules");

            migrationBuilder.DropIndex(
                name: "IX_ProjectModules_InstituteProjectId",
                table: "ProjectModules");

            migrationBuilder.DropColumn(
                name: "InstituteProjectId",
                table: "ProjectModules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstituteProjectId",
                table: "ProjectModules",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "ProjectModules" ("Id", "ProjectId", "ModuleType", "Title", "Description", "Sequence", "OriginalModuleId", "InstituteProjectId")
                SELECT "Id", NULL, "ModuleType", "Title", "Description", "Sequence", "OriginalModuleId", "InstituteProjectId"
                FROM "InstituteProjectModules";
                """);

            migrationBuilder.Sql(
                """
                SELECT setval(
                  pg_get_serial_sequence('"ProjectModules"', 'Id'),
                  GREATEST((SELECT COALESCE(MAX("Id"), 0) FROM "ProjectModules"), 1)
                );
                """);

            migrationBuilder.DropTable(
                name: "InstituteProjectModules");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModules_InstituteProjectId",
                table: "ProjectModules",
                column: "InstituteProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectModules_InstituteProjects_InstituteProjectId",
                table: "ProjectModules",
                column: "InstituteProjectId",
                principalTable: "InstituteProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
