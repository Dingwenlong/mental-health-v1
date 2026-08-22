using MentalHealth.Application.Abstractions.Clock;

namespace MentalHealth.Infrastructure.Identity;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
