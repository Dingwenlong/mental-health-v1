using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.ContractTests.Fakes;

namespace MentalHealth.ContractTests.Providers;

public abstract class NotificationSenderContract
{
    protected abstract NotificationSenderHarness CreateHarness();

    [Fact]
    public async Task Send_delivers_same_idempotency_key_once()
    {
        var harness = CreateHarness();
        var message = CreateMessage("notice-001", "follow-up");

        await harness.Sender.SendAsync(message, CancellationToken.None);
        await harness.Sender.SendAsync(message, CancellationToken.None);

        Assert.Equal(1, harness.DeliveryCount());
    }

    [Fact]
    public async Task Send_rejects_same_idempotency_key_with_changed_message()
    {
        var harness = CreateHarness();
        await harness.Sender.SendAsync(
            CreateMessage("notice-001", "follow-up"),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => harness.Sender.SendAsync(
                CreateMessage("notice-001", "crisis"),
                CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", exception.Code);
        Assert.Equal(1, harness.DeliveryCount());
    }

    [Fact]
    public async Task Send_rejects_blank_idempotency_key()
    {
        var harness = CreateHarness();

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => harness.Sender.SendAsync(CreateMessage(" ", "follow-up"), CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", exception.Code);
        Assert.Equal(0, harness.DeliveryCount());
    }

    [Fact]
    public async Task Send_honors_a_pre_cancelled_token()
    {
        var harness = CreateHarness();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Sender.SendAsync(
                CreateMessage("notice-001", "follow-up"),
                new CancellationToken(canceled: true)));
        Assert.Equal(0, harness.DeliveryCount());
    }

    private static NotificationMessage CreateMessage(string idempotencyKey, string type) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            type,
            new Dictionary<string, string> { ["taskId"] = "task-001" },
            idempotencyKey);
}

public sealed class FakeNotificationSenderContractTests : NotificationSenderContract
{
    protected override NotificationSenderHarness CreateHarness()
    {
        var sender = new FakeNotificationSender();
        return new NotificationSenderHarness(sender, () => sender.DeliveryCount);
    }
}

public sealed record NotificationSenderHarness(
    INotificationSender Sender,
    Func<int> DeliveryCount);
