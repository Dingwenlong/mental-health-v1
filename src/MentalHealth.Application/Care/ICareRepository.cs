using MentalHealth.Domain.Care;
using MentalHealth.Domain.FollowUps;

namespace MentalHealth.Application.Care;

public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize);
public sealed record CareScope(string Role, Guid? SubjectId, Guid? PractitionerId);
public sealed record CheckInView(Guid Id, DateOnly Date, int Mood, decimal SleepHours, string? Note, int Version);
public sealed record TrendDay(DateOnly Date, int? Mood, decimal? SleepHours, int ExerciseCount);
public sealed record ExerciseView(string Id, string Title, string Instruction, int DurationSeconds);
public sealed record SharingView(Guid Id, Guid FollowUpId, Guid DoctorId, string DoctorName, bool Active, DateTimeOffset GrantedAt);
public sealed record SharingCandidate(Guid FollowUpId, Guid DoctorId, string DoctorName, DateTimeOffset? DueAt);
public sealed record CareTaskView(Guid Id, string Kind, string? ExerciseId, DateOnly DueDate, string Status, string? Feedback);
public sealed record CarePlanView(Guid Id, Guid FollowUpId, string Title, string Status, int Version,
    DateTimeOffset CreatedAt, IReadOnlyList<CareTaskView> Tasks);
public sealed record ConsultationListItem(Guid Id, Guid? OrderId, string Kind, string Channel, string Status,
    string? PractitionerName, DateTimeOffset? ScheduledAt, DateTimeOffset? CompletedAt, string AnalysisStatus);
public sealed record ClinicalSubject(Guid SubjectId, DateTimeOffset? NextFollowUpAt, int FollowUpCount);
public sealed record ClinicalRecord(Guid FollowUpId, string FollowUpStatus, DateTimeOffset? DueAt, Guid SessionId,
    Guid AssessmentId, decimal Score, string Level, string Notice, IReadOnlyList<ClinicalReviewView> Reviews);
public sealed record ClinicalReviewView(string Level, string Reason, DateTimeOffset ReviewedAt);
public sealed record ClinicalSubjectView(Guid SubjectId, bool SharingActive, Page<ClinicalRecord> Records,
    IReadOnlyList<CheckInView> CheckIns, IReadOnlyList<TrendDay> Trends, Page<CarePlanView> Plans);
public sealed record WorkspaceSummary(string Role, int ConsultationCount, int PendingFollowUps, int OverdueFollowUps,
    int ActivePlans, int CompletedPlanTasks, int PlanTasks);
public sealed record CareDataSnapshot(IReadOnlyList<DailyCheckIn> CheckIns, IReadOnlyList<ExerciseCompletion> Exercises,
    IReadOnlyList<SharingGrant> SharingGrants, IReadOnlyList<CarePlanView> Plans);

public interface ICareRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<DailyCheckIn?> FindCheckInAsync(Guid subjectId, DateOnly date, CancellationToken ct);
    Task<Page<DailyCheckIn>> CheckInsAsync(Guid subjectId, DateOnly? from, DateOnly? to, int page, int size, CancellationToken ct);
    void Add(DailyCheckIn entry);
    void Remove(DailyCheckIn entry);
    Task<ExerciseCompletion?> FindCompletionAsync(Guid id, CancellationToken ct);
    Task<Page<ExerciseCompletion>> CompletionsAsync(Guid subjectId, int page, int size, CancellationToken ct);
    Task<IReadOnlyList<ExerciseCompletion>> CompletionsInRangeAsync(Guid subjectId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    void Add(ExerciseCompletion completion);
    Task<FollowUpTask?> FindFollowUpAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SharingCandidate>> SharingCandidatesAsync(Guid subjectId, CancellationToken ct);
    Task<Page<SharingView>> SharingGrantsAsync(Guid subjectId, int page, int size, CancellationToken ct);
    Task<SharingGrant?> FindGrantAsync(Guid id, CancellationToken ct);
    Task RevokeDoctorGrantsAsync(Guid subjectId, Guid doctorId, DateTimeOffset now, CancellationToken ct);
    Task<SharingGrant?> FindActiveGrantAsync(Guid followUpId, int assignmentVersion, CancellationToken ct);
    Task<bool> HasSharingAsync(Guid subjectId, Guid doctorId, CancellationToken ct);
    void Add(SharingGrant grant);
    Task<bool> IsActiveDoctorAsync(Guid id, CancellationToken ct);
    Task<CarePlan?> FindPlanAsync(Guid id, CancellationToken ct);
    Task<CarePlan?> FindPlanByKeyAsync(Guid authorId, string key, CancellationToken ct);
    Task<bool> HasOpenPlanAsync(Guid followUpId, Guid exceptId, CancellationToken ct);
    Task<Page<CarePlanView>> PlansAsync(CareScope scope, Guid? subjectId, int page, int size, CancellationToken ct);
    void Add(CarePlan plan);
    Task<Page<ConsultationListItem>> ConsultationsAsync(CareScope scope, bool reports, string? status,
        DateTimeOffset? from, DateTimeOffset? to, int page, int size, CancellationToken ct);
    Task<Page<ClinicalSubject>> SubjectsAsync(Guid doctorId, int page, int size, CancellationToken ct);
    Task<bool> HasFollowUpAsync(Guid subjectId, Guid doctorId, CancellationToken ct);
    Task<Page<ClinicalRecord>> ClinicalRecordsAsync(Guid subjectId, Guid doctorId, int page, int size, CancellationToken ct);
    Task<WorkspaceSummary> SummaryAsync(CareScope scope, DateTimeOffset now, CancellationToken ct);
}
