using MentalHealth.Application.Abstractions;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consultations;
using MentalHealth.Application.Security;
using MentalHealth.Domain.Audit;
using MentalHealth.Domain.Care;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;

namespace MentalHealth.Application.Care;

public sealed class CareContinuityService(ICareRepository repository, IAuditTrail audit, IClock clock, IUiCopyCatalog copy)
{
    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct) => repository.InTransactionAsync(action, ct);
    public static void ValidatePage(int page, int size)
    {
        if (page is < 1 or > 10000 || size is < 1 or > 100) throw new DomainException("PAGE_INVALID");
    }
    public static CareScope Scope(ConsultationActor actor)
    {
        if (actor.Roles.Contains(AppRoles.User)) return new(AppRoles.User, actor.RequireOwnedSubject(), null);
        if (actor.Roles.Contains(AppRoles.Doctor)) return new(AppRoles.Doctor, null, actor.RequireDoctor());
        if (actor.Roles.Contains(AppRoles.Counselor) && actor.PractitionerId is { } practitioner)
            return new(AppRoles.Counselor, null, practitioner);
        if (actor.Roles.Contains(AppRoles.OperationsAdmin)) return new(AppRoles.OperationsAdmin, null, null);
        throw new DomainException("FORBIDDEN_RESOURCE");
    }

    public async Task<CheckInView> PutCheckInAsync(ConsultationActor actor, DateOnly date, int mood, decimal sleepHours,
        string? note, int? version, CancellationToken ct)
    {
        var subjectId = actor.RequireOwnedSubject();
        var entry = await repository.FindCheckInAsync(subjectId, date, ct);
        if (entry is null)
        {
            if (version is not null) throw new DomainException("CARE_CONFLICT");
            entry = DailyCheckIn.Create(subjectId, date, mood, sleepHours, note, clock.UtcNow);
            repository.Add(entry);
        }
        else
        {
            if (entry.Mood == mood && entry.SleepHours == sleepHours && entry.Note == Normalize(note)) return View(entry);
            if (version != entry.Version) throw new DomainException("CARE_CONFLICT");
            entry.Update(mood, sleepHours, note, clock.UtcNow);
        }
        AddAudit(actor, "DailyCheckInSaved", entry.Id);
        await repository.SaveAsync(ct);
        return View(entry);
    }

    public async Task<bool> DeleteCheckInAsync(ConsultationActor actor, DateOnly date, CancellationToken ct)
    {
        var entry = await repository.FindCheckInAsync(actor.RequireOwnedSubject(), date, ct);
        if (entry is not null)
        {
            repository.Remove(entry);
            AddAudit(actor, "DailyCheckInDeleted", entry.Id);
            await repository.SaveAsync(ct);
        }
        return true;
    }

    public async Task<Page<CheckInView>> CheckInsAsync(ConsultationActor actor, DateOnly? from, DateOnly? to,
        int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        if (from > to) throw new DomainException("CHECK_IN_DATE_INVALID");
        var result = await repository.CheckInsAsync(actor.RequireOwnedSubject(), from, to, page, size, ct);
        return new(result.Items.Select(View).ToArray(), result.Total, page, size);
    }

    public Task<IReadOnlyList<TrendDay>> TrendsAsync(ConsultationActor actor, int days, CancellationToken ct) =>
        TrendsForSubjectAsync(actor.RequireOwnedSubject(), days, ct);

    private async Task<IReadOnlyList<TrendDay>> TrendsForSubjectAsync(Guid subjectId, int days, CancellationToken ct)
    {
        if (days is not (7 or 30)) throw new DomainException("TREND_RANGE_INVALID");
        var today = CareDate.Today(clock.UtcNow);
        var from = today.AddDays(1 - days);
        var entries = (await repository.CheckInsAsync(subjectId, from, today, 1, 100, ct)).Items.ToDictionary(item => item.Date);
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime();
        var completions = await repository.CompletionsInRangeAsync(subjectId, start, start.AddDays(days), ct);
        var counts = completions.GroupBy(item => CareDate.Today(item.CompletedAt)).ToDictionary(group => group.Key, group => group.Count());
        return Enumerable.Range(0, days).Select(offset =>
        {
            var date = from.AddDays(offset);
            entries.TryGetValue(date, out var entry);
            return new TrendDay(date, entry?.Mood, entry?.SleepHours, counts.GetValueOrDefault(date));
        }).ToArray();
    }

    public IReadOnlyList<ExerciseView> Exercises() => ExerciseCatalog.All.Select(item =>
        new ExerciseView(item.Id, copy.Get(item.TitleKey), copy.Get(item.InstructionKey), item.DurationSeconds)).ToArray();

    public async Task<ExerciseCompletion> CompleteExerciseAsync(ConsultationActor actor, Guid id, string exerciseId, CancellationToken ct)
    {
        var subjectId = actor.RequireOwnedSubject();
        var existing = await repository.FindCompletionAsync(id, ct);
        if (existing is not null)
        {
            if (existing.SubjectId != subjectId || existing.ExerciseId != exerciseId) throw new DomainException("CARE_CONFLICT");
            return existing;
        }
        var completion = ExerciseCompletion.Create(id, subjectId, exerciseId, clock.UtcNow);
        repository.Add(completion);
        AddAudit(actor, "ExerciseCompleted", completion.Id);
        await repository.SaveAsync(ct);
        return completion;
    }

    public Task<Page<ExerciseCompletion>> CompletionsAsync(ConsultationActor actor, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        return repository.CompletionsAsync(actor.RequireOwnedSubject(), page, size, ct);
    }

    public Task<IReadOnlyList<SharingCandidate>> CandidatesAsync(ConsultationActor actor, CancellationToken ct) =>
        repository.SharingCandidatesAsync(actor.RequireOwnedSubject(), ct);
    public Task<Page<SharingView>> GrantsAsync(ConsultationActor actor, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        return repository.SharingGrantsAsync(actor.RequireOwnedSubject(), page, size, ct);
    }

    public async Task<Guid> GrantAsync(ConsultationActor actor, Guid followUpId, bool acknowledged, CancellationToken ct)
    {
        var subjectId = actor.RequireOwnedSubject();
        if (!acknowledged) throw new DomainException("SHARING_CONSENT_REQUIRED");
        var followUp = await repository.FindFollowUpAsync(followUpId, ct);
        if (followUp is null || followUp.SubjectId != subjectId || followUp.AssigneeId is not { } doctorId
            || !Open(followUp) || !await repository.IsActiveDoctorAsync(doctorId, ct))
            throw new DomainException("FORBIDDEN_RESOURCE");
        var existing = await repository.FindActiveGrantAsync(followUpId, followUp.AssignmentVersion, ct);
        if (existing is not null) return existing.Id;
        var grant = SharingGrant.Create(subjectId, followUpId, doctorId, followUp.AssignmentVersion, clock.UtcNow);
        repository.Add(grant);
        AddAudit(actor, "DailySharingGranted", grant.Id);
        await repository.SaveAsync(ct);
        return grant.Id;
    }

    public async Task<bool> RevokeAsync(ConsultationActor actor, Guid id, CancellationToken ct)
    {
        var subjectId = actor.RequireOwnedSubject();
        var grant = await repository.FindGrantAsync(id, ct);
        if (grant is null || grant.SubjectId != subjectId) throw new DomainException("FORBIDDEN_RESOURCE");
        await repository.RevokeDoctorGrantsAsync(subjectId, grant.DoctorId, clock.UtcNow, ct);
        AddAudit(actor, "DailySharingRevoked", id);
        await repository.SaveAsync(ct);
        return true;
    }

    public async Task<CarePlanView> CreatePlanAsync(ConsultationActor actor, Guid followUpId, string title,
        string key, IReadOnlyList<CareTaskInput> tasks, CancellationToken ct)
    {
        var doctorId = actor.RequireDoctor();
        var followUp = await DemandFollowUpAsync(actor, followUpId, true, ct);
        var existing = await repository.FindPlanByKeyAsync(doctorId, key, ct);
        if (existing is not null)
        {
            var sameTasks = existing.Tasks.OrderBy(item => item.Position)
                .Select(item => new CareTaskInput(item.Kind, item.ExerciseId, item.DueDate)).SequenceEqual(tasks);
            if (existing.FollowUpId != followUpId || existing.Title != title.Trim() || !sameTasks)
                throw new DomainException("CARE_CONFLICT");
            return View(existing);
        }
        if (await repository.HasOpenPlanAsync(followUpId, Guid.Empty, ct)) throw new DomainException("CARE_PLAN_EXISTS");
        var plan = CarePlan.Create(followUp.SubjectId, followUpId, doctorId, title, key, clock.UtcNow);
        plan.ReplaceDraft(title, tasks, clock.UtcNow);
        repository.Add(plan);
        AddAudit(actor, "CarePlanCreated", plan.Id);
        await repository.SaveAsync(ct);
        return View(plan);
    }

    public async Task<CarePlanView> UpdateDraftAsync(ConsultationActor actor, Guid id, string title,
        IReadOnlyList<CareTaskInput> tasks, int version, CancellationToken ct)
    {
        var plan = await DemandPlanAsync(actor, id, true, ct);
        if (version != plan.Version) throw new DomainException("CARE_CONFLICT");
        plan.ReplaceDraft(title, tasks, clock.UtcNow);
        AddAudit(actor, "CarePlanEdited", id);
        await repository.SaveAsync(ct);
        return View(plan);
    }

    public async Task<CarePlanView> ChangePlanAsync(ConsultationActor actor, Guid id, bool publish, CancellationToken ct)
    {
        var plan = await DemandPlanAsync(actor, id, true, ct, requireOpen: publish);
        if (publish) plan.Publish(clock.UtcNow); else plan.Cancel(clock.UtcNow);
        AddAudit(actor, publish ? "CarePlanPublished" : "CarePlanCancelled", id);
        await repository.SaveAsync(ct);
        return View(plan);
    }

    public async Task<CarePlanView> FeedbackAsync(ConsultationActor actor, Guid id, Guid taskId,
        string status, string? feedback, bool acknowledged, CancellationToken ct)
    {
        _ = actor.RequireOwnedSubject();
        var plan = await DemandPlanAsync(actor, id, false, ct);
        plan.RecordFeedback(taskId, status, feedback, acknowledged, clock.UtcNow);
        AddAudit(actor, "CareTaskResponded", taskId);
        await repository.SaveAsync(ct);
        return View(plan);
    }

    public async Task<CarePlanView> PlanAsync(ConsultationActor actor, Guid id, CancellationToken ct) =>
        View(await DemandPlanAsync(actor, id, false, ct));
    public async Task<Page<CarePlanView>> PlansAsync(ConsultationActor actor, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        var scope = Scope(actor);
        if (scope.Role is not (AppRoles.User or AppRoles.Doctor)) throw new DomainException("FORBIDDEN_RESOURCE");
        if (scope.Role == AppRoles.Doctor) await DemandDoctorAsync(actor, ct);
        return await repository.PlansAsync(scope, null, page, size, ct);
    }

    public Task<Page<ConsultationListItem>> ConsultationsAsync(ConsultationActor actor, bool reports, string? status,
        DateTimeOffset? from, DateTimeOffset? to, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        var scope = Scope(actor);
        if (scope.Role is not (AppRoles.User or AppRoles.Counselor)) throw new DomainException("FORBIDDEN_RESOURCE");
        if (status is not null)
        {
            var valid = reports
                ? status == "NotRequested" || (Enum.TryParse<MentalHealth.Domain.Analysis.AnalysisJobStatus>(status, out var reportStatus) && Enum.IsDefined(reportStatus))
                : Enum.TryParse<MentalHealth.Domain.Consultations.ConsultationStatus>(status, out var sessionStatus) && Enum.IsDefined(sessionStatus);
            if (!valid) throw new DomainException("CONSULTATION_FILTER_INVALID");
        }
        if (from > to) throw new DomainException("CONSULTATION_FILTER_INVALID");
        return repository.ConsultationsAsync(scope, reports, status, from?.ToUniversalTime(), to?.ToUniversalTime(), page, size, ct);
    }

    public async Task<Page<ClinicalSubject>> SubjectsAsync(ConsultationActor actor, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        var doctorId = await DemandDoctorAsync(actor, ct);
        return await repository.SubjectsAsync(doctorId, page, size, ct);
    }

    public async Task<ClinicalSubjectView> SubjectAsync(ConsultationActor actor, Guid subjectId, int page, int size, CancellationToken ct)
    {
        ValidatePage(page, size);
        var doctorId = await DemandDoctorAsync(actor, ct);
        if (!await repository.HasFollowUpAsync(subjectId, doctorId, ct)) throw new DomainException("FORBIDDEN_RESOURCE");
        var shared = await repository.HasSharingAsync(subjectId, doctorId, ct);
        var today = CareDate.Today(clock.UtcNow);
        var entries = shared ? (await repository.CheckInsAsync(subjectId, today.AddDays(-29), today, 1, 100, ct)).Items : [];
        var trends = shared ? await TrendsForSubjectAsync(subjectId, 30, ct) : [];
        AddAudit(actor, "CareSubjectViewed", subjectId);
        await repository.SaveAsync(ct);
        return new(subjectId, shared, await repository.ClinicalRecordsAsync(subjectId, doctorId, page, size, ct),
            entries.Select(View).ToArray(), trends, await repository.PlansAsync(Scope(actor), subjectId, page, size, ct));
    }

    public async Task<WorkspaceSummary> SummaryAsync(ConsultationActor actor, CancellationToken ct)
    {
        var scope = Scope(actor);
        if (scope.Role == AppRoles.Doctor) await DemandDoctorAsync(actor, ct);
        return await repository.SummaryAsync(scope, clock.UtcNow, ct);
    }

    private async Task<Guid> DemandDoctorAsync(ConsultationActor actor, CancellationToken ct)
    {
        var id = actor.RequireDoctor();
        if (!await repository.IsActiveDoctorAsync(id, ct)) throw new DomainException("FORBIDDEN_RESOURCE");
        return id;
    }
    private async Task<FollowUpTask> DemandFollowUpAsync(ConsultationActor actor, Guid id, bool requireOpen, CancellationToken ct)
    {
        var doctorId = await DemandDoctorAsync(actor, ct);
        var followUp = await repository.FindFollowUpAsync(id, ct);
        if (followUp is null || followUp.AssigneeId != doctorId) throw new DomainException("FORBIDDEN_RESOURCE");
        if (requireOpen && !Open(followUp)) throw new DomainException("CARE_PLAN_STATE_INVALID");
        return followUp;
    }
    private async Task<CarePlan> DemandPlanAsync(ConsultationActor actor, Guid id, bool write, CancellationToken ct, bool requireOpen = true)
    {
        var plan = await repository.FindPlanAsync(id, ct);
        if (plan is null) throw new DomainException("CARE_PLAN_NOT_FOUND");
        if (!write && actor.Roles.Contains(AppRoles.User))
        {
            if (plan.SubjectId != actor.RequireOwnedSubject() || plan.PublishedAt is null)
                throw new DomainException("FORBIDDEN_RESOURCE");
            return plan;
        }
        await DemandFollowUpAsync(actor, plan.FollowUpId, write && requireOpen, ct);
        return plan;
    }
    private void AddAudit(ConsultationActor actor, string action, Guid resourceId) =>
        audit.Add(AuditEvent.Create(actor.UserId, action, "Care", resourceId, clock.UtcNow));
    private static bool Open(FollowUpTask task) => task.Status is not (FollowUpStatus.Completed or FollowUpStatus.Cancelled);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static CheckInView View(DailyCheckIn entry) => new(entry.Id, entry.Date, entry.Mood, entry.SleepHours, entry.Note, entry.Version);
    public static CarePlanView View(CarePlan plan) => new(plan.Id, plan.FollowUpId, plan.Title, plan.Status.ToString(), plan.Version,
        plan.CreatedAt, plan.Tasks.OrderBy(item => item.Position).Select(task =>
            new CareTaskView(task.Id, task.Kind, task.ExerciseId, task.DueDate, task.Status, task.Feedback)).ToArray());
}
