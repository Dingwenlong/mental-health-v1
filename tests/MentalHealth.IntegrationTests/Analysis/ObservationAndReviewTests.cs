using System.Net;
using System.Net.Http.Json;
using MentalHealth.AnalysisWorker.Pipeline;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Analysis;
using MentalHealth.Domain.Analysis;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.Analysis;

[Collection(AuthApiCollection.Name)]
public sealed class ObservationAndReviewTests(AuthApiFixture fixture)
{
    [Fact]
    public async Task L3_assessment_opens_case_schedules_doctor_and_keeps_review_separate()
    {
        var firstSlotId = await CreateDoctorSlotAsync(
            DateTimeOffset.UtcNow.AddHours(4));
        var secondSlotId = await CreateDoctorSlotAsync(
            DateTimeOffset.UtcNow.AddHours(6));
        var otherDoctorId = await CreateDoctorAsync();
        var otherDoctorSlotId = await CreateDoctorSlotAsync(
            otherDoctorId,
            DateTimeOffset.UtcNow.AddHours(5));
        using var started = await ConsultationScenario.StartVideoAsync(fixture);
        await CompleteAndTranscribeAsync(started);

        Guid assessmentId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            var stage = new ScoreAssessmentStage(
                db,
                db,
                db,
                db,
                scope.ServiceProvider.GetRequiredService<IClock>(),
                scope.ServiceProvider.GetRequiredService<AttentionIndexCalculator>(),
                scope.ServiceProvider.GetRequiredService<CreateObservationCaseHandler>());
            var assessment = await stage.RunAsync(
                started.SessionId,
                started.User.SubjectId,
                transcriptRevision: 1,
                [new ModalityScore(Modality.Scale, 82m, 1m)],
                new Dictionary<Modality, IReadOnlyCollection<FeatureObservation>>(),
                CrisisResult.None,
                CancellationToken.None);
            assessmentId = assessment.Id;
        }

        using var doctor = await fixture.CreateTrustedApiClientForAsync(
            "doctor@demo.local");
        using var queueResponse = await doctor.GetAsync("/api/v1/risk-cases");
        queueResponse.EnsureSuccessStatusCode();
        var queue = await ConsultationScenario.ReadJsonAsync(queueResponse);
        var riskCase = Assert.Single(
            queue.EnumerateArray(),
            item => item.GetProperty("assessmentId").GetGuid() == assessmentId);
        var caseId = riskCase.GetProperty("id").GetGuid();
        var followUpTaskId = riskCase.GetProperty("followUpTaskId").GetGuid();
        Assert.Equal("L3", riskCase.GetProperty("currentLevel").GetString());
        Assert.Equal("Open", riskCase.GetProperty("status").GetString());
        Assert.Equal(
            "Scheduled",
            riskCase.GetProperty("followUp").GetProperty("status").GetString());

        using var assignedQueueResponse = await doctor.GetAsync(
            "/api/v1/risk-cases?assignedToMe=true");
        assignedQueueResponse.EnsureSuccessStatusCode();
        var assignedQueue = await ConsultationScenario.ReadJsonAsync(
            assignedQueueResponse);
        _ = Assert.Single(
            assignedQueue.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == caseId);

        using var userQueue = await started.User.Client.GetAsync("/api/v1/risk-cases");
        Assert.Equal(HttpStatusCode.Forbidden, userQueue.StatusCode);

        using var missingReason = await doctor.PostAsJsonAsync(
            $"/api/v1/risk-cases/{caseId}/reviews",
            new { reviewedLevel = "L2", reason = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingReason.StatusCode);
        Assert.Equal(
            "REVIEW_REASON_REQUIRED",
            await ConsultationScenario.ReadProblemCodeAsync(missingReason));

        const string reviewReason = "结合会谈内容，当前更符合需要三天内回访的情况。";
        using var reviewed = await doctor.PostAsJsonAsync(
            $"/api/v1/risk-cases/{caseId}/reviews",
            new { reviewedLevel = "L2", reason = reviewReason });
        Assert.Equal(HttpStatusCode.Created, reviewed.StatusCode);

        using var userFollowUps = await started.User.Client.GetAsync(
            "/api/v1/follow-ups");
        userFollowUps.EnsureSuccessStatusCode();
        var userTasks = await ConsultationScenario.ReadJsonAsync(userFollowUps);
        var userTask = Assert.Single(
            userTasks.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == followUpTaskId);
        Assert.Equal("Scheduled", userTask.GetProperty("status").GetString());
        Assert.Equal(firstSlotId, userTask.GetProperty("availabilitySlotId").GetGuid());
        Assert.True(
            userTask.GetProperty("dueAt").GetDateTimeOffset()
            <= userTask.GetProperty("deadline").GetDateTimeOffset());

        using var emptyRescheduleReason = await doctor.PostAsJsonAsync(
            $"/api/v1/follow-ups/{followUpTaskId}/reschedule",
            new { availabilitySlotId = secondSlotId, reason = "" });
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            emptyRescheduleReason.StatusCode);

