using MentalHealth.AnalysisWorker.Consumers;
using MentalHealth.AnalysisWorker.Pipeline;
using MentalHealth.Infrastructure;

namespace MentalHealth.AnalysisWorker;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddAnalysisInfrastructure(builder.Configuration);
        builder.Services.AddScoped<ConsultationCompletedConsumer>();
        builder.Services.AddScoped<ScoreAssessmentStage>();
        builder.Services.AddScoped(provider => new AnalysisPipeline(
            provider.GetRequiredService<MentalHealth.Infrastructure.Outbox.PostgresOutboxReader>(),
            provider.GetRequiredService<ConsultationCompletedConsumer>(),
            $"{Environment.MachineName}:{Environment.ProcessId}"));
        builder.Services.AddHostedService<Worker>();

        builder.Build().Run();
    }
}
