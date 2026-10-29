using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Prototype.Options;
using CustomAlertManager.RssReader.Services;
using CustomAlertManager.Scheduler.Services;
using CustomAlertManager.Scheduler.Models;

Subscription subscription;
try
{
    subscription = ArgumentParser.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(ArgumentParser.Usage);
    return 1;
}

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CustomAlertManager.Prototype/1.0");

var scheduler = new AlertScheduler(new HttpFeedFetcher(httpClient), new ConsoleAlertChannel());
var result = await scheduler.RunAsync(subscription, cancellationSource.Token);

Console.WriteLine();
Console.WriteLine($"Checked {result.FeedCount} feed(s); {result.Matches.Count} matching item(s) published since {result.Cutoff.ToLocalTime():g}.");
return 0;
