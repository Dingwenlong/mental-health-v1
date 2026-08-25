using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using System.Collections.Concurrent;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using StackExchange.Redis;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MentalHealth.IntegrationTests.Auth;

public sealed class AuthApiFixture : IAsyncLifetime
{
    private readonly string? _clientPhone;
    private readonly string? _adminPhone;
    private readonly bool _aliyunPhoneLoginEnabled;
    private readonly bool _configureTestPhoneProviders;
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
    private readonly FakeCaptchaVerifier _captcha = new();
    private readonly FakeSmsVerificationProvider _sms = new();
    private readonly FakeLoginFailureDelay _failureDelay = new();
    private static readonly ConcurrentDictionary<string, string> SyntheticPhones = new();
    private static int _syntheticPhoneSequence;

    public const string InitialPassword = "Synthetic-password-2026!";

    public AuthApiFixture()
        : this(
            "13800138001",
            "13900139002",
            aliyunPhoneLoginEnabled: true,
            configureTestPhoneProviders: true)
    {
    }

    internal AuthApiFixture(
        string? clientPhone,
        string? adminPhone,
        bool aliyunPhoneLoginEnabled = false,
        bool configureTestPhoneProviders = false)
    {
        _clientPhone = clientPhone;
        _adminPhone = adminPhone;
        _aliyunPhoneLoginEnabled = aliyunPhoneLoginEnabled;
        _configureTestPhoneProviders = configureTestPhoneProviders;
    }

    public HttpClient Client => _client
        ?? throw new InvalidOperationException("The API fixture is not initialized.");

    public IServiceProvider Services => _factory?.Services
        ?? throw new InvalidOperationException("The API fixture is not initialized.");

    public IReadOnlyList<TestLogEntry> CapturedLogs => _logs.Entries;

    public FakeCaptchaVerifier Captcha => _captcha;

    public FakeSmsVerificationProvider Sms => _sms;

    public FakeLoginFailureDelay FailureDelay => _failureDelay;

    public ControllableLoginChallengeStore ChallengeStore =>
        Services.GetRequiredService<ControllableLoginChallengeStore>();

