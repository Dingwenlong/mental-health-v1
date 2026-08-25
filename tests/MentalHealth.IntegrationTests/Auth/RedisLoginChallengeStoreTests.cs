using System.Security.Cryptography;
using System.Text;
using MentalHealth.Api.Services;
using MentalHealth.Application.Security;
using MentalHealth.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace MentalHealth.IntegrationTests.Auth;

[Collection(RedisLoginCollection.Name)]
public sealed class RedisLoginChallengeStoreTests(RedisLoginFixture fixture)
{
    private const string KeyPrefix = "auth:{phone-login}";

    [Fact]
    public async Task Prechallenge_uses_hashed_key_expires_in_300_seconds_and_is_taken_once()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var state = PreChallenge("+8613800138001");

        var ticket = await store.CreatePreChallengeAsync(state);

        Assert.DoesNotContain(ticket.Token, ticket.Id, StringComparison.Ordinal);
        Assert.Equal(Sha256(ticket.Token), ticket.Id);
        var key = $"{KeyPrefix}:pre:{ticket.Id}";
        var ttl = await fixture.Database.KeyTimeToLiveAsync(key);
        Assert.InRange(ttl!.Value.TotalSeconds, 298, 300);
        var taken = await store.TakePreChallengeAsync(ticket.Token);
        Assert.NotNull(taken);
        Assert.Equal(state.NationalPhoneNumber, taken.NationalPhoneNumber);
        Assert.Equal(state.UserId, taken.UserId);
        Assert.Equal(state.Client, taken.Client);
        Assert.Equal(state.SceneId, taken.SceneId);
        Assert.Equal(ticket.ExpiresAt, taken.ExpiresAt);
        Assert.Null(await store.TakePreChallengeAsync(ticket.Token));
    }

    [Fact]
    public async Task Store_owns_one_300_second_expiry_for_ticket_state_and_redis_ttl()
    {
        await fixture.ResetAsync();
        var now = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(now);
        var store = fixture.CreateStore(timeProvider);

        var preTicket = await store.CreatePreChallengeAsync(
            PreChallenge("+8613800138016"));
        var preState = await store.TakePreChallengeAsync(preTicket.Token);

        Assert.Equal(now.AddSeconds(300), preTicket.ExpiresAt);
        Assert.Equal(now, preState!.CreatedAt);
        Assert.Equal(preTicket.ExpiresAt, preState.ExpiresAt);

        timeProvider.UtcNow = now.AddDays(1);
        var challengeTicket = await store.CreateChallengeAsync(
            Challenge("+8613800138016", Guid.NewGuid()));
        var challengeState = await store.GetChallengeForDispatchAsync(challengeTicket.Id);
        var challengeTtl = await fixture.Database.KeyTimeToLiveAsync(
            $"{KeyPrefix}:challenge:{challengeTicket.Id}");

        Assert.Equal(timeProvider.UtcNow.AddSeconds(300), challengeTicket.ExpiresAt);
        Assert.Equal(timeProvider.UtcNow, challengeState!.SentAt);
        Assert.Equal(challengeTicket.ExpiresAt, challengeState.ExpiresAt);
        Assert.InRange(challengeTtl!.Value.TotalSeconds, 298, 300);
    }

    [Fact]
    public async Task Sms_rate_limit_rejects_second_per_minute_with_exact_retry_after()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        const string phone = "+8613800138002";
        const string ip = "203.0.113.2";

        Assert.True((await store.CheckSmsSendRateAsync(phone, ip)).IsAllowed);
        await fixture.Database.KeyExpireAsync(
            $"{KeyPrefix}:rate:phone:60s:{Sha256(phone)}",
            TimeSpan.FromSeconds(17));

        var denied = await store.CheckSmsSendRateAsync(phone, ip);

        Assert.False(denied.IsAllowed);
        Assert.Equal(17, denied.RetryAfterSeconds);
    }

    [Fact]
    public async Task Concurrent_sms_rate_checks_allow_one_request_without_partial_increments()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        const string phone = "+8613800138013";
        const string ip = "203.0.113.13";

        var decisions = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => store.CheckSmsSendRateAsync(phone, ip)));

        Assert.Single(decisions, decision => decision.IsAllowed);
        await fixture.Database.KeyDeleteAsync(
            $"{KeyPrefix}:rate:phone:60s:{Sha256(phone)}");
        Assert.True((await store.CheckSmsSendRateAsync(
            phone,
            "203.0.113.14")).IsAllowed);
    }

    [Fact]
    public async Task Sms_rate_limit_rejects_sixth_per_hour_and_eleventh_per_day()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        const string hourlyPhone = "+8613800138003";
        const string dailyPhone = "+8613800138004";

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.True((await store.CheckSmsSendRateAsync(
                hourlyPhone,
                $"203.0.113.{attempt + 10}")).IsAllowed);
            await fixture.Database.KeyDeleteAsync(
                $"{KeyPrefix}:rate:phone:60s:{Sha256(hourlyPhone)}");
        }

        var hourlyDenied = await store.CheckSmsSendRateAsync(
            hourlyPhone,
            "203.0.113.30");
        Assert.False(hourlyDenied.IsAllowed);
        Assert.InRange(hourlyDenied.RetryAfterSeconds, 3598, 3600);

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            Assert.True((await store.CheckSmsSendRateAsync(
                dailyPhone,
                $"198.51.100.{attempt}")).IsAllowed);
            await fixture.Database.KeyDeleteAsync([
                $"{KeyPrefix}:rate:phone:60s:{Sha256(dailyPhone)}",
                $"{KeyPrefix}:rate:phone:hour:{Sha256(dailyPhone)}"
            ]);
        }

        var dailyDenied = await store.CheckSmsSendRateAsync(
            dailyPhone,
            "198.51.100.50");
        Assert.False(dailyDenied.IsAllowed);
        Assert.InRange(dailyDenied.RetryAfterSeconds, 86_398, 86_400);
    }

    [Fact]
    public async Task Sms_rate_limit_rejects_eleventh_per_ip_minute_and_101st_per_ip_day()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        const string minuteIp = "203.0.113.40";
        const string dailyIp = "198.51.100.40";

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.True((await store.CheckSmsSendRateAsync(
                $"+86138{attempt:D8}",
                minuteIp)).IsAllowed);
        }

        var minuteDenied = await store.CheckSmsSendRateAsync(
            "+8613911111111",
            minuteIp);
        Assert.False(minuteDenied.IsAllowed);
        Assert.InRange(minuteDenied.RetryAfterSeconds, 58, 60);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            Assert.True((await store.CheckSmsSendRateAsync(
                $"+86139{attempt:D8}",
                dailyIp)).IsAllowed);
            await fixture.Database.KeyDeleteAsync(
                $"{KeyPrefix}:rate:ip:minute:{Sha256(dailyIp)}");
        }

        var dailyDenied = await store.CheckSmsSendRateAsync(
            "+8613711111111",
            dailyIp);
        Assert.False(dailyDenied.IsAllowed);
        Assert.InRange(dailyDenied.RetryAfterSeconds, 86_398, 86_400);
    }

    [Fact]
    public async Task Bootstrap_rate_limit_rejects_31st_per_ip_minute()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        const string ip = "192.0.2.31";

        for (var attempt = 0; attempt < 30; attempt++)
        {
            Assert.True((await store.CheckBootstrapRateAsync(ip)).IsAllowed);
        }

        var denied = await store.CheckBootstrapRateAsync(ip);
        Assert.False(denied.IsAllowed);
        Assert.InRange(denied.RetryAfterSeconds, 58, 60);
    }

    [Fact]
    public async Task Challenge_expires_in_300_seconds_and_rejects_sixth_verification()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var ticket = await store.CreateChallengeAsync(Challenge("+8613800138005", Guid.NewGuid()));
        var ttl = await fixture.Database.KeyTimeToLiveAsync($"{KeyPrefix}:challenge:{ticket.Id}");
        Assert.InRange(ttl!.Value.TotalSeconds, 298, 300);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var lease = await store.TryAcquireVerificationAsync(ticket.Token);
            Assert.True(lease.IsAcquired);
            Assert.Equal(attempt, lease.Challenge!.FailedAttempts);
            var lockTtl = await fixture.Database.KeyTimeToLiveAsync(
                $"{KeyPrefix}:verify-lock:{ticket.Id}");
            Assert.InRange(lockTtl!.Value.TotalSeconds, 28, 30);
            await store.ReleaseVerificationLeaseAsync(ticket.Id, lease.LeaseId!);
        }

        var denied = await store.TryAcquireVerificationAsync(ticket.Token);
        Assert.False(denied.IsAcquired);
        Assert.Null(denied.Challenge);
    }

    [Fact]
    public async Task Concurrent_verification_only_grants_one_lease()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var ticket = await store.CreateChallengeAsync(Challenge("+8613800138006", Guid.NewGuid()));

        var leases = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => store.TryAcquireVerificationAsync(ticket.Token)));

        Assert.Single(leases, lease => lease.IsAcquired);
    }

    [Fact]
    public async Task Successful_consumption_is_atomic_and_preserves_nullable_user_binding()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var userId = Guid.NewGuid();
        var known = await store.CreateChallengeAsync(Challenge("+8613800138007", userId));
        var unknown = await store.CreateChallengeAsync(Challenge("+8613800138008", null));

        var knownLease = await store.TryAcquireVerificationAsync(known.Token);
        Assert.True(knownLease.IsAcquired);
        Assert.Equal(known.Id, knownLease.Challenge!.ChallengeId);
        Assert.Equal(known.Id, knownLease.Challenge.OutId);
        Assert.Equal(userId, knownLease.Challenge.UserId);

        var consumed = await store.ConsumeChallengeAsync(
            known.Id,
            knownLease.LeaseId!);
        Assert.True(consumed.WasConsumed);
        Assert.Equal(userId, consumed.UserId);
        Assert.False((await store.ConsumeChallengeAsync(
            known.Id,
            knownLease.LeaseId!)).WasConsumed);
        Assert.False((await store.TryAcquireVerificationAsync(known.Token)).IsAcquired);

        var unknownLease = await store.TryAcquireVerificationAsync(unknown.Token);
        Assert.True(unknownLease.IsAcquired);
        var unknownConsumed = await store.ConsumeChallengeAsync(
            unknown.Id,
            unknownLease.LeaseId!);
        Assert.True(unknownConsumed.WasConsumed);
        Assert.Null(unknownConsumed.UserId);
    }

    [Fact]
    public async Task Stale_lease_cannot_release_or_consume_a_successor_lease()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var ticket = await store.CreateChallengeAsync(Challenge("+8613800138014", Guid.NewGuid()));
        var staleLease = await store.TryAcquireVerificationAsync(ticket.Token);
        Assert.True(staleLease.IsAcquired);
        const string successorLeaseId = "synthetic-successor-lease";
        var lockKey = $"{KeyPrefix}:verify-lock:{ticket.Id}";
        await fixture.Database.StringSetAsync(
            lockKey,
            successorLeaseId,
            TimeSpan.FromSeconds(30));

        await store.ReleaseVerificationLeaseAsync(ticket.Id, staleLease.LeaseId!);

        Assert.Equal(successorLeaseId, await fixture.Database.StringGetAsync(lockKey));
        Assert.False((await store.ConsumeChallengeAsync(
            ticket.Id,
            staleLease.LeaseId!)).WasConsumed);
        Assert.True((await store.ConsumeChallengeAsync(
            ticket.Id,
            successorLeaseId)).WasConsumed);
    }

    [Fact]
    public async Task Dispatch_stream_contains_only_the_challenge_id()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var ticket = await store.CreateChallengeAsync(Challenge("+8613800138009", Guid.NewGuid()));

        var entries = await fixture.Database.StreamRangeAsync($"{KeyPrefix}:sms:dispatch");
        var entry = Assert.Single(entries);
        var field = Assert.Single(entry.Values);
        Assert.Equal("challengeId", field.Name.ToString());
        Assert.Equal(ticket.Id, field.Value.ToString());
    }

    [Fact]
    public async Task Worker_retries_temporary_failure_then_sends_and_acknowledges()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var sms = new FakeSmsVerificationProvider { TemporaryFailuresRemaining = 2 };
        var ticket = await store.CreateChallengeAsync(Challenge("+8613800138010", Guid.NewGuid()));
        var worker = new SmsDispatchWorker(
            fixture.Connection,
            store,
            sms,
            NullLogger<SmsDispatchWorker>.Instance,
            new SmsDispatchWorkerSettings(
                ClaimIdleTime: TimeSpan.FromMilliseconds(10),
                PollDelay: TimeSpan.FromMilliseconds(10),
                MaxAttempts: 3));

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await sms.WaitUntilSentAsync(ticket.Id);
            Assert.Equal(3, sms.SendAttempts);
            await WaitUntilAsync(async () =>
                (await fixture.Database.StreamPendingAsync(
                    $"{KeyPrefix}:sms:dispatch",
                    SmsDispatchWorker.ConsumerGroup)).PendingMessageCount == 0);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    [Fact]
    public async Task Worker_acknowledges_unknown_user_without_calling_sms_provider()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var sms = new FakeSmsVerificationProvider();
        var ticket = await store.CreateChallengeAsync(Challenge("+8613800138011", null));
        var worker = new SmsDispatchWorker(
            fixture.Connection,
            store,
            sms,
            NullLogger<SmsDispatchWorker>.Instance,
            new SmsDispatchWorkerSettings(
                ClaimIdleTime: TimeSpan.FromMilliseconds(10),
                PollDelay: TimeSpan.FromMilliseconds(10),
                MaxAttempts: 3));

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                try
                {
                    var pending = await fixture.Database.StreamPendingAsync(
                        $"{KeyPrefix}:sms:dispatch",
                        SmsDispatchWorker.ConsumerGroup);
                    var groups = await fixture.Database.StreamGroupInfoAsync(
                        $"{KeyPrefix}:sms:dispatch");
                    return pending.PendingMessageCount == 0
                        && groups.Single().LastDeliveredId != "0-0";
                }
                catch (RedisServerException)
                {
                    return false;
                }
            });
            Assert.Equal(0, sms.SendAttempts);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    [Fact]
    public async Task Worker_claims_an_idle_pending_message_from_a_previous_consumer()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var sms = new FakeSmsVerificationProvider();
        var ticket = await store.CreateChallengeAsync(Challenge("+8613800138012", Guid.NewGuid()));
        await fixture.Database.StreamCreateConsumerGroupAsync(
            $"{KeyPrefix}:sms:dispatch",
            SmsDispatchWorker.ConsumerGroup,
            "0-0");
        var abandoned = await fixture.Database.StreamReadGroupAsync(
            $"{KeyPrefix}:sms:dispatch",
            SmsDispatchWorker.ConsumerGroup,
            "previous-consumer",
            ">",
            count: 1);
        Assert.Single(abandoned);
        await Task.Delay(20);
        var worker = new SmsDispatchWorker(
            fixture.Connection,
            store,
            sms,
            NullLogger<SmsDispatchWorker>.Instance,
            new SmsDispatchWorkerSettings(
                ClaimIdleTime: TimeSpan.FromMilliseconds(10),
                PollDelay: TimeSpan.FromMilliseconds(10),
                MaxAttempts: 3));

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await sms.WaitUntilSentAsync(ticket.Id, TimeSpan.FromMilliseconds(500));
            await WaitUntilAsync(async () =>
                (await fixture.Database.StreamPendingAsync(
                    $"{KeyPrefix}:sms:dispatch",
                    SmsDispatchWorker.ConsumerGroup)).PendingMessageCount == 0);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    [Fact]
    public async Task Worker_terminal_failure_acknowledges_without_logging_sensitive_exception_text()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        const string phone = "+8613800199999";
        const string providerSecret = "captcha-param-code-secret-246810";
        var sms = new ThrowingSmsVerificationProvider(providerSecret);
        var logger = new CollectingLogger<SmsDispatchWorker>();
        _ = await store.CreateChallengeAsync(Challenge(phone, Guid.NewGuid()));
        var worker = new SmsDispatchWorker(
            fixture.Connection,
            store,
            sms,
            logger,
            new SmsDispatchWorkerSettings(
                ClaimIdleTime: TimeSpan.FromMilliseconds(10),
                PollDelay: TimeSpan.FromMilliseconds(10),
                MaxAttempts: 3));

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                try
                {
                    return sms.SendAttempts == 3
                        && (await fixture.Database.StreamPendingAsync(
                            $"{KeyPrefix}:sms:dispatch",
                            SmsDispatchWorker.ConsumerGroup)).PendingMessageCount == 0;
                }
                catch (RedisServerException)
                {
                    return false;
                }
            });
            var logs = string.Join('\n', logger.Messages);
            Assert.DoesNotContain(phone, logs, StringComparison.Ordinal);
            Assert.DoesNotContain(providerSecret, logs, StringComparison.Ordinal);
            Assert.DoesNotContain(FakeSmsVerificationProvider.ValidCode, logs, StringComparison.Ordinal);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    [Fact]
    public async Task Worker_deletes_many_terminal_entries_after_acknowledging_them()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var sms = new FakeSmsVerificationProvider();
        for (var index = 0; index < 50; index++)
        {
            _ = await store.CreateChallengeAsync(
                Challenge($"+86137{index:D8}", null));
        }

        var worker = new SmsDispatchWorker(
            fixture.Connection,
            store,
            sms,
            NullLogger<SmsDispatchWorker>.Instance,
            new SmsDispatchWorkerSettings(
                ClaimIdleTime: TimeSpan.FromMilliseconds(10),
                PollDelay: TimeSpan.FromMilliseconds(10),
                MaxAttempts: 3));

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
            {
                try
                {
                    var groups = await fixture.Database.StreamGroupInfoAsync(
                        $"{KeyPrefix}:sms:dispatch");
                    return groups.Length == 1
                        && groups[0].PendingMessageCount == 0
                        && groups[0].Lag == 0;
                }
                catch (RedisServerException)
                {
                    return false;
                }
            });
            Assert.Equal(
                0,
                await fixture.Database.StreamLengthAsync($"{KeyPrefix}:sms:dispatch"));
            Assert.Equal(0, sms.SendAttempts);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    [Fact]
    public async Task Ack_delete_removes_only_the_acknowledged_entry_and_keeps_other_pending_entries()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        _ = await store.CreateChallengeAsync(Challenge("+8613800138017", null));
        _ = await store.CreateChallengeAsync(Challenge("+8613800138018", null));
        var stream = $"{KeyPrefix}:sms:dispatch";
        await fixture.Database.StreamCreateConsumerGroupAsync(
            stream,
            SmsDispatchWorker.ConsumerGroup,
            "0-0");
        var pendingEntries = await fixture.Database.StreamReadGroupAsync(
            stream,
            SmsDispatchWorker.ConsumerGroup,
            "pending-owner",
            ">",
            count: 2);
        Assert.Equal(2, pendingEntries.Length);

        Assert.True(await store.AcknowledgeAndDeleteSmsDispatchAsync(
            pendingEntries[0].Id!));

        var remaining = await fixture.Database.StreamRangeAsync(stream);
        Assert.Single(remaining);
        Assert.Equal(pendingEntries[1].Id, remaining[0].Id);
        Assert.Equal(
            1,
            (await fixture.Database.StreamPendingAsync(
                stream,
                SmsDispatchWorker.ConsumerGroup)).PendingMessageCount);
    }

    [Fact]
    public async Task Dispatch_attempts_are_persisted_and_capped_across_owner_leases()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var ticket = await store.CreateChallengeAsync(
            Challenge("+8613800138019", Guid.NewGuid()));

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var lease = await store.TryAcquireSmsDispatchAsync(ticket.Id);
            Assert.Equal(SmsDispatchLeaseState.Acquired, lease.State);
            Assert.Equal(attempt, lease.Attempt);
            var failure = await store.FailSmsDispatchAsync(
                ticket.Id,
                lease.LeaseId!,
                terminal: false);
            Assert.Equal(
                attempt == 3
                    ? SmsDispatchFailureState.Terminal
                    : SmsDispatchFailureState.Retryable,
                failure);
        }

        var exhausted = await store.TryAcquireSmsDispatchAsync(ticket.Id);
        Assert.Equal(SmsDispatchLeaseState.Terminal, exhausted.State);
        Assert.Equal(3, exhausted.Attempt);
    }

    [Fact]
    public async Task Persisted_sent_state_prevents_resend_when_pending_message_is_reclaimed()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var sms = new FakeSmsVerificationProvider();
        var ticket = await store.CreateChallengeAsync(
            Challenge("+8613800138020", Guid.NewGuid()));
        var stream = $"{KeyPrefix}:sms:dispatch";
        await fixture.Database.StreamCreateConsumerGroupAsync(
            stream,
            SmsDispatchWorker.ConsumerGroup,
            "0-0");
        var pending = await fixture.Database.StreamReadGroupAsync(
            stream,
            SmsDispatchWorker.ConsumerGroup,
            "crashed-after-success",
            ">",
            count: 1);
        Assert.Single(pending);
        var dispatch = await store.TryAcquireSmsDispatchAsync(ticket.Id);
        Assert.Equal(SmsDispatchLeaseState.Acquired, dispatch.State);
        Assert.True(await store.CompleteSmsDispatchAsync(
            ticket.Id,
            dispatch.LeaseId!));
        await Task.Delay(20);
        var worker = CreateWorker(fixture, store, sms);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
                await fixture.Database.StreamLengthAsync(stream) == 0);
            Assert.Equal(0, sms.SendAttempts);
            Assert.Equal(
                SmsDispatchLeaseState.Sent,
                (await store.TryAcquireSmsDispatchAsync(ticket.Id)).State);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    [Fact]
    public async Task Persisted_attempts_limit_worker_to_one_final_try_after_restart()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var sms = new FakeSmsVerificationProvider { TemporaryFailuresRemaining = 10 };
        var ticket = await store.CreateChallengeAsync(
            Challenge("+8613800138021", Guid.NewGuid()));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var seeded = await store.TryAcquireSmsDispatchAsync(ticket.Id);
            Assert.Equal(SmsDispatchLeaseState.Acquired, seeded.State);
            Assert.Equal(
                SmsDispatchFailureState.Retryable,
                await store.FailSmsDispatchAsync(
                    ticket.Id,
                    seeded.LeaseId!,
                    terminal: false));
        }

        var firstWorker = CreateWorker(fixture, store, sms);
        await firstWorker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
                sms.SendAttempts == 1
                && await fixture.Database.StreamLengthAsync(
                    $"{KeyPrefix}:sms:dispatch") == 0);
        }
        finally
        {
            await firstWorker.StopAsync(CancellationToken.None);
            firstWorker.Dispose();
        }

        await fixture.Database.StreamAddAsync(
            $"{KeyPrefix}:sms:dispatch",
            "challengeId",
            ticket.Id);
        var restartedWorker = CreateWorker(fixture, store, sms);
        await restartedWorker.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(async () =>
                await fixture.Database.StreamLengthAsync(
                    $"{KeyPrefix}:sms:dispatch") == 0);
            Assert.Equal(1, sms.SendAttempts);
            var exhausted = await store.TryAcquireSmsDispatchAsync(ticket.Id);
            Assert.Equal(SmsDispatchLeaseState.Terminal, exhausted.State);
            Assert.Equal(3, exhausted.Attempt);
        }
        finally
        {
            await restartedWorker.StopAsync(CancellationToken.None);
            restartedWorker.Dispose();
        }
    }

    [Fact]
    public async Task Owner_lease_prevents_parallel_send_when_slow_message_is_claimed()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        var slowSms = new BlockingSmsVerificationProvider();
        var competingSms = new FakeSmsVerificationProvider();
        var ticket = await store.CreateChallengeAsync(
            Challenge("+8613800138022", Guid.NewGuid()));
        var firstWorker = CreateWorker(fixture, store, slowSms);
        var secondWorker = CreateWorker(fixture, store, competingSms);

        await firstWorker.StartAsync(CancellationToken.None);
        try
        {
            await slowSms.WaitUntilStartedAsync();
            await secondWorker.StartAsync(CancellationToken.None);
            await Task.Delay(150);
            Assert.Equal(0, competingSms.SendAttempts);
            slowSms.Release();
            await WaitUntilAsync(async () =>
                await fixture.Database.StreamLengthAsync(
                    $"{KeyPrefix}:sms:dispatch") == 0);
            Assert.Equal(1, slowSms.SendAttempts);
            Assert.Equal(0, competingSms.SendAttempts);
            Assert.Equal(
                SmsDispatchLeaseState.Sent,
                (await store.TryAcquireSmsDispatchAsync(ticket.Id)).State);
        }
        finally
        {
            slowSms.Release();
            await secondWorker.StopAsync(CancellationToken.None);
            secondWorker.Dispose();
            await firstWorker.StopAsync(CancellationToken.None);
            firstWorker.Dispose();
        }
    }

    [Fact]
    public async Task All_phone_login_keys_share_one_explicit_cluster_hash_slot()
    {
        await fixture.ResetAsync();
        var store = fixture.CreateStore();
        _ = await store.CreatePreChallengeAsync(PreChallenge("+8613800138015"));
        _ = await store.CheckBootstrapRateAsync("203.0.113.15");
        _ = await store.CheckSmsSendRateAsync("+8613800138015", "203.0.113.15");
        var challenge = await store.CreateChallengeAsync(
            Challenge("+8613800138015", Guid.NewGuid()));
        Assert.True((await store.TryAcquireVerificationAsync(challenge.Token)).IsAcquired);
        var server = fixture.Connection.GetServer(fixture.Connection.GetEndPoints().Single());

        var keys = server.Keys(pattern: "auth:*")
            .Select(key => key.ToString())
            .ToArray();

        Assert.Contains(keys, key => key.Contains(":pre:", StringComparison.Ordinal));
        Assert.Contains(keys, key => key.Contains(":challenge:", StringComparison.Ordinal));
        Assert.Contains(keys, key => key.Contains(":verify-lock:", StringComparison.Ordinal));
        Assert.Contains(keys, key => key.Contains(":rate:", StringComparison.Ordinal));
        Assert.Contains($"{KeyPrefix}:sms:dispatch", keys);
        Assert.All(keys, key => Assert.Contains("{phone-login}", key, StringComparison.Ordinal));
        Assert.Single(keys.Select(RedisClusterSlot).Distinct());
    }

    private static PhoneLoginPreChallengeDraft PreChallenge(string phone) =>
        new(
            phone,
            Guid.NewGuid(),
            "android",
            "android-scene");

    private static PhoneLoginChallengeDraft Challenge(string phone, Guid? userId) =>
        new(
            phone,
            userId,
            "android",
            "android-scene");

    private static SmsDispatchWorker CreateWorker(
        RedisLoginFixture fixture,
        ILoginChallengeStore store,
        ISmsVerificationProvider sms) =>
        new(
            fixture.Connection,
            store,
            sms,
            NullLogger<SmsDispatchWorker>.Instance,
            new SmsDispatchWorkerSettings(
                ClaimIdleTime: TimeSpan.FromMilliseconds(10),
                PollDelay: TimeSpan.FromMilliseconds(10),
                MaxAttempts: 3));

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static int RedisClusterSlot(string key)
    {
        var open = key.IndexOf('{');
        var close = open < 0 ? -1 : key.IndexOf('}', open + 1);
        var hashInput = open >= 0 && close > open + 1
            ? key[(open + 1)..close]
            : key;
        ushort crc = 0;
        foreach (var value in Encoding.UTF8.GetBytes(hashInput))
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (ushort)((crc & 0x8000) != 0
                    ? (crc << 1) ^ 0x1021
                    : crc << 1);
            }
        }

        return crc % 16_384;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}

