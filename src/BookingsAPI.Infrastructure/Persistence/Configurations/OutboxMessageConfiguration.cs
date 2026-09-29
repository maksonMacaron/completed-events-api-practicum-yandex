using BookingsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingsAPI.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.HasIndex(message => new { message.PublishedAt, message.LockedUntil });
        builder.HasIndex(message => new { message.BookingId, message.Id });

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();
        builder.Property(message => message.BookingId)
            .HasColumnName("booking_id")
            .IsRequired();
        builder.Property(message => message.Type)
            .HasColumnName("type")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(message => message.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();
        builder.Property(message => message.PublishedAt)
            .HasColumnName("published_at");
        builder.Property(message => message.LockedUntil)
            .HasColumnName("locked_until");
    }
}
