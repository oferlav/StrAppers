using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class InstituteProjects_Description_ToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""InstituteProjects"" ALTER COLUMN ""Description"" TYPE text;");
            migrationBuilder.Sql(
                @"ALTER TABLE ""InstituteProjects"" ALTER COLUMN ""Mission"" TYPE text;");
            migrationBuilder.Sql(
                @"ALTER TABLE ""InstituteProjects"" ALTER COLUMN ""ShortBrief"" TYPE text;");
            migrationBuilder.Sql(
                @"ALTER TABLE ""InstituteProjects"" ALTER COLUMN ""SystemDesignFormatted"" TYPE text;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""InstituteProjects"" ALTER COLUMN ""Description"" TYPE character varying(1000) USING LEFT(""Description"", 1000);");
            migrationBuilder.Sql(
                @"ALTER TABLE ""InstituteProjects"" ALTER COLUMN ""Mission"" TYPE character varying(2000) USING LEFT(""Mission"", 2000);");
            migrationBuilder.Sql(
                @"ALTER TABLE ""InstituteProjects"" ALTER COLUMN ""ShortBrief"" TYPE character varying(2000) USING LEFT(""ShortBrief"", 2000);");
            migrationBuilder.Sql(
                @"ALTER TABLE ""InstituteProjects"" ALTER COLUMN ""SystemDesignFormatted"" TYPE character varying(2000) USING LEFT(""SystemDesignFormatted"", 2000);");
        }
    }
}
