using CustomAlertManager.AlertSender.Interfaces;
using CustomAlertManager.AlertSender.Models;
using CustomAlertManager.Scheduler.Interfaces;
using CustomAlertManager.Scheduler.Models;

namespace CustomAlertManager.Scheduler.Services;

public sealed class AlertDispatcher(IDeliveryStore deliveryStore, IEnumerable<IAlertChannel> channels) : IAlertDispatcher
{
    private const int BatchSize = 50;

    public async Task<int> DispatchDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var channelsByKind = channels.ToDictionary(channel => channel.Kind);
        var due = await deliveryStore.GetDueDeliveriesAsync(now, BatchSize, cancellationToken);

        foreach (var delivery in due)
        {
            var result = await SendAsync(channelsByKind, delivery, cancellationToken);
            var update = DeliveryRetryPolicy.Apply(delivery, result, DateTimeOffset.UtcNow);
            await deliveryStore.RecordAttemptAsync(update, cancellationToken);
        }

        return due.Count;
    }

    private static async Task<DeliveryResult> SendAsync(
        IReadOnlyDictionary<ChannelKind, IAlertChannel> channelsByKind,
        PendingDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (!channelsByKind.TryGetValue(delivery.Kind, out var channel))
        {
            return DeliveryResult.Permanent($"No {delivery.Kind} channel is configured.");
        }

        try
        {
            return await channel.SendAsync(delivery.Destination, delivery.Message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A channel bug should not stop the other deliveries; treat it as retryable.
            return DeliveryResult.Transient(ex.Message);
        }
    }
}
