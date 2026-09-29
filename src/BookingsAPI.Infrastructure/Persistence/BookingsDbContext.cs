using BookingsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsAPI.Infrastructure.Persistence;

public sealed class BookingsDbContext : DbContext
{
    public BookingsDbContext(DbContextOptions<BookingsDbContext> options) : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<KnownEvent> KnownEvents => Set<KnownEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingsDbContext).Assembly);
    }
}
