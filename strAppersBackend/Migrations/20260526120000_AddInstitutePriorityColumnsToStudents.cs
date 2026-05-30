using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutePriorityColumnsToStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstitutePriority1",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstitutePriority2",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstitutePriority3",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstitutePriority4",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstitutePriority1",
                table: "Students",
                column: "InstitutePriority1");

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstitutePriority2",
                table: "Students",
                column: "InstitutePriority2");

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstitutePriority3",
                table: "Students",
                column: "InstitutePriority3");

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstitutePriority4",
                table: "Students",
                column: "InstitutePriority4");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_InstituteProjects_InstitutePriority1",
                table: "Students",
                column: "InstitutePriority1",
                principalTable: "InstituteProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_InstituteProjects_InstitutePriority2",
                table: "Students",
                column: "InstitutePriority2",
                principalTable: "InstituteProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_InstituteProjects_InstitutePriority3",
                table: "Students",
                column: "InstitutePriority3",
                principalTable: "InstituteProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_InstituteProjects_InstitutePriority4",
                table: "Students",
                column: "InstitutePriority4",
                principalTable: "InstituteProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_InstituteProjects_InstitutePriority1",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_InstituteProjects_InstitutePriority2",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_InstituteProjects_InstitutePriority3",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_InstituteProjects_InstitutePriority4",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_InstitutePriority1",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_InstitutePriority2",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_InstitutePriority3",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_InstitutePriority4",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InstitutePriority1",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InstitutePriority2",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InstitutePriority3",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "InstitutePriority4",
                table: "Students");
        }
    }
}
