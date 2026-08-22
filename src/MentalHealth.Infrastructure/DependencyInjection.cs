using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Infrastructure.Outbox;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace MentalHealth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseConnection = RequireConnectionString(configuration, "MentalHealth");
        var redisConnection = RequireConnectionString(configuration, "Redis");

        services.AddSingleton<OutboxSaveChangesInterceptor>();
        services.AddDbContextFactory<MentalHealthDbContext>((provider, options) =>
            options
                .UseNpgsql(databaseConnection)
                .AddInterceptors(provider.GetRequiredService<OutboxSaveChangesInterceptor>()));

        services
            .AddOptions<LocalObjectStorageOptions>()
            .Bind(configuration.GetSection(LocalObjectStorageOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.RootPath),
                "Local object storage root is required.")
            .ValidateOnStart();
        services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));

        return services;
    }

    private static string RequireConnectionString(
        IConfiguration configuration,
        string name)
    {
        var connectionString = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                $"Connection string '{name}' is required.")
            : connectionString;
    }
}
