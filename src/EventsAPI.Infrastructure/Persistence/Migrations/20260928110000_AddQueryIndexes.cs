using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsAPI.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260928110000_AddQueryIndexes")]
public partial class AddQueryIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_bookings_status",
            table: "bookings",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_events_end_at",
            table: "events",
            column: "end_at");

        migrationBuilder.CreateIndex(
            name: "IX_events_start_at",
            table: "events",
            column: "start_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_bookings_status",
            table: "bookings");

        migrationBuilder.DropIndex(
            name: "IX_events_end_at",
            table: "events");

        migrationBuilder.DropIndex(
            name: "IX_events_start_at",
            table: "events");
    }
}
