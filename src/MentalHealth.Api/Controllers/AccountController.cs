using System.Net.Mail;
using MentalHealth.Contracts.Common;
using MentalHealth.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MentalHealth.Infrastructure.Persistence;

namespace MentalHealth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/account")]
public sealed class AccountController(
    UserManager<AppUser> userManager,
    MentalHealthDbContext db) : ControllerBase
{
    [HttpGet("contact-email")]
    public async Task<IActionResult> GetContactEmail()
    {
        var user = await CurrentUserAsync();
        return user is null
            ? Forbid()
            : Ok(new ContactEmailResponse(user.Email));
    }

    [HttpPut("contact-email")]
    public async Task<IActionResult> PutContactEmail(ContactEmailRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null)
        {
            return Forbid();
        }

        string? email = null;
        if (request.Email is not null)
        {
            email = request.Email.Trim();
            if (!IsExactEmailAddress(email))
            {
                return InvalidEmail();
            }
        }

        user.Email = email;
        user.NormalizedEmail = email is null ? null : userManager.NormalizeEmail(email);
        user.EmailConfirmed = false;
        if (email is null)
        {
            await db.SaveChangesAsync();
            return NoContent();
        }

        var update = await userManager.UpdateAsync(user);
        return update.Succeeded ? NoContent() : InvalidEmail();
    }

    private async Task<AppUser?> CurrentUserAsync()
    {
        return Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)
            ? await userManager.FindByIdAsync(userId.ToString())
            : null;
    }

    private static bool IsExactEmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return string.Equals(
                new MailAddress(value).Address,
                value,
                StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static ObjectResult InvalidEmail()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "联系邮箱格式不正确"
        };
        problem.Extensions["code"] = ApiProblemCodes.ContactEmailInvalid;
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity
        };
    }
}

public sealed record ContactEmailRequest(string? Email);

public sealed record ContactEmailResponse(string? Email);
