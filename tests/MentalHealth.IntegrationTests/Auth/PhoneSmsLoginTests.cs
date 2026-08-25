using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealth.Contracts.Common;

namespace MentalHealth.IntegrationTests.Auth;

[Collection(AuthApiCollection.Name)]
public sealed class PhoneSmsLoginTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Registered_client_completes_captcha_sms_and_receives_phone_jwt()
    {
        await fixture.ResetPhoneLoginAsync();
        var bootstrap = await fixture.BootstrapAsync("13800138001", "android");
        Assert.Equal("xfkdn8", bootstrap.Prefix);

        var challenge = await fixture.CreateChallengeAsync(
            bootstrap.PreChallengeToken,
            FakeCaptchaVerifier.ValidParam);
        await fixture.Sms.WaitUntilSentAsync(challenge.ChallengeId);

        using var response = await VerifyAsync(
            challenge.ChallengeToken,
            FakeSmsVerificationProvider.ValidCode);

        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>())!
            .AccessToken;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, claim =>
            claim.Type == "phone_number" && claim.Value == "+8613800138001");
        Assert.Contains(jwt.Claims, claim =>
            claim.Type == "scope" && claim.Value == "api");
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == "email");
        Assert.True(await fixture.IsPhoneConfirmedAsync("+8613800138001"));
    }

    [Fact]
    public async Task Unknown_and_registered_accounts_have_the_same_public_flow_shape()
    {
        await fixture.ResetPhoneLoginAsync();
        using var unknownBootstrap = await BootstrapRawAsync("13700137003", "android");
        using var knownBootstrap = await BootstrapRawAsync("13800138001", "android");

        Assert.Equal(knownBootstrap.StatusCode, unknownBootstrap.StatusCode);
        var unknownBootstrapJson = await unknownBootstrap.Content.ReadAsStringAsync();
        var knownBootstrapJson = await knownBootstrap.Content.ReadAsStringAsync();
        Assert.Equal(ReadShape(knownBootstrapJson), ReadShape(unknownBootstrapJson));

        var unknown = JsonSerializer.Deserialize<BootstrapResponse>(
            unknownBootstrapJson,
            JsonSerializerOptions.Web);
        var known = JsonSerializer.Deserialize<BootstrapResponse>(
            knownBootstrapJson,
            JsonSerializerOptions.Web);
        using var unknownChallenge = await CreateChallengeRawAsync(
            unknown!.PreChallengeToken,
            FakeCaptchaVerifier.ValidParam);
        using var knownChallenge = await CreateChallengeRawAsync(
            known!.PreChallengeToken,
            FakeCaptchaVerifier.ValidParam);

        Assert.Equal(knownChallenge.StatusCode, unknownChallenge.StatusCode);
        var unknownChallengeJson = await unknownChallenge.Content.ReadAsStringAsync();
        var knownChallengeJson = await knownChallenge.Content.ReadAsStringAsync();
        Assert.Equal(ReadShape(knownChallengeJson), ReadShape(unknownChallengeJson));
        var knownBody = JsonSerializer.Deserialize<ChallengeResponse>(
            knownChallengeJson,
            JsonSerializerOptions.Web);
        await fixture.Sms.WaitUntilSentAsync(knownBody!.ChallengeId);
        Assert.Equal(1, fixture.Sms.SendAttempts);
    }

    [Fact]
    public async Task Captcha_failure_does_not_enqueue_sms()
    {
        await fixture.ResetPhoneLoginAsync();
        var bootstrap = await fixture.BootstrapAsync("13800138001");
        using var response = await CreateChallengeRawAsync(
            bootstrap.PreChallengeToken,
            "synthetic-captcha-rejected");

        await AssertProblemAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            ApiProblemCodes.CaptchaFailed);
        Assert.Equal(0, fixture.Sms.SendAttempts);
    }

    [Fact]
    public async Task Invalid_phone_challenge_and_rate_limit_use_stable_problem_codes()
    {
        await fixture.ResetPhoneLoginAsync();
        using var invalidPhone = await BootstrapRawAsync("not-a-phone", "android");
        await AssertProblemAsync(
            invalidPhone,
            HttpStatusCode.BadRequest,
            ApiProblemCodes.InvalidPhoneNumber);

        using var invalidChallenge = await CreateChallengeRawAsync(
            "missing-pre-challenge",
            FakeCaptchaVerifier.ValidParam);
        await AssertProblemAsync(
            invalidChallenge,
            HttpStatusCode.BadRequest,
            ApiProblemCodes.LoginChallengeInvalid);

        var first = await fixture.BootstrapAsync("13800138001");
        using var accepted = await CreateChallengeRawAsync(
            first.PreChallengeToken,
            FakeCaptchaVerifier.ValidParam);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        using var reusedPreChallenge = await CreateChallengeRawAsync(
            first.PreChallengeToken,
            FakeCaptchaVerifier.ValidParam);
        await AssertProblemAsync(
            reusedPreChallenge,
            HttpStatusCode.BadRequest,
            ApiProblemCodes.LoginChallengeInvalid);
        var second = await fixture.BootstrapAsync("13800138001");
        using var limited = await CreateChallengeRawAsync(
            second.PreChallengeToken,
            FakeCaptchaVerifier.ValidParam);
        await AssertProblemAsync(
            limited,
            HttpStatusCode.TooManyRequests,
            ApiProblemCodes.SmsRateLimited);
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task Bad_expired_and_fifth_attempt_verifications_fail_identically()
    {
        await fixture.ResetPhoneLoginAsync();
        using var malformed = await VerifyAsync("expired-challenge", "12x");
        var expected = await ReadProblemSignatureAsync(malformed);
        Assert.Equal(
            (HttpStatusCode.Unauthorized, ApiProblemCodes.InvalidSmsCode),
            expected);

        var challenge = await CreateRegisteredChallengeAsync();
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            using var response = await VerifyAsync(challenge.ChallengeToken, "135790");
            Assert.Equal(expected, await ReadProblemSignatureAsync(response));
        }

        Assert.Equal(7, fixture.FailureDelay.Targets.Count);
        Assert.All(fixture.FailureDelay.Targets, target =>
            Assert.InRange(target, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(1000)));

        await fixture.ResetPhoneLoginAsync();
        var unknownBootstrap = await fixture.BootstrapAsync("13700137004");
        var unknownChallenge = await fixture.CreateChallengeAsync(
            unknownBootstrap.PreChallengeToken,
            FakeCaptchaVerifier.ValidParam);
        using var unknown = await VerifyAsync(
            unknownChallenge.ChallengeToken,
            FakeSmsVerificationProvider.ValidCode);
        Assert.Equal(expected, await ReadProblemSignatureAsync(unknown));

        Assert.Single(fixture.FailureDelay.Targets);
        Assert.All(fixture.FailureDelay.Targets, target =>
            Assert.InRange(target, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(1000)));
    }

    [Fact]
    public async Task Concurrent_repeat_verification_succeeds_only_once()
    {
        await fixture.ResetPhoneLoginAsync();
        var challenge = await CreateRegisteredChallengeAsync();

        var responses = await Task.WhenAll(
            VerifyAsync(challenge.ChallengeToken, FakeSmsVerificationProvider.ValidCode),
            VerifyAsync(challenge.ChallengeToken, FakeSmsVerificationProvider.ValidCode));
        try
        {
            Assert.Equal(1, responses.Count(response => response.IsSuccessStatusCode));
            var failed = responses.Single(response => !response.IsSuccessStatusCode);
            Assert.Equal(
                (HttpStatusCode.Unauthorized, ApiProblemCodes.InvalidSmsCode),
                await ReadProblemSignatureAsync(failed));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Provider_or_Redis_outage_returns_503_and_does_not_login()
    {
        await fixture.ResetPhoneLoginAsync();
        var bootstrap = await fixture.BootstrapAsync("13800138001");
        fixture.Captcha.ProviderUnavailable = true;
        using var captchaUnavailable = await CreateChallengeRawAsync(
            bootstrap.PreChallengeToken,
            FakeCaptchaVerifier.ValidParam);
        await AssertProblemAsync(
            captchaUnavailable,
            HttpStatusCode.ServiceUnavailable,
            ApiProblemCodes.AuthProviderUnavailable);

        await fixture.ResetPhoneLoginAsync();
        var challenge = await CreateRegisteredChallengeAsync();
        fixture.Sms.CheckUnavailable = true;
        using var smsUnavailable = await VerifyAsync(
            challenge.ChallengeToken,
            FakeSmsVerificationProvider.ValidCode);
        await AssertProblemAsync(
            smsUnavailable,
            HttpStatusCode.ServiceUnavailable,
            ApiProblemCodes.AuthProviderUnavailable);
        Assert.False(await fixture.IsPhoneConfirmedAsync("+8613800138001"));

        await fixture.ResetPhoneLoginAsync();
        fixture.ChallengeStore.Unavailable = true;
        using var redisUnavailable = await BootstrapRawAsync("13800138001", "android");
        await AssertProblemAsync(
            redisUnavailable,
            HttpStatusCode.ServiceUnavailable,
            ApiProblemCodes.AuthProviderUnavailable);
        fixture.ChallengeStore.Unavailable = false;
    }

    [Fact]
    public async Task Post_consumption_completion_failure_is_delayed_and_returns_503()
    {
        await fixture.ResetPhoneLoginAsync();
        var challenge = await CreateRegisteredChallengeAsync();
        fixture.Jwt.ThrowOnIssue = true;
        fixture.ClearCapturedLogs();

        using var failed = await VerifyAsync(
            challenge.ChallengeToken,
            FakeSmsVerificationProvider.ValidCode);

        Assert.DoesNotContain(
            "accessToken",
            await failed.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
        await AssertProblemAsync(
            failed,
            HttpStatusCode.ServiceUnavailable,
            ApiProblemCodes.AuthProviderUnavailable);
        var target = Assert.Single(fixture.FailureDelay.Targets);
        Assert.InRange(
            target,
            TimeSpan.FromMilliseconds(800),
            TimeSpan.FromMilliseconds(1000));
        Assert.DoesNotContain(
            fixture.CapturedLogs,
            entry => new[]
            {
                ControllableJwtTokenService.FailureMessage,
                "+8613800138001",
                FakeSmsVerificationProvider.ValidCode,
                challenge.ChallengeToken,
                challenge.ChallengeId
            }.Any(value => entry.Message.Contains(value, StringComparison.Ordinal)));

        fixture.Jwt.ThrowOnIssue = false;
        using var consumed = await VerifyAsync(
            challenge.ChallengeToken,
            FakeSmsVerificationProvider.ValidCode);
        await AssertProblemAsync(
            consumed,
            HttpStatusCode.Unauthorized,
            ApiProblemCodes.InvalidSmsCode);
    }

    [Fact]
    public async Task Password_and_Mfa_routes_are_removed()
    {
        using var login = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "abc@qq.com", password = "unused" });
        using var mfa = await fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/mfa/setup",
            new { totpCode = "123456" });

        Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, mfa.StatusCode);
    }

    private async Task<ChallengeResponse> CreateRegisteredChallengeAsync()
    {
        var bootstrap = await fixture.BootstrapAsync("13800138001");
        var challenge = await fixture.CreateChallengeAsync(
            bootstrap.PreChallengeToken,
            FakeCaptchaVerifier.ValidParam);
        await fixture.Sms.WaitUntilSentAsync(challenge.ChallengeId);
        return challenge;
    }

    private Task<HttpResponseMessage> BootstrapRawAsync(string phoneNumber, string client) =>
        fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/captcha/bootstrap",
            new { phoneNumber, client });

    private Task<HttpResponseMessage> CreateChallengeRawAsync(
        string preChallengeToken,
        string captchaVerifyParam) =>
        fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/sms/challenges",
            new { preChallengeToken, captchaVerifyParam });

    private Task<HttpResponseMessage> VerifyAsync(string challengeToken, string code) =>
        fixture.Client.PostAsJsonAsync(
            "/api/v1/auth/sms/verify",
            new { challengeToken, code });

    private static async Task<(HttpStatusCode Status, string Code)>
        ReadProblemSignatureAsync(HttpResponseMessage response)
    {
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        return (
            response.StatusCode,
            body.RootElement.GetProperty("code").GetString()!);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code) =>
        Assert.Equal((status, code), await ReadProblemSignatureAsync(response));

    private static string ReadShape(string json)
    {
        using var body = JsonDocument.Parse(json);
        return string.Join(
            ";",
            body.RootElement.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => property.Value.ValueKind == JsonValueKind.String
                    ? $"{property.Name}:{property.Value.GetString()!.Length}"
                    : $"{property.Name}:{property.Value.ValueKind}"));
    }
}

public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
