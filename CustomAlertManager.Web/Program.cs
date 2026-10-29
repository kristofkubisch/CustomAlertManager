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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
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
});
builder.Services.AddTransient<IAlertEngine, AlertEngine>();
builder.Services.AddScoped<SubscriptionTestRunner>();
builder.Services.AddTransient<IAlertChannel>(provider => provider.GetRequiredService<SlackAlertChannel>());
builder.Services.AddTransient<IAlertChannel>(provider => new SendGridAlertChannel(provider.GetRequiredService<IEmailSender>()));

builder.Services.AddSingleton<TargetVerificationTokens>();
builder.Services.AddScoped<SubscriptionService>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Subscriptions");
    options.Conventions.AuthorizeFolder("/Targets");
    // The confirmation link from the email must work without signing in.
    options.Conventions.AllowAnonymousToPage("/Targets/Verify");
});

var app = builder.Build();

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

// Lets the integration tests host the app with WebApplicationFactory.
public partial class Program;
