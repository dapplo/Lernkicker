using Lernkicker.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using global::Telegram.Bot;
using global::Telegram.Bot.Types;
using global::Telegram.Bot.Types.Enums;

namespace Lernkicker.Telegram;

/// <summary>
/// Long polling (Telegram never connects to the app) and sending the queued messages. Only private chats are
/// read; groups and channels are ignored.
/// </summary>
internal sealed class TelegramBot(string token, TelegramMessenger messenger, IServiceScopeFactory scopes, ILogger<TelegramBot> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bot = new TelegramBotClient(token);
        try
        {
            await StartAsync(bot, stoppingToken);
            await Task.WhenAll(ReceiveAsync(bot, stoppingToken), SendAsync(bot, stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down
        }
    }

    private async Task StartAsync(TelegramBotClient bot, CancellationToken stoppingToken)
    {
        while (true)
        {
            try
            {
                var me = await bot.GetMe(stoppingToken);
                messenger.BotUsername = me.Username;
                await bot.DeleteWebhook(cancellationToken: stoppingToken);
                foreach (var language in MessageTexts.Languages)
                {
                    await bot.SetMyCommands(MessageTexts.Commands(language).Select(c => new BotCommand { Command = c.Command, Description = c.Description }),
                        languageCode: language == MessageTexts.DefaultLanguage ? null : language, cancellationToken: stoppingToken);
                }

                logger.LogInformation("Telegram bot @{Bot} started", me.Username);
                return;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Telegram bot could not start; trying again");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task ReceiveAsync(TelegramBotClient bot, CancellationToken stoppingToken)
    {
        int? offset = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            Update[] updates;
            try
            {
                updates = await bot.GetUpdates(offset, timeout: 30, allowedUpdates: [UpdateType.Message], cancellationToken: stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Telegram updates could not be read");
                await Task.Delay(RetryDelay, stoppingToken);
                continue;
            }

            foreach (var update in updates)
            {
                offset = update.Id + 1;
                if (update.Message is { Text: { } text, Chat.Type: ChatType.Private } message)
                {
                    await HandleAsync(message.Chat.Id, text, message.From?.LanguageCode);
                }
            }
        }
    }

    private async Task HandleAsync(long chatId, string text, string? languageCode)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var reply = await scope.ServiceProvider.GetRequiredService<TelegramUseCases>().HandleAsync(chatId, text, MessageTexts.Language(languageCode));
            if (reply is not null)
            {
                messenger.Send(chatId, reply.Language, reply.Notification);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Telegram message could not be handled");
        }
    }

    private async Task SendAsync(TelegramBotClient bot, CancellationToken stoppingToken)
    {
        await foreach (var message in messenger.Outbox.ReadAllAsync(stoppingToken))
        {
            try
            {
                await bot.SendMessage(message.ChatId, MessageTexts.Format(message.Notification, message.Language),
                    linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true }, cancellationToken: stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Telegram message could not be sent");
            }
        }
    }
}
