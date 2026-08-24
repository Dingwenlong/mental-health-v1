using System.Net;
using System.Net.Http.Json;
using MentalHealth.AnalysisWorker.Pipeline;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Domain.Analysis;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Analysis;

[Collection(AuthApiCollection.Name)]
public sealed class RiskReportTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task Report_is_explainable_private_and_unchanged_after_rule_activation()
    {
        using var started = await ConsultationScenario.StartVideoAsync(fixture);
        await CompleteAndTranscribeAsync(started);

        using var beforeAssessment = await started.User.Client.GetAsync(
            $"/api/v1/results/{started.SessionId}");
        Assert.Equal(HttpStatusCode.NotFound, beforeAssessment.StatusCode);
        Assert.Equal(
            "RESULT_NOT_FOUND",
            await ConsultationScenario.ReadProblemCodeAsync(beforeAssessment));

        Guid assessmentId;
        string originalRuleVersion;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var calculator = scope.ServiceProvider
                .GetRequiredService<AttentionIndexCalculator>();
            var stage = new ScoreAssessmentStage(
                db,
                db,
                db,
                db,
                clock,
                calculator);
            var observations = new Dictionary<Modality, IReadOnlyCollection<FeatureObservation>>
            {
                [Modality.Scale] =
                [
                    new FeatureObservation(
                        "questionnaire_total",
                        80,
                        1,
                        "DemoWellbeingScaleV1:items[0..6]",
                        "questionnaire-v1")
                ],
                [Modality.Text] =
                [
                    new FeatureObservation(
                        "negative_term_ratio",
                        .6,
                        .8,
                        "transcript:1:0-11",
                        "text-v1")
                ],
                [Modality.Audio] =
                [
                    new FeatureObservation(
                        "pause_ratio",
                        .4,
                        .5,
                        "audio:0-5s",
                        "audio-v1")
                ]
            };
            var assessment = await stage.RunAsync(
                started.SessionId,
                started.User.SubjectId,
                transcriptRevision: 1,
                [
                    new ModalityScore(Modality.Scale, 80m, 1m),
                    new ModalityScore(Modality.Text, 60m, .8m),
                    new ModalityScore(Modality.Audio, 40m, .5m)
                ],
                observations,
                CrisisResult.None,
                CancellationToken.None);
            assessmentId = assessment.Id;
            originalRuleVersion = assessment.RuleSetVersion;
        }

        using var reportResponse = await started.User.Client.GetAsync(
            $"/api/v1/results/{started.SessionId}");
        reportResponse.EnsureSuccessStatusCode();
        var report = await ConsultationScenario.ReadJsonAsync(reportResponse);
        Assert.Equal(assessmentId, report.GetProperty("id").GetGuid());
        Assert.Equal(originalRuleVersion, report.GetProperty("ruleSetVersion").GetString());
        Assert.Equal(67.058824m, report.GetProperty("score").GetDecimal());
        Assert.Equal(.85m, report.GetProperty("availableWeight").GetDecimal());
        Assert.Equal(.725m, report.GetProperty("confidence").GetDecimal());
        Assert.Equal("L2", report.GetProperty("level").GetString());
        Assert.Equal(
            ["Video", "Trend"],
            report.GetProperty("missing")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());
        Assert.Equal(3, report.GetProperty("evidence").GetArrayLength());
        Assert.Equal("这是比赛演示，不是诊断", report.GetProperty("notice").GetString());

        using var other = await ConsultationScenario.CreateUserAsync(fixture);
        using var forbidden = await other.Client.GetAsync(
            $"/api/v1/results/{started.SessionId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var admin = await fixture.CreateTrustedApiClientForAsync(
            "admin@demo.local");
        var version = $"risk-{Guid.NewGuid():N}";
        using var rejected = await admin.PostAsJsonAsync(
            "/api/v1/admin/risk-rules",
            RuleRequest(version + "-unsafe", crisisRulesEnabled: false));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal(
            "CRISIS_RULES_REQUIRED",
            await ConsultationScenario.ReadProblemCodeAsync(rejected));

        using var created = await admin.PostAsJsonAsync(
            "/api/v1/admin/risk-rules",
            RuleRequest(version, crisisRulesEnabled: true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var activated = await admin.PostAsync(
            $"/api/v1/admin/risk-rules/{version}/activate",
            content: null);
        Assert.True(
            activated.IsSuccessStatusCode,
            $"Activation failed: {(int)activated.StatusCode} {await activated.Content.ReadAsStringAsync()}\n"
                + string.Join("\n", fixture.CapturedLogs.TakeLast(20).Select(entry =>
                    $"{entry.Level} {entry.Category}: {entry.Message}")));

        using var unchangedResponse = await started.User.Client.GetAsync(
            $"/api/v1/results/{started.SessionId}");
        unchangedResponse.EnsureSuccessStatusCode();
        var unchanged = await ConsultationScenario.ReadJsonAsync(unchangedResponse);
        Assert.Equal(assessmentId, unchanged.GetProperty("id").GetGuid());
        Assert.Equal(
            originalRuleVersion,
            unchanged.GetProperty("ruleSetVersion").GetString());
        Assert.Equal(67.058824m, unchanged.GetProperty("score").GetDecimal());

        await using var verificationScope = fixture.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider
            .GetRequiredService<MentalHealthDbContext>();
        var saved = await verification.RiskAssessments
            .AsNoTracking()
            .SingleAsync(item => item.Id == assessmentId);
        var job = await verification.AnalysisJobs
            .AsNoTracking()
            .SingleAsync(item => item.SessionId == started.SessionId);
        Assert.Equal(originalRuleVersion, saved.RuleSetVersion);
        Assert.Equal(AnalysisJobStatus.Completed, job.Status);
        Assert.Equal(assessmentId, job.AssessmentId);
        Assert.Equal(1, await verification.RiskRuleSets.CountAsync(item => item.Active));
    }

    private static object RuleRequest(string version, bool crisisRulesEnabled) => new
    {
        version,
        scaleWeight = .45m,
        textWeight = .25m,
        audioWeight = .15m,
        videoWeight = .05m,
        trendWeight = .10m,
        thresholds = new[] { 25m, 50m, 75m },
        crisisRulesEnabled
    };

    private static async Task CompleteAndTranscribeAsync(StartedConsultation started)
    {
        using var complete = await started.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{started.SessionId}/complete",
            new { idempotencyKey = $"complete-{Guid.NewGuid():N}" });
        complete.EnsureSuccessStatusCode();
        using var transcript = await started.User.Client.PostAsJsonAsync(
            $"/api/v1/consultations/{started.SessionId}/transcript",
            new
            {
                source = "ManualUpload",
                text = "最近睡不好，也很疲惫，但目前没有立即危险。"
            });
        Assert.Equal(HttpStatusCode.Created, transcript.StatusCode);
    }
}
