using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Identity;
using StackExchange.Redis;

namespace MentalHealth.Api.Services;

public sealed record SmsDispatchWorkerSettings(
    TimeSpan ClaimIdleTime,
    TimeSpan PollDelay,
    int MaxAttempts)
{
    public static SmsDispatchWorkerSettings Default { get; } = new(
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMilliseconds(250),
        3);
}

public sealed class SmsDispatchWorker : BackgroundService
{
    public const string ConsumerGroup = "sms-dispatchers";

    private const string ChallengeIdField = "challengeId";
    private readonly IDatabase _database;
    private readonly ILoginChallengeStore _store;
    private readonly ISmsVerificationProvider _sms;
    private readonly ILogger<SmsDispatchWorker> _logger;
    private readonly SmsDispatchWorkerSettings _settings;
    private readonly RedisValue _consumerName =
        $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";

    public SmsDispatchWorker(
        IConnectionMultiplexer redis,
        ILoginChallengeStore store,
        ISmsVerificationProvider sms,
        ILogger<SmsDispatchWorker> logger)
        : this(redis, store, sms, logger, SmsDispatchWorkerSettings.Default)
    {
    }

    public SmsDispatchWorker(
        IConnectionMultiplexer redis,
        ILoginChallengeStore store,
        ISmsVerificationProvider sms,
        ILogger<SmsDispatchWorker> logger,
        SmsDispatchWorkerSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MaxAttempts, 1);
        _database = redis.GetDatabase();
        _store = store;
        _sms = sms;
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await _database.StreamAutoClaimAsync(
                    RedisLoginChallengeStore.SmsDispatchStream,
                    ConsumerGroup,
                    _consumerName,
                    checked((long)_settings.ClaimIdleTime.TotalMilliseconds),
                    "0-0",
                    count: 10);
                if (!claimed.IsNull && claimed.ClaimedEntries.Length > 0)
                {
                    await ProcessEntriesAsync(claimed.ClaimedEntries, stoppingToken);
                    continue;
                }

                var entries = await _database.StreamReadGroupAsync(
                    RedisLoginChallengeStore.SmsDispatchStream,
                    ConsumerGroup,
                    _consumerName,
                    ">",
                    count: 10);
                if (entries.Length == 0)
                {
                    await Task.Delay(_settings.PollDelay, stoppingToken);
                    continue;
                }

                await ProcessEntriesAsync(entries, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RedisException)
            {
                _logger.LogWarning(
                    "SMS dispatch queue is temporarily unavailable.");
                await Task.Delay(_settings.PollDelay, stoppingToken);
            }
        }
    }

    private async Task EnsureConsumerGroupAsync()
    {
        try
        {
            await _database.StreamCreateConsumerGroupAsync(
                RedisLoginChallengeStore.SmsDispatchStream,
                ConsumerGroup,
                "0-0",
                createStream: true);
        }
        catch (RedisServerException exception)
            when (exception.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal))
        {
        }
    }

    private async Task ProcessEntriesAsync(
        StreamEntry[] entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var challengeId = entry.Values
                .FirstOrDefault(value => value.Name == ChallengeIdField)
                .Value;
            if (challengeId.IsNullOrEmpty)
            {
                await AcknowledgeAsync(entry.Id);
                continue;
            }

            var challenge = await _store.GetChallengeForDispatchAsync(
                challengeId!,
                cancellationToken);
            if (challenge?.UserId is null)
            {
                await AcknowledgeAsync(entry.Id);
                continue;
            }

            var acknowledged = await TrySendAsync(challenge, cancellationToken);
            if (acknowledged)
            {
                await AcknowledgeAsync(entry.Id);
            }
        }
    }

    private async Task<bool> TrySendAsync(
        PhoneLoginChallenge challenge,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _settings.MaxAttempts; attempt++)
        {
            try
            {
                await _sms.SendAsync(
                    challenge.NationalPhoneNumber,
                    challenge.OutId,
                    cancellationToken);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (PhoneLoginProviderException exception)
            {
                if (exception.Code != "SMS_PROVIDER_UNAVAILABLE"
                    || attempt == _settings.MaxAttempts)
                {
                    _logger.LogWarning(
                        "SMS dispatch stopped after {AttemptCount} attempts with result {ResultCode}.",
                        attempt,
                        exception.Code);
                    return true;
                }
            }
            catch (Exception)
            {
                if (attempt == _settings.MaxAttempts)
                {
                    _logger.LogWarning(
                        "SMS dispatch stopped after {AttemptCount} attempts with an unexpected result.",
                        attempt);
                    return true;
                }
            }
        }

        return true;
    }

    private Task AcknowledgeAsync(RedisValue messageId) =>
        _database.StreamAcknowledgeAsync(
            RedisLoginChallengeStore.SmsDispatchStream,
            ConsumerGroup,
            messageId);
}
