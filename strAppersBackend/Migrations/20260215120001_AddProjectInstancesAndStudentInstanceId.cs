using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectInstancesAndStudentInstanceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    InstanceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectInstances_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInstances_InstanceId",
                table: "ProjectInstances",
                column: "InstanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInstances_ProjectId",
                table: "ProjectInstances",
                column: "ProjectId");

            migrationBuilder.AddColumn<int>(
                name: "InstanceId",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstanceId",
                table: "Students",
                column: "InstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_ProjectInstances_InstanceId",
                table: "Students",
                column: "InstanceId",
                principalTable: "ProjectInstances",
                principalColumn: "InstanceId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_ProjectInstances_InstanceId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_InstanceId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InstanceId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "ProjectInstances");
        }
    }
}
