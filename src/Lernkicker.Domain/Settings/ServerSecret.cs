namespace Lernkicker.Domain.Settings;

/// <summary>
/// A secret of the whole installation that the admin sets in the app, such as the Telegram bot token. It is
/// stored encrypted; <see cref="ProtectedValue"/> is never the plain value.
/// </summary>
public sealed class ServerSecret
{
    public const string TelegramBotToken = "telegram.botToken";

    public string Name { get; private set; }
    public string ProtectedValue { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public ServerSecret(string name, string protectedValue, DateTimeOffset updatedAt)
    {
        Name = name;
        ProtectedValue = protectedValue;
        UpdatedAt = updatedAt;
    }

    public void Change(string protectedValue, DateTimeOffset at)
    {
        ProtectedValue = protectedValue;
        UpdatedAt = at;
    }
}
