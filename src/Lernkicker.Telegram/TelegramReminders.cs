using Lernkicker.Application.Ports;
using Lernkicker.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lernkicker.Telegram;

/// <summary>
/// Once a day at <c>Lernkicker:Telegram:ReminderHour</c> (family time zone, default 18, -1 = off), every
/// family with a linked chat gets tomorrow's dates, if there are any.
/// </summary>
internal sealed class TelegramReminders(int hour, ILocalClock clock, IServiceScopeFactory scopes, ILogger<TelegramReminders> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (hour is < 0 or > 23)
        {
            return;
        }

        // Started after the hour: today's reminder is not sent late
        DateOnly? sentOn = clock.TimeOfDay.Hour >= hour ? clock.Today : null;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (clock.TimeOfDay.Hour >= hour && sentOn != clock.Today)
                {
                    sentOn = clock.Today;
                    await SendAllAsync();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down
        }
    }

    private async Task SendAllAsync()
    {
        IReadOnlyList<Guid> families;
        using (var scope = scopes.CreateScope())
        {
            families = await scope.ServiceProvider.GetRequiredService<TelegramUseCases>().FamiliesForRemindersAsync();
        }

        foreach (var familyId in families)
        {
            // One scope per family: each runs in its own family
            using var scope = scopes.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<TelegramUseCases>().SendRemindersAsync(familyId);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Reminders for family {Id} could not be sent", familyId);
            }
        }
    }
}
