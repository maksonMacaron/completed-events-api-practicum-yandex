using EventsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsAPI.Infrastructure.Persistence.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(item => item.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(item => item.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(item => item.StartAt)
            .HasColumnName("start_at")
            .IsRequired();

        builder.Property(item => item.EndAt)
            .HasColumnName("end_at")
            .IsRequired();

        builder.Property(item => item.TotalSeats)
            .HasColumnName("total_seats")
            .IsRequired();

        builder.Property(item => item.AvailableSeats)
            .HasColumnName("available_seats")
            .IsRequired();

        builder.HasMany(item => item.Bookings)
            .WithOne(booking => booking.Event)
            .HasForeignKey(booking => booking.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
