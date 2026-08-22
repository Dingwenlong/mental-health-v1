using System.Security.Cryptography;
using System.Text;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Consultations.Ai;

public sealed record AiTurnResult(
    Message UserMessage,
    Message Reply,
    string RuleId,
    bool IsCrisis,
    bool Created,
    bool NotificationAccepted);

public sealed class SendAiTurnHandler(
    IConsultationRepository consultations,
    SessionAccessService access,
    IConversationProvider conversation,
    CrisisRuleEngine crisis,
    INotificationSender notifications,
    IClock clock)
{
    public async Task<AiTurnResult> HandleAsync(
        ConsultationActor actor,
        Guid sessionId,
        string text,
        string clientMessageId,
        CancellationToken cancellationToken)
    {
        var subjectId = actor.RequireOwnedSubject();
        var permitted = await access.DemandAsync(actor, sessionId, cancellationToken);
        if (permitted.SenderKind != MessageSenderKind.User
            || permitted.Session.SubjectId != subjectId)
        {
            throw new DomainException("FORBIDDEN_RESOURCE");
        }

        if (permitted.Session.Kind != ConsultationKind.AiVirtual
            || permitted.Session.Channel != ConsultationChannel.Chat)
        {
            throw new DomainException("AI_CHAT_SESSION_REQUIRED");
        }

        if (permitted.Session.Status != ConsultationStatus.InProgress)
        {
            throw new DomainException("INVALID_SESSION_STATE");
        }

        ValidateInput(text, clientMessageId);
        var userResult = await consultations.AppendMessageAsync(
            sessionId,
            actor.UserId,
            MessageSenderKind.User,
            text,
            clientMessageId,
            clock.UtcNow,
            cancellationToken);
        var assistantClientMessageId = BuildAssistantClientMessageId(
            userResult.Message.ClientMessageId);
        var messages = await consultations.ListMessagesAsync(
            sessionId,
            0,
            cancellationToken);
        var existingReply = messages.SingleOrDefault(message =>
            string.Equals(
                message.ClientMessageId,
                assistantClientMessageId,
                StringComparison.Ordinal));
        var messagesThroughTurn = messages
            .Where(message => message.Sequence <= userResult.Message.Sequence)
            .ToArray();
        var context = BuildContext(
            permitted.Session,
            messagesThroughTurn,
            userResult.Message);
        var crisisResult = crisis.Evaluate(messagesThroughTurn
            .Where(message => message.SenderKind == MessageSenderKind.User)
            .Select(message => message.Text)
            .ToArray());
        var generated = crisisResult.IsCrisis
            ? new ConversationReply(
                crisis.CrisisReply,
                crisisResult.RuleId,
                IsCrisis: true)
            : await conversation.ReplyAsync(context, cancellationToken);

        if (existingReply is not null)
        {
            var notificationAccepted = await TryNotifyIfCrisisAsync(
                permitted.Session,
                generated,
                assistantClientMessageId,
                cancellationToken);
            return new AiTurnResult(
                userResult.Message,
                existingReply,
                generated.RuleId,
                generated.IsCrisis,
                Created: false,
                notificationAccepted);
        }

        if (generated.IsCrisis)
        {
            permitted.Session.RequestEscalation(
                BuildDeterministicGuid(
                    sessionId,
                    assistantClientMessageId,
                    "escalation"),
                generated.RuleId,
                clock.UtcNow);
        }

        var replyResult = await consultations.AppendMessageAsync(
            sessionId,
            Guid.Empty,
            MessageSenderKind.Assistant,
            generated.Text,
            assistantClientMessageId,
            clock.UtcNow,
            cancellationToken);
        var accepted = await TryNotifyIfCrisisAsync(
            permitted.Session,
            generated,
            assistantClientMessageId,
            cancellationToken);
        return new AiTurnResult(
            userResult.Message,
            replyResult.Message,
            generated.RuleId,
            generated.IsCrisis,
            replyResult.Created,
            accepted);
    }

    private async Task<bool> TryNotifyIfCrisisAsync(
        ConsultationSession session,
        ConversationReply reply,
        string assistantClientMessageId,
        CancellationToken cancellationToken)
    {
        if (!reply.IsCrisis)
        {
            return true;
        }

        var eventId = BuildDeterministicGuid(
            session.Id,
            assistantClientMessageId,
            "escalation");
        try
        {
            await notifications.SendAsync(
                new NotificationMessage(
                    session.SubjectId,
                    "crisis",
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["sessionId"] = session.Id.ToString("N"),
                        ["ruleId"] = reply.RuleId
                    },
                    $"crisis-{eventId:N}"),
                cancellationToken);
            return true;
        }
        catch (ProviderException)
        {
            return false;
        }
    }

    private static ConversationContext BuildContext(
        ConsultationSession session,
        IReadOnlyList<Message> messages,
        Message latest)
    {
        var history = messages
            .Where(message => message.Id != latest.Id)
            .TakeLast(16)
            .Select(message => new ConversationTurn(
                message.Id,
                message.SenderKind == MessageSenderKind.Assistant
                    ? ConversationRole.Assistant
                    : ConversationRole.User,
                message.Text,
                message.SentAt))
            .ToArray();
        return new ConversationContext(
            session.Id,
            session.SubjectId,
            history,
            latest.Text);
    }

    private static string BuildAssistantClientMessageId(string clientMessageId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(clientMessageId));
        return $"ai-{Convert.ToHexString(hash)[..32].ToLowerInvariant()}";
    }

    private static Guid BuildDeterministicGuid(
        Guid sessionId,
        string clientMessageId,
        string purpose)
    {
        var value = $"{sessionId:N}|{clientMessageId}|{purpose}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static void ValidateInput(string text, string clientMessageId)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 4000)
        {
            throw new DomainException("MESSAGE_TEXT_INVALID");
        }

        if (string.IsNullOrWhiteSpace(clientMessageId)
            || clientMessageId.Trim().Length > 100)
        {
            throw new DomainException("CLIENT_MESSAGE_ID_INVALID");
        }
    }
}
