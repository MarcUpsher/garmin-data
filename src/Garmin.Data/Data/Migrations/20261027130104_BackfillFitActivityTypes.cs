using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmin.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillFitActivityTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Activities
                SET ActivityType = CASE
                    WHEN Sport = 0 THEN 'other'
                    WHEN Sport = 1 THEN 'running'
                    WHEN Sport = 2 THEN 'cycling'
                    WHEN Sport = 5 THEN 'swimming'
                    WHEN Sport = 10 AND SubSport = 20 THEN 'strength_training'
                    WHEN Sport = 10 AND SubSport = 26 THEN 'indoor_cardio'
                    WHEN Sport = 11 AND SubSport = 30 THEN 'casual_walking'
                    WHEN Sport = 11 THEN 'walking'
                    END
                WHERE SourceKind IN ('FIT', 'FITArchive')
                  AND (ActivityType IS NULL OR trim(ActivityType) = '')
                  AND (Sport IN (0, 1, 2, 5, 11)
                       OR (Sport = 10 AND SubSport IN (20, 26)))
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not erase classifications that may have been edited after this migration.
        }
    }
}
