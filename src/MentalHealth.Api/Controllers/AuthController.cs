using System.Globalization;
using System.Text.Encodings.Web;
using MentalHealth.Api.Authorization;
using MentalHealth.Application.Security;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Contracts.Common;
using MentalHealth.Domain.Audit;
using MentalHealth.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IJwtTokenService tokenService,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    IClock clock) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return UnauthorizedProblem(
                ApiProblemCodes.InvalidCredentials,
                "邮箱或密码不正确");
        }

        var passwordResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!passwordResult.Succeeded && !passwordResult.RequiresTwoFactor)
        {
            return UnauthorizedProblem(
                ApiProblemCodes.InvalidCredentials,
                "邮箱或密码不正确");
        }

        var roles = await userManager.GetRolesAsync(user);
        var subject = new JwtTokenSubject(
            user.Id,
            user.Email!,
            roles.ToArray(),
            user.SubjectId,
            user.PractitionerId);

        if (RequiresMfa(user, roles))
        {
            if (!user.TwoFactorEnabled)
            {
                var setupToken = tokenService.Issue(subject, JwtTokenScope.MfaSetup);
                return UnauthorizedProblem(
                    ApiProblemCodes.MfaRequired,
                    "需要先设置动态验证码",
                    new Dictionary<string, object?>
                    {
                        ["mfaSetupRequired"] = true,
                        ["setupToken"] = setupToken.Value,
                        ["setupTokenExpiresAt"] = setupToken.ExpiresAt
                    });
            }

            if (string.IsNullOrWhiteSpace(request.TotpCode))
            {
                return UnauthorizedProblem(
                    ApiProblemCodes.MfaRequired,
                    "请输入动态验证码");
            }

            var validCode = await userManager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                NormalizeCode(request.TotpCode));
            if (!validCode)
            {
                return UnauthorizedProblem(
                    ApiProblemCodes.InvalidMfaCode,
                    "动态验证码不正确");
            }
        }

        var accessToken = tokenService.Issue(subject, JwtTokenScope.Api);
        return Ok(new TokenResponse(accessToken.Value, accessToken.ExpiresAt));
    }

    [Authorize(Policy = Policies.MfaSetup)]
    [HttpPost("mfa/setup")]
    public async Task<IActionResult> SetupMfa(
        MfaSetupRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId))
        {
            return ForbiddenProblem();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.TwoFactorEnabled)
        {
            return ForbiddenProblem();
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!RequiresMfa(user, roles))
        {
            return ForbiddenProblem();
        }

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        if (string.IsNullOrWhiteSpace(request.TotpCode))
        {
            var issuer = UrlEncoder.Default.Encode("心理健康系统");
            var account = UrlEncoder.Default.Encode(user.Email!);
            var uri = string.Create(
                CultureInfo.InvariantCulture,
                $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6");
            return Ok(new MfaSetupResponse(key!, uri, false));
        }

        var validCode = await userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            NormalizeCode(request.TotpCode));
        if (!validCode)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "动态验证码不正确",
                Extensions = { ["code"] = ApiProblemCodes.InvalidMfaCode }
            });
        }

        var enabled = await userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enabled.Succeeded)
        {
            throw new InvalidOperationException("Failed to enable MFA.");
        }

        auditTrail.Add(AuditEvent.Create(
            user.Id,
            "MfaEnabled",
            nameof(AppUser),
            user.Id,
            clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(new MfaSetupResponse(key!, string.Empty, true));
    }

    private ObjectResult UnauthorizedProblem(
        string code,
        string title,
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = title
        };
        problem.Extensions["code"] = code;
        if (extensions is not null)
        {
            foreach (var extension in extensions)
            {
                problem.Extensions[extension.Key] = extension.Value;
            }
        }

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };
    }

    private static ObjectResult ForbiddenProblem()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "无权执行这项操作"
        };
        problem.Extensions["code"] = ApiProblemCodes.ForbiddenResource;
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }

    private static string NormalizeCode(string code) =>
        code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

    private static bool RequiresMfa(
        AppUser user,
        IEnumerable<string> roles)
    {
        return user.RequiresMfa
            || roles.Contains(AppRoles.Doctor, StringComparer.Ordinal)
            || roles.Contains(AppRoles.OperationsAdmin, StringComparer.Ordinal);
    }
}

public sealed record LoginRequest(string Email, string Password, string? TotpCode);

public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record MfaSetupRequest(string? TotpCode);

public sealed record MfaSetupResponse(
    string ManualKey,
    string ProvisioningUri,
    bool Enabled);
