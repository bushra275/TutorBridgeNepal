using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorSupportTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "StudentProfileId",
                table: "SupportTickets",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "TutorProfileId",
                table: "SupportTickets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TutorProfileId",
                table: "SupportTickets",
                column: "TutorProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportTickets_TutorProfiles_TutorProfileId",
                table: "SupportTickets",
                column: "TutorProfileId",
                principalTable: "TutorProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportTickets_TutorProfiles_TutorProfileId",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_TutorProfileId",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "TutorProfileId",
                table: "SupportTickets");

            migrationBuilder.AlterColumn<int>(
                name: "StudentProfileId",
                table: "SupportTickets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
