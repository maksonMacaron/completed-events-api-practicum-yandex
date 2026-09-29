using BookingsAPI.Application.Abstractions.Persistence;

namespace BookingsAPI.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly BookingsDbContext _context;

    public UnitOfWork(BookingsDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
