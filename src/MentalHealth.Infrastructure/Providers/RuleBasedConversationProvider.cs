using System.Text.Json;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Domain.Analysis;

namespace MentalHealth.Infrastructure.Providers;

public sealed class RuleBasedConversationProvider : IConversationProvider
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly ConversationRuleFile _rules;

    public RuleBasedConversationProvider(string rulesPath)
    {
        _rules = ReadRequiredFile<ConversationRuleFile>(rulesPath);
        ValidateConversationRules(_rules);
    }

    public Task<ConversationReply> ReplyAsync(
        ConversationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(context.LatestText))
        {
            throw new ProviderException("CONVERSATION_TEXT_REQUIRED");
        }

        var rule = _rules.Rules.FirstOrDefault(candidate =>
            candidate.Any.Any(term => context.LatestText.Contains(
                term,
                StringComparison.Ordinal)));
        return Task.FromResult(rule is null
            ? new ConversationReply(_rules.Fallback, "fallback", IsCrisis: false)
            : new ConversationReply(rule.Reply, rule.Id, IsCrisis: false));
    }

    public static CrisisRuleEngine LoadCrisisRuleEngine(string rulesPath)
    {
        var file = ReadRequiredFile<RiskRuleFile>(rulesPath);
        if (!string.Equals(file.Version, "risk-v1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Risk rule version is not supported.");
        }

        return new CrisisRuleEngine(
            file.CrisisReply,
            file.LookbackMessageCount,
            file.NegationWindow,
            file.NegationTerms,
            file.QuotationTerms,
            file.Rules.Select(rule => new CrisisRule(
                rule.Id,
                rule.Any,
                rule.AllGroups
                    .Select(group => (IReadOnlyList<string>)group)
                    .ToArray()))
                .ToArray());
    }

    private static T ReadRequiredFile<T>(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new InvalidOperationException("Conversation rule file is missing.");
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Conversation rule file is empty.");
    }

    private static void ValidateConversationRules(ConversationRuleFile rules)
    {
        if (!string.Equals(rules.Version, "conversation-v1", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(rules.Fallback)
            || rules.Fallback.Length > 4000
            || rules.Rules.Count == 0
            || rules.Rules.Select(rule => rule.Id)
                .Distinct(StringComparer.Ordinal)
                .Count() != rules.Rules.Count)
        {
            throw new InvalidOperationException("Conversation rules are invalid.");
        }

        foreach (var rule in rules.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id)
                || string.IsNullOrWhiteSpace(rule.Reply)
                || rule.Reply.Length > 4000
                || rule.Any.Count == 0
                || rule.Any.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("Conversation rule is invalid.");
            }
        }
    }

    private sealed record ConversationRuleFile(
        string Version,
        string Fallback,
        IReadOnlyList<ConversationRuleDocument> Rules);

    private sealed record ConversationRuleDocument(
        string Id,
        IReadOnlyList<string> Any,
        string Reply);

    private sealed record RiskRuleFile(
        string Version,
        string CrisisReply,
        int LookbackMessageCount,
        int NegationWindow,
        IReadOnlyList<string> NegationTerms,
        IReadOnlyList<string> QuotationTerms,
        IReadOnlyList<RiskRuleDocument> Rules);

    private sealed record RiskRuleDocument(
        string Id,
        IReadOnlyList<string> Any,
        IReadOnlyList<IReadOnlyList<string>> AllGroups);
}
