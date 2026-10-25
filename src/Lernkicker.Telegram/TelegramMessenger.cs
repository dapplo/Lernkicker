using System.Threading.Channels;
using Lernkicker.Application;
using Lernkicker.Application.Ports;

namespace Lernkicker.Telegram;

/// <summary>Queues messages; <see cref="TelegramBot"/> sends them, so a slow or failing Telegram never delays the app.</summary>
public sealed class TelegramMessenger(string? token) : IMessenger
{
    private readonly Channel<OutgoingMessage> _queue = Channel.CreateBounded<OutgoingMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public bool IsEnabled => token is not null;

    public string? BotUsername { get; internal set; }

    public void Send(long chatId, string language, Notification notification)
    {
        if (IsEnabled)
        {
            _queue.Writer.TryWrite(new OutgoingMessage(chatId, language, notification));
        }
    }

    internal ChannelReader<OutgoingMessage> Outbox => _queue.Reader;
}

internal sealed record OutgoingMessage(long ChatId, string Language, Notification Notification);
