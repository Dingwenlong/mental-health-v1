using MentalHealth.Domain.Audit;

namespace MentalHealth.Application.Audit;

public interface IAuditTrail
{
    void Add(AuditEvent auditEvent);
}
