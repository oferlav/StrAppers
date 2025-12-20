using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <inheritdoc />
    public partial class ChangeProjectBoardObservedToInteger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert boolean Observed column to integer
            // Note: This migration assumes the database change will be done manually
            // If false -> 0, if true -> 1 (or keep existing count if already converted)
            
            migrationBuilder.Sql(@"
                -- For PostgreSQL
                ALTER TABLE ""ProjectBoards"" 
                ALTER COLUMN ""Observed"" TYPE INTEGER 
                USING CASE 
                    WHEN ""Observed"" = true THEN 1 
                    WHEN ""Observed"" = false THEN 0 
                    ELSE ""Observed""::INTEGER 
                END;
                
                -- Set default value
                ALTER TABLE ""ProjectBoards"" 
                ALTER COLUMN ""Observed"" SET DEFAULT 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert back to boolean
            migrationBuilder.Sql(@"
                -- For PostgreSQL
                ALTER TABLE ""ProjectBoards"" 
                ALTER COLUMN ""Observed"" TYPE BOOLEAN 
                USING CASE 
                    WHEN ""Observed"" > 0 THEN true 
                    ELSE false 
                END;
                
                -- Set default value
                ALTER TABLE ""ProjectBoards"" 
                ALTER COLUMN ""Observed"" SET DEFAULT false;
            ");
        }
    }
}




