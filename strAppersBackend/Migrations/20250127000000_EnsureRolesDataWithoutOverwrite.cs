using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class EnsureRolesDataWithoutOverwrite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sync Roles data to match dev database exactly
            // This migration will update existing records to match dev, then data stays static
            migrationBuilder.Sql(@"
                INSERT INTO ""Roles"" (""Id"", ""Name"", ""Description"", ""Category"", ""Type"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                VALUES 
                    (1, 'Product Manager', 'Leads product planning and execution', 'Leadership', 0, true, TIMESTAMP '2025-08-04 20:54:12.981445+03', NULL),
                    (2, 'Frontend Developer', 'Develops user interface and user experience', 'Technical', 2, true, TIMESTAMP '2025-08-04 20:54:12.981445+03', NULL),
                    (3, 'Backend Developer', 'Develops server-side logic and database integration', 'Technical', 2, true, TIMESTAMP '2025-08-04 20:54:12.981445+03', NULL),
                    (4, 'UI/UX Designer', 'Designs user interface and user experience', 'Technical', 3, true, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
                    (5, 'Quality Assurance', 'Tests software and ensures quality standards', 'Technical', 0, false, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
                    (6, 'Full Stack Developer', 'Develop backend + UI', 'Leadership', 1, true, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
                    (7, 'Marketing', 'Conducts research and Market analysis. Responsible for Media', 'Academic', 0, true, TIMESTAMP '2025-08-04 20:54:12.981446+03', NULL),
                    (8, 'Documentation Specialist', 'Creates and maintains project documentation', 'Administrative', 0, false, TIMESTAMP '2025-08-04 20:54:12.981447+03', NULL)
                ON CONFLICT (""Id"") DO UPDATE SET
                    ""Name"" = EXCLUDED.""Name"",
                    ""Description"" = EXCLUDED.""Description"",
                    ""Category"" = EXCLUDED.""Category"",
                    ""Type"" = EXCLUDED.""Type"",
                    ""IsActive"" = EXCLUDED.""IsActive"",
                    ""CreatedAt"" = EXCLUDED.""CreatedAt"",
                    ""UpdatedAt"" = EXCLUDED.""UpdatedAt"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: We don't want to delete Roles data when rolling back
            // This migration only ensures data exists, it doesn't create it
        }
    }
}

