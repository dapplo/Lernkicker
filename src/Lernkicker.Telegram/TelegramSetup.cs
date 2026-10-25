using Lernkicker.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lernkicker.Telegram;

public static class TelegramSetup
{
    /// <summary>
    /// The bot. Its token comes from <c>Lernkicker:Telegram:BotToken</c> or <c>Lernkicker:Telegram:BotTokenFile</c>
    /// (a Docker secret) or, when neither is set, from the admin page. Without a token Telegram stays off and
    /// the parents' page says so.
    /// </summary>
    public static IServiceCollection AddTelegram(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Lernkicker:Telegram");
        var token = section["BotToken"] is { Length: > 0 } inline
            ? inline
            : section["BotTokenFile"] is { Length: > 0 } file && File.Exists(file) ? File.ReadAllText(file).Trim() : null;

        services.AddSingleton(new TelegramMessenger(token));
        services.AddSingleton<IMessenger>(sp => sp.GetRequiredService<TelegramMessenger>());
        services.AddHostedService<TelegramBot>();
        services.AddHostedService(sp => ActivatorUtilities.CreateInstance<TelegramReminders>(sp, section.GetValue("ReminderHour", 18)));

        return services;
    }
}
