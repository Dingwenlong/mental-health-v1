using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.Shared;

namespace MentalHealth.UnitTests.Consultations;

public sealed class MessageTests
{
    private static readonly DateTimeOffset SentAt =
        DateTimeOffset.Parse("2026-08-22T10:00:00+08:00");

    [Fact]
    public void Create_preserves_text_and_assigns_sequence()
    {
        var message = Message.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageSenderKind.User,
            "  合成消息正文  ",
            "client-001",
            3,
            SentAt);

        Assert.Equal("合成消息正文", message.Text);
        Assert.Equal("client-001", message.ClientMessageId);
        Assert.Equal(3, message.Sequence);
        Assert.Equal(SentAt, message.SentAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_text(string text)
    {
        var exception = Assert.Throws<DomainException>(() => Message.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageSenderKind.User,
            text,
            "client-001",
            1,
            SentAt));

        Assert.Equal("MESSAGE_TEXT_INVALID", exception.Code);
    }

    [Fact]
    public void Create_rejects_text_over_4000_characters()
    {
        var exception = Assert.Throws<DomainException>(() => Message.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MessageSenderKind.Practitioner,
            new string('x', 4001),
            "client-001",
            1,
            SentAt));

        Assert.Equal("MESSAGE_TEXT_INVALID", exception.Code);
    }
}
