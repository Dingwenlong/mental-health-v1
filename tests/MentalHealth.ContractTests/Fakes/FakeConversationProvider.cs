using MentalHealth.Application.Abstractions.Providers;

namespace MentalHealth.ContractTests.Fakes;

internal sealed class FakeConversationProvider : IConversationProvider
{
    public Task<ConversationReply> ReplyAsync(
        ConversationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(context.LatestText))
        {
            throw new ProviderException("CONVERSATION_TEXT_REQUIRED");
        }

        return Task.FromResult(new ConversationReply(
            $"收到：{context.LatestText}",
            "FAKE_ECHO",
            IsCrisis: false));
    }
}
