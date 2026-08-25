using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MentalHealth.Application.Security;
using StackExchange.Redis;

namespace MentalHealth.Infrastructure.Identity;

public sealed class RedisLoginChallengeStore : ILoginChallengeStore
{
    public const string KeyPrefix = "auth:{phone-login}";
    public const string SmsDispatchStream = $"{KeyPrefix}:sms:dispatch";
    public const string SmsDispatchConsumerGroup = "sms-dispatchers";

    private static readonly TimeSpan StateLifetime = TimeSpan.FromSeconds(300);
    private const int VerificationLeaseMilliseconds = 30_000;
    private const int VerificationAttemptLimit = 5;
    private const int SmsDispatchLeaseMilliseconds = 120_000;
    private const int SmsDispatchAttemptLimit = 3;
    private readonly IDatabase _database;
    private readonly TimeProvider _timeProvider;

    public RedisLoginChallengeStore(IConnectionMultiplexer redis)
        : this(redis, TimeProvider.System)
    {
    }

    public RedisLoginChallengeStore(
        IConnectionMultiplexer redis,
        TimeProvider timeProvider)
    {
        _database = redis.GetDatabase();
        _timeProvider = timeProvider;
    }

    private const string RateLimitScript = """
        local retryAfter = 0
        for index = 1, #KEYS do
            local limit = tonumber(ARGV[(index - 1) * 2 + 1])
            local ttl = tonumber(ARGV[(index - 1) * 2 + 2])
            local count = tonumber(redis.call('GET', KEYS[index]) or '0')
            if count >= limit then
                local remaining = redis.call('PTTL', KEYS[index])
                if remaining < 0 then
                    remaining = ttl
                end
                if remaining > retryAfter then
                    retryAfter = remaining
                end
            end
        end
        if retryAfter > 0 then
            return {0, math.ceil(retryAfter / 1000)}
        end
        for index = 1, #KEYS do
            local ttl = tonumber(ARGV[(index - 1) * 2 + 2])
            local count = redis.call('INCR', KEYS[index])
            if count == 1 then
                redis.call('PEXPIRE', KEYS[index], ttl)
            end
        end
        return {1, 0}
        """;

    private const string CreateChallengeScript = """
        redis.call('HSET', KEYS[1],
            'id', ARGV[1],
            'phone', ARGV[2],
            'userId', ARGV[3],
            'client', ARGV[4],
            'sceneId', ARGV[5],
            'outId', ARGV[1],
            'sentAt', ARGV[6],
            'expiresAt', ARGV[7],
            'attempts', 0,
            'dispatchStatus', 'pending',
            'dispatchAttempts', 0)
        redis.call('PEXPIRE', KEYS[1], ARGV[8])
        redis.call('XADD', KEYS[2], '*', 'challengeId', ARGV[1])
        return 1
        """;

    private const string AcquireVerificationScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 then
            return {0}
        end
        if redis.call('PTTL', KEYS[1]) <= 0 then
            return {0}
        end
        local attempts = tonumber(redis.call('HGET', KEYS[1], 'attempts') or '0')
        if attempts >= tonumber(ARGV[1]) then
            return {0}
        end
        if not redis.call('SET', KEYS[2], ARGV[3], 'NX', 'PX', ARGV[2]) then
            return {0}
        end
        attempts = redis.call('HINCRBY', KEYS[1], 'attempts', 1)
        return {
            1,
            redis.call('HGET', KEYS[1], 'id'),
            redis.call('HGET', KEYS[1], 'phone'),
            redis.call('HGET', KEYS[1], 'userId'),
            redis.call('HGET', KEYS[1], 'client'),
            redis.call('HGET', KEYS[1], 'sceneId'),
            redis.call('HGET', KEYS[1], 'outId'),
            redis.call('HGET', KEYS[1], 'sentAt'),
            redis.call('HGET', KEYS[1], 'expiresAt'),
            attempts
        }
        """;

    private const string ConsumeChallengeScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 or redis.call('EXISTS', KEYS[2]) == 0 then
            return {0, ''}
        end
        if redis.call('GET', KEYS[2]) ~= ARGV[1] then
            return {0, ''}
        end
        local userId = redis.call('HGET', KEYS[1], 'userId') or ''
        redis.call('DEL', KEYS[1], KEYS[2])
        return {1, userId}
        """;

