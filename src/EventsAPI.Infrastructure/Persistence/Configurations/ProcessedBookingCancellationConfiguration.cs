using EventsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsAPI.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedBookingCancellationConfiguration
    : IEntityTypeConfiguration<ProcessedBookingCancellation>
{
    public void Configure(EntityTypeBuilder<ProcessedBookingCancellation> builder)
    {
        builder.ToTable("processed_booking_cancellations");
        builder.HasKey(item => item.BookingId);

        builder.Property(item => item.BookingId)
            .HasColumnName("booking_id")
            .ValueGeneratedNever();

        builder.Property(item => item.ProcessedAt)
            .HasColumnName("processed_at")
            .IsRequired();
    }
}
