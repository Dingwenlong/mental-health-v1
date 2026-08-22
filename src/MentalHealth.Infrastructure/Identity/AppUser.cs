using Microsoft.AspNetCore.Identity;

namespace MentalHealth.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public Guid? SubjectId { get; set; }

    public Guid? PractitionerId { get; set; }

    public bool RequiresMfa { get; set; }
}
