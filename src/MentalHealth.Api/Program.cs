using MentalHealth.Api.Authorization;
using MentalHealth.Api.Hubs;
using MentalHealth.Api.Services;
using MentalHealth.Api.Middleware;
using MentalHealth.Infrastructure;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);
const string LocalClientCorsPolicy = "LocalClients";
var allowedClientOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
if (allowedClientOrigins.Length == 0)
{
    throw new InvalidOperationException("At least one client CORS origin is required.");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
builder.Services.AddSingleton<NotificationPublisher>();
builder.Services.AddHostedService<MediaUploadCleanupWorker>();
builder.Services.AddHostedService<SmsDispatchWorker>();
builder.Services.AddCors(options => options.AddPolicy(
    LocalClientCorsPolicy,
    policy => policy
        .WithOrigins(allowedClientOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMentalHealthAuthorization();
builder.Services.PostConfigure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options =>
    {
        options.Events ??= new JwtBearerEvents();
        options.Events.OnMessageReceived = context =>
        {
            var token = context.Request.Query["access_token"].ToString();
            if (!string.IsNullOrWhiteSpace(token)
                && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
            {
                context.Token = token;
            }

            return Task.CompletedTask;
        };
    });

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<PhoneLoginAccountUpgrader>().UpgradeAsync();
    await scope.ServiceProvider.GetRequiredService<DemoCatalogSeeder>().SeedAsync();
}

app.UseMiddleware<SensitiveLogRedactionMiddleware>();
app.UseExceptionHandler();
app.UseStaticFiles();
app.UseCors(LocalClientCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapHub<DevelopmentProbeHub>("/hubs/development-probe");
}

app.MapControllers();
app.MapHub<ConsultationHub>("/hubs/chat");
app.MapHub<RtcSignalingHub>("/hubs/rtc");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .ExcludeFromDescription();

app.Run();

public partial class Program;
