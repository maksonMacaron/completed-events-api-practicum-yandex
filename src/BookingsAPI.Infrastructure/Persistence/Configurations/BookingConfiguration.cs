using BookingsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingsAPI.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");
        builder.HasKey(booking => booking.Id);
        builder.HasIndex(booking => booking.UserId);
        builder.HasIndex(booking => new { booking.Status, booking.ConfirmationPublishedAt });

        builder.Property(booking => booking.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(booking => booking.EventId)
            .HasColumnName("event_id")
            .IsRequired();
        builder.Property(booking => booking.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(booking => booking.Seats)
            .HasColumnName("seats")
            .IsRequired();
        builder.Property(booking => booking.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(booking => booking.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(booking => booking.ProcessedAt)
            .HasColumnName("processed_at");
        builder.Property(booking => booking.ConfirmationPublishedAt)
            .HasColumnName("confirmation_published_at");
    }
}
