using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.ContractTests.Fakes;

namespace MentalHealth.ContractTests.Providers;

public abstract class ConversationProviderContract
{
    protected abstract IConversationProvider CreateProvider();

    [Fact]
    public async Task Reply_returns_text_rule_and_crisis_decision()
    {
        var provider = CreateProvider();
        var context = new ConversationContext(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            [],
            "我今天有点紧张");

        var reply = await provider.ReplyAsync(context, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(reply.Text));
        Assert.False(string.IsNullOrWhiteSpace(reply.RuleId));
    }

    [Fact]
    public async Task Reply_rejects_blank_latest_text()
    {
        var provider = CreateProvider();
        var context = new ConversationContext(Guid.NewGuid(), Guid.NewGuid(), [], " ");

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => provider.ReplyAsync(context, CancellationToken.None));

        Assert.Equal("CONVERSATION_TEXT_REQUIRED", exception.Code);
    }

    [Fact]
    public async Task Reply_honors_a_pre_cancelled_token()
    {
        var provider = CreateProvider();
        var context = new ConversationContext(Guid.NewGuid(), Guid.NewGuid(), [], "测试");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.ReplyAsync(context, new CancellationToken(canceled: true)));
    }
}

public sealed class FakeConversationProviderContractTests : ConversationProviderContract
{
    protected override IConversationProvider CreateProvider() => new FakeConversationProvider();
}
