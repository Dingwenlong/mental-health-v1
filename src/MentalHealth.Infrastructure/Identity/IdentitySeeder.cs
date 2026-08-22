using MentalHealth.Application.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace MentalHealth.Infrastructure.Identity;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<AppUser> userManager,
    IConfiguration configuration)
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

        await EnsureUserAsync(
            "user@demo.local",
            AppRoles.User,
            password,
            requiresMfa: false,
            subjectId: DemoSubjectId);
        await EnsureUserAsync(
            "counselor@demo.local",
            AppRoles.Counselor,
            password,
            requiresMfa: false,
            practitionerId: DemoCounselorId);
        await EnsureUserAsync(
            "doctor@demo.local",
            AppRoles.Doctor,
            password,
            requiresMfa: true,
            practitionerId: DemoDoctorId);
        await EnsureUserAsync(
            "admin@demo.local",
            AppRoles.OperationsAdmin,
            password,
            requiresMfa: true);
    }

    private async Task EnsureUserAsync(
        string email,
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
                RequiresMfa = requiresMfa,
                SubjectId = subjectId,
                PractitionerId = practitionerId
            };
            EnsureSucceeded(await userManager.CreateAsync(user, password));
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
        }
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
