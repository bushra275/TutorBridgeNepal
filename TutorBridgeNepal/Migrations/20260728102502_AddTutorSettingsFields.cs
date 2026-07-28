using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoAcceptReturningStudents",
                table: "TutorProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CancellationWindowHours",
                table: "TutorProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeactivated",
                table: "TutorProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsListedInSearch",
                table: "TutorProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxSessionsPerDay",
                table: "TutorProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumBookingNoticeHours",
                table: "TutorProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyNewMessages",
                table: "TutorProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyNewSessionRequests",
                table: "TutorProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyWeeklyEarningsSummary",
                table: "TutorProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowAvailabilityBadge",
                table: "TutorProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoAcceptReturningStudents",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "CancellationWindowHours",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "IsDeactivated",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "IsListedInSearch",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "MaxSessionsPerDay",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "MinimumBookingNoticeHours",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "NotifyNewMessages",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "NotifyNewSessionRequests",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "NotifyWeeklyEarningsSummary",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "ShowAvailabilityBadge",
                table: "TutorProfiles");
        }
    }
}
