using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.Infrastructure.Media;

public sealed class TextFeatureExtractor
{
    public const string Version = "text-rules-v1";

    private static readonly IReadOnlyDictionary<string, string[]> Patterns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["self_harm_intent"] =
            [
                "伤害自己",
                "结束生命",
                "不想活",
                "自杀"
            ],
            ["hopelessness"] =
            [
                "没有希望",
                "没希望",
                "不会变好",
                "看不到希望"
            ],
            ["social_withdrawal"] =
            [
                "不想见人",
                "不想再见任何人",
                "不想回复",
                "躲着所有人"
            ]
        };

    private static readonly string[] SelfHarmNegations =
    [
        "没有想",
        "没想",
        "并不想",
        "不会",
        "从未"
    ];

    public IReadOnlyList<FeatureObservation> Extract(string? text)
    {
        var normalized = text?.Trim() ?? string.Empty;
        return Patterns
            .Select(entry => ExtractOne(normalized, entry.Key, entry.Value))
            .ToArray();
    }

    private static FeatureObservation ExtractOne(
        string text,
        string code,
        IReadOnlyCollection<string> patterns)
    {
        var match = patterns
            .Select(pattern => new TextMatch(
                text.IndexOf(pattern, StringComparison.Ordinal),
                pattern.Length))
            .Where(item => item.Start >= 0)
            .OrderBy(item => item.Start)
            .FirstOrDefault();
        if (match is null)
        {
            return Observation(code, 0d, "none");
        }

        if (code == "self_harm_intent" && IsNegated(text, match))
        {
            var contextStart = Math.Max(0, match.Start - 8);
            return Observation(
                code,
                0d,
                $"chars:{contextStart}-{match.Start + match.Length}");
        }

        return Observation(
            code,
            1d,
            $"chars:{match.Start}-{match.Start + match.Length}");
    }

    private static bool IsNegated(string text, TextMatch match)
    {
        var windowStart = Math.Max(0, match.Start - 8);
        var prefix = text[windowStart..match.Start];
        return SelfHarmNegations.Any(negation =>
            prefix.EndsWith(negation, StringComparison.Ordinal));
    }

    private static FeatureObservation Observation(
        string code,
        double value,
        string sourceRange) => new(
            code,
            value,
            Quality: 1d,
            sourceRange,
            Version);

    private sealed record TextMatch(int Start, int Length);
}
