using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmin.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActivityTitle",
                table: "Activities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("UPDATE Activities SET ActivityTitle = (SELECT json_extract(CAST(SourceBytes AS TEXT), '$.Title') FROM ActivitySourceFiles WHERE ActivitySourceFiles.ActivityId = Activities.Id) WHERE SourceKind = 'GarminConnectCsv';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityTitle",
                table: "Activities");
        }
    }
}
