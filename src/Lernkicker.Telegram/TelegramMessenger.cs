using System.Threading.Channels;
using Lernkicker.Application;
using Lernkicker.Application.Ports;
using global::Telegram.Bot;
using global::Telegram.Bot.Exceptions;

namespace Lernkicker.Telegram;

/// <summary>
/// Queues messages; <see cref="TelegramBot"/> sends them, so a slow or failing Telegram never delays the app.
/// Holds the current token: from the configuration (fixed) or set by the admin in the app (the bot restarts).
/// </summary>
public sealed class TelegramMessenger(string? configuredToken) : IMessenger
{
    private readonly Channel<OutgoingMessage> _queue = Channel.CreateBounded<OutgoingMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private readonly Lock _lock = new();
    private string? _token = configuredToken;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsEnabled => Token is not null;

    public bool TokenFromConfiguration { get; } = configuredToken is not null;

    public string? BotUsername { get; internal set; }

    public void Send(long chatId, string language, Notification notification)
    {
        if (IsEnabled)
        {
            _queue.Writer.TryWrite(new OutgoingMessage(chatId, language, notification));
        }
    }

    public async Task<BotTokenCheck> CheckTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var me = await new TelegramBotClient(token).GetMe(cancellationToken);
            return new BotTokenCheck(BotTokenCheckResult.Valid, me.Username);
        }
        catch (ApiRequestException e) when (e.ErrorCode is 401 or 404)
        {
            return new BotTokenCheck(BotTokenCheckResult.Rejected);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new BotTokenCheck(BotTokenCheckResult.Unreachable);
        }
    }

    public void UseToken(string? token)
    {
        if (TokenFromConfiguration)
        {
            return;
        }

        TaskCompletionSource changed;
        lock (_lock)
        {
            if (_token == token)
            {
                return;
            }

            _token = token;
            BotUsername = null;
            changed = _changed;
            _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        changed.SetResult();
    }

    /// <summary>The token now, and a task that completes when it changes.</summary>
    internal (string? Token, Task Changed) Current()
    {
        lock (_lock)
        {
            return (_token, _changed.Task);
        }
    }

    private string? Token
    {
        get
        {
            lock (_lock)
            {
                return _token;
            }
        }
    }

    internal ChannelReader<OutgoingMessage> Outbox => _queue.Reader;
}

internal sealed record OutgoingMessage(long ChatId, string Language, Notification Notification);
