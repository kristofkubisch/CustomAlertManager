using Serilog;
using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Entities;
using CustomAlertManager.Data.Services;
using CustomAlertManager.RssReader.Interfaces;
using CustomAlertManager.RssReader.Services;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Services;
using CustomAlertManager.Web.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logging) => logging
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(CustomAlertManager.Common.Services.RepositoryPaths.LogDirectory, "web-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));
var configuration = builder.Configuration;

var connectionString = configuration.GetConnectionString("AlertDatabase")
    ?? AlertDbContext.DefaultConnectionString;
builder.Services.AddDbContextFactory<AlertDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped(provider =>
    provider.GetRequiredService<IDbContextFactory<AlertDbContext>>().CreateDbContext());

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        // Relaxed so the default admin password (which has no uppercase letter) is accepted.
        options.Password.RequireUppercase = false;
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<AlertDbContext>();

builder.Services.AddSingleton(new SendGridOptions(
    ApiKey: configuration["SendGrid:ApiKey"] ?? string.Empty,
    FromAddress: configuration["SendGrid:FromAddress"] ?? string.Empty,
    FromName: configuration["SendGrid:FromName"] ?? "CustomAlertManager"));
builder.Services.AddSingleton<IEmailSender, SendGridEmailSender>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, IdentityEmailSender>();

// Channels for the admin test-alert page. The worker has its own registration for the same channels.
builder.Services.AddHttpClient<SlackAlertChannel>(client => client.Timeout = TimeSpan.FromSeconds(15));

// The admin test runs the same feed engine as the Worker, on demand.
builder.Services.AddHttpClient<IFeedFetcher, HttpFeedFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CustomAlertManager.Web/1.0");
})
    // Refuses private and local addresses on every connection, including after redirects.
    .ConfigurePrimaryHttpMessageHandler(() => PublicOnlyConnector.CreateHandler());
builder.Services.AddTransient<IAlertEngine, AlertEngine>();
builder.Services.AddScoped<SubscriptionTestRunner>();
builder.Services.AddTransient<IAlertChannel>(provider => provider.GetRequiredService<SlackAlertChannel>());
builder.Services.AddTransient<IAlertChannel>(provider => new SendGridAlertChannel(provider.GetRequiredService<IEmailSender>()));

builder.Services.AddSingleton<TargetVerificationTokens>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<NewsSourceService>();
builder.Services.AddScoped<UserAdministrationService>();

builder.Services.AddAuthorization(options => options.AddPolicy("Admin", policy => policy.RequireRole("Admin")));

// A deactivated user loses access on their next request, even while their login cookie is still valid.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnValidatePrincipal = async context =>
    {
        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await users.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Subscriptions");
    options.Conventions.AuthorizeFolder("/Targets");
    options.Conventions.AuthorizeFolder("/Admin", "Admin");
    // The confirmation link from the email must work without signing in.
    options.Conventions.AllowAnonymousToPage("/Targets/Verify");
});

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
if (string.IsNullOrWhiteSpace(configuration["SendGrid:ApiKey"]))
{
    startupLogger.LogWarning("SendGrid:ApiKey is not configured; email, confirmation and password-reset mail will fail.");
}

if (string.IsNullOrWhiteSpace(configuration["Admin:Password"]))
{
    startupLogger.LogWarning("Admin:Password is not configured; the default admin account is not created.");
}

var seed = configuration.GetValue<bool>("Seed:Enabled")
    ? new DevSeedOptions(configuration["Seed:SlackWebhookUrl"], configuration["Seed:EmailAddress"])
    : null;
var contextFactory = app.Services.GetRequiredService<IDbContextFactory<AlertDbContext>>();
await AlertDatabaseInitializer.InitializeAsync(contextFactory, seed, CancellationToken.None);

var admin = new AdminSeedOptions(
    Username: configuration["Admin:Username"] ?? string.Empty,
    Email: configuration["Admin:Email"] ?? string.Empty,
    Password: configuration["Admin:Password"] ?? string.Empty);
using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        admin);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Lets the integration tests host the app with WebApplicationFactory.
public partial class Program;
