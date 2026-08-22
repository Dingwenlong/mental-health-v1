namespace MentalHealth.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PersistenceCollection : ICollectionFixture<PersistenceFixture>
{
    public const string Name = "local-infrastructure";
}