        const string reassignReason = "原医生临时无法出席，转给另一位医生。";
        using var reassigned = await doctor.PostAsJsonAsync(
            $"/api/v1/follow-ups/{followUpTaskId}/reassign",
            new { availabilitySlotId = otherDoctorSlotId, reason = reassignReason });
        reassigned.EnsureSuccessStatusCode();
        var reassignedTask = await ConsultationScenario.ReadJsonAsync(reassigned);
        Assert.Equal(otherDoctorId, reassignedTask.GetProperty("assigneeId").GetGuid());

        const string rescheduleReason = "用户确认改到稍后的可用时间。";
        using var rescheduled = await doctor.PostAsJsonAsync(
            $"/api/v1/follow-ups/{followUpTaskId}/reschedule",
            new { availabilitySlotId = secondSlotId, reason = rescheduleReason });
        rescheduled.EnsureSuccessStatusCode();
        var rescheduledTask = await ConsultationScenario.ReadJsonAsync(rescheduled);
        Assert.Equal(
            secondSlotId,
            rescheduledTask.GetProperty("availabilitySlotId").GetGuid());

        const string completeReason = "已完成演示回访并记录结果。";
        using var completed = await doctor.PostAsJsonAsync(
            $"/api/v1/follow-ups/{followUpTaskId}/complete",
            new { reason = completeReason });
        completed.EnsureSuccessStatusCode();
        Assert.Equal(
            "Completed",
            (await ConsultationScenario.ReadJsonAsync(completed))
                .GetProperty("status")
                .GetString());

        await using var verificationScope = fixture.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider
            .GetRequiredService<MentalHealthDbContext>();
        var savedAssessment = await verification.RiskAssessments
            .AsNoTracking()
            .SingleAsync(item => item.Id == assessmentId);
        var savedCase = await verification.ObservationCases
            .AsNoTracking()
            .SingleAsync(item => item.Id == caseId);
        var reviews = await verification.ClinicalReviews
            .AsNoTracking()
            .Where(item => item.ObservationCaseId == caseId)
            .ToArrayAsync();
        var reasons = await verification.AuditEvents
            .AsNoTracking()
            .Where(item => item.ResourceId == caseId || item.ResourceId == followUpTaskId)
            .Select(item => item.Reason)
            .ToArrayAsync();
        Assert.Equal(RiskLevel.L3, savedAssessment.Level);
        Assert.Equal(RiskLevel.L2, savedCase.CurrentLevel);
        Assert.Equal(reviewReason, Assert.Single(reviews).Reason);
        Assert.Contains(reviewReason, reasons);
        Assert.Contains(reassignReason, reasons);
        Assert.Contains(rescheduleReason, reasons);
        Assert.Contains(completeReason, reasons);
    }

    private async Task<Guid> CreateDoctorSlotAsync(DateTimeOffset startAt)
    {
        return await CreateDoctorSlotAsync(
            IdentitySeeder.DemoDoctorId,
            startAt);
    }

    private async Task<Guid> CreateDoctorSlotAsync(
        Guid practitionerId,
        DateTimeOffset startAt)
    {
        using var admin = await fixture.CreateTrustedApiClientForAsync(
            "123@qq.com");
        using var response = await admin.PostAsJsonAsync(
            $"/api/v1/admin/catalog/practitioners/{practitionerId}/slots",
            new { startAt, endAt = startAt.AddMinutes(30) });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ConsultationScenario.ReadJsonAsync(response))
            .GetProperty("id")
            .GetGuid();
    }

    private async Task<Guid> CreateDoctorAsync()
    {
        using var admin = await fixture.CreateTrustedApiClientForAsync(
            "123@qq.com");
        using var response = await admin.PostAsJsonAsync(
            "/api/v1/admin/catalog/practitioners",
            new
            {
                displayName = $"回访医生-{Guid.NewGuid():N}",
                role = "Doctor"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ConsultationScenario.ReadJsonAsync(response))
            .GetProperty("id")
            .GetGuid();
    }

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
                text = "这是重点观察流程使用的合成人工转写。"
            });
        Assert.Equal(HttpStatusCode.Created, transcript.StatusCode);
    }
}
