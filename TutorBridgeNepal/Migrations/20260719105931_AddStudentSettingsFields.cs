using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurriculumBoard",
                table: "StudentProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyNewMessages",
                table: "StudentProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyProgressUpdates",
                table: "StudentProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifySessionReminders",
                table: "StudentProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SchoolName",
                table: "StudentProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowProfileToTutors",
                table: "StudentProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SubjectsEnrolled",
                table: "StudentProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurriculumBoard",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "NotifyNewMessages",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "NotifyProgressUpdates",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "NotifySessionReminders",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SchoolName",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "ShowProfileToTutors",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SubjectsEnrolled",
                table: "StudentProfiles");
        }
    }
}
