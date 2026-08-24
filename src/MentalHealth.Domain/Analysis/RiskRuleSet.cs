using System.Collections.ObjectModel;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Analysis;

public sealed class RiskRuleSet
{
    private static readonly Guid V1Id =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    private RiskRuleSet()
    {
    }

    private RiskRuleSet(
        Guid id,
        string version,
        IReadOnlyDictionary<Modality, decimal> weights,
        IReadOnlyList<decimal> thresholds,
        DateTimeOffset createdAt,
        bool active)
    {
        Id = id;
        Version = version;
        ScaleWeight = weights[Modality.Scale];
        TextWeight = weights[Modality.Text];
        AudioWeight = weights[Modality.Audio];
        VideoWeight = weights[Modality.Video];
        TrendWeight = weights[Modality.Trend];
        L1Threshold = thresholds[0];
        L2Threshold = thresholds[1];
        L3Threshold = thresholds[2];
        CrisisRulesEnabled = true;
        CreatedAt = createdAt;
        Active = active;
        ActivatedAt = active ? createdAt : null;
    }

    public Guid Id { get; private set; }

    public string Version { get; private set; } = string.Empty;

    public decimal ScaleWeight { get; private set; }

    public decimal TextWeight { get; private set; }

    public decimal AudioWeight { get; private set; }

    public decimal VideoWeight { get; private set; }

    public decimal TrendWeight { get; private set; }

    public decimal L1Threshold { get; private set; }

    public decimal L2Threshold { get; private set; }

    public decimal L3Threshold { get; private set; }

    public bool CrisisRulesEnabled { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    public IReadOnlyDictionary<Modality, decimal> Weights =>
        new ReadOnlyDictionary<Modality, decimal>(
            new Dictionary<Modality, decimal>
            {
                [Modality.Scale] = ScaleWeight,
                [Modality.Text] = TextWeight,
                [Modality.Audio] = AudioWeight,
                [Modality.Video] = VideoWeight,
                [Modality.Trend] = TrendWeight
            });

    public IReadOnlyList<decimal> Thresholds =>
        [L1Threshold, L2Threshold, L3Threshold];

    public static RiskRuleSet V1 => new(
        V1Id,
        "risk-v1",
        new Dictionary<Modality, decimal>
        {
            [Modality.Scale] = .45m,
            [Modality.Text] = .25m,
            [Modality.Audio] = .15m,
            [Modality.Video] = .05m,
            [Modality.Trend] = .10m
        },
        [25m, 50m, 75m],
        DateTimeOffset.UnixEpoch,
        active: true);

    public static RiskRuleSet Create(
        string version,
        IReadOnlyDictionary<Modality, decimal> weights,
        IReadOnlyList<decimal> thresholds,
        DateTimeOffset createdAt,
        bool crisisRulesEnabled = true)
    {
        var normalizedVersion = version?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedVersion)
            || normalizedVersion.Length > 64)
        {
            throw new DomainException("RISK_RULE_VERSION_INVALID");
        }

        if (!crisisRulesEnabled)
        {
            throw new DomainException("CRISIS_RULES_REQUIRED");
        }

        ValidateWeights(weights);
        ValidateThresholds(thresholds);
        return new RiskRuleSet(
            Guid.NewGuid(),
            normalizedVersion,
            weights,
            thresholds,
            createdAt,
            active: false);
    }

    public void Activate(DateTimeOffset now)
    {
        Active = true;
        ActivatedAt = now;
    }

    public void Deactivate()
    {
        Active = false;
    }

    private static void ValidateWeights(
        IReadOnlyDictionary<Modality, decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var modalities = Enum.GetValues<Modality>();
        if (weights.Count != modalities.Length
            || modalities.Any(modality => !weights.TryGetValue(modality, out var weight)
                || weight <= 0m
                || weight > 1m)
            || weights.Values.Sum() != 1m)
        {
            throw new DomainException("RISK_RULE_WEIGHTS_INVALID");
        }
    }

    private static void ValidateThresholds(IReadOnlyList<decimal> thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        if (thresholds.Count != 3
            || thresholds[0] <= 0m
            || thresholds[0] >= thresholds[1]
            || thresholds[1] >= thresholds[2]
            || thresholds[2] >= 100m)
        {
            throw new DomainException("RISK_RULE_THRESHOLDS_INVALID");
        }
    }
}
