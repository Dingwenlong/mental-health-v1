namespace MentalHealth.Application.Security;

public static class AppRoles
{
    public const string User = "User";
    public const string Counselor = "Counselor";
    public const string Doctor = "Doctor";
    public const string OperationsAdmin = "OperationsAdmin";

    public static readonly IReadOnlyCollection<string> All =
        [User, Counselor, Doctor, OperationsAdmin];
}
