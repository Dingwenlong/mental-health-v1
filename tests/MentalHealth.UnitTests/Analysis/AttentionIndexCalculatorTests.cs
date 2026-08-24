using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Shared;

namespace MentalHealth.UnitTests.Analysis;

public sealed class AttentionIndexCalculatorTests
{
    private readonly AttentionIndexCalculator _calculator = new();

    [Fact]
    public void Missing_video_and_trend_are_not_fabricated()
    {
        var result = _calculator.Calculate(
        [
            new ModalityScore(Modality.Scale, 80m, 1.00m),
            new ModalityScore(Modality.Text, 60m, 0.80m),
            new ModalityScore(Modality.Audio, 40m, 0.50m)
        ], RiskRuleSet.V1);

        Assert.InRange(result.Score, 67.05m, 67.07m);
        Assert.Equal(0.85m, result.AvailableWeight);
        Assert.Equal(0.725m, result.Confidence);
        Assert.Equal(RiskLevel.L2, result.Level);
        Assert.Equal([Modality.Video, Modality.Trend], result.Missing);
    }

    [Fact]
    public void Crisis_is_preserved_even_when_numeric_score_is_low()
    {
        var result = _calculator.Calculate(
            [new ModalityScore(Modality.Scale, 5m, 1m)],
            RiskRuleSet.V1,
            CrisisResult.Match("CRISIS_IN_PROGRESS"));

        Assert.True(result.IsCrisis);
        Assert.Equal(RiskLevel.Crisis, result.Level);
        Assert.Equal("CRISIS_IN_PROGRESS", result.CrisisRuleId);
    }

    [Theory]
    [InlineData(24.99, RiskLevel.L0)]
    [InlineData(25, RiskLevel.L1)]
    [InlineData(49.99, RiskLevel.L1)]
    [InlineData(50, RiskLevel.L2)]
    [InlineData(74.99, RiskLevel.L2)]
    [InlineData(75, RiskLevel.L3)]
    [InlineData(100, RiskLevel.L3)]
    public void Boundary_scores_use_the_frozen_levels(double score, RiskLevel expected)
    {
        var result = _calculator.Calculate(
            [new ModalityScore(Modality.Scale, (decimal)score, 1m)],
            RiskRuleSet.V1);

        Assert.Equal(expected, result.Level);
    }

    [Fact]
    public void Scores_and_quality_are_clamped_before_calculation()
    {
        var result = _calculator.Calculate(
        [
            new ModalityScore(Modality.Scale, 150m, 2m),
            new ModalityScore(Modality.Text, -10m, -1m)
        ], RiskRuleSet.V1);

        Assert.Equal(64.285714m, result.Score, precision: 6);
        Assert.Equal(0.45m, result.Confidence);
    }

    [Fact]
    public void Rule_weights_must_total_one_and_thresholds_must_increase()
    {
        Assert.Throws<DomainException>(() => RiskRuleSet.Create(
            "bad-weight",
            new Dictionary<Modality, decimal>
            {
                [Modality.Scale] = .5m,
                [Modality.Text] = .2m,
                [Modality.Audio] = .1m,
                [Modality.Video] = .1m,
                [Modality.Trend] = .2m
            },
            [25m, 50m, 75m],
            DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => RiskRuleSet.Create(
            "bad-threshold",
            RiskRuleSet.V1.Weights,
            [25m, 25m, 75m],
            DateTimeOffset.UtcNow));
    }
}
