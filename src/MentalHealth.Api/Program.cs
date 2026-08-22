using MentalHealth.Api.Authorization;
using MentalHealth.Api.Hubs;
using MentalHealth.Infrastructure;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddCors(options => options.AddPolicy(
    LocalClientCorsPolicy,
    policy => policy
        .WithOrigins(allowedClientOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMentalHealthAuthorization();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MentalHealthDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<DemoCatalogSeeder>().SeedAsync();
}

app.UseExceptionHandler();
app.UseCors(LocalClientCorsPolicy);
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
