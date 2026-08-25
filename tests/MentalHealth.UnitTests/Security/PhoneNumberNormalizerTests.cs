using MentalHealth.Application.Security;

namespace MentalHealth.UnitTests.Security;

public sealed class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("13800138000", "+8613800138000")]
    [InlineData("+8613800138000", "+8613800138000")]
    public void Mainland_number_is_normalized(string input, string expected)
    {
        Assert.True(PhoneNumberNormalizer.TryNormalizeMainlandChina(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1380013800")]
    [InlineData("+85291234567")]
    [InlineData("138 0013 8000")]
    public void Unsupported_number_is_rejected(string input)
    {
        Assert.False(PhoneNumberNormalizer.TryNormalizeMainlandChina(input, out _));
    }
}
