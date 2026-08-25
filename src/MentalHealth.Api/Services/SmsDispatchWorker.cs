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
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMilliseconds(250),
        3);
}

public sealed class SmsDispatchWorker : BackgroundService
{
    public const string ConsumerGroup = RedisLoginChallengeStore.SmsDispatchConsumerGroup;

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
        if (settings.MaxAttempts != 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "SMS dispatch requires exactly three persisted attempts.");
        }
        if (settings.ClaimIdleTime <= TimeSpan.Zero
            || settings.PollDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "SMS dispatch timing values must be positive.");
        }

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
                    count: 1);
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
                    count: 1);
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

            await ProcessDispatchAsync(challengeId!, entry.Id, cancellationToken);
        }
    }

    private async Task ProcessDispatchAsync(
        string challengeId,
        RedisValue messageId,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var dispatch = await _store.TryAcquireSmsDispatchAsync(
                challengeId,
                cancellationToken);
            if (dispatch.State is SmsDispatchLeaseState.Missing
                or SmsDispatchLeaseState.Sent
                or SmsDispatchLeaseState.Terminal)
            {
                await AcknowledgeAsync(messageId);
                return;
            }

            if (dispatch.State == SmsDispatchLeaseState.Busy)
            {
                return;
            }

            var challenge = dispatch.Challenge!;
            if (challenge.UserId is null)
            {
                if (await _store.CompleteSmsDispatchAsync(
                    challengeId,
                    dispatch.LeaseId!,
                    cancellationToken))
                {
                    await AcknowledgeAsync(messageId);
                }

                return;
            }

            try
            {
                await _sms.SendAsync(
                    challenge.NationalPhoneNumber,
                    challenge.OutId,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (PhoneLoginProviderException exception)
            {
                var failure = await _store.FailSmsDispatchAsync(
                    challengeId,
                    dispatch.LeaseId!,
                    terminal: exception.Code != "SMS_PROVIDER_UNAVAILABLE",
                    cancellationToken);
                if (failure == SmsDispatchFailureState.Retryable)
                {
                    continue;
                }

                if (failure == SmsDispatchFailureState.Terminal)
                {
                    _logger.LogWarning(
                        "SMS dispatch stopped after {AttemptCount} attempts with result {ResultCode}.",
                        dispatch.Attempt,
                        exception.Code);
                    await AcknowledgeAsync(messageId);
                }

                return;
            }
            catch (Exception)
            {
                var failure = await _store.FailSmsDispatchAsync(
                    challengeId,
                    dispatch.LeaseId!,
                    terminal: false,
                    cancellationToken);
                if (failure == SmsDispatchFailureState.Retryable)
                {
                    continue;
                }

                if (failure == SmsDispatchFailureState.Terminal)
                {
                    _logger.LogWarning(
                        "SMS dispatch stopped after {AttemptCount} attempts with an unexpected result.",
                        dispatch.Attempt);
                    await AcknowledgeAsync(messageId);
                }

                return;
            }

            if (await _store.CompleteSmsDispatchAsync(
                challengeId,
                dispatch.LeaseId!,
                cancellationToken))
            {
                await AcknowledgeAsync(messageId);
            }

            return;
        }
    }

    private async Task AcknowledgeAsync(RedisValue messageId) =>
        _ = await _store.AcknowledgeAndDeleteSmsDispatchAsync(messageId!);
}
