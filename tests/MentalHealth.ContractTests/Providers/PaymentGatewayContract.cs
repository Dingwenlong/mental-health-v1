using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.ContractTests.Fakes;
using MentalHealth.Infrastructure.Providers;

namespace MentalHealth.ContractTests.Providers;

public abstract class PaymentGatewayContract
{
    protected abstract PaymentGatewayHarness CreateHarness();

    [Fact]
    public async Task Confirm_returns_same_result_and_charges_once_for_same_idempotency_key()
    {
        var harness = CreateHarness();
        var request = CreateRequest("payment-001", 100);

        var first = await harness.Gateway.ConfirmAsync(request, CancellationToken.None);
        var second = await harness.Gateway.ConfirmAsync(request, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(PaymentStatus.Confirmed, first.Status);
        Assert.Equal(1, harness.ChargeCount());
    }

    [Fact]
    public async Task Confirm_rejects_same_idempotency_key_with_changed_payment()
    {
        var harness = CreateHarness();
        await harness.Gateway.ConfirmAsync(CreateRequest("payment-001", 100), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => harness.Gateway.ConfirmAsync(
                CreateRequest("payment-001", 200),
                CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", exception.Code);
        Assert.Equal(1, harness.ChargeCount());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Confirm_rejects_non_positive_amount(long amountInMinorUnits)
    {
        var harness = CreateHarness();

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => harness.Gateway.ConfirmAsync(
                CreateRequest("payment-001", amountInMinorUnits),
                CancellationToken.None));

        Assert.Equal("PAYMENT_AMOUNT_INVALID", exception.Code);
        Assert.Equal(0, harness.ChargeCount());
    }

    [Fact]
    public async Task Confirm_rejects_blank_idempotency_key()
    {
        var harness = CreateHarness();

        var exception = await Assert.ThrowsAsync<ProviderException>(
            () => harness.Gateway.ConfirmAsync(CreateRequest(" ", 100), CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", exception.Code);
        Assert.Equal(0, harness.ChargeCount());
    }

    [Fact]
    public async Task Confirm_honors_a_pre_cancelled_token()
    {
        var harness = CreateHarness();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Gateway.ConfirmAsync(
                CreateRequest("payment-001", 100),
                new CancellationToken(canceled: true)));
        Assert.Equal(0, harness.ChargeCount());
    }

    private static PaymentRequest CreateRequest(string idempotencyKey, long amountInMinorUnits) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            amountInMinorUnits,
            "CNY",
            idempotencyKey);
}

public sealed class FakePaymentGatewayContractTests : PaymentGatewayContract
{
    protected override PaymentGatewayHarness CreateHarness()
    {
        var gateway = new FakePaymentGateway();
        return new PaymentGatewayHarness(gateway, () => gateway.ChargeCount);
    }
}

public sealed class DemoPaymentGatewayContractTests : PaymentGatewayContract
{
    protected override PaymentGatewayHarness CreateHarness()
    {
        var gateway = new DemoPaymentGateway(new FakeClock(
            DateTimeOffset.Parse("2026-08-22T01:00:00+00:00")));
        return new PaymentGatewayHarness(
            gateway,
            () => gateway.ConfirmationCount);
    }
}

public sealed record PaymentGatewayHarness(
    IPaymentGateway Gateway,
    Func<int> ChargeCount);
