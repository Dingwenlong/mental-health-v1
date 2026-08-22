using MentalHealth.Domain.Consultations;

namespace MentalHealth.Application.Catalog;

public interface IOrderRepository
{
    Task<DemoOrder?> FindByIdempotencyKeyAsync(
        Guid subjectId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<DemoOrder?> FindAsync(
        Guid subjectId,
        Guid orderId,
        CancellationToken cancellationToken);

    void Add(DemoOrder order);
}
