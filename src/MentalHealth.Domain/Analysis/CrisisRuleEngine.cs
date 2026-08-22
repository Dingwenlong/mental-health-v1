namespace MentalHealth.Domain.Analysis;

public sealed record CrisisRule(
    string Id,
    IReadOnlyList<string> Any,
    IReadOnlyList<IReadOnlyList<string>> AllGroups);

public sealed record CrisisResult(bool IsCrisis, string RuleId);

public sealed class CrisisRuleEngine
{
    private static readonly char[] ClauseSeparators =
        ['。', '！', '？', '!', '?', '；', ';', '，', ',', '\n', '\r'];
    private static readonly char[] SentenceSeparators =
        ['。', '！', '？', '!', '?', '；', ';', '\n', '\r'];
    private static readonly string[] FirstPersonPivots =
        ["但我", "但是我", "可是我", "不过我", "而我", "我自己"];

    private readonly int _lookbackMessageCount;
    private readonly int _negationWindow;
    private readonly string[] _negationTerms;
    private readonly string[] _quotationTerms;
    private readonly CrisisRule[] _rules;

    public CrisisRuleEngine(
        string crisisReply,
        int lookbackMessageCount,
        int negationWindow,
        IReadOnlyList<string> negationTerms,
        IReadOnlyList<string> quotationTerms,
        IReadOnlyList<CrisisRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(crisisReply);
        ArgumentNullException.ThrowIfNull(negationTerms);
        ArgumentNullException.ThrowIfNull(quotationTerms);
        ArgumentNullException.ThrowIfNull(rules);
        if (lookbackMessageCount is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(lookbackMessageCount));
        }

        if (negationWindow is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(negationWindow));
        }

        CrisisReply = crisisReply.Trim();
        _lookbackMessageCount = lookbackMessageCount;
        _negationWindow = negationWindow;
        _negationTerms = NormalizeTerms(negationTerms, nameof(negationTerms));
        _quotationTerms = NormalizeTerms(quotationTerms, nameof(quotationTerms));
        _rules = rules.Select(ValidateRule).ToArray();
        if (_rules.Length == 0
            || _rules.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count()
                != _rules.Length)
        {
            throw new ArgumentException("Crisis rules must have unique ids.", nameof(rules));
        }
    }

    public string CrisisReply { get; }

    public CrisisResult Evaluate(IReadOnlyList<string> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var recent = messages
            .TakeLast(_lookbackMessageCount)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message.Trim())
            .ToArray();

        foreach (var rule in _rules)
        {
            var anyMatched = rule.Any.Count > 0
                && rule.Any.Any(term => recent.Any(text => IsActive(text, term)));
            var groupsMatched = rule.AllGroups.Count > 0
                && rule.AllGroups.All(group =>
                    group.Any(term => recent.Any(text => IsActive(text, term))));
            if (anyMatched || groupsMatched)
            {
                return new CrisisResult(true, rule.Id);
            }
        }

        return new CrisisResult(false, "NO_CRISIS");
    }

    private bool IsActive(string text, string term)
    {
        var searchFrom = 0;
        while (searchFrom < text.Length)
        {
            var match = text.IndexOf(term, searchFrom, StringComparison.Ordinal);
            if (match < 0)
            {
                return false;
            }

            var clauseStart = match == 0
                ? 0
                : text.LastIndexOfAny(ClauseSeparators, match - 1) + 1;
            var clauseEnd = text.IndexOfAny(ClauseSeparators, match);
            if (clauseEnd < 0)
            {
                clauseEnd = text.Length;
            }

            var clause = text[clauseStart..clauseEnd];
            var relativeMatch = match - clauseStart;
            var quoted = IsQuoted(text, match);
            var prefixStart = Math.Max(0, relativeMatch - _negationWindow);
            var prefix = clause[prefixStart..relativeMatch];
            var negated = _negationTerms.Any(negation =>
                prefix.Contains(negation, StringComparison.Ordinal));
            if (!quoted && !negated)
            {
                return true;
            }

            searchFrom = checked(match + term.Length);
        }

        return false;
    }

    private bool IsQuoted(string text, int match)
    {
        var sentenceStart = match == 0
            ? 0
            : text.LastIndexOfAny(SentenceSeparators, match - 1) + 1;
        var prefix = text[sentenceStart..match];
        var quotationIndex = _quotationTerms
            .Select(term => prefix.LastIndexOf(term, StringComparison.Ordinal))
            .DefaultIfEmpty(-1)
            .Max();
        if (quotationIndex < 0)
        {
            return false;
        }

        var pivotIndex = FirstPersonPivots
            .Select(term => prefix.LastIndexOf(term, StringComparison.Ordinal))
            .DefaultIfEmpty(-1)
            .Max();
        return pivotIndex < quotationIndex;
    }

    private static CrisisRule ValidateRule(CrisisRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var id = rule.Id?.Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Crisis rule id is required.", nameof(rule));
        }

        var any = NormalizeTerms(rule.Any, nameof(rule));
        var groups = rule.AllGroups
            .Select(group => (IReadOnlyList<string>)NormalizeTerms(group, nameof(rule)))
            .ToArray();
        if ((any.Length == 0 && groups.Length == 0)
            || groups.Any(group => group.Count == 0))
        {
            throw new ArgumentException("Crisis rule terms are required.", nameof(rule));
        }

        return new CrisisRule(id, any, groups);
    }

    private static string[] NormalizeTerms(
        IReadOnlyList<string> terms,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(terms);
        var normalized = terms
            .Select(term => term?.Trim())
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length != terms.Count)
        {
            throw new ArgumentException("Rule terms must be non-empty and unique.", parameterName);
        }

        return normalized;
    }
}
