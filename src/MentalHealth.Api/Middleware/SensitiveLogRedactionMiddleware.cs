namespace MentalHealth.Api.Middleware;

public sealed class SensitiveLogRedactionMiddleware(
    RequestDelegate next,
    ILogger<SensitiveLogRedactionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "HTTP request completed {Method} {Path} {StatusCode}",
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty,
                context.Response.StatusCode);
        }
    }
}
