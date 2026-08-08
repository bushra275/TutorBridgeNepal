using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialFilePathAndUploadedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "TutorCredentials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                table: "TutorCredentials",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "TutorCredentials");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "TutorCredentials");
        }
    }
}
