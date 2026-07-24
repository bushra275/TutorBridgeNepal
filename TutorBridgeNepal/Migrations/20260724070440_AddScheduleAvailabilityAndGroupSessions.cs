using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorBridgeNepal.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleAvailabilityAndGroupSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_TutorAvailabilitySlotId",
                table: "Bookings");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "TutorAvailabilitySlots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TutorTimeOffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorProfileId = table.Column<int>(type: "int", nullable: false),
                    StartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorTimeOffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorTimeOffs_TutorProfiles_TutorProfileId",
                        column: x => x.TutorProfileId,
                        principalTable: "TutorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorWeeklyAvailabilityRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorProfileId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsDayOff = table.Column<bool>(type: "bit", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorWeeklyAvailabilityRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorWeeklyAvailabilityRules_TutorProfiles_TutorProfileId",
                        column: x => x.TutorProfileId,
                        principalTable: "TutorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorAvailabilitySlotId",
                table: "Bookings",
                column: "TutorAvailabilitySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorTimeOffs_TutorProfileId",
                table: "TutorTimeOffs",
                column: "TutorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorWeeklyAvailabilityRules_TutorProfileId_DayOfWeek",
                table: "TutorWeeklyAvailabilityRules",
                columns: new[] { "TutorProfileId", "DayOfWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorTimeOffs");

            migrationBuilder.DropTable(
                name: "TutorWeeklyAvailabilityRules");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_TutorAvailabilitySlotId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "TutorAvailabilitySlots");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TutorAvailabilitySlotId",
                table: "Bookings",
                column: "TutorAvailabilitySlotId",
                unique: true);
        }
    }
}
