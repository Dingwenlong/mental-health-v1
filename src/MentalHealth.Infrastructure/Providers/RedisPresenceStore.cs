using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Consultations;
using StackExchange.Redis;

namespace MentalHealth.Infrastructure.Providers;

public sealed class RedisPresenceStore(IConnectionMultiplexer redis)
    : IPresenceStore
{
    private static readonly TimeSpan PresenceLifetime = TimeSpan.FromHours(2);

    public async Task<bool> JoinAsync(
        Guid sessionId,
        Guid userId,
        MessageSenderKind kind,
        string connectionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        var database = redis.GetDatabase();
        var memberKey = MemberKey(sessionId, userId, kind);
        var connectionKey = ConnectionKey(connectionId);
        var membership = Membership(sessionId, userId, kind);
        var added = await database.SetAddAsync(memberKey, connectionId)
            .WaitAsync(cancellationToken);
        await database.KeyExpireAsync(memberKey, PresenceLifetime)
            .WaitAsync(cancellationToken);
        await database.SetAddAsync(connectionKey, membership)
            .WaitAsync(cancellationToken);
        await database.KeyExpireAsync(connectionKey, PresenceLifetime)
            .WaitAsync(cancellationToken);
        var count = await database.SetLengthAsync(memberKey)
            .WaitAsync(cancellationToken);
        return added && count == 1;
    }

    public async Task<IReadOnlyList<PresenceChange>> LeaveConnectionAsync(
        string connectionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        var database = redis.GetDatabase();
        var connectionKey = ConnectionKey(connectionId);
        var memberships = await database.SetMembersAsync(connectionKey)
            .WaitAsync(cancellationToken);
        var changes = new List<PresenceChange>();
        foreach (var value in memberships)
        {
            if (!TryParseMembership(value.ToString(), out var membership))
            {
                continue;
            }

            var memberKey = MemberKey(
                membership.SessionId,
                membership.UserId,
                membership.Kind);
            await database.SetRemoveAsync(memberKey, connectionId)
                .WaitAsync(cancellationToken);
            var remaining = await database.SetLengthAsync(memberKey)
                .WaitAsync(cancellationToken);
            if (remaining == 0)
            {
                await database.KeyDeleteAsync(memberKey)
                    .WaitAsync(cancellationToken);
                changes.Add(new PresenceChange(
                    membership.SessionId,
                    membership.UserId,
                    membership.Kind,
                    false));
            }
        }

        await database.KeyDeleteAsync(connectionKey)
            .WaitAsync(cancellationToken);
        return changes;
    }

    private static RedisKey MemberKey(
        Guid sessionId,
        Guid userId,
        MessageSenderKind kind) =>
        $"presence:session:{sessionId:N}:user:{userId:N}:{kind}";

    private static RedisKey ConnectionKey(string connectionId) =>
        $"presence:connection:{connectionId}";

    private static string Membership(
        Guid sessionId,
        Guid userId,
        MessageSenderKind kind) =>
        $"{sessionId:N}|{userId:N}|{kind}";

    private static bool TryParseMembership(
        string value,
        out PresenceMembership membership)
    {
        var parts = value.Split('|');
        if (parts.Length == 3
            && Guid.TryParseExact(parts[0], "N", out var sessionId)
            && Guid.TryParseExact(parts[1], "N", out var userId)
            && Enum.TryParse<MessageSenderKind>(parts[2], out var kind)
            && Enum.IsDefined(kind))
        {
            membership = new PresenceMembership(sessionId, userId, kind);
            return true;
        }

        membership = default;
        return false;
    }

    private readonly record struct PresenceMembership(
        Guid SessionId,
        Guid UserId,
        MessageSenderKind Kind);
}
