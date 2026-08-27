using MentalHealth.Application.Analysis;
using MentalHealth.Application.DataRights;
using MentalHealth.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.UnitTests.FollowUps;

public sealed class AnalysisDependencyTests
{
    [Fact]
    public void Standalone_analysis_host_can_resolve_its_scoped_handlers()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MentalHealth"] = "Host=127.0.0.1;Database=synthetic-unused;Username=unused",
            ["LocalObjectStorage:RootPath"] = Path.GetTempPath()
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(settings);
        services.AddAnalysisInfrastructure(settings);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RequestAnalysisHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateObservationCaseHandler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DemoRetentionHandler>());
    }
}
