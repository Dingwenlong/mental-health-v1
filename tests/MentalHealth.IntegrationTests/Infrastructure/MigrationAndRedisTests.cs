using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace MentalHealth.IntegrationTests.Infrastructure;

[Collection(PersistenceCollection.Name)]
public sealed class MigrationAndRedisTests(PersistenceFixture fixture)
{
    [Fact]
    public async Task Initial_migration_creates_required_tables()
    {
        await using var db = fixture.CreateDbContext();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();

        Assert.Contains(appliedMigrations, migration => migration.EndsWith("_Initial"));
        _ = await db.ConsultationSessions.CountAsync();
        _ = await db.FollowUpTasks.CountAsync();
        _ = await db.OutboxMessages.CountAsync();
    }

    [Fact]
    public async Task Redis_accepts_a_short_lived_synthetic_value()
    {
        using var connection = await ConnectionMultiplexer.ConnectAsync(
            fixture.RedisConnectionString);
        var database = connection.GetDatabase();
        var key = $"mental-health-v1:test:{Guid.NewGuid():N}";

        try
        {
            Assert.True(await database.StringSetAsync(
                key,
                "synthetic",
                TimeSpan.FromMinutes(1)));
            Assert.Equal("synthetic", (string?)await database.StringGetAsync(key));
            Assert.True(await database.PingAsync() < TimeSpan.FromSeconds(2));
        }
        finally
        {
            await database.KeyDeleteAsync(key);
        }
    }
}
