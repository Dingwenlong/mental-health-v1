using MentalHealth.Application.Security;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Domain.Consultations;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace MentalHealth.Infrastructure.Identity;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    MentalHealthDbContext db,
    IClock clock)
{
    public static readonly Guid DemoSubjectId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static readonly Guid DemoCounselorId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");

    public static readonly Guid DemoDoctorId =
        Guid.Parse("20000000-0000-0000-0000-000000000002");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("IdentitySeed:Enabled"))
        {
            return;
        }

        foreach (var role in AppRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(
                    new IdentityRole<Guid>(role)));
            }
        }

        await EnsurePractitionerAsync(
            DemoCounselorId,
            "咨询师",
            "演示咨询师",
            PractitionerRole.Counselor,
            cancellationToken);
        await EnsurePractitionerAsync(
            DemoDoctorId,
            "精神科医生",
            "演示精神科医生",
            PractitionerRole.Doctor,
            cancellationToken);

        await EnsureUserAsync(
            "abc@qq.com",
            AppRoles.User,
            requiresMfa: false,
            subjectId: DemoSubjectId,
            legacyEmail: "user@demo.local",
            phoneLoginAccount: true,
            configuredPhone: configuration["PhoneLogin:Accounts:ClientPhone"]);
        await EnsureUserAsync(
            "counselor@demo.local",
            AppRoles.Counselor,
            requiresMfa: false,
            practitionerId: DemoCounselorId);
        await EnsureUserAsync(
            "doctor@demo.local",
            AppRoles.Doctor,
            requiresMfa: true,
            practitionerId: DemoDoctorId);
        await EnsureUserAsync(
            "123@qq.com",
            AppRoles.OperationsAdmin,
            requiresMfa: false,
            legacyEmail: "admin@demo.local",
            phoneLoginAccount: true,
            configuredPhone: configuration["PhoneLogin:Accounts:AdminPhone"]);
    }

    private async Task EnsureUserAsync(
        string email,
        string role,
        bool requiresMfa,
        Guid? subjectId = null,
        Guid? practitionerId = null,
        string? legacyEmail = null,
        bool phoneLoginAccount = false,
        string? configuredPhone = null)
    {
        AppUser? user = null;
        var resolvedByPhone = false;
        if (phoneLoginAccount
            && PhoneNumberNormalizer.TryNormalizeMainlandChina(
                configuredPhone ?? string.Empty,
                out var normalizedPhone))
        {
            user = await userManager.Users.SingleOrDefaultAsync(
                candidate => candidate.PhoneNumber == normalizedPhone);
            resolvedByPhone = user is not null;
        }

        user ??= await userManager.FindByEmailAsync(email)
            ?? (legacyEmail is null
                ? null
                : await userManager.FindByEmailAsync(legacyEmail));
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                RequiresMfa = requiresMfa,
                SubjectId = subjectId,
                PractitionerId = practitionerId
            };
            EnsureSucceeded(await userManager.CreateAsync(user));
        }

        else
        {
            var emailChanged = !resolvedByPhone && user.Email != email;
            var changed = emailChanged
                || (!phoneLoginAccount && user.UserName != email)
                || (!phoneLoginAccount && user.EmailConfirmed)
                || user.RequiresMfa != requiresMfa
                || user.SubjectId != subjectId
                || user.PractitionerId != practitionerId
                || (!phoneLoginAccount && user.PhoneNumber is not null)
                || (!phoneLoginAccount && user.PhoneNumberConfirmed)
                || (!phoneLoginAccount && user.PasswordHash is not null);
            if (changed)
            {
                if (emailChanged)
                {
                    user.Email = email;
                }

                if (!phoneLoginAccount || emailChanged)
                {
                    user.UserName = email;
                }

                if (!phoneLoginAccount)
                {
                    user.EmailConfirmed = false;
                    user.PhoneNumber = null;
                    user.PhoneNumberConfirmed = false;
                    user.PasswordHash = null;
                }

                user.RequiresMfa = requiresMfa;
                user.SubjectId = subjectId;
                user.PractitionerId = practitionerId;
                EnsureSucceeded(await userManager.UpdateAsync(user));
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
        }
    }

    private async Task EnsurePractitionerAsync(
        Guid practitionerId,
        string displayName,
        string legacyDisplayName,
        PractitionerRole role,
        CancellationToken cancellationToken)
    {
        var practitioner = await db.Practitioners.SingleOrDefaultAsync(
            item => item.Id == practitionerId,
            cancellationToken);
        if (practitioner is null)
        {
            db.Practitioners.Add(Practitioner.Create(
                practitionerId,
                displayName,
                role,
                clock.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var migratedDisplayName = practitioner.DisplayName.StartsWith(
            "演示·",
            StringComparison.Ordinal)
            ? practitioner.DisplayName[3..].Trim()
            : string.Equals(
                practitioner.DisplayName,
                legacyDisplayName,
                StringComparison.Ordinal)
                ? displayName
                : null;
        if (migratedDisplayName is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(migratedDisplayName))
        {
            migratedDisplayName = displayName;
        }

        practitioner.Update(
            migratedDisplayName,
            practitioner.Role,
            clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
        }
    }
}
