using EventsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsAPI.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedBookingMessageConfiguration
    : IEntityTypeConfiguration<ProcessedBookingMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedBookingMessage> builder)
    {
        builder.ToTable("processed_booking_messages");
        builder.HasKey(item => item.BookingId);

        builder.Property(item => item.BookingId)
            .HasColumnName("booking_id")
            .ValueGeneratedNever();

        builder.Property(item => item.ProcessedAt)
            .HasColumnName("processed_at")
            .IsRequired();
    }
}
