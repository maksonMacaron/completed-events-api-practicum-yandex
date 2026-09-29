using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processed_booking_cancellations",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_booking_cancellations", x => x.booking_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_booking_cancellations");
        }
    }
}
