using Lernkicker.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lernkicker.Telegram;

public static class TelegramSetup
{
    /// <summary>
    /// The bot, when <c>Lernkicker:Telegram:BotToken</c> or <c>Lernkicker:Telegram:BotTokenFile</c> (a Docker
    /// secret) is set; otherwise Telegram stays off and the parents' page says so.
    /// </summary>
    public static IServiceCollection AddTelegram(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Lernkicker:Telegram");
        var token = section["BotToken"] is { Length: > 0 } inline
            ? inline
            : section["BotTokenFile"] is { Length: > 0 } file && File.Exists(file) ? File.ReadAllText(file).Trim() : null;

        services.AddSingleton(new TelegramMessenger(token));
        services.AddSingleton<IMessenger>(sp => sp.GetRequiredService<TelegramMessenger>());
        if (token is not null)
        {
            services.AddHostedService(sp => ActivatorUtilities.CreateInstance<TelegramBot>(sp, token));
            services.AddHostedService(sp => ActivatorUtilities.CreateInstance<TelegramReminders>(sp, section.GetValue("ReminderHour", 18)));
        }

        return services;
    }
}