    private const string ReleaseVerificationScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private const string AcknowledgeAndDeleteDispatchScript = """
        local acknowledged = redis.call('XACK', KEYS[1], ARGV[1], ARGV[2])
        if acknowledged == 1 then
            redis.call('XDEL', KEYS[1], ARGV[2])
        end
        return acknowledged
        """;

    private const string AcquireSmsDispatchScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 or redis.call('PTTL', KEYS[1]) <= 0 then
            return {0, 0}
        end
        local status = redis.call('HGET', KEYS[1], 'dispatchStatus') or 'pending'
        local attempts = tonumber(redis.call('HGET', KEYS[1], 'dispatchAttempts') or '0')
        if status == 'sent' then
            return {3, attempts}
        end
        local serverTime = redis.call('TIME')
        local now = tonumber(serverTime[1]) * 1000 + math.floor(tonumber(serverTime[2]) / 1000)
        local leaseOwner = redis.call('HGET', KEYS[1], 'dispatchLeaseOwner') or ''
        local leaseUntil = tonumber(redis.call('HGET', KEYS[1], 'dispatchLeaseUntil') or '0')
        if status == 'sending' and leaseOwner ~= '' and leaseUntil > now then
            return {2, attempts}
        end
        if status == 'terminal' or attempts >= tonumber(ARGV[3]) then
            redis.call('HSET', KEYS[1], 'dispatchStatus', 'terminal')
            return {4, attempts}
        end
        attempts = redis.call('HINCRBY', KEYS[1], 'dispatchAttempts', 1)
        redis.call('HSET', KEYS[1],
            'dispatchStatus', 'sending',
            'dispatchLeaseOwner', ARGV[1],
            'dispatchLeaseUntil', now + tonumber(ARGV[2]))
        return {
            1,
            attempts,
            redis.call('HGET', KEYS[1], 'id'),
            redis.call('HGET', KEYS[1], 'phone'),
            redis.call('HGET', KEYS[1], 'userId'),
            redis.call('HGET', KEYS[1], 'client'),
            redis.call('HGET', KEYS[1], 'sceneId'),
            redis.call('HGET', KEYS[1], 'outId'),
            redis.call('HGET', KEYS[1], 'sentAt'),
            redis.call('HGET', KEYS[1], 'expiresAt'),
            tonumber(redis.call('HGET', KEYS[1], 'attempts') or '0')
        }
        """;

    private const string CompleteSmsDispatchScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 then
            return 0
        end
        if redis.call('HGET', KEYS[1], 'dispatchStatus') ~= 'sending'
            or redis.call('HGET', KEYS[1], 'dispatchLeaseOwner') ~= ARGV[1] then
            return 0
        end
        redis.call('HSET', KEYS[1], 'dispatchStatus', 'sent')
        redis.call('HDEL', KEYS[1], 'dispatchLeaseOwner', 'dispatchLeaseUntil')
        return 1
        """;

