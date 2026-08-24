using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Analysis;
using MentalHealth.Domain.Analysis;

namespace MentalHealth.AnalysisWorker.Pipeline;

public sealed class ScoreAssessmentStage(
    IRiskRuleSetRepository rules,
    IRiskAssessmentRepository assessments,
    IAnalysisRepository analysisJobs,
    IUnitOfWork unitOfWork,
    IClock clock,
    AttentionIndexCalculator calculator,
    CreateObservationCaseHandler observationCases)
{
    public async Task<RiskAssessment> RunAsync(
        Guid sessionId,
        Guid subjectId,
        int? transcriptRevision,
        IReadOnlyCollection<ModalityScore> modalityScores,
        IReadOnlyDictionary<Modality, IReadOnlyCollection<FeatureObservation>> observations,
        CrisisResult? crisis,
        CancellationToken cancellationToken)
    {
        var ruleSet = await rules.FindActiveRuleSetAsync(cancellationToken)
            ?? throw new InvalidOperationException("Active risk rule set is missing.");
        var existing = await assessments.FindAssessmentAsync(
            sessionId,
            ruleSet.Version,
            transcriptRevision,
            cancellationToken);
        if (existing is not null)
        {
            await observationCases.HandleAsync(existing, cancellationToken);
            return existing;
        }

        var result = calculator.Calculate(modalityScores, ruleSet, crisis);
        var evidence = BuildEvidence(modalityScores, observations, result);
        var assessment = RiskAssessment.Create(
            sessionId,
            subjectId,
            transcriptRevision,
            ruleSet,
            result,
            evidence,
            clock.UtcNow);
        assessments.Add(assessment);

        var job = await analysisJobs.GetOrCreateJobAsync(
            sessionId,
            clock.UtcNow,
            cancellationToken);
        job.Complete(assessment.Id, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await observationCases.HandleAsync(assessment, cancellationToken);
        return assessment;
    }

    private static IReadOnlyCollection<RiskEvidence> BuildEvidence(
        IReadOnlyCollection<ModalityScore> scores,
        IReadOnlyDictionary<Modality, IReadOnlyCollection<FeatureObservation>> observations,
        AttentionIndexResult result)
    {
        var evidence = new List<RiskEvidence>();
        foreach (var score in scores)
        {
            var contribution = result.Contributions[score.Modality];
            var features = observations.TryGetValue(score.Modality, out var found)
                ? found.ToArray()
                : [];
            if (features.Length == 0)
            {
                evidence.Add(new RiskEvidence(
                    $"{score.Modality.ToString().ToLowerInvariant()}_score",
                    score.Modality.ToString(),
                    contribution,
                    "aggregate",
                    Math.Clamp(score.Quality, 0m, 1m)));
                continue;
            }

            var contributionPerFeature = Math.Round(
                contribution / features.Length,
                6,
                MidpointRounding.AwayFromZero);
            evidence.AddRange(features.Select(feature => new RiskEvidence(
                feature.Code,
                score.Modality.ToString(),
                contributionPerFeature,
                feature.SourceRange,
                Math.Clamp((decimal)feature.Quality, 0m, 1m))));
        }

        return evidence;
    }
}