    internal static string CreateSyntheticPhoneNumber(string stableUserKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableUserKey);
        return SyntheticPhones.GetOrAdd(stableUserKey, _ =>
        {
            var sequence = Interlocked.Increment(ref _syntheticPhoneSequence);
            if (sequence > 99_999_999)
            {
                throw new InvalidOperationException(
                    "The synthetic integration-test phone range is exhausted.");
            }

            return $"+86139{sequence:D8}";
        });
    }

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
            ["Database:InitializeOnStartup"] = "true",
            ["IdentitySeed:Enabled"] = "true",
            ["CatalogSeed:Enabled"] = "true",
            ["PhoneLogin:Aliyun:Enabled"] = _aliyunPhoneLoginEnabled.ToString(),
            ["PhoneLogin:Accounts:ClientPhone"] = _clientPhone,
            ["PhoneLogin:Accounts:AdminPhone"] = _adminPhone
        };
        if (_configureTestPhoneProviders)
        {
            settings["PhoneLogin:Aliyun:Prefix"] = "xfkdn8";
            settings["PhoneLogin:Aliyun:AdminSceneId"] = "synthetic-admin-scene";
            settings["PhoneLogin:Aliyun:AndroidSceneId"] = "synthetic-android-scene";
            settings["PhoneLogin:Aliyun:CaptchaEkey"] =
                "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
            settings["PhoneLogin:Aliyun:AccessKeyId"] = "synthetic-access-key-id";
            settings["PhoneLogin:Aliyun:AccessKeySecret"] =
                "synthetic-access-key-secret";
            settings["PhoneLogin:Aliyun:SmsSignName"] = "synthetic-sign";
            settings["PhoneLogin:Aliyun:SmsTemplateCode"] = "synthetic-template";
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.AddProvider(_logs));
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICaptchaVerifier>();
                services.RemoveAll<ISmsVerificationProvider>();
                services.RemoveAll<ILoginChallengeStore>();
                services.RemoveAll<ILoginFailureDelay>();
                services.AddSingleton<ICaptchaVerifier>(_captcha);
                services.AddSingleton<ISmsVerificationProvider>(_sms);
                services.AddSingleton<ILoginFailureDelay>(_failureDelay);
                services.AddSingleton<RedisLoginChallengeStore>();
                services.AddSingleton<ControllableLoginChallengeStore>(provider =>
                    new ControllableLoginChallengeStore(
                        provider.GetRequiredService<RedisLoginChallengeStore>()));
                services.AddSingleton<ILoginChallengeStore>(provider =>
                    provider.GetRequiredService<ControllableLoginChallengeStore>());
            });
        });

        _client = _factory.CreateClient();
    }

    public async Task ResetPhoneLoginAsync()
    {
        ChallengeStore.Unavailable = false;
        _captcha.Reset();
        _sms.Reset();
        _failureDelay.Reset();

        var redis = Services.GetRequiredService<IConnectionMultiplexer>();
        var database = redis.GetDatabase();
        foreach (var endpoint in redis.GetEndPoints())
        {
            var server = redis.GetServer(endpoint);
            await foreach (var key in server.KeysAsync(
                database.Database,
                "auth:{phone-login}:*"))
            {
                if (key != RedisLoginChallengeStore.SmsDispatchStream)
                {
                    _ = await database.KeyDeleteAsync(key);
                }
            }
        }

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        await db.Users
            .Where(user => user.PhoneNumber == "+8613800138001"
                || user.PhoneNumber == "+8613900139002")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.PhoneNumberConfirmed, false));
    }

    public async Task<bool> IsPhoneConfirmedAsync(string phoneNumber)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        return await db.Users
            .Where(user => user.PhoneNumber == phoneNumber)
            .Select(user => user.PhoneNumberConfirmed)
            .SingleAsync();
    }

    public async Task ResetContactEmailsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        await db.Users
            .Where(user => user.PhoneNumber == "+8613800138001")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.Email, "abc@qq.com")
                .SetProperty(user => user.NormalizedEmail, "ABC@QQ.COM")
                .SetProperty(user => user.EmailConfirmed, false));
        await db.Users
            .Where(user => user.PhoneNumber == "+8613900139002")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.Email, "123@qq.com")
                .SetProperty(user => user.NormalizedEmail, "123@QQ.COM")
                .SetProperty(user => user.EmailConfirmed, false));
    }

    public async Task<BootstrapResponse> BootstrapAsync(
        string phoneNumber,
        string client = "android")
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/captcha/bootstrap",
            new { phoneNumber, client });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BootstrapResponse>())!;
    }

    public async Task<ChallengeResponse> CreateChallengeAsync(
        string preChallengeToken,
        string captchaVerifyParam)
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/sms/challenges",
            new { preChallengeToken, captchaVerifyParam });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChallengeResponse>())!;
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
                user.PhoneNumber ?? CreateSyntheticPhoneNumber(
                    $"trusted-token:{user.Id:N}"),
                roles.ToArray(),
                user.SubjectId,
                user.PractitionerId)).Value;
    }

    public async Task<(string? ClientPhone, string? AdminPhone)>
        ReadPublicAccountPhonesAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT \"NormalizedEmail\", \"PhoneNumber\" FROM \"AspNetUsers\" "
            + "WHERE \"NormalizedEmail\" IN ('ABC@QQ.COM', '123@QQ.COM')",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        string? clientPhone = null;
        string? adminPhone = null;
        while (await reader.ReadAsync())
        {
            var email = reader.GetString(0);
            var phone = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (email == "ABC@QQ.COM")
            {
                clientPhone = phone;
            }
            else if (email == "123@QQ.COM")
            {
                adminPhone = phone;
            }
        }

        return (clientPhone, adminPhone);
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

public sealed record BootstrapResponse(
    string PreChallengeToken,
    string Prefix,
    string EncryptedSceneId,
    DateTimeOffset ExpiresAt);

public sealed record ChallengeResponse(
    string ChallengeId,
    string ChallengeToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ResendAt);
