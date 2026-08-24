namespace MentalHealth.Domain.Analysis;

public sealed record RiskEvidence(
    string Code,
    string Modality,
    decimal Contribution,
    string SourceRange,
    decimal Quality);
