using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.ContractTests.Fakes;

namespace MentalHealth.ContractTests.Providers;

public sealed class InMemoryObjectStorageContractTests : ObjectStorageContract
{
    protected override IObjectStorage CreateStorage() => new InMemoryObjectStorage();
}
