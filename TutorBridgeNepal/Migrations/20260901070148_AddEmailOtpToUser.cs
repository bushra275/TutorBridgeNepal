using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOtpToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyWeeklyEarningsSummary",
                table: "TutorProfiles");

            migrationBuilder.AddColumn<string>(
                name: "EmailOtpCode",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailOtpExpiresAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailOtpCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmailOtpExpiresAt",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<bool>(
                name: "NotifyWeeklyEarningsSummary",
                table: "TutorProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
