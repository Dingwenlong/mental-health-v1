using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealth.Domain.Care;
using MentalHealth.Domain.FollowUps;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.IntegrationTests.Auth;
using MentalHealth.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MentalHealth.IntegrationTests.FollowUps;

[Collection(AuthApiCollection.Name)]
public sealed class CareContinuityApiTests(AuthApiFixture fixture)
{
    private static string Today => CareDate.Today(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Daily_entries_are_private_idempotent_paginated_and_leave_missing_days_empty()
    {
        using var user = await ConsultationScenario.CreateUserAsync(fixture);
        using var other = await ConsultationScenario.CreateUserAsync(fixture);
        var body = new { mood = 3, sleepHours = 7.5, note = "PRIVATE-NOTE" };
        using var put = await user.Client.PutAsJsonAsync($"/api/v1/me/check-ins/{Today}", body);
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync() + "\n" + string.Join("\n", fixture.CapturedLogs.Where(item => item.Level >= Microsoft.Extensions.Logging.LogLevel.Error).Select(item => item.Message + " " + item.Exception)));
        var saved = await Json(put);
        using var repeat = await user.Client.PutAsJsonAsync($"/api/v1/me/check-ins/{Today}", body);
        repeat.EnsureSuccessStatusCode();
        Assert.Equal(saved.GetProperty("id").GetGuid(), (await Json(repeat)).GetProperty("id").GetGuid());
        using var changed = await user.Client.PutAsJsonAsync($"/api/v1/me/check-ins/{Today}", new { mood = 4, sleepHours = 8, note = "changed", version = saved.GetProperty("version").GetInt32() });
        changed.EnsureSuccessStatusCode();
        using var stale = await user.Client.PutAsJsonAsync($"/api/v1/me/check-ins/{Today}", new { mood = 2, sleepHours = 8, version = 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var own = await Get(user.Client, "/api/v1/me/check-ins?pageSize=1");
        Assert.Equal(1, own.GetProperty("total").GetInt32());
        var others = await Get(other.Client, "/api/v1/me/check-ins");
        Assert.Empty(others.GetProperty("items").EnumerateArray());
        var trend = await Get(user.Client, "/api/v1/me/trends?days=7");
        Assert.Equal(7, trend.GetArrayLength());
        Assert.Equal(6, trend.EnumerateArray().Count(day => day.GetProperty("mood").ValueKind == JsonValueKind.Null));
        using var invalid = await user.Client.GetAsync("/api/v1/me/check-ins?page=0");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        using var future = await user.Client.PutAsJsonAsync($"/api/v1/me/check-ins/{CareDate.Today(DateTimeOffset.UtcNow).AddDays(1):yyyy-MM-dd}", body);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, future.StatusCode);
        using var deleted = await user.Client.DeleteAsync($"/api/v1/me/check-ins/{Today}");
        deleted.EnsureSuccessStatusCode();
        Assert.Equal(0, (await Get(user.Client, "/api/v1/me/check-ins")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Sharing_requires_current_assignment_and_stays_revoked_after_reassignment_back()
    {
        using var user = await ConsultationScenario.CreateUserAsync(fixture);
        using var doctor = await fixture.CreateTrustedApiClientForAsync("doctor@demo.local");
        using var admin = await fixture.CreateTrustedApiClientForAsync("123@qq.com");
        using var counselor = await fixture.CreateTrustedApiClientForAsync("counselor@demo.local");
        var taskId = await SeedFollowUp(user.SubjectId);
        var subjects = await Get(doctor, "/api/v1/clinical/subjects?pageSize=100");
        Assert.Contains(subjects.GetProperty("items").EnumerateArray(), item => item.GetProperty("subjectId").GetGuid() == user.SubjectId);
        using var saved = await user.Client.PutAsJsonAsync($"/api/v1/me/check-ins/{Today}", new { mood = 2, sleepHours = 6, note = "PRIVATE-SHARED-NOTE" });
        saved.EnsureSuccessStatusCode();
        var path = $"/api/v1/clinical/subjects/{user.SubjectId}";
        Assert.False((await Get(doctor, path)).GetProperty("sharingActive").GetBoolean());
        foreach (var unauthorized in new[] { admin, counselor, user.Client })
        {
            using var denied = await unauthorized.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.DoesNotContain("PRIVATE-SHARED-NOTE", await denied.Content.ReadAsStringAsync());
        }
        using var missingConsent = await user.Client.PostAsJsonAsync("/api/v1/me/sharing-grants", new { followUpId = taskId });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingConsent.StatusCode);
        var grantId = await Grant(user.Client, taskId);
        Assert.Equal(grantId, await Grant(user.Client, taskId));
        var shared = await Get(doctor, path);
        Assert.True(shared.GetProperty("sharingActive").GetBoolean());
        Assert.Contains("PRIVATE-SHARED-NOTE", shared.ToString());
        using var revoked = await user.Client.DeleteAsync($"/api/v1/me/sharing-grants/{grantId}");
        revoked.EnsureSuccessStatusCode();
        Assert.DoesNotContain("PRIVATE-SHARED-NOTE", (await Get(doctor, path)).ToString());
        await Grant(user.Client, taskId);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            var task = await db.FollowUpTasks.SingleAsync(item => item.Id == taskId);
            var slot = await db.AvailabilitySlots.FirstOrDefaultAsync();
            if (slot is null)
            {
                slot = MentalHealth.Domain.Consultations.AvailabilitySlot.Create(IdentitySeeder.DemoDoctorId, DateTimeOffset.UtcNow.AddHours(8), DateTimeOffset.UtcNow.AddHours(9), DateTimeOffset.UtcNow);
                db.AvailabilitySlots.Add(slot);
                await db.SaveChangesAsync();
            }
            task.Reschedule(Guid.NewGuid(), slot.Id, DateTimeOffset.UtcNow.AddDays(2), "换医生", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            task.Reschedule(IdentitySeeder.DemoDoctorId, slot.Id, DateTimeOffset.UtcNow.AddDays(3), "重新安排", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        Assert.False((await Get(doctor, path)).GetProperty("sharingActive").GetBoolean());
        var summary = await Get(admin, "/api/v1/workspace/summary");
        Assert.DoesNotContain("PRIVATE", summary.ToString());
        Assert.DoesNotContain("subjectId", summary.ToString());
    }

    [Fact]
    public async Task Plans_publish_once_reject_edits_and_keep_feedback_separate_from_private_data()
    {
        using var user = await ConsultationScenario.CreateUserAsync(fixture);
        using var other = await ConsultationScenario.CreateUserAsync(fixture);
        using var doctor = await fixture.CreateTrustedApiClientForAsync("doctor@demo.local");
        using var admin = await fixture.CreateTrustedApiClientForAsync("123@qq.com");
        var followUpId = await SeedFollowUp(user.SubjectId);
        var body = new { followUpId, title = "本周安排", idempotencyKey = Guid.NewGuid().ToString(), tasks = new[] { new { kind = "CheckIn", exerciseId = (string?)null, dueDate = Today } } };
        using var create = await doctor.PostAsJsonAsync("/api/v1/care-plans", body);
        create.EnsureSuccessStatusCode();
        var plan = await Json(create);
        var id = plan.GetProperty("id").GetGuid();
        using var repeat = await doctor.PostAsJsonAsync("/api/v1/care-plans", body);
        repeat.EnsureSuccessStatusCode();
        Assert.Equal(id, (await Json(repeat)).GetProperty("id").GetGuid());
        Assert.Empty((await Get(user.Client, "/api/v1/care-plans")).GetProperty("items").EnumerateArray());
        using var publish = await doctor.PostAsync($"/api/v1/care-plans/{id}/publish", null);
        publish.EnsureSuccessStatusCode();
        using var edit = await doctor.PutAsJsonAsync($"/api/v1/care-plans/{id}", new { title = "不应改写", tasks = body.tasks, version = (await Json(publish)).GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edit.StatusCode);
        foreach (var deniedClient in new[] { other.Client, admin })
        {
            using var denied = await deniedClient.GetAsync($"/api/v1/care-plans/{id}");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        var taskId = plan.GetProperty("tasks")[0].GetProperty("id").GetGuid();
        var feedbackPath = $"/api/v1/care-plans/{id}/tasks/{taskId}/feedback";
        using var missing = await user.Client.PostAsJsonAsync(feedbackPath, new { status = "Done" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        using var feedback = await user.Client.PostAsJsonAsync(feedbackPath, new { status = "Done", feedback = "FEEDBACK-ONLY", acknowledged = true });
        feedback.EnsureSuccessStatusCode();
        Assert.Equal("Completed", (await Json(feedback)).GetProperty("status").GetString());
        Assert.Contains("FEEDBACK-ONLY", (await Get(doctor, $"/api/v1/care-plans/{id}")).ToString());
        Assert.False((await Get(doctor, $"/api/v1/clinical/subjects/{user.SubjectId}")).GetProperty("sharingActive").GetBoolean());
        await using var verifyScope = fixture.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        Assert.Equal(FollowUpStatus.Scheduled, (await verifyDb.FollowUpTasks.SingleAsync(item => item.Id == followUpId)).Status);
        Assert.DoesNotContain(await verifyDb.AuditEvents.ToListAsync(), entry => entry.Reason != null && entry.Reason.Contains("FEEDBACK-ONLY"));
    }

    [Fact]
    public async Task Exercise_completion_deduplicates_and_export_and_deletion_include_all_new_data()
    {
        using var user = await ConsultationScenario.CreateUserAsync(fixture);
        using var other = await ConsultationScenario.CreateUserAsync(fixture);
        var followUp = await SeedFollowUp(user.SubjectId);
        using var put = await user.Client.PutAsJsonAsync($"/api/v1/me/check-ins/{Today}", new { mood = 4, sleepHours = 8, note = "EXPORT-NOTE" });
        put.EnsureSuccessStatusCode();
        var completion = new { id = Guid.NewGuid(), exerciseId = "grounding" };
        for (var i = 0; i < 2; i++)
        {
            using var done = await user.Client.PostAsJsonAsync("/api/v1/me/exercise-completions", completion);
            done.EnsureSuccessStatusCode();
        }
        Assert.Equal(1, (await Get(user.Client, "/api/v1/me/exercise-completions")).GetProperty("total").GetInt32());
        using var reused = await other.Client.PostAsJsonAsync("/api/v1/me/exercise-completions", completion);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        await Grant(user.Client, followUp);
        using var doctor = await fixture.CreateTrustedApiClientForAsync("doctor@demo.local");
        using var create = await doctor.PostAsJsonAsync("/api/v1/care-plans", new
        {
            followUpId = followUp,
            title = "EXPORT-PLAN",
            idempotencyKey = Guid.NewGuid().ToString(),
            tasks = new[] { new { kind = "Exercise", exerciseId = "grounding", dueDate = Today } }
        });
        create.EnsureSuccessStatusCode();
        var planId = (await Json(create)).GetProperty("id").GetGuid();
        using var published = await doctor.PostAsync($"/api/v1/care-plans/{planId}/publish", null);
        published.EnsureSuccessStatusCode();
        using var export = await user.Client.GetAsync("/api/v1/data-rights/export");
        export.EnsureSuccessStatusCode();
        using var stream = new MemoryStream(await export.Content.ReadAsByteArrayAsync());
        using var zip = new ZipArchive(stream);
        using var reader = new StreamReader(zip.GetEntry("care.json")!.Open());
        var text = await reader.ReadToEndAsync();
        Assert.Contains("EXPORT-NOTE", text);
        Assert.Contains("grounding", text);
        Assert.Contains("EXPORT-PLAN", text);
        using var clear = await user.Client.DeleteAsync("/api/v1/data-rights/demo-data");
        clear.EnsureSuccessStatusCode();
        foreach (var path in new[] { "check-ins", "exercise-completions", "sharing-grants" })
            Assert.Equal(0, (await Get(user.Client, $"/api/v1/me/{path}")).GetProperty("total").GetInt32());
        Assert.All((await Get(user.Client, "/api/v1/me/trends")).EnumerateArray(), day => Assert.Equal(JsonValueKind.Null, day.GetProperty("mood").ValueKind));
        Assert.Empty((await Get(user.Client, "/api/v1/care-plans")).GetProperty("items").EnumerateArray());
        using var afterExport = await user.Client.GetAsync("/api/v1/data-rights/export");
        afterExport.EnsureSuccessStatusCode();
        using var afterStream = new MemoryStream(await afterExport.Content.ReadAsByteArrayAsync());
        using var afterZip = new ZipArchive(afterStream);
        using var afterReader = new StreamReader(afterZip.GetEntry("care.json")!.Open());
        var cleared = await afterReader.ReadToEndAsync();
        Assert.DoesNotContain("EXPORT-NOTE", cleared);
        Assert.DoesNotContain("EXPORT-PLAN", cleared);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        Assert.False(await db.CarePlanTasks.AnyAsync(item => item.PlanId == planId));
    }

    [Fact]
    public async Task Draft_edits_replace_tasks_without_duplicates_and_cancelled_drafts_stay_private()
    {
        using var user = await ConsultationScenario.CreateUserAsync(fixture);
        using var doctor = await fixture.CreateTrustedApiClientForAsync("doctor@demo.local");
        var followUpId = await SeedFollowUp(user.SubjectId);
        var originalTasks = new[] { new { kind = "CheckIn", exerciseId = (string?)null, dueDate = Today } };
        using var create = await doctor.PostAsJsonAsync("/api/v1/care-plans", new
        {
            followUpId,
            title = "草稿",
            idempotencyKey = Guid.NewGuid().ToString(),
            tasks = originalTasks
        });
        create.EnsureSuccessStatusCode();
        var draft = await Json(create);
        var id = draft.GetProperty("id").GetGuid();
        var version = draft.GetProperty("version").GetInt32();
        using var update = await doctor.PutAsJsonAsync($"/api/v1/care-plans/{id}", new
        {
            title = "调整草稿",
            version,
            tasks = new[] { new { kind = "Exercise", exerciseId = "pause", dueDate = Today } }
        });
        update.EnsureSuccessStatusCode();
        using var stale = await doctor.PutAsJsonAsync($"/api/v1/care-plans/{id}", new { title = "旧页面", version, tasks = originalTasks });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var saved = await Get(doctor, $"/api/v1/care-plans/{id}");
        Assert.Single(saved.GetProperty("tasks").EnumerateArray());
        Assert.Equal("pause", saved.GetProperty("tasks")[0].GetProperty("exerciseId").GetString());
        using var cancel = await doctor.PostAsync($"/api/v1/care-plans/{id}/cancel", null);
        cancel.EnsureSuccessStatusCode();
        Assert.Empty((await Get(user.Client, "/api/v1/care-plans")).GetProperty("items").EnumerateArray());
        using var forbidden = await user.Client.GetAsync($"/api/v1/care-plans/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var publish = await doctor.PostAsync($"/api/v1/care-plans/{id}/publish", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, publish.StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        Assert.Equal(1, await db.CarePlanTasks.CountAsync(item => item.PlanId == id));
    }

    [Fact]
    public async Task Consultation_lists_respect_actor_filters_and_report_state()
    {
        using var started = await ConsultationScenario.StartVideoAsync(fixture);
        using var other = await ConsultationScenario.CreateUserAsync(fixture);
        using var counselor = await fixture.CreateTrustedApiClientForAsync("counselor@demo.local");
        using var admin = await fixture.CreateTrustedApiClientForAsync("123@qq.com");
        var own = await Get(started.User.Client, "/api/v1/consultations?status=InProgress");
        Assert.Contains(own.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == started.SessionId);
        Assert.Empty((await Get(other.Client, "/api/v1/consultations")).GetProperty("items").EnumerateArray());
        Assert.Contains((await Get(counselor, "/api/v1/consultations?pageSize=100")).GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == started.SessionId);
        Assert.Empty((await Get(started.User.Client, "/api/v1/results")).GetProperty("items").EnumerateArray());
        using var complete = await started.User.Client.PostAsJsonAsync($"/api/v1/consultations/{started.SessionId}/complete", new { idempotencyKey = Guid.NewGuid().ToString() });
        complete.EnsureSuccessStatusCode();
        Assert.Single((await Get(started.User.Client, "/api/v1/results")).GetProperty("items").EnumerateArray());
        Assert.Single((await Get(started.User.Client, "/api/v1/results?status=NotRequested")).GetProperty("items").EnumerateArray());
        Assert.Empty((await Get(started.User.Client, "/api/v1/results?status=Completed")).GetProperty("items").EnumerateArray());
        Assert.Empty((await Get(started.User.Client, $"/api/v1/consultations?from={DateTimeOffset.UtcNow.AddDays(1):yyyy-MM-dd}")).GetProperty("items").EnumerateArray());
        using var denied = await admin.GetAsync("/api/v1/consultations");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("cancel")]
    [InlineData("reschedule")]
    [InlineData("reassign")]
    public async Task Another_doctor_cannot_take_over_or_change_a_follow_up(string action)
    {
        using var user = await ConsultationScenario.CreateUserAsync(fixture);
        using var doctor = await fixture.CreateTrustedApiClientForAsync("doctor@demo.local");
        var owner = Guid.NewGuid();
        var followUpId = await SeedFollowUp(user.SubjectId, owner);
        Guid slotId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            var slot = MentalHealth.Domain.Consultations.AvailabilitySlot.Create(IdentitySeeder.DemoDoctorId,
                DateTimeOffset.UtcNow.AddHours(12), DateTimeOffset.UtcNow.AddHours(13), DateTimeOffset.UtcNow);
            db.AvailabilitySlots.Add(slot);
            await db.SaveChangesAsync();
            slotId = slot.Id;
        }
        using var response = await doctor.PostAsJsonAsync($"/api/v1/follow-ups/{followUpId}/{action}",
            new { availabilitySlotId = slotId, reason = "合成越权测试" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verify = fixture.Services.CreateAsyncScope();
        var saved = await verify.ServiceProvider.GetRequiredService<MentalHealthDbContext>().FollowUpTasks.SingleAsync(task => task.Id == followUpId);
        Assert.Equal(owner, saved.AssigneeId);
        Assert.Equal(FollowUpStatus.Scheduled, saved.Status);
    }

    [Fact]
    public async Task An_unassigned_follow_up_can_still_be_scheduled_through_the_existing_flow()
    {
        using var user = await ConsultationScenario.CreateUserAsync(fixture);
        using var doctor = await fixture.CreateTrustedApiClientForAsync("doctor@demo.local");
        Guid followUpId, slotId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
            var task = FollowUpTask.Propose(user.SubjectId, Guid.NewGuid(), DateTimeOffset.UtcNow);
            var slot = MentalHealth.Domain.Consultations.AvailabilitySlot.Create(IdentitySeeder.DemoDoctorId,
                DateTimeOffset.UtcNow.AddHours(14), DateTimeOffset.UtcNow.AddHours(15), DateTimeOffset.UtcNow);
            db.FollowUpTasks.Add(task);
            db.AvailabilitySlots.Add(slot);
            await db.SaveChangesAsync();
            followUpId = task.Id;
            slotId = slot.Id;
        }
        using var response = await doctor.PostAsJsonAsync($"/api/v1/follow-ups/{followUpId}/reschedule",
            new { availabilitySlotId = slotId, reason = "安排合成回访" });
        response.EnsureSuccessStatusCode();
        var saved = await Json(response);
        Assert.Equal(IdentitySeeder.DemoDoctorId, saved.GetProperty("assigneeId").GetGuid());
        Assert.Equal("Scheduled", saved.GetProperty("status").GetString());
    }

    private async Task<Guid> SeedFollowUp(Guid subjectId, Guid? assigneeId = null)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
        var task = FollowUpTask.Schedule(subjectId, Guid.NewGuid(), assigneeId ?? IdentitySeeder.DemoDoctorId, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow);
        db.FollowUpTasks.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }
    private static async Task<Guid> Grant(HttpClient client, Guid taskId)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/me/sharing-grants", new { followUpId = taskId, acknowledged = true });
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("id").GetGuid();
    }
    private static async Task<JsonElement> Get(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await Json(response);
    }
    private static Task<JsonElement> Json(HttpResponseMessage response) => response.Content.ReadFromJsonAsync<JsonElement>();
}
