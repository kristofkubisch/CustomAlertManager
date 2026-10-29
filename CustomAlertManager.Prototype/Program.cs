using CustomAlertManager.AlertSender.Services;
using CustomAlertManager.Prototype.Options;
using CustomAlertManager.RssReader.Services;
using CustomAlertManager.Scheduler.Models;
using CustomAlertManager.Scheduler.Services;

Subscription subscription;
try
{
    subscription = ArgumentParser.Parse(args, DateTimeOffset.UtcNow);
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

var engine = new AlertEngine(new HttpFeedFetcher(httpClient));
var result = await engine.ProcessAsync([subscription], cancellationSource.Token);
var matches = result.Matches[0].Items.Select(sourced => sourced.Item).ToList();

await new ConsoleAlertChannel().SendAsync(matches, cancellationSource.Token);

Console.WriteLine();
Console.WriteLine($"Checked {subscription.Sources.Count} feed(s); {matches.Count} matching item(s) published since {subscription.LastAlertAt.ToLocalTime():g}.");
return 0;
