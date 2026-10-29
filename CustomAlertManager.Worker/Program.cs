using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Data;
using CustomAlertManager.Data.Services;
using CustomAlertManager.RssReader.Interfaces;
using CustomAlertManager.RssReader.Services;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Services;
using CustomAlertManager.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;

var connectionString = configuration.GetConnectionString("AlertDatabase")
    ?? AlertDbContext.DefaultConnectionString;
builder.Services.AddAlertData(connectionString);

builder.Services.AddHttpClient<IFeedFetcher, HttpFeedFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CustomAlertManager.Worker/1.0");
});

builder.Services.AddHttpClient<SlackAlertChannel>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient<SendGridAlertChannel>(client =>
{
    client.BaseAddress = new Uri("https://api.sendgrid.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton(new SendGridOptions(
    ApiKey: configuration["SendGrid:ApiKey"] ?? string.Empty,
    FromAddress: configuration["SendGrid:FromAddress"] ?? string.Empty,
    FromName: configuration["SendGrid:FromName"] ?? "CustomAlertManager"));

builder.Services.AddScoped<IAlertEngine, AlertEngine>();
builder.Services.AddTransient<IAlertChannel>(provider => provider.GetRequiredService<SlackAlertChannel>());
builder.Services.AddTransient<IAlertChannel>(provider => provider.GetRequiredService<SendGridAlertChannel>());
builder.Services.AddScoped<IAlertDispatcher, AlertDispatcher>();
builder.Services.AddHostedService<AlertWorker>();

var host = builder.Build();

var seed = configuration.GetValue<bool>("Seed:Enabled")
    ? new DevSeedOptions(configuration["Seed:SlackWebhookUrl"], configuration["Seed:EmailAddress"])
    : null;
var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AlertDbContext>>();
await AlertDatabaseInitializer.InitializeAsync(contextFactory, seed, CancellationToken.None);

await host.RunAsync();
