using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddJoinRequestsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JoinRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChannelId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    StudentEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StudentFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StudentLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    JoinDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Added = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AddedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_Added",
                table: "JoinRequests",
                column: "Added");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_ChannelId",
                table: "JoinRequests",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_JoinDate",
                table: "JoinRequests",
                column: "JoinDate");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_ProjectId",
                table: "JoinRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_StudentEmail",
                table: "JoinRequests",
                column: "StudentEmail");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_StudentId",
                table: "JoinRequests",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JoinRequests");
        }
    }
}





