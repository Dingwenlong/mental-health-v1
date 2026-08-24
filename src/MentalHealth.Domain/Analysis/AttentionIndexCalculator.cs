using System.Collections.ObjectModel;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Analysis;

public sealed record AttentionIndexResult(
    decimal Score,
    decimal AvailableWeight,
    decimal Confidence,
    RiskLevel Level,
    bool IsCrisis,
    string? CrisisRuleId,
    IReadOnlyList<Modality> Missing,
    IReadOnlyDictionary<Modality, decimal> Contributions);

public sealed class AttentionIndexCalculator
{
    public AttentionIndexResult Calculate(
        IReadOnlyCollection<ModalityScore> inputs,
        RiskRuleSet ruleSet,
        CrisisResult? crisis = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(ruleSet);
        if (inputs.Count == 0)
        {
            throw new DomainException("ASSESSMENT_INPUT_REQUIRED");
        }

        if (inputs.Select(input => input.Modality).Distinct().Count() != inputs.Count)
        {
            throw new DomainException("ASSESSMENT_MODALITY_DUPLICATE");
        }

        var available = inputs
            .Select(input => new ModalityScore(
                input.Modality,
                Math.Clamp(input.Score, 0m, 100m),
                Math.Clamp(input.Quality, 0m, 1m)))
            .ToArray();
        var availableWeight = available.Sum(input => ruleSet.Weights[input.Modality]);
        if (availableWeight <= 0m)
        {
            throw new DomainException("ASSESSMENT_INPUT_REQUIRED");
        }

        var contributions = available.ToDictionary(
            input => input.Modality,
            input => Round(
                input.Score * ruleSet.Weights[input.Modality] / availableWeight));
        var score = Round(contributions.Values.Sum());
        var confidence = Round(available.Sum(input =>
            ruleSet.Weights[input.Modality] * input.Quality));
        var matchedCrisis = crisis is { IsCrisis: true };
        var level = matchedCrisis
            ? RiskLevel.Crisis
            : LevelFor(score, ruleSet.Thresholds);
        var missing = Enum.GetValues<Modality>()
            .Where(modality => available.All(input => input.Modality != modality))
            .ToArray();
        return new AttentionIndexResult(
            score,
            availableWeight,
            confidence,
            level,
            matchedCrisis,
            matchedCrisis ? crisis!.RuleId : null,
            missing,
            new ReadOnlyDictionary<Modality, decimal>(contributions));
    }

    private static RiskLevel LevelFor(
        decimal score,
        IReadOnlyList<decimal> thresholds)
    {
        if (score < thresholds[0])
        {
            return RiskLevel.L0;
        }

        if (score < thresholds[1])
        {
            return RiskLevel.L1;
        }

        return score < thresholds[2] ? RiskLevel.L2 : RiskLevel.L3;
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
