using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private string? _storageRoot;
    private readonly TestLogCollector _logs = new();

    public const string InitialPassword = "Synthetic-password-2026!";

    public HttpClient Client => _client
        ?? throw new InvalidOperationException("The API fixture is not initialized.");

    public IServiceProvider Services => _factory?.Services
        ?? throw new InvalidOperationException("The API fixture is not initialized.");

    public IReadOnlyList<TestLogEntry> CapturedLogs => _logs.Entries;

    public void ClearCapturedLogs() => _logs.Clear();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _storageRoot = Path.Combine(
            Path.GetTempPath(),
            "mental-health-v1-tests",
            Guid.NewGuid().ToString("N"));

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MentalHealth"] = _postgres.GetConnectionString(),
            ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
            ["LocalObjectStorage:RootPath"] = _storageRoot,
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
            builder.ConfigureLogging(logging => logging.AddProvider(_logs));
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

    public HttpMessageHandler CreateServerHandler() => _factory?.Server.CreateHandler()
        ?? throw new InvalidOperationException("The API fixture is not initialized.");

    public async Task<HttpClient> CreateTrustedApiClientForAsync(string email)
    {
        var token = await IssueTrustedApiTokenForAsync(email);
        return CreateClientWithBearer(token);
    }

    public async Task<string> IssueTrustedApiTokenForAsync(string email)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Test user '{email}' was not seeded.");
        var roles = await userManager.GetRolesAsync(user);
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        return tokenService.Issue(
            new JwtTokenSubject(
                user.Id,
                user.Email!,
                roles.ToArray(),
                user.SubjectId,
                user.PractitionerId),
            JwtTokenScope.Api).Value;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        try
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                await Task.WhenAll(
                    _postgres.DisposeAsync().AsTask(),
                    _redis.DisposeAsync().AsTask());
            }
            finally
            {
                DeleteStorageRoot();
            }
        }
    }

    private void DeleteStorageRoot()
    {
        if (string.IsNullOrWhiteSpace(_storageRoot)
            || !Directory.Exists(_storageRoot))
        {
            return;
        }

        var allowedRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "mental-health-v1-tests"));
        var allowedPrefix = allowedRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(_storageRoot);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!target.StartsWith(allowedPrefix, comparison))
        {
            throw new InvalidOperationException(
                "Test storage root is outside the allowed temporary directory.");
        }

        Directory.Delete(target, recursive: true);
    }
}
