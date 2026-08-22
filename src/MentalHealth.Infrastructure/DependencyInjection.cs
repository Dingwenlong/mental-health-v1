using MentalHealth.Application.Abstractions.Providers;
using MentalHealth.Application.Abstractions.Clock;
using MentalHealth.Application.Abstractions;
using MentalHealth.Application.Security;
using MentalHealth.Application.Abstractions.Persistence;
using MentalHealth.Application.Audit;
using MentalHealth.Application.Consents;
using MentalHealth.Application.Catalog;
using MentalHealth.Application.Consultations;
using MentalHealth.Infrastructure.Identity;
using MentalHealth.Infrastructure.Content;
using MentalHealth.Infrastructure.Outbox;
using MentalHealth.Infrastructure.Persistence;
using MentalHealth.Infrastructure.Storage;
using MentalHealth.Infrastructure.Providers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

namespace MentalHealth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var uiCopyPath = configuration["UiCopy:Path"];
        if (string.IsNullOrWhiteSpace(uiCopyPath))
        {
            uiCopyPath = Path.Combine(
                AppContext.BaseDirectory,
                "content",
                "zh-CN",
                "ui-copy.v1.json");
        }

        services.AddSingleton<IUiCopyCatalog>(new JsonUiCopyCatalog(uiCopyPath));
        services.AddSingleton<OutboxSaveChangesInterceptor>();
        services.AddDbContextFactory<MentalHealthDbContext>((provider, options) =>
            options
                .UseNpgsql(RequireConnectionString(
                    provider.GetRequiredService<IConfiguration>(),
                    "MentalHealth"))
                .AddInterceptors(provider.GetRequiredService<OutboxSaveChangesInterceptor>()));

        services
            .AddOptions<LocalObjectStorageOptions>()
            .Bind(configuration.GetSection(LocalObjectStorageOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.RootPath),
                "Local object storage root is required.")
            .ValidateOnStart();
        services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        services.AddSingleton<IConnectionMultiplexer>(provider =>
            ConnectionMultiplexer.Connect(RequireConnectionString(
                provider.GetRequiredService<IConfiguration>(),
                "Redis")));

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(ValidateJwtOptions, "JWT configuration is invalid.")
            .ValidateOnStart();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<DemoPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(provider =>
            provider.GetRequiredService<DemoPaymentGateway>());

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 20;
                options.Password.RequiredUniqueChars = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<MentalHealthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<DemoCatalogSeeder>();
        services.AddScoped<IConsentRepository>(provider =>
            provider.GetRequiredService<MentalHealthDbContext>());
        services.AddScoped<IAuditTrail>(provider =>
            provider.GetRequiredService<MentalHealthDbContext>());
        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<MentalHealthDbContext>());
        services.AddScoped<RecordConsentHandler>();
        services.AddScoped<ICatalogRepository>(provider =>
            provider.GetRequiredService<MentalHealthDbContext>());
        services.AddScoped<IOrderRepository>(provider =>
            provider.GetRequiredService<MentalHealthDbContext>());
        services.AddScoped<CatalogQueryHandler>();
        services.AddScoped<AdminCatalogHandler>();
        services.AddScoped<OrderHandler>();
        services.AddScoped<IConsultationRepository>(provider =>
            provider.GetRequiredService<MentalHealthDbContext>());
        services.AddScoped<CreateConsultationHandler>();
        services.AddScoped<StartConsultationHandler>();
        services.AddScoped<SendMessageHandler>();
        services.AddScoped<CompleteConsultationHandler>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, configured) =>
            {
                var jwtOptions = configured.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });

        return services;
    }

    private static bool ValidateJwtOptions(JwtOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.Issuer)
            && !string.IsNullOrWhiteSpace(options.Audience)
            && Encoding.UTF8.GetByteCount(options.SigningKey) >= 32
            && options.AccessTokenMinutes is >= 1 and <= 60
            && options.MfaSetupTokenMinutes is >= 1 and <= 15;
    }

    private static string RequireConnectionString(
        IConfiguration configuration,
        string name)
    {
        var connectionString = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                $"Connection string '{name}' is required.")
            : connectionString;
    }
}
