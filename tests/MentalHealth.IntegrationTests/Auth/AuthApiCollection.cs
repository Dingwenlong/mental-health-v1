namespace MentalHealth.IntegrationTests.Auth;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthApiCollection : ICollectionFixture<AuthApiFixture>
{
    public const string Name = "auth-api";
}
