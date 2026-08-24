using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consultations;
using MentalHealth.Domain.Analysis;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Shared;
using MentalHealth.Contracts.Common;

namespace MentalHealth.Application.Analysis;

public sealed record RiskRuleSetInput(
    string Version,
    IReadOnlyDictionary<Modality, decimal> Weights,
    IReadOnlyList<decimal> Thresholds,
    bool CrisisRulesEnabled);

public interface IRiskRuleSetRepository
{
    Task<RiskRuleSet?> FindRuleSetAsync(
        string version,
        CancellationToken cancellationToken);

    Task<RiskRuleSet?> FindActiveRuleSetAsync(CancellationToken cancellationToken);

    void Add(RiskRuleSet ruleSet);
}

public interface IRiskAssessmentRepository
{
    Task<RiskAssessment?> FindLatestAssessmentAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<RiskAssessment?> FindAssessmentAsync(
        Guid sessionId,
        string ruleSetVersion,
        int? transcriptRevision,
        CancellationToken cancellationToken);

    void Add(RiskAssessment assessment);
}

public sealed class CreateRiskRuleSetHandler(
    IRiskRuleSetRepository rules,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<RiskRuleSet> CreateAsync(
        Guid actorUserId,
        RiskRuleSetInput input,
        CancellationToken cancellationToken)
    {
        var version = input.Version?.Trim() ?? string.Empty;
        if (await rules.FindRuleSetAsync(version, cancellationToken)
            is not null)
        {
            throw new DomainException(ApiProblemCodes.RiskRuleVersionExists);
        }

        var ruleSet = RiskRuleSet.Create(
            version,
            input.Weights,
            input.Thresholds,
            clock.UtcNow,
            input.CrisisRulesEnabled);
        rules.Add(ruleSet);
        auditTrail.Add(AuditEvent.Create(
            actorUserId,
            "RiskRuleSetCreated",
            "RiskRuleSet",
            ruleSet.Id,
            clock.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ruleSet;
    }

    public async Task<RiskRuleSet> ActivateAsync(
        Guid actorUserId,
        string version,
        CancellationToken cancellationToken)
    {
        var normalizedVersion = version?.Trim() ?? string.Empty;
        var ruleSet = await rules.FindRuleSetAsync(normalizedVersion, cancellationToken)
            ?? throw new DomainException(ApiProblemCodes.RiskRuleVersionNotFound);
        var current = await rules.FindActiveRuleSetAsync(cancellationToken);
        if (current?.Id == ruleSet.Id)
        {
            return ruleSet;
        }

        await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            if (current is not null)
            {
                current.Deactivate();
                await unitOfWork.SaveChangesAsync(transactionToken);
            }

            ruleSet.Activate(clock.UtcNow);
            auditTrail.Add(AuditEvent.Create(
                actorUserId,
                "RiskRuleSetActivated",
                "RiskRuleSet",
                ruleSet.Id,
                clock.UtcNow));
            await unitOfWork.SaveChangesAsync(transactionToken);
        }, cancellationToken);
        return ruleSet;
    }
}

public sealed class RiskReportQueryHandler(
    SessionAccessService access,
    IRiskAssessmentRepository assessments)
{
    public async Task<RiskAssessment> HandleAsync(
        ConsultationActor actor,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await access.DemandAsync(actor, sessionId, cancellationToken);
        return await assessments.FindLatestAssessmentAsync(sessionId, cancellationToken)
            ?? throw new DomainException(ApiProblemCodes.ResultNotFound);
    }
}