    private const string FailSmsDispatchScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 then
            return 0
        end
        if redis.call('HGET', KEYS[1], 'dispatchStatus') ~= 'sending'
            or redis.call('HGET', KEYS[1], 'dispatchLeaseOwner') ~= ARGV[1] then
            return 0
        end
        local attempts = tonumber(redis.call('HGET', KEYS[1], 'dispatchAttempts') or '0')
        local terminal = ARGV[2] == '1' or attempts >= tonumber(ARGV[3])
        redis.call('HSET', KEYS[1], 'dispatchStatus', terminal and 'terminal' or 'pending')
        redis.call('HDEL', KEYS[1], 'dispatchLeaseOwner', 'dispatchLeaseUntil')
        return terminal and 2 or 1
        """;

    public async Task<PhoneLoginTicket> CreatePreChallengeAsync(
        PhoneLoginPreChallengeDraft preChallenge,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(StateLifetime);
        var state = new PhoneLoginPreChallenge(
            preChallenge.NationalPhoneNumber,
            preChallenge.UserId,
            preChallenge.Client,
            preChallenge.SceneId,
            now,
            expiresAt);
        var ticket = CreateTicket(expiresAt);
        var created = await _database.StringSetAsync(
            PreChallengeKey(ticket.Id),
            JsonSerializer.Serialize(state),
            expiresAt - now,
            When.NotExists);
        return created
            ? ticket
            : throw new InvalidOperationException("Unable to allocate a prechallenge token.");
    }

    public async Task<PhoneLoginPreChallenge?> TakePreChallengeAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetDeleteAsync(
            PreChallengeKey(Hash(token)));
        return value.IsNull
            ? null
            : JsonSerializer.Deserialize<PhoneLoginPreChallenge>((string)value!);
    }

    public Task<RateLimitDecision> CheckBootstrapRateAsync(
        string sourceIp,
        CancellationToken cancellationToken = default) =>
        CheckRatesAsync(
            [RateKey("bootstrap:minute", sourceIp)],
            [(30, 60_000)],
            cancellationToken);

    public Task<RateLimitDecision> CheckSmsSendRateAsync(
        string nationalPhoneNumber,
        string sourceIp,
        CancellationToken cancellationToken = default)
    {
        var phoneHash = Hash(nationalPhoneNumber);
        var ipHash = Hash(sourceIp);
        return CheckRatesAsync(
            [
                $"{KeyPrefix}:rate:phone:60s:{phoneHash}",
                $"{KeyPrefix}:rate:phone:hour:{phoneHash}",
                $"{KeyPrefix}:rate:phone:day:{phoneHash}",
                $"{KeyPrefix}:rate:ip:minute:{ipHash}",
                $"{KeyPrefix}:rate:ip:day:{ipHash}"
            ],
            [
                (1, 60_000),
                (5, 3_600_000),
                (10, 86_400_000),
                (10, 60_000),
                (100, 86_400_000)
            ],
            cancellationToken);
    }

    public async Task<PhoneLoginTicket> CreateChallengeAsync(
        PhoneLoginChallengeDraft challenge,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(StateLifetime);
        var ticket = CreateTicket(expiresAt);
        await _database.ScriptEvaluateAsync(
            CreateChallengeScript,
            [ChallengeKey(ticket.Id), SmsDispatchStream],
            [
                ticket.Id,
                challenge.NationalPhoneNumber,
                challenge.UserId?.ToString("D") ?? string.Empty,
                challenge.Client,
                challenge.SceneId,
                now.ToString("O", CultureInfo.InvariantCulture),
                expiresAt.ToString("O", CultureInfo.InvariantCulture),
                checked((long)(expiresAt - now).TotalMilliseconds)
            ]);
        return ticket;
    }

    public async Task<PhoneLoginChallenge?> GetChallengeForDispatchAsync(
        string challengeId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var values = await _database.HashGetAllAsync(ChallengeKey(challengeId));
        return values.Length == 0 ? null : ReadChallenge(values);
    }

    public async Task<VerificationLease> TryAcquireVerificationAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var challengeId = Hash(token);
        var leaseId = CreateSecretToken();
        var result = (RedisResult[]?)await _database.ScriptEvaluateAsync(
            AcquireVerificationScript,
            [ChallengeKey(challengeId), VerificationLockKey(challengeId)],
            [VerificationAttemptLimit, VerificationLeaseMilliseconds, leaseId]);
        if (result is null || (long)result[0] == 0)
        {
            return new VerificationLease(false, null, null);
        }

        return new VerificationLease(
            true,
            leaseId,
            new PhoneLoginChallenge(
                (string)result[1]!,
                (string)result[2]!,
                ParseUserId((string)result[3]!),
                (string)result[4]!,
                (string)result[5]!,
                (string)result[6]!,
                DateTimeOffset.Parse((string)result[7]!, CultureInfo.InvariantCulture),
                DateTimeOffset.Parse((string)result[8]!, CultureInfo.InvariantCulture),
                checked((int)(long)result[9])));
    }

    public async Task ReleaseVerificationLeaseAsync(
        string challengeId,
        string leaseId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.ScriptEvaluateAsync(
            ReleaseVerificationScript,
            [VerificationLockKey(challengeId)],
            [leaseId]);
    }

    public async Task<ChallengeConsumption> ConsumeChallengeAsync(
        string challengeId,
        string leaseId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = (RedisResult[]?)await _database.ScriptEvaluateAsync(
            ConsumeChallengeScript,
            [ChallengeKey(challengeId), VerificationLockKey(challengeId)],
            [leaseId]);
        return result is not null && (long)result[0] == 1
            ? new ChallengeConsumption(true, ParseUserId((string)result[1]!))
            : new ChallengeConsumption(false, null);
    }

    public async Task<bool> AcknowledgeAndDeleteSmsDispatchAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _database.ScriptEvaluateAsync(
            AcknowledgeAndDeleteDispatchScript,
            [SmsDispatchStream],
            [SmsDispatchConsumerGroup, messageId]);
        return (long)result == 1;
    }

    public async Task<SmsDispatchLease> TryAcquireSmsDispatchAsync(
        string challengeId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var leaseId = CreateSecretToken();
        var result = (RedisResult[]?)await _database.ScriptEvaluateAsync(
            AcquireSmsDispatchScript,
            [ChallengeKey(challengeId)],
            [leaseId, SmsDispatchLeaseMilliseconds, SmsDispatchAttemptLimit]);
        var state = result is null
            ? SmsDispatchLeaseState.Missing
            : (SmsDispatchLeaseState)checked((int)(long)result[0]);
        var attempt = result is null ? 0 : checked((int)(long)result[1]);
        if (state != SmsDispatchLeaseState.Acquired)
        {
            return new SmsDispatchLease(state, null, null, attempt);
        }

        return new SmsDispatchLease(
            state,
            leaseId,
            new PhoneLoginChallenge(
                (string)result![2]!,
                (string)result[3]!,
                ParseUserId((string)result[4]!),
                (string)result[5]!,
                (string)result[6]!,
                (string)result[7]!,
                DateTimeOffset.Parse((string)result[8]!, CultureInfo.InvariantCulture),
                DateTimeOffset.Parse((string)result[9]!, CultureInfo.InvariantCulture),
                checked((int)(long)result[10])),
            attempt);
    }

    public async Task<bool> CompleteSmsDispatchAsync(
        string challengeId,
        string leaseId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _database.ScriptEvaluateAsync(
            CompleteSmsDispatchScript,
            [ChallengeKey(challengeId)],
            [leaseId]);
        return (long)result == 1;
    }

    public async Task<SmsDispatchFailureState> FailSmsDispatchAsync(
        string challengeId,
        string leaseId,
        bool terminal,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _database.ScriptEvaluateAsync(
            FailSmsDispatchScript,
            [ChallengeKey(challengeId)],
            [leaseId, terminal ? 1 : 0, SmsDispatchAttemptLimit]);
        return (SmsDispatchFailureState)checked((int)(long)result);
    }

    private async Task<RateLimitDecision> CheckRatesAsync(
        RedisKey[] keys,
        (int Limit, int TtlMilliseconds)[] limits,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var arguments = limits
            .SelectMany(limit => new RedisValue[] { limit.Limit, limit.TtlMilliseconds })
            .ToArray();
        var result = (RedisResult[]?)await _database.ScriptEvaluateAsync(
            RateLimitScript,
            keys,
            arguments);
        return result is not null && (long)result[0] == 1
            ? RateLimitDecision.Allowed
            : RateLimitDecision.Denied(checked((int)(long)result![1]));
    }

    private static PhoneLoginChallenge ReadChallenge(HashEntry[] entries)
    {
        var values = entries.ToDictionary(
            entry => entry.Name.ToString(),
            entry => entry.Value.ToString(),
            StringComparer.Ordinal);
        return new PhoneLoginChallenge(
            values["id"],
            values["phone"],
            ParseUserId(values["userId"]),
            values["client"],
            values["sceneId"],
            values["outId"],
            DateTimeOffset.Parse(values["sentAt"], CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(values["expiresAt"], CultureInfo.InvariantCulture),
            int.Parse(values["attempts"], CultureInfo.InvariantCulture));
    }

    private static PhoneLoginTicket CreateTicket(DateTimeOffset expiresAt)
    {
        var token = CreateSecretToken();
        return new PhoneLoginTicket(Hash(token), token, expiresAt);
    }

    private static string CreateSecretToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static Guid? ParseUserId(string value) =>
        Guid.TryParse(value, out var userId) ? userId : null;

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static RedisKey PreChallengeKey(string id) => $"{KeyPrefix}:pre:{id}";

    private static RedisKey ChallengeKey(string id) => $"{KeyPrefix}:challenge:{id}";

    private static RedisKey VerificationLockKey(string id) =>
        $"{KeyPrefix}:verify-lock:{id}";

    private static RedisKey RateKey(string window, string subject) =>
        $"{KeyPrefix}:rate:{window}:{Hash(subject)}";
}
