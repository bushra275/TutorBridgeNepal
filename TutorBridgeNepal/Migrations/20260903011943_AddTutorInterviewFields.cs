using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorInterviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InterviewMeetingLink",
                table: "TutorProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InterviewScheduledAt",
                table: "TutorProfiles",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterviewMeetingLink",
                table: "TutorProfiles");

            migrationBuilder.DropColumn(
                name: "InterviewScheduledAt",
                table: "TutorProfiles");
        }
    }
}
