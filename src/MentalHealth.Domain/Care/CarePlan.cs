using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Care;

public enum CarePlanStatus { Draft, Active, Completed, Cancelled }
public sealed record CareTaskInput(string Kind, string? ExerciseId, DateOnly DueDate);

public sealed class CarePlan
{
    private readonly List<CarePlanTask> _tasks = [];
    private CarePlan() { }
    public Guid Id { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid FollowUpId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Title { get; private set; } = "";
    public string CreationKey { get; private set; } = "";
    public CarePlanStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyCollection<CarePlanTask> Tasks => _tasks;

    public static CarePlan Create(Guid subjectId, Guid followUpId, Guid authorId, string title, string creationKey, DateTimeOffset now)
    {
        if (subjectId == Guid.Empty || followUpId == Guid.Empty || authorId == Guid.Empty
            || string.IsNullOrWhiteSpace(creationKey) || creationKey.Length > 100)
            throw new DomainException("CARE_PLAN_INVALID");
        ValidateTitle(title);
        return new CarePlan
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            FollowUpId = followUpId,
            AuthorId = authorId,
            Title = title.Trim(),
            CreationKey = creationKey,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void ReplaceDraft(string title, IReadOnlyList<CareTaskInput> tasks, DateTimeOffset now)
    {
        Require(CarePlanStatus.Draft);
        ValidateTitle(title);
        if (tasks.Count is < 1 or > 30 || tasks.Any(task => task is null)) throw new DomainException("CARE_TASK_INVALID");
        var replacements = tasks.Select((task, index) => CarePlanTask.Create(Id, task, index, now)).ToArray();
        if (tasks.Distinct().Count() != tasks.Count) throw new DomainException("CARE_TASK_DUPLICATE");
        Title = title.Trim();
        _tasks.Clear();
        _tasks.AddRange(replacements);
        Touch(now);
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status == CarePlanStatus.Active) return;
        Require(CarePlanStatus.Draft);
        if (_tasks.Count == 0 || _tasks.Any(task => task.DueDate < CareDate.Today(now)))
            throw new DomainException("CARE_TASK_INVALID");
        Status = CarePlanStatus.Active;
        PublishedAt = now;
        Touch(now);
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == CarePlanStatus.Cancelled) return;
        if (Status == CarePlanStatus.Completed) throw new DomainException("CARE_PLAN_STATE_INVALID");
        Status = CarePlanStatus.Cancelled;
        Touch(now);
    }

    public void RecordFeedback(Guid taskId, string status, string? feedback, bool acknowledged, DateTimeOffset now)
    {
        if (!acknowledged) throw new DomainException("CARE_FEEDBACK_CONSENT_REQUIRED");
        if (Status is not (CarePlanStatus.Active or CarePlanStatus.Completed))
            throw new DomainException("CARE_PLAN_STATE_INVALID");
        var task = _tasks.SingleOrDefault(item => item.Id == taskId) ?? throw new DomainException("CARE_TASK_NOT_FOUND");
        task.Respond(status, feedback, now);
        if (_tasks.All(item => item.Status != "Pending")) Status = CarePlanStatus.Completed;
        Touch(now);
    }

    private void Touch(DateTimeOffset now) { UpdatedAt = now; Version++; }
    private void Require(CarePlanStatus expected)
    {
        if (Status != expected) throw new DomainException("CARE_PLAN_STATE_INVALID");
    }
    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 120) throw new DomainException("CARE_PLAN_INVALID");
    }
}

public sealed class CarePlanTask
{
    private CarePlanTask() { }
    public Guid Id { get; private set; }
    public Guid PlanId { get; private set; }
    public int Position { get; private set; }
    public string Kind { get; private set; } = "";
    public string? ExerciseId { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string Status { get; private set; } = "Pending";
    public string? Feedback { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    internal static CarePlanTask Create(Guid planId, CareTaskInput input, int position, DateTimeOffset now)
    {
        if (input.DueDate < CareDate.Today(now) || input.DueDate > CareDate.Today(now).AddDays(90)
            || (input.Kind == "CheckIn" ? input.ExerciseId is not null : input.Kind != "Exercise" || !ExerciseCatalog.Contains(input.ExerciseId)))
            throw new DomainException("CARE_TASK_INVALID");
        return new CarePlanTask
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            Position = position,
            Kind = input.Kind,
            ExerciseId = input.ExerciseId,
            DueDate = input.DueDate
        };
    }

    internal void Respond(string status, string? feedback, DateTimeOffset now)
    {
        if (status is not ("Done" or "Skipped") || feedback?.Length > 500) throw new DomainException("CARE_FEEDBACK_INVALID");
        var normalized = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        if (Status != "Pending")
        {
            if (Status == status && Feedback == normalized) return;
            throw new DomainException("CARE_TASK_ALREADY_RECORDED");
        }
        Status = status;
        Feedback = normalized;
        RespondedAt = now;
    }
}
