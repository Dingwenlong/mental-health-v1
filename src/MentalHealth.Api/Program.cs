using MentalHealth.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();

var app = builder.Build();

app.UseExceptionHandler();

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
