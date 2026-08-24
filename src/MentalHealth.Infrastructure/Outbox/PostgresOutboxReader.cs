using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MentalHealth.Infrastructure.Outbox;

public sealed record OutboxLease(
    Guid Id,
    Guid AggregateId,
    string Type,
    string Payload,
    int Attempts);

public sealed record OutboxFailureResult(int Attempts, bool Terminal);

public sealed class PostgresOutboxReader(
    IDbContextFactory<MentalHealthDbContext> dbContextFactory,
    IClock clock)
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2)
    ];

    public async Task<IReadOnlyList<OutboxLease>> LeaseBatchAsync(
        string workerId,
        int maximumCount,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (maximumCount is < 1 or > 100 || leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        var now = clock.UtcNow;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages
            .FromSqlInterpolated($$"""
                SELECT * FROM outbox_messages
                WHERE type = 'ConsultationCompleted'
                  AND processed_at IS NULL
                  AND (locked_until IS NULL OR locked_until < {{now}})
                ORDER BY occurred_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT {{maximumCount}}
                """)
            .ToArrayAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.Lease(workerId, now.Add(leaseDuration));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages
            .Select(message => new OutboxLease(
                message.Id,
                message.AggregateId,
                message.Type,
                message.Payload,
                message.Attempts))
            .ToArray();
    }

    public async Task MarkProcessedAsync(
        Guid messageId,
        string workerId,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var message = await db.OutboxMessages.SingleAsync(
            item => item.Id == messageId,
            cancellationToken);
        message.MarkProcessed(workerId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<OutboxFailureResult> RecordFailureAsync(
        Guid messageId,
        string workerId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var message = await db.OutboxMessages.SingleAsync(
            item => item.Id == messageId,
            cancellationToken);
        var nextAttempt = checked(message.Attempts + 1);
        var terminal = nextAttempt >= RetryDelays.Length;
        var delay = RetryDelays[Math.Min(nextAttempt - 1, RetryDelays.Length - 1)];
        var attempts = message.RecordFailure(
            workerId,
            errorCode,
            clock.UtcNow,
            delay,
            terminal);
        await db.SaveChangesAsync(cancellationToken);
        return new OutboxFailureResult(attempts, terminal);
    }
}
