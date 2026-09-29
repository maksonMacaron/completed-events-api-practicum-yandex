using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCatalogAndPublicationLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmed_at",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "publication_locked_until",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE bookings
                SET confirmed_at = COALESCE(confirmation_published_at, processed_at)
                WHERE status = 'Confirmed'
                   OR (status = 'Cancelled' AND seat_release_required)
                """);

            migrationBuilder.Sql(
                """
                UPDATE bookings
                SET cancelled_at = processed_at
                WHERE status = 'Cancelled'
                """);

            migrationBuilder.CreateTable(
                name: "known_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_known_events", x => x.event_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_publication_locked_until",
                table: "bookings",
                column: "publication_locked_until");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "known_events");

            migrationBuilder.DropIndex(
                name: "IX_bookings_publication_locked_until",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "publication_locked_until",
                table: "bookings");
        }
    }
}