file sealed class ThrowingSmsVerificationProvider(string failureText)
    : ISmsVerificationProvider
{
    private int _sendAttempts;

    public int SendAttempts => Volatile.Read(ref _sendAttempts);

    public Task SendAsync(
        string nationalPhoneNumber,
        string outId,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _sendAttempts);
        throw new InvalidOperationException(failureText);
    }

    public Task<bool> CheckAsync(
        string nationalPhoneNumber,
        string outId,
        string code,
        CancellationToken cancellationToken) => Task.FromResult(false);
}

file sealed class CollectingLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = [];

    public IReadOnlyList<string> Messages => _messages;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _messages.Add($"{formatter(state, exception)}\n{exception}");
    }
}

file sealed class BlockingSmsVerificationProvider : ISmsVerificationProvider
{
    private readonly TaskCompletionSource _started = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _sendAttempts;

    public int SendAttempts => Volatile.Read(ref _sendAttempts);

    public async Task SendAsync(
        string nationalPhoneNumber,
        string outId,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _sendAttempts);
        _started.TrySetResult();
        await _released.Task.WaitAsync(cancellationToken);
    }

    public Task<bool> CheckAsync(
        string nationalPhoneNumber,
        string outId,
        string code,
        CancellationToken cancellationToken) => Task.FromResult(false);

    public Task WaitUntilStartedAsync() =>
        _started.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void Release() => _released.TrySetResult();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RedisLoginCollection : ICollectionFixture<RedisLoginFixture>
{
    public const string Name = "redis-login";
}

public sealed class RedisLoginFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder("redis:8-alpine").Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public IDatabase Database => Connection.GetDatabase();

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        var configuration = ConfigurationOptions.Parse(_redis.GetConnectionString());
        configuration.AllowAdmin = true;
        Connection = await ConnectionMultiplexer.ConnectAsync(configuration);
    }

    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync();
        await _redis.DisposeAsync();
    }

    public RedisLoginChallengeStore CreateStore(TimeProvider? timeProvider = null) =>
        new(Connection, timeProvider ?? TimeProvider.System);

    public async Task ResetAsync()
    {
        foreach (var endpoint in Connection.GetEndPoints())
        {
            await Connection.GetServer(endpoint).FlushDatabaseAsync();
        }
    }
}

file sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
