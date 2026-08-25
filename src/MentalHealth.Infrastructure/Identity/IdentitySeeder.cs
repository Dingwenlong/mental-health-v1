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

        var password = configuration["DemoAccounts:InitialPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "DemoAccounts:InitialPassword is required when identity seeding is enabled.");
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
            "演示咨询师",
            PractitionerRole.Counselor,
            cancellationToken);
        await EnsurePractitionerAsync(
            DemoDoctorId,
            "演示精神科医生",
            PractitionerRole.Doctor,
            cancellationToken);

        await EnsureUserAsync(
            "user@demo.local",
            "+8613800138000",
            AppRoles.User,
            password,
            requiresMfa: false,
            subjectId: DemoSubjectId);
        await EnsureUserAsync(
            "counselor@demo.local",
            "+8613800138001",
            AppRoles.Counselor,
            password,
            requiresMfa: false,
            practitionerId: DemoCounselorId);
        await EnsureUserAsync(
            "doctor@demo.local",
            "+8613800138002",
            AppRoles.Doctor,
            password,
            requiresMfa: true,
            practitionerId: DemoDoctorId);
        await EnsureUserAsync(
            "admin@demo.local",
            "+8613800138003",
            AppRoles.OperationsAdmin,
            password,
            requiresMfa: true);
    }

    private async Task EnsureUserAsync(
        string email,
        string phoneNumber,
        string role,
        string password,
        bool requiresMfa,
        Guid? subjectId = null,
        Guid? practitionerId = null)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                PhoneNumber = phoneNumber,
                RequiresMfa = requiresMfa,
                SubjectId = subjectId,
                PractitionerId = practitionerId
            };
            EnsureSucceeded(await userManager.CreateAsync(user, password));
        }

        else
        {
            var changed = user.RequiresMfa != requiresMfa
                || user.SubjectId != subjectId
                || user.PractitionerId != practitionerId
                || user.PhoneNumber != phoneNumber;
            if (changed)
            {
                user.RequiresMfa = requiresMfa;
                user.SubjectId = subjectId;
                user.PractitionerId = practitionerId;
                user.PhoneNumber = phoneNumber;
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
        PractitionerRole role,
        CancellationToken cancellationToken)
    {
        if (await db.Practitioners.AnyAsync(
            practitioner => practitioner.Id == practitionerId,
            cancellationToken))
        {
            return;
        }

        db.Practitioners.Add(Practitioner.Create(
            practitionerId,
            displayName,
            role,
            clock.UtcNow));
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
