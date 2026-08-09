using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorVerificationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Education",
                table: "TutorProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExperienceSummary",
                table: "TutorProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationDecidedAt",
                table: "TutorProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "TutorCredentials",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Education", table: "TutorProfiles");
            migrationBuilder.DropColumn(name: "ExperienceSummary", table: "TutorProfiles");
            migrationBuilder.DropColumn(name: "VerificationDecidedAt", table: "TutorProfiles");
            migrationBuilder.DropColumn(name: "DocumentType", table: "TutorCredentials");
        }
    }
}