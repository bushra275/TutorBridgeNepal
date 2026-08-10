using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionModeAndDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "TutorAvailabilitySlots",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BookingId",
                table: "SupportTickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDisputed",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_BookingId",
                table: "SupportTickets",
                column: "BookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportTickets_Bookings_BookingId",
                table: "SupportTickets",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportTickets_Bookings_BookingId",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_BookingId",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "TutorAvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "IsDisputed",
                table: "Bookings");
        }
    }
}
