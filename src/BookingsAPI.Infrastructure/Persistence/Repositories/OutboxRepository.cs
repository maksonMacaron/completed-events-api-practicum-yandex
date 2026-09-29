using BookingsAPI.Application.Abstractions.Persistence;
using BookingsAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace BookingsAPI.Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(1);

    private readonly BookingsDbContext _context;
    private readonly TimeProvider _timeProvider;

    public OutboxRepository(BookingsDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var messages = await _context.OutboxMessages
            .FromSqlInterpolated($$"""
                SELECT message.*
                FROM outbox_messages AS message
                WHERE message.published_at IS NULL
                  AND (message.locked_until IS NULL OR message.locked_until < {{now}})
                  AND NOT EXISTS (
                      SELECT 1
                      FROM outbox_messages AS previous
                      WHERE previous.booking_id = message.booking_id
                        AND previous.id < message.id
                        AND previous.published_at IS NULL
                  )
                ORDER BY message.id
                LIMIT 20
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        var lockedUntil = now.Add(LockDuration);
        foreach (var message in messages)
            message.Lock(lockedUntil);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages;
    }

    public async Task MarkPublishedAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var outboxMessage = await _context.OutboxMessages.FirstOrDefaultAsync(
            message => message.Id == id,
            cancellationToken);
        if (outboxMessage is null || outboxMessage.PublishedAt.HasValue)
            return;

        var publishedAt = _timeProvider.GetUtcNow().UtcDateTime;
        outboxMessage.MarkPublished(publishedAt);

        var booking = await _context.Bookings.FirstAsync(
            item => item.Id == outboxMessage.BookingId,
            cancellationToken);

        switch (outboxMessage.Type)
        {
            case nameof(BookingConfirmed):
                booking.MarkConfirmationPublished(_timeProvider);
                break;
            case nameof(BookingCancelled):
                booking.MarkCancellationPublished(_timeProvider);
                break;
            default:
                throw new InvalidOperationException(
                    $"Неизвестный тип outbox-сообщения: {outboxMessage.Type}");
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
