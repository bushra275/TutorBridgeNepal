using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InterviewCompletedAt",
                table: "TutorProfiles",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterviewCompletedAt",
                table: "TutorProfiles");
        }
    }
}
