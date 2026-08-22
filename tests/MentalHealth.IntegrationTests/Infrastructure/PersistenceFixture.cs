using Microsoft.EntityFrameworkCore;
using MentalHealth.Infrastructure.Outbox;
using MentalHealth.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace MentalHealth.IntegrationTests.Infrastructure;

public sealed class PersistenceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("mental_health_tests")
        .WithUsername("mental_health")
        .WithPassword("synthetic-test-password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:8-alpine")
        .Build();

    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.WhenAll(
        _postgres.DisposeAsync().AsTask(),
        _redis.DisposeAsync().AsTask());

    public MentalHealthDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MentalHealthDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new OutboxSaveChangesInterceptor())
            .Options;
        return new MentalHealthDbContext(options);
    }
}
