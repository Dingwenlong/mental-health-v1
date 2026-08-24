using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Analysis;

public sealed class RiskAssessment
{
    private readonly List<RiskEvidence> _evidence = [];

    private RiskAssessment()
    {
    }

    private RiskAssessment(
        Guid sessionId,
        Guid subjectId,
        int? transcriptRevision,
        string ruleSetVersion,
        AttentionIndexResult result,
        IReadOnlyCollection<RiskEvidence> evidence,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        SubjectId = subjectId;
        TranscriptRevision = transcriptRevision;
        RuleSetVersion = ruleSetVersion;
        Score = result.Score;
        AvailableWeight = result.AvailableWeight;
        Confidence = result.Confidence;
        Level = result.Level;
        IsCrisis = result.IsCrisis;
        CrisisRuleId = result.CrisisRuleId;
        MissingMask = ToMask(result.Missing);
        CreatedAt = createdAt;
        _evidence.AddRange(evidence);
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid SubjectId { get; private set; }

    public int? TranscriptRevision { get; private set; }

    public string RuleSetVersion { get; private set; } = string.Empty;

    public decimal Score { get; private set; }

    public decimal AvailableWeight { get; private set; }

    public decimal Confidence { get; private set; }

    public RiskLevel Level { get; private set; }

    public bool IsCrisis { get; private set; }

    public string? CrisisRuleId { get; private set; }

    public int MissingMask { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<RiskEvidence> Evidence => _evidence;

    public IReadOnlyList<Modality> Missing => Enum.GetValues<Modality>()
        .Where(modality => (MissingMask & (1 << (int)modality)) != 0)
        .ToArray();

    public static RiskAssessment Create(
        Guid sessionId,
        Guid subjectId,
        int? transcriptRevision,
        RiskRuleSet ruleSet,
        AttentionIndexResult result,
        IReadOnlyCollection<RiskEvidence> evidence,
        DateTimeOffset createdAt)
    {
        if (sessionId == Guid.Empty || subjectId == Guid.Empty)
        {
            throw new DomainException("ASSESSMENT_REFERENCE_INVALID");
        }

        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count == 0
            || evidence.Any(item => string.IsNullOrWhiteSpace(item.Code)
                || string.IsNullOrWhiteSpace(item.Modality)
                || string.IsNullOrWhiteSpace(item.SourceRange)
                || item.Quality is < 0m or > 1m))
        {
            throw new DomainException("ASSESSMENT_EVIDENCE_INVALID");
        }

        return new RiskAssessment(
            sessionId,
            subjectId,
            transcriptRevision,
            ruleSet.Version,
            result,
            evidence,
            createdAt);
    }

    private static int ToMask(IEnumerable<Modality> modalities)
    {
        var mask = 0;
        foreach (var modality in modalities)
        {
            mask |= 1 << (int)modality;
        }

        return mask;
    }
}
