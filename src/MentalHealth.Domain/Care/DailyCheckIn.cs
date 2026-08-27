using MentalHealth.Domain.Shared;

namespace MentalHealth.Domain.Care;

public static class CareDate
{
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(8)).DateTime);
}

public sealed class DailyCheckIn
{
    private DailyCheckIn() { }
    public Guid Id { get; private set; }
    public Guid SubjectId { get; private set; }
    public DateOnly Date { get; private set; }
    public int Mood { get; private set; }
    public decimal SleepHours { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int Version { get; private set; }

    public static DailyCheckIn Create(Guid subjectId, DateOnly date, int mood, decimal sleepHours, string? note, DateTimeOffset now)
    {
        if (subjectId == Guid.Empty || date > CareDate.Today(now) || date.Year < 2000)
            throw new DomainException("CHECK_IN_DATE_INVALID");
        var entry = new DailyCheckIn { Id = Guid.NewGuid(), SubjectId = subjectId, Date = date };
        entry.Update(mood, sleepHours, note, now);
        return entry;
    }

    public void Update(int mood, decimal sleepHours, string? note, DateTimeOffset now)
    {
        if (mood is < 1 or > 5 || sleepHours is < 0 or > 24 || decimal.Round(sleepHours, 1) != sleepHours || note?.Length > 500)
            throw new DomainException("CHECK_IN_VALUE_INVALID");
        Mood = mood;
        SleepHours = sleepHours;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAt = now;
        Version++;
    }
}

public sealed record ExerciseDefinition(string Id, string TitleKey, string InstructionKey, int DurationSeconds);

public static class ExerciseCatalog
{
    public static readonly IReadOnlyList<ExerciseDefinition> All = Array.AsReadOnly(new[]
    {
        new ExerciseDefinition("grounding", "care.exercise.grounding", "care.exercise.groundingGuide", 90),
        new ExerciseDefinition("pause", "care.exercise.pause", "care.exercise.pauseGuide", 120),
        new ExerciseDefinition("small-step", "care.exercise.smallStep", "care.exercise.smallStepGuide", 120)
    });
    public static bool Contains(string? id) => All.Any(item => item.Id == id);
}

public sealed class ExerciseCompletion
{
    private ExerciseCompletion() { }
    public Guid Id { get; private set; }
    public Guid SubjectId { get; private set; }
    public string ExerciseId { get; private set; } = "";
    public DateTimeOffset CompletedAt { get; private set; }
    public static ExerciseCompletion Create(Guid id, Guid subjectId, string exerciseId, DateTimeOffset now)
    {
        if (id == Guid.Empty || subjectId == Guid.Empty || !ExerciseCatalog.Contains(exerciseId))
            throw new DomainException("EXERCISE_INVALID");
        return new ExerciseCompletion { Id = id, SubjectId = subjectId, ExerciseId = exerciseId, CompletedAt = now };
    }
}
