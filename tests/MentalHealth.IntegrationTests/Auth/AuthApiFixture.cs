using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace MentalHealth.IntegrationTests.Auth;

public sealed class AuthApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("mental_health_auth_tests")
        .WithUsername("mental_health")
        .WithPassword("synthetic-test-password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:8-alpine")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public const string InitialPassword = "Synthetic-password-2026!";

    public HttpClient Client => _client
        ?? throw new InvalidOperationException("The API fixture is not initialized.");

    public IServiceProvider Services => _factory?.Services
        ?? throw new InvalidOperationException("The API fixture is not initialized.");

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        var storageRoot = Path.Combine(
            Path.GetTempPath(),
            "mental-health-v1-tests",
            Guid.NewGuid().ToString("N"));

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MentalHealth"] = _postgres.GetConnectionString(),
            ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
            ["LocalObjectStorage:RootPath"] = storageRoot,
            ["Jwt:Issuer"] = "mental-health-v1-tests",
            ["Jwt:Audience"] = "mental-health-v1-tests",
            ["Jwt:SigningKey"] = "synthetic-test-signing-key-with-at-least-32-bytes",
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:MfaSetupTokenMinutes"] = "5",
            ["Database:InitializeOnStartup"] = "true",
            ["IdentitySeed:Enabled"] = "true",
            ["CatalogSeed:Enabled"] = "true",
            ["DemoAccounts:InitialPassword"] = InitialPassword
        };

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(settings));
        });

        _client = _factory.CreateClient();
    }

    public HttpClient CreateClientWithBearer(string token)
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("The API fixture is not initialized.");
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<HttpClient> CreateTrustedApiClientForAsync(string email)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Test user '{email}' was not seeded.");
        var roles = await userManager.GetRolesAsync(user);
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = tokenService.Issue(
            new JwtTokenSubject(
                user.Id,
                user.Email!,
                roles.ToArray(),
                user.SubjectId,
                user.PractitionerId),
            JwtTokenScope.Api);
        return CreateClientWithBearer(token.Value);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask());
    }
}
