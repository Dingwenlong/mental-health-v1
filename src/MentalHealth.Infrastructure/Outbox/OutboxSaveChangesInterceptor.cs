using MentalHealth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MentalHealth.Infrastructure.Outbox;

public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is MentalHealthDbContext db)
        {
            db.EnqueueDomainEvents();
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is MentalHealthDbContext db)
        {
            db.EnqueueDomainEvents();
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        if (eventData.Context is MentalHealthDbContext db)
        {
            db.ClearDomainEvents();
        }

        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is MentalHealthDbContext db)
        {
            db.ClearDomainEvents();
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
