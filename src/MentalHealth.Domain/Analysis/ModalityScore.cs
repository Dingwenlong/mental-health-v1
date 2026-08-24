namespace MentalHealth.Domain.Analysis;

public enum Modality
{
    Scale,
    Text,
    Audio,
    Video,
    Trend
}

public sealed record ModalityScore(
    Modality Modality,
    decimal Score,
    decimal Quality);
