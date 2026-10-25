using Lernkicker.Domain.Messaging;
using Lernkicker.Domain.Settings;

namespace Lernkicker.Application.Ports;

/// <summary>
/// Sends messages to a chat (Telegram). The adapter turns each <see cref="Notification"/> into text in the
/// given UI language, so the use cases stay free of user-facing text. Sending is fire and forget: a failure
/// must never break the action that caused the message.
/// </summary>
public interface IMessenger
{
    /// <summary>False when no bot is configured; then nothing can be linked or sent.</summary>
    bool IsEnabled { get; }

    /// <summary>The bot's username (for t.me links), once known.</summary>
    string? BotUsername { get; }

    void Send(long chatId, string language, Notification notification);

    /// <summary>True when the token comes from the configuration (a file); then it cannot be changed in the app.</summary>
    bool TokenFromConfiguration { get; }

    /// <summary>Asks Telegram whether <paramref name="token"/> belongs to a bot.</summary>
    Task<BotTokenCheck> CheckTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>(Re)starts the bot with this token, or stops it with null. Ignored while <see cref="TokenFromConfiguration"/>.</summary>
    void UseToken(string? token);
}

public enum BotTokenCheckResult { Valid, Rejected, Unreachable }

/// <summary>The answer of Telegram to a token, with the bot's username when it is valid.</summary>
public sealed record BotTokenCheck(BotTokenCheckResult Result, string? Username = null);

/// <summary>Encrypts secrets before they are stored (ASP.NET Core Data Protection in the web adapter).</summary>
public interface ISecretProtector
{
    string Protect(string value);

    /// <summary>The plain value, or null when it cannot be decrypted any more (e.g. the keys were lost).</summary>
    string? Unprotect(string protectedValue);
}

/// <summary>Settings of the whole installation, not of a family.</summary>
public interface IServerSettings
{
    Task<ServerSetting?> FindAsync(string name);
    void Add(ServerSetting setting);
}

/// <summary>Secrets of the whole installation, not of a family.</summary>
public interface IServerSecrets
{
    Task<ServerSecret?> FindAsync(string name);
    void Add(ServerSecret secret);
    void Remove(ServerSecret secret);
}

/// <summary>The Telegram links of the logged-in family, plus the lookups the bot needs before it knows the family.</summary>
public interface ITelegramRepository
{
    void Add(TelegramLink link);
    void Remove(TelegramLink link);
    Task<TelegramLink?> FindByParentAsync(Guid parentId);
    Task<IReadOnlyList<TelegramLink>> ListAsync();

    void Add(TelegramLinkCode code);
    void Remove(TelegramLinkCode code);
    Task RemoveCodesAsync(Guid parentId);

    /// <summary>Removes the link of a chat in any family (a chat is linked to one parent at most).</summary>
    Task RemoveByChatAnyFamilyAsync(long chatId);

    /// <summary>A code of any family; the bot gets it from a chat that is not linked yet.</summary>
    Task<TelegramLinkCode?> FindCodeAnyFamilyAsync(string codeHash);

    /// <summary>The link of a chat in any family; the bot only knows the chat id.</summary>
    Task<TelegramLink?> FindByChatAnyFamilyAsync(long chatId);

    /// <summary>The families that have at least one link, for the daily reminders.</summary>
    Task<IReadOnlyList<Guid>> FamiliesWithLinksAnyFamilyAsync();
}
