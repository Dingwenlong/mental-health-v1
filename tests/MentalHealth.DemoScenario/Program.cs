using MentalHealth.AnalysisWorker.Pipeline;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Consultations;
using MentalHealth.Infrastructure;
using MentalHealth.Infrastructure.Media;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

if (args.Length != 2 || args[0] != "--synthetic-input" || !Guid.TryParse(args[1], out var sessionId))
    throw new ArgumentException("Use --synthetic-input <completed consultation id> in a disposable test database.");
var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("MentalHealth"));
if (connection.Host != "127.0.0.1" || connection.Port == 5432
    || connection.Database?.StartsWith("mental_health_care_test_", StringComparison.Ordinal) != true)
    throw new InvalidOperationException("Only isolated loopback test databases on a non-default port are accepted.");

var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<IConfiguration>(config);
services.AddAnalysisInfrastructure(config);
services.AddScoped<ScoreAssessmentStage>();
await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
var session = await db.ConsultationSessions.SingleAsync(item => item.Id == sessionId);
if (session.Status != ConsultationStatus.Completed)
    throw new InvalidOperationException("Complete the synthetic consultation through the API first.");
var transcript = await db.ManualTranscripts.Where(item => item.SessionId == sessionId)
    .OrderByDescending(item => item.Revision).FirstAsync();
var observations = scope.ServiceProvider.GetRequiredService<TextFeatureExtractor>().Extract(transcript.Text);
// This fixed synthetic input follows the existing report integration-test pattern.
// It is not a new scoring model and is never registered in the API or background worker.
var result = await scope.ServiceProvider.GetRequiredService<ScoreAssessmentStage>().RunAsync(
    sessionId, session.SubjectId, transcript.Revision,
    [new ModalityScore(Modality.Text, 90m, 1m)],
    new Dictionary<Modality, IReadOnlyCollection<FeatureObservation>> { [Modality.Text] = observations },
    CrisisResult.None, CancellationToken.None);
Console.WriteLine($"Synthetic input processed by the existing score stage. Assessment: {result.Id}");
