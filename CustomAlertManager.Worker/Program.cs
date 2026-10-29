using CustomAlertManager.AlertSender.Interfaces;
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

var connectionString = builder.Configuration.GetConnectionString("AlertDatabase")
    ?? AlertDbContext.DefaultConnectionString;
builder.Services.AddAlertData(connectionString);

builder.Services.AddHttpClient<IFeedFetcher, HttpFeedFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CustomAlertManager.Worker/1.0");
});
builder.Services.AddSingleton<IAlertEngine, AlertEngine>();
builder.Services.AddSingleton<IAlertChannel, ConsoleAlertChannel>();
builder.Services.AddHostedService<AlertWorker>();

var host = builder.Build();

var contextFactory = host.Services.GetRequiredService<IDbContextFactory<AlertDbContext>>();
await AlertDatabaseInitializer.InitializeAsync(
    contextFactory,
    builder.Configuration.GetValue<bool>("Seed:Enabled"),
    CancellationToken.None);

await host.RunAsync();
