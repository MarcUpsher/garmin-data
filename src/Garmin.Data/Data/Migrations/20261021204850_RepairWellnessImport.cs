using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garmin.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairWellnessImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "FloorsDescended",
                table: "DailyWellnessRecords",
                type: "REAL",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "FloorsAscended",
                table: "DailyWellnessRecords",
                type: "REAL",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyBatteryCharged",
                table: "DailyWellnessRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyBatteryDrained",
                table: "DailyWellnessRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyBatteryEndOfDay",
                table: "DailyWellnessRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyBatteryHigh",
                table: "DailyWellnessRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyBatteryLow",
                table: "DailyWellnessRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportVersion",
                table: "DailyWellnessRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyBatteryCharged",
                table: "DailyWellnessRecords");

            migrationBuilder.DropColumn(
                name: "BodyBatteryDrained",
                table: "DailyWellnessRecords");

            migrationBuilder.DropColumn(
                name: "BodyBatteryEndOfDay",
                table: "DailyWellnessRecords");

            migrationBuilder.DropColumn(
                name: "BodyBatteryHigh",
                table: "DailyWellnessRecords");

            migrationBuilder.DropColumn(
                name: "BodyBatteryLow",
                table: "DailyWellnessRecords");

            migrationBuilder.DropColumn(
                name: "ImportVersion",
                table: "DailyWellnessRecords");

            migrationBuilder.AlterColumn<int>(
                name: "FloorsDescended",
                table: "DailyWellnessRecords",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FloorsAscended",
                table: "DailyWellnessRecords",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true);
        }
    }
}
