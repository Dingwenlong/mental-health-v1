using System.Data;
using MentalHealth.Application.Care;
using MentalHealth.Application.Security;
using MentalHealth.Domain.Care;
using MentalHealth.Domain.Consultations;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MentalHealth.Infrastructure.Persistence;

public sealed class CareRepository(MentalHealthDbContext db) : ICareRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException) { throw new DomainException("CARE_CONFLICT"); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" or "40001" })
        { throw new DomainException("CARE_CONFLICT"); }
        catch (PostgresException exception) when (exception.SqlState is "40001" or "40P01")
        { throw new DomainException("CARE_CONFLICT"); }
    }

    public async Task SaveAsync(CancellationToken ct) => _ = await db.SaveChangesAsync(ct);
    public Task<DailyCheckIn?> FindCheckInAsync(Guid subjectId, DateOnly date, CancellationToken ct) =>
        db.DailyCheckIns.SingleOrDefaultAsync(item => item.SubjectId == subjectId && item.Date == date, ct);
    public Task<Page<DailyCheckIn>> CheckInsAsync(Guid subjectId, DateOnly? from, DateOnly? to, int page, int size, CancellationToken ct)
    {
        var query = db.DailyCheckIns.AsNoTracking().Where(item => item.SubjectId == subjectId);
        if (from is { } start) query = query.Where(item => item.Date >= start);
        if (to is { } end) query = query.Where(item => item.Date <= end);
        return PageAsync(query.OrderByDescending(item => item.Date), page, size, ct);
    }
    public void Add(DailyCheckIn entry) => db.DailyCheckIns.Add(entry);
    public void Remove(DailyCheckIn entry) => db.DailyCheckIns.Remove(entry);
    public Task<ExerciseCompletion?> FindCompletionAsync(Guid id, CancellationToken ct) => db.ExerciseCompletions.FindAsync([id], ct).AsTask();
    public Task<Page<ExerciseCompletion>> CompletionsAsync(Guid subjectId, int page, int size, CancellationToken ct) =>
        PageAsync(db.ExerciseCompletions.AsNoTracking().Where(item => item.SubjectId == subjectId)
            .OrderByDescending(item => item.CompletedAt).ThenBy(item => item.Id), page, size, ct);
    public async Task<IReadOnlyList<ExerciseCompletion>> CompletionsInRangeAsync(Guid subjectId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await db.ExerciseCompletions.AsNoTracking().Where(item => item.SubjectId == subjectId && item.CompletedAt >= from && item.CompletedAt < to).ToArrayAsync(ct);
    public void Add(ExerciseCompletion completion) => db.ExerciseCompletions.Add(completion);
    public Task<FollowUpTask?> FindFollowUpAsync(Guid id, CancellationToken ct) => db.FollowUpTasks.FindAsync([id], ct).AsTask();
    public async Task<IReadOnlyList<SharingCandidate>> SharingCandidatesAsync(Guid subjectId, CancellationToken ct) =>
        await (from task in db.FollowUpTasks.AsNoTracking()
               join doctor in db.Practitioners on task.AssigneeId equals doctor.Id
               where task.SubjectId == subjectId && task.Status != FollowUpStatus.Completed && task.Status != FollowUpStatus.Cancelled
                   && doctor.Active && doctor.Role == PractitionerRole.Doctor
               orderby task.DueAt, task.Id
               select new SharingCandidate(task.Id, doctor.Id, doctor.DisplayName, task.DueAt)).ToArrayAsync(ct);
    public Task<Page<SharingView>> SharingGrantsAsync(Guid subjectId, int page, int size, CancellationToken ct) =>
        PageAsync(from grant in db.SharingGrants.AsNoTracking()
                  join task in db.FollowUpTasks on grant.FollowUpId equals task.Id
                  join doctor in db.Practitioners on grant.DoctorId equals doctor.Id
                  where grant.SubjectId == subjectId
                  orderby grant.GrantedAt descending, grant.Id
                  select new SharingView(grant.Id, grant.FollowUpId, grant.DoctorId, doctor.DisplayName,
                      grant.RevokedAt == null && task.AssigneeId == grant.DoctorId && task.AssignmentVersion == grant.AssignmentVersion
                      && doctor.Active && task.Status != FollowUpStatus.Completed && task.Status != FollowUpStatus.Cancelled,
                      grant.GrantedAt), page, size, ct);
    public Task<SharingGrant?> FindGrantAsync(Guid id, CancellationToken ct) => db.SharingGrants.FindAsync([id], ct).AsTask();
    public async Task RevokeDoctorGrantsAsync(Guid subjectId, Guid doctorId, DateTimeOffset now, CancellationToken ct) =>
        _ = await db.SharingGrants.Where(item => item.SubjectId == subjectId && item.DoctorId == doctorId && item.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now), ct);
    public Task<SharingGrant?> FindActiveGrantAsync(Guid followUpId, int assignmentVersion, CancellationToken ct) =>
        db.SharingGrants.SingleOrDefaultAsync(item => item.FollowUpId == followUpId && item.AssignmentVersion == assignmentVersion && item.RevokedAt == null, ct);
    public Task<bool> HasSharingAsync(Guid subjectId, Guid doctorId, CancellationToken ct) =>
        (from grant in db.SharingGrants
         join task in db.FollowUpTasks on grant.FollowUpId equals task.Id
         where grant.SubjectId == subjectId && grant.DoctorId == doctorId && grant.RevokedAt == null
             && task.AssigneeId == doctorId && task.AssignmentVersion == grant.AssignmentVersion
             && task.Status != FollowUpStatus.Completed && task.Status != FollowUpStatus.Cancelled
         select grant.Id).AnyAsync(ct);
    public void Add(SharingGrant grant) => db.SharingGrants.Add(grant);
    public Task<bool> IsActiveDoctorAsync(Guid id, CancellationToken ct) => db.Practitioners.AnyAsync(item => item.Id == id && item.Active && item.Role == PractitionerRole.Doctor, ct);
    public Task<CarePlan?> FindPlanAsync(Guid id, CancellationToken ct) => db.CarePlans.Include(item => item.Tasks).SingleOrDefaultAsync(item => item.Id == id, ct);
    public Task<CarePlan?> FindPlanByKeyAsync(Guid authorId, string key, CancellationToken ct) =>
        db.CarePlans.Include(item => item.Tasks).SingleOrDefaultAsync(item => item.AuthorId == authorId && item.CreationKey == key, ct);
    public Task<bool> HasOpenPlanAsync(Guid followUpId, Guid exceptId, CancellationToken ct) =>
        db.CarePlans.AnyAsync(item => item.FollowUpId == followUpId && item.Id != exceptId && (item.Status == CarePlanStatus.Draft || item.Status == CarePlanStatus.Active), ct);
    public void Add(CarePlan plan) => db.CarePlans.Add(plan);
    public async Task<Page<CarePlanView>> PlansAsync(CareScope scope, Guid? subjectId, int page, int size, CancellationToken ct)
    {
        var query = db.CarePlans.AsNoTracking().Include(item => item.Tasks).AsQueryable();
        query = scope.Role == AppRoles.User
            ? query.Where(item => item.SubjectId == scope.SubjectId && item.PublishedAt != null)
            : query.Where(item => db.FollowUpTasks.Any(task => task.Id == item.FollowUpId && task.AssigneeId == scope.PractitionerId));
        if (subjectId is { } subject) query = query.Where(item => item.SubjectId == subject);
        var result = await PageAsync(query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id), page, size, ct);
        return new(result.Items.Select(CareContinuityService.View).ToArray(), result.Total, page, size);
    }

    public async Task<Page<ConsultationListItem>> ConsultationsAsync(CareScope scope, bool reports, string? status,
        DateTimeOffset? from, DateTimeOffset? to, int page, int size, CancellationToken ct)
    {
        var query = Sessions(scope);
        if (reports) query = query.Where(item => item.Status == ConsultationStatus.Completed);
        if (reports && status is not null)
        {
            if (status == "Completed")
                query = query.Where(item => db.RiskAssessments.Any(assessment => assessment.SessionId == item.Id));
            else
            {
                query = query.Where(item => !db.RiskAssessments.Any(assessment => assessment.SessionId == item.Id));
                if (status == "NotRequested") query = query.Where(item => !db.AnalysisJobs.Any(job => job.SessionId == item.Id));
                else
                {
                    var jobStatus = Enum.Parse<MentalHealth.Domain.Analysis.AnalysisJobStatus>(status);
                    query = query.Where(item => db.AnalysisJobs.Any(job => job.SessionId == item.Id && job.Status == jobStatus));
                }
            }
        }
        else if (status is not null)
        {
            var value = Enum.Parse<ConsultationStatus>(status);
            query = query.Where(item => item.Status == value);
        }
        if (from is { } start) query = query.Where(item => item.ScheduledAt >= start);
        if (to is { } end) query = query.Where(item => item.ScheduledAt <= end);
        var result = await PageAsync(query.OrderByDescending(item => item.ScheduledAt).ThenBy(item => item.Id), page, size, ct);
        var ids = result.Items.Select(item => item.Id).ToArray();
        var practitioners = await db.Practitioners.AsNoTracking().Where(item => result.Items.Select(session => session.AssignedPractitionerId).Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, ct);
        var readyIds = await db.RiskAssessments.AsNoTracking().Where(item => ids.Contains(item.SessionId)).Select(item => item.SessionId).Distinct().ToArrayAsync(ct);
        var jobs = await db.AnalysisJobs.AsNoTracking().Where(item => ids.Contains(item.SessionId)).ToArrayAsync(ct);
        return new(result.Items.Select(item => new ConsultationListItem(item.Id, item.OrderId, item.Kind.ToString(), item.Channel.ToString(), item.Status.ToString(),
            item.AssignedPractitionerId is { } practitioner ? practitioners.GetValueOrDefault(practitioner) : null, item.ScheduledAt, item.CompletedAt,
            readyIds.Contains(item.Id) ? "Completed" : jobs.Where(job => job.SessionId == item.Id).OrderByDescending(job => job.CreatedAt).FirstOrDefault()?.Status.ToString() ?? "NotRequested")).ToArray(), result.Total, page, size);
    }
    public async Task<Page<ClinicalSubject>> SubjectsAsync(Guid doctorId, int page, int size, CancellationToken ct)
    {
        var query = db.FollowUpTasks.AsNoTracking().Where(task => task.AssigneeId == doctorId)
            .GroupBy(task => task.SubjectId).Select(group => new
            {
                SubjectId = group.Key,
                NextFollowUpAt = group.Min(task => task.Status != FollowUpStatus.Completed && task.Status != FollowUpStatus.Cancelled ? task.DueAt : null),
                FollowUpCount = group.Count()
            });
        var result = await PageAsync(query.OrderBy(item => item.NextFollowUpAt).ThenBy(item => item.SubjectId), page, size, ct);
        return new(result.Items.Select(item => new ClinicalSubject(item.SubjectId, item.NextFollowUpAt, item.FollowUpCount)).ToArray(), result.Total, page, size);
    }
    public Task<bool> HasFollowUpAsync(Guid subjectId, Guid doctorId, CancellationToken ct) =>
        db.FollowUpTasks.AnyAsync(task => task.SubjectId == subjectId && task.AssigneeId == doctorId, ct);
    public async Task<Page<ClinicalRecord>> ClinicalRecordsAsync(Guid subjectId, Guid doctorId, int page, int size, CancellationToken ct)
    {
        var tasks = await PageAsync(db.FollowUpTasks.AsNoTracking().Where(task => task.SubjectId == subjectId && task.AssigneeId == doctorId)
            .OrderByDescending(task => task.ProposedAt).ThenBy(task => task.Id), page, size, ct);
        var ids = tasks.Items.Select(task => task.AssessmentId).ToArray();
        var assessments = await db.RiskAssessments.AsNoTracking().Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        var reviews = await db.ClinicalReviews.AsNoTracking().Where(item => ids.Contains(item.AssessmentId)).OrderBy(item => item.ReviewedAt).ToArrayAsync(ct);
        var records = tasks.Items.Where(task => assessments.ContainsKey(task.AssessmentId)).Select(task =>
        {
            var assessment = assessments[task.AssessmentId];
            return new ClinicalRecord(task.Id, task.Status.ToString(), task.DueAt, assessment.SessionId,
                assessment.Id, assessment.Score, assessment.Level.ToString(), "此结果不能替代诊断。需要医疗帮助时，请联系医生。",
                reviews.Where(review => review.AssessmentId == assessment.Id).Select(review =>
                    new ClinicalReviewView(review.ReviewedLevel.ToString(), review.Reason, review.ReviewedAt)).ToArray());
        }).ToArray();
        return new(records, tasks.Total, page, size);
    }
    public async Task<WorkspaceSummary> SummaryAsync(CareScope scope, DateTimeOffset now, CancellationToken ct)
    {
        if (scope.Role == AppRoles.Counselor)
            return new(scope.Role, await Sessions(scope).CountAsync(ct), 0, 0, 0, 0, 0);
        var followUps = db.FollowUpTasks.AsNoTracking().AsQueryable();
        if (scope.Role == AppRoles.User) followUps = followUps.Where(task => task.SubjectId == scope.SubjectId);
        if (scope.Role == AppRoles.Doctor) followUps = followUps.Where(task => task.AssigneeId == scope.PractitionerId);
        var pending = followUps.Where(task => task.Status != FollowUpStatus.Completed && task.Status != FollowUpStatus.Cancelled);
        var consultationCount = scope.Role == AppRoles.OperationsAdmin ? await db.ConsultationSessions.CountAsync(ct)
            : scope.Role == AppRoles.User ? await Sessions(scope).CountAsync(ct) : 0;
        if (scope.Role == AppRoles.OperationsAdmin)
            return new(scope.Role, consultationCount, await pending.CountAsync(ct), await pending.CountAsync(task => task.DueAt < now, ct), 0, 0, 0);
        var plans = db.CarePlans.AsNoTracking().Where(plan => followUps.Any(task => task.Id == plan.FollowUpId) && plan.PublishedAt != null);
        var taskQuery = db.CarePlanTasks.Where(task => plans.Any(plan => plan.Id == task.PlanId && plan.Status != CarePlanStatus.Cancelled));
        return new(scope.Role, consultationCount, await pending.CountAsync(ct), await pending.CountAsync(task => task.DueAt < now, ct),
            await plans.CountAsync(plan => plan.Status == CarePlanStatus.Active, ct), await taskQuery.CountAsync(task => task.Status == "Done", ct), await taskQuery.CountAsync(ct));
    }

    public async Task<CareDataSnapshot> ExportAsync(Guid subjectId, CancellationToken ct) => new(
        await db.DailyCheckIns.AsNoTracking().Where(item => item.SubjectId == subjectId).OrderBy(item => item.Date).ToArrayAsync(ct),
        await db.ExerciseCompletions.AsNoTracking().Where(item => item.SubjectId == subjectId).OrderBy(item => item.CompletedAt).ToArrayAsync(ct),
        await db.SharingGrants.AsNoTracking().Where(item => item.SubjectId == subjectId).OrderBy(item => item.GrantedAt).ToArrayAsync(ct),
        (await db.CarePlans.AsNoTracking().Include(item => item.Tasks).Where(item => item.SubjectId == subjectId && item.PublishedAt != null)
            .OrderBy(item => item.CreatedAt).ToArrayAsync(ct)).Select(CareContinuityService.View).ToArray());
    private IQueryable<ConsultationSession> Sessions(CareScope scope) => scope.Role == AppRoles.User
        ? db.ConsultationSessions.AsNoTracking().Where(item => item.SubjectId == scope.SubjectId)
        : db.ConsultationSessions.AsNoTracking().Where(item => item.AssignedPractitionerId == scope.PractitionerId);
    private static async Task<Page<T>> PageAsync<T>(IQueryable<T> query, int page, int size, CancellationToken ct) =>
        new(await query.Skip((page - 1) * size).Take(size).ToArrayAsync(ct), await query.CountAsync(ct), page, size);
}
