namespace MentalHealth.Domain.Consultations;

public enum ConsultationStatus
{
    Draft,
    AwaitingConsent,
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}
