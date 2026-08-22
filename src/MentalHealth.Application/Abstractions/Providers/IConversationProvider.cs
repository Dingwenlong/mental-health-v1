namespace MentalHealth.Application.Abstractions.Providers;

public enum ConversationRole
{
    User,
    Assistant
}

public sealed record ConversationTurn(
    Guid Id,
    ConversationRole Role,
    string Text,
    DateTimeOffset OccurredAt);

public sealed record ConversationContext(
    Guid SessionId,
    Guid SubjectId,
    IReadOnlyList<ConversationTurn> History,
    string LatestText);

public sealed record ConversationReply(string Text, string RuleId, bool IsCrisis);

public interface IConversationProvider
{
    Task<ConversationReply> ReplyAsync(
        ConversationContext context,
        CancellationToken cancellationToken);
}
