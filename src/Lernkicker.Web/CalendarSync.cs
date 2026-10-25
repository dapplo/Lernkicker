using Lernkicker.Application.UseCases;

namespace Lernkicker.Web;

/// <summary>Imports every calendar subscription again every few hours (<c>Lernkicker:Calendar:SyncHours</c>, 0 = off).</summary>
public sealed class CalendarSync(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<CalendarSync> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hours = configuration.GetValue("Lernkicker:Calendar:SyncHours", 6.0);
        if (hours <= 0)
        {
            return;
        }

        // Give the app time to start (and migrate) first
        using var timer = new PeriodicTimer(TimeSpan.FromHours(hours));
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            do
            {
                await SyncAllAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down
        }
    }

    private async Task SyncAllAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<Application.Ports.SubscriptionRef> subscriptions;
        using (var scope = scopes.CreateScope())
        {
            subscriptions = await scope.ServiceProvider.GetRequiredService<CalendarUseCases>().ListSubscriptionsToSyncAsync();
        }

        foreach (var subscription in subscriptions)
        {
            // One scope per subscription: each runs in its own family
            using var scope = scopes.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<CalendarUseCases>().SyncAsync(subscription, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Calendar subscription {Id} could not be synced", subscription.Id);
            }
        }
    }
}
