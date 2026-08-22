using MentalHealth.Api.Authorization;
using MentalHealth.Api.Hubs;
using MentalHealth.Infrastructure;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMentalHealthAuthorization();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapHub<DevelopmentProbeHub>("/hubs/development-probe");
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .ExcludeFromDescription();

app.Run();

public partial class Program;
