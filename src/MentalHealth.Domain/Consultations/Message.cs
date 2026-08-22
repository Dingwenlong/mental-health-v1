using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Consultations;

public enum MessageSenderKind
{
    User,
    Practitioner
}

public sealed class Message
{
    private Message()
    {
    }

    private Message(
        Guid sessionId,
        Guid senderUserId,
        MessageSenderKind senderKind,
        string text,
        string clientMessageId,
        int sequence,
        DateTimeOffset sentAt)
    {
        if (sessionId == Guid.Empty)
        {
            throw new DomainException("MESSAGE_SESSION_REQUIRED");
        }

        if (senderUserId == Guid.Empty || !Enum.IsDefined(senderKind))
        {
            throw new DomainException("MESSAGE_SENDER_INVALID");
        }

        var normalizedText = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText)
            || normalizedText.Length > 4000)
        {
            throw new DomainException("MESSAGE_TEXT_INVALID");
        }

        var normalizedClientMessageId = clientMessageId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedClientMessageId)
            || normalizedClientMessageId.Length > 100)
        {
            throw new DomainException("CLIENT_MESSAGE_ID_INVALID");
        }

        if (sequence < 1)
        {
            throw new DomainException("MESSAGE_SEQUENCE_INVALID");
        }

        Id = Guid.NewGuid();
        SessionId = sessionId;
        SenderUserId = senderUserId;
        SenderKind = senderKind;
        Text = normalizedText;
        ClientMessageId = normalizedClientMessageId;
        Sequence = sequence;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public MessageSenderKind SenderKind { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public string ClientMessageId { get; private set; } = string.Empty;

    public int Sequence { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    public static Message Create(
        Guid sessionId,
        Guid senderUserId,
        MessageSenderKind senderKind,
        string text,
        string clientMessageId,
        int sequence,
        DateTimeOffset sentAt) => new(
            sessionId,
            senderUserId,
            senderKind,
            text,
            clientMessageId,
            sequence,
            sentAt);
}
