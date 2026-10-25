namespace Lernkicker.Contracts;

/// <summary><c>Enabled</c>: the server has a bot. <c>LinkedAt</c>: when the parent connected a chat, else null.</summary>
public sealed record TelegramStatusDto(bool Enabled, string? BotUsername, DateTimeOffset? LinkedAt);

/// <summary>Opens the bot with a one-time code, valid until <c>ExpiresAt</c>.</summary>
public sealed record TelegramLinkDto(string Url, DateTimeOffset ExpiresAt);
