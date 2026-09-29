using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_published_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "seat_release_required",
                table: "bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_status_cancellation_published_at",
                table: "bookings",
                columns: new[] { "status", "cancellation_published_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_status_cancellation_published_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "cancellation_published_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "seat_release_required",
                table: "bookings");
        }
    }
}
