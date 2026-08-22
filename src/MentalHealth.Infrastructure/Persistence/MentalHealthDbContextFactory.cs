using MentalHealth.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MentalHealth.Infrastructure.Persistence;

public sealed class MentalHealthDbContextFactory : IDesignTimeDbContextFactory<MentalHealthDbContext>
{
    public MentalHealthDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__MentalHealth")
            ?? "Host=127.0.0.1;Port=54329;Database=mental_health;Username=mental_health";
        var options = new DbContextOptionsBuilder<MentalHealthDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new OutboxSaveChangesInterceptor())
            .Options;

        return new MentalHealthDbContext(options);
    }
}
