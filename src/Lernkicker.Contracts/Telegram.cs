namespace Lernkicker.Contracts;

/// <summary><c>Enabled</c>: the server has a bot. <c>LinkedAt</c>: when the parent connected a chat, else null.</summary>
public sealed record TelegramStatusDto(bool Enabled, string? BotUsername, DateTimeOffset? LinkedAt);

/// <summary>Opens the bot with a one-time code, valid until <c>ExpiresAt</c>; <c>Code</c> can also be sent to the bot as a message.</summary>
public sealed record TelegramLinkDto(string Url, string Code, DateTimeOffset ExpiresAt);

/// <summary>The bot as the admin sees it; the token itself is never returned.</summary>
public sealed record TelegramBotDto(bool Configured, bool FromFile, string? BotUsername, DateTimeOffset? SetAt);

public sealed record SetBotToken(string Token);
