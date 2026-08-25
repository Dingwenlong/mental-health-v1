using System.Net;
using System.Security.Cryptography;
using MentalHealth.Application.Security;
using MentalHealth.Contracts.Common;
using MentalHealth.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    UserManager<AppUser> userManager,
    ILoginChallengeStore challengeStore,
    ICaptchaVerifier captchaVerifier,
    ISmsVerificationProvider smsVerificationProvider,
    IJwtTokenService tokenService,
    ILoginFailureDelay failureDelay,
    TimeProvider timeProvider,
    IOptions<AliyunPhoneLoginOptions> phoneLoginOptions) : ControllerBase
{
    private static readonly TimeSpan FailureDelayFloor = TimeSpan.FromMilliseconds(800);
    private const int FailureDelayJitterMilliseconds = 200;
    private readonly AliyunPhoneLoginOptions _phoneLoginOptions = phoneLoginOptions.Value;

    [AllowAnonymous]
    [HttpPost("captcha/bootstrap")]
    public async Task<IActionResult> BootstrapCaptcha(
        CaptchaBootstrapRequest request,
        CancellationToken cancellationToken)
    {
        if (!PhoneNumberNormalizer.TryNormalizeMainlandChina(
                request.PhoneNumber ?? string.Empty,
                out var phoneNumber))
        {
            return ProblemResult(
                StatusCodes.Status400BadRequest,
                ApiProblemCodes.InvalidPhoneNumber,
                "手机号格式不正确");
        }

        var sceneId = request.Client switch
        {
            "admin" => _phoneLoginOptions.AdminSceneId,
            "android" => _phoneLoginOptions.AndroidSceneId,
            _ => null
        };
        if (sceneId is null)
        {
            return ProblemResult(
                StatusCodes.Status400BadRequest,
                ApiProblemCodes.LoginChallengeInvalid,
                "登录请求无效");
        }

        if (!_phoneLoginOptions.Enabled)
        {
            return ProviderUnavailable();
        }

        try
        {
            var rate = await challengeStore.CheckBootstrapRateAsync(
                SourceIp(),
                cancellationToken);
            if (!rate.IsAllowed)
            {
                return RateLimited(rate.RetryAfterSeconds);
            }

            var userId = await userManager.Users
                .AsNoTracking()
                .Where(user => user.PhoneNumber == phoneNumber)
                .Select(user => (Guid?)user.Id)
                .SingleOrDefaultAsync(cancellationToken);
            var ticket = await challengeStore.CreatePreChallengeAsync(
                new PhoneLoginPreChallengeDraft(
                    phoneNumber,
                    userId,
                    request.Client!,
                    sceneId),
                cancellationToken);
            var encryptedSceneId = new EncryptedSceneIdFactory(
                    _phoneLoginOptions.CaptchaEkey)
                .Create(sceneId);
            return Ok(new CaptchaBootstrapResponse(
                ticket.Token,
                _phoneLoginOptions.Prefix,
                encryptedSceneId,
                FormatUtc(ticket.ExpiresAt)));
        }
        catch (RedisException)
        {
            return ProviderUnavailable();
        }
    }

    [AllowAnonymous]
    [HttpPost("sms/challenges")]
    public async Task<IActionResult> CreateSmsChallenge(
        SmsChallengeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var preChallenge = string.IsNullOrWhiteSpace(request.PreChallengeToken)
                ? null
                : await challengeStore.TakePreChallengeAsync(
                    request.PreChallengeToken,
                    cancellationToken);
            if (preChallenge is null)
            {
                return ProblemResult(
                    StatusCodes.Status400BadRequest,
                    ApiProblemCodes.LoginChallengeInvalid,
                    "登录请求已失效");
            }

            var rate = await challengeStore.CheckSmsSendRateAsync(
                preChallenge.NationalPhoneNumber,
                SourceIp(),
                cancellationToken);
            if (!rate.IsAllowed)
            {
                return RateLimited(rate.RetryAfterSeconds);
            }

            bool captchaAccepted;
            try
            {
                captchaAccepted = await captchaVerifier.VerifyAsync(
                    preChallenge.SceneId,
                    request.CaptchaVerifyParam ?? string.Empty,
                    cancellationToken);
            }
            catch (PhoneLoginProviderException)
            {
                return ProviderUnavailable();
            }

            if (!captchaAccepted)
            {
                return ProblemResult(
                    StatusCodes.Status422UnprocessableEntity,
                    ApiProblemCodes.CaptchaFailed,
                    "人机验证未通过");
            }

            var ticket = await challengeStore.CreateChallengeAsync(
                new PhoneLoginChallengeDraft(
                    preChallenge.NationalPhoneNumber,
                    preChallenge.UserId,
                    preChallenge.Client,
                    preChallenge.SceneId),
                cancellationToken);
            var createdAt = ticket.ExpiresAt.AddMinutes(-5);
            return Accepted(new SmsChallengeResponse(
                ticket.Id,
                ticket.Token,
                FormatUtc(ticket.ExpiresAt),
                FormatUtc(createdAt.AddMinutes(1))));
        }
        catch (RedisException)
        {
            return ProviderUnavailable();
        }
    }

    [AllowAnonymous]
    [HttpPost("sms/verify")]
    public async Task<IActionResult> VerifySmsCode(
        SmsVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var startedTimestamp = timeProvider.GetTimestamp();
        if (string.IsNullOrWhiteSpace(request.ChallengeToken)
            || request.Code is null
            || request.Code.Length != 6
            || request.Code.Any(character => character is < '0' or > '9'))
        {
            await DelayFailureAsync(startedTimestamp, cancellationToken);
            return InvalidSmsCode();
        }

        VerificationLease lease;
        try
        {
            lease = await challengeStore.TryAcquireVerificationAsync(
                request.ChallengeToken,
                cancellationToken);
        }
        catch (RedisException)
        {
            await DelayFailureAsync(startedTimestamp, cancellationToken);
            return ProviderUnavailable();
        }

        if (!lease.IsAcquired || lease.Challenge is null || lease.LeaseId is null)
        {
            await DelayFailureAsync(startedTimestamp, cancellationToken);
            return InvalidSmsCode();
        }

        var challenge = lease.Challenge;
        if (challenge.UserId is null)
        {
            if (!await TryReleaseLeaseAsync(challenge.ChallengeId, lease.LeaseId, cancellationToken))
            {
                await DelayFailureAsync(startedTimestamp, cancellationToken);
                return ProviderUnavailable();
            }

            await DelayFailureAsync(startedTimestamp, cancellationToken);
            return InvalidSmsCode();
        }

        bool codeAccepted;
        try
        {
            codeAccepted = await smsVerificationProvider.CheckAsync(
                challenge.NationalPhoneNumber,
                challenge.OutId,
                request.Code,
                cancellationToken);
        }
        catch (PhoneLoginProviderException)
        {
            _ = await TryReleaseLeaseAsync(
                challenge.ChallengeId,
                lease.LeaseId,
                cancellationToken);
            await DelayFailureAsync(startedTimestamp, cancellationToken);
            return ProviderUnavailable();
        }

        if (!codeAccepted)
        {
            if (!await TryReleaseLeaseAsync(challenge.ChallengeId, lease.LeaseId, cancellationToken))
            {
                await DelayFailureAsync(startedTimestamp, cancellationToken);
                return ProviderUnavailable();
            }

            await DelayFailureAsync(startedTimestamp, cancellationToken);
            return InvalidSmsCode();
        }

        ChallengeConsumption consumption;
        try
        {
            consumption = await challengeStore.ConsumeChallengeAsync(
                challenge.ChallengeId,
                lease.LeaseId,
                cancellationToken);
        }
        catch (RedisException)
        {
            await DelayFailureAsync(startedTimestamp, cancellationToken);
            return ProviderUnavailable();
        }

        if (!consumption.WasConsumed || consumption.UserId != challenge.UserId)
        {
            await DelayFailureAsync(startedTimestamp, cancellationToken);
            return InvalidSmsCode();
        }

        var user = await userManager.FindByIdAsync(challenge.UserId.Value.ToString());
        if (user is null)
        {
            await DelayFailureAsync(startedTimestamp, cancellationToken);
            return InvalidSmsCode();
        }

        user.PhoneNumberConfirmed = true;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            throw new InvalidOperationException("Failed to confirm the login phone number.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = tokenService.Issue(new JwtTokenSubject(
            user.Id,
            challenge.NationalPhoneNumber,
            roles.ToArray(),
            user.SubjectId,
            user.PractitionerId));
        return Ok(new TokenResponse(token.Value, token.ExpiresAt));
    }

    private async Task<bool> TryReleaseLeaseAsync(
        string challengeId,
        string leaseId,
        CancellationToken cancellationToken)
    {
        try
        {
            await challengeStore.ReleaseVerificationLeaseAsync(
                challengeId,
                leaseId,
                cancellationToken);
            return true;
        }
        catch (RedisException)
        {
            return false;
        }
    }

    private Task DelayFailureAsync(long startedTimestamp, CancellationToken cancellationToken)
    {
        var jitter = RandomNumberGenerator.GetInt32(
            FailureDelayJitterMilliseconds + 1);
        return failureDelay.DelayAsync(
            FailureDelayFloor.Add(TimeSpan.FromMilliseconds(jitter)),
            startedTimestamp,
            cancellationToken);
    }

    private string SourceIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString()
        ?? IPAddress.None.ToString();

    private static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O");

    private ObjectResult RateLimited(int retryAfterSeconds)
    {
        Response.Headers.RetryAfter = Math.Max(1, retryAfterSeconds).ToString();
        return ProblemResult(
            StatusCodes.Status429TooManyRequests,
            ApiProblemCodes.SmsRateLimited,
            "请求过于频繁，请稍后重试");
    }

    private static ObjectResult InvalidSmsCode() => ProblemResult(
        StatusCodes.Status401Unauthorized,
        ApiProblemCodes.InvalidSmsCode,
        "验证码无效或已过期");

    private static ObjectResult ProviderUnavailable() => ProblemResult(
        StatusCodes.Status503ServiceUnavailable,
        ApiProblemCodes.AuthProviderUnavailable,
        "登录服务暂时不可用");

    private static ObjectResult ProblemResult(int status, string code, string title)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}

public sealed record CaptchaBootstrapRequest(string? PhoneNumber, string? Client);

public sealed record CaptchaBootstrapResponse(
    string PreChallengeToken,
    string Prefix,
    string EncryptedSceneId,
    string ExpiresAt);

public sealed record SmsChallengeRequest(
    string? PreChallengeToken,
    string? CaptchaVerifyParam);

public sealed record SmsChallengeResponse(
    string ChallengeId,
    string ChallengeToken,
    string ExpiresAt,
    string ResendAt);

public sealed record SmsVerificationRequest(string? ChallengeToken, string? Code);

public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
