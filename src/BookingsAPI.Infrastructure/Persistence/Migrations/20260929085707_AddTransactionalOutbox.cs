using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookingsAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionalOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_publication_locked_until",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "publication_locked_until",
                table: "bookings");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_booking_id_id",
                table: "outbox_messages",
                columns: new[] { "booking_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_published_at_locked_until",
                table: "outbox_messages",
                columns: new[] { "published_at", "locked_until" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.AddColumn<DateTime>(
                name: "publication_locked_until",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_publication_locked_until",
                table: "bookings",
                column: "publication_locked_until");
        }
    }
}
