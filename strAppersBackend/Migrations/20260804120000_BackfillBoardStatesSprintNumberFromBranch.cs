using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace strAppersBackend.Migrations
{
    /// <summary>
    /// Backfills BoardStates.SprintNumber from GithubBranch for rows written before the capture-time
    /// fix. Until then only the TestRunner writer set SprintNumber, so code reviews, PR validations,
    /// merges and push events were invisible to any per-sprint query (the assessment engine filters
    /// BoardStates by an exact SprintNumber match).
    ///
    /// Parsing follows the platform branch convention, same as ParseSprintNumber in MentorController:
    /// "{sprint}-B" / "{sprint}-F" / "{sprint}-B-{roleIndex}" -> sprint number; "Bugs-B" / "Bugs-F" -> 0.
    /// Rows whose branch does not match (deployment/runtime-error rows, frontend logs, "main",
    /// GitHub Pages source branches) are left null, which is correct — they are not sprint-scoped.
    ///
    /// The digit guard is capped at 6 characters so a malformed branch cannot overflow int.
    /// </summary>
    public partial class BackfillBoardStatesSprintNumberFromBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""BoardStates""
                SET ""SprintNumber"" = CASE
                        WHEN split_part(""GithubBranch"", '-', 1) ~ '^[0-9]{1,6}$'
                            THEN split_part(""GithubBranch"", '-', 1)::int
                        ELSE 0
                    END
                WHERE ""SprintNumber"" IS NULL
                  AND ""GithubBranch"" IS NOT NULL
                  AND ""GithubBranch"" <> ''
                  AND (
                        split_part(""GithubBranch"", '-', 1) ~ '^[0-9]{1,6}$'
                     OR lower(split_part(""GithubBranch"", '-', 1)) = 'bugs'
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally not reversed. Every writer that has a branch now populates SprintNumber at
            // capture time, so clearing the column would also destroy values written normally after this
            // migration ran — there is no way to tell backfilled rows from freshly written ones.
        }
    }
}
