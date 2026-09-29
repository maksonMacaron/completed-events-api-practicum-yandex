using BookingsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingsAPI.Infrastructure.Persistence.Configurations;

internal sealed class KnownEventConfiguration : IEntityTypeConfiguration<KnownEvent>
{
    public void Configure(EntityTypeBuilder<KnownEvent> builder)
    {
        builder.ToTable("known_events");
        builder.HasKey(item => item.EventId);

        builder.Property(item => item.EventId)
            .HasColumnName("event_id")
            .ValueGeneratedNever();
        builder.Property(item => item.IsAvailable)
            .HasColumnName("is_available")
            .IsRequired();
        builder.Property(item => item.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
