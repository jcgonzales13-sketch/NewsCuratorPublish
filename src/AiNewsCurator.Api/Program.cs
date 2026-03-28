using AiNewsCurator.Api.Middleware;
using AiNewsCurator.Api.Operations;
using AiNewsCurator.Application;
using AiNewsCurator.Application.Configuration;
using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Application.HostedServices;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddMappedEnvironmentVariables();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery();
builder.Services
    .AddAuthentication(OpsCookieAuthenticationDefaults.Scheme)
    .AddCookie(OpsCookieAuthenticationDefaults.Scheme);
builder.Services.Configure<CookieAuthenticationOptions>(OpsCookieAuthenticationDefaults.Scheme, options =>
{
    var appOptions = builder.Configuration.Get<AppOptions>() ?? new AppOptions();
    options.Cookie.Name = appOptions.OpsSessionCookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/ops/login";
    options.AccessDeniedPath = "/ops/login";
});
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (builder.Configuration.GetValue<bool>("EnableScheduler"))
{
    builder.Services.AddHostedService<DailySchedulerService>();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    var sourceRepository = scope.ServiceProvider.GetRequiredService<ISourceRepository>();
    var opsUserRepository = scope.ServiceProvider.GetRequiredService<IOpsUserRepository>();
    var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;
    await initializer.InitializeAsync(CancellationToken.None);
    await sourceRepository.SeedDefaultsAsync(CancellationToken.None);

    var bootstrapEmail = OpsAuthEmailNormalizer.Normalize(appOptions.OpsBootstrapEmail);
    if (!string.IsNullOrWhiteSpace(bootstrapEmail))
    {
        var existingOpsUser = await opsUserRepository.FindByEmailAsync(bootstrapEmail, CancellationToken.None);
        if (existingOpsUser is null)
        {
            var now = DateTimeOffset.UtcNow;
            await opsUserRepository.AddAsync(
                new OpsUser
                {
                    Email = bootstrapEmail,
                    DisplayName = string.IsNullOrWhiteSpace(appOptions.OpsBootstrapName) ? null : appOptions.OpsBootstrapName.Trim(),
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                },
                CancellationToken.None);
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.UseAuthentication();
app.UseMiddleware<OperationsAccessMiddleware>();
app.UseMiddleware<InternalApiKeyMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultControllerRoute();

app.Run();
