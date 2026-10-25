using System.Text.RegularExpressions;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Settings;

namespace Lernkicker.Application.UseCases;

/// <summary>
/// The admin sets the bot token in the app instead of a file: it is checked with Telegram, stored encrypted
/// and the bot restarts with it. A token from the configuration (file) always wins.
/// </summary>
public sealed partial class TelegramBotUseCases(IServerSecrets secrets, ISecretProtector protector, IMessenger messenger, IUnitOfWork unitOfWork, TimeProvider time)
{
    public async Task<TelegramBotSettings> SettingsAsync()
    {
        if (messenger.TokenFromConfiguration)
        {
            return new TelegramBotSettings(true, true, messenger.BotUsername, null);
        }

        var stored = await secrets.FindAsync(ServerSecret.TelegramBotToken);
        return new TelegramBotSettings(stored is not null, false, stored is null ? null : messenger.BotUsername, stored?.UpdatedAt);
    }

    public async Task<Result> SetTokenAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (messenger.TokenFromConfiguration)
        {
            return AppError.Invalid(ErrorCodes.TelegramTokenFromFile);
        }

        token = token?.Trim();
        if (token is null || !TokenFormat().IsMatch(token))
        {
            return AppError.Invalid(ErrorCodes.TelegramTokenInvalid);
        }

        switch ((await messenger.CheckTokenAsync(token, cancellationToken)).Result)
        {
            case BotTokenCheckResult.Rejected:
                return AppError.Invalid(ErrorCodes.TelegramTokenRejected);
            case BotTokenCheckResult.Unreachable:
                return AppError.Invalid(ErrorCodes.TelegramUnreachable);
        }

        var protectedValue = protector.Protect(token);
        if (await secrets.FindAsync(ServerSecret.TelegramBotToken) is { } stored)
        {
            stored.Change(protectedValue, time.GetUtcNow());
        }
        else
        {
            secrets.Add(new ServerSecret(ServerSecret.TelegramBotToken, protectedValue, time.GetUtcNow()));
        }

        await unitOfWork.SaveChangesAsync();
        messenger.UseToken(token);
        return Result.Ok();
    }

    /// <summary>Stops the bot. Links stay, so connecting the same bot again later needs no new linking.</summary>
    public async Task<Result> RemoveTokenAsync()
    {
        if (messenger.TokenFromConfiguration)
        {
            return AppError.Invalid(ErrorCodes.TelegramTokenFromFile);
        }

        if (await secrets.FindAsync(ServerSecret.TelegramBotToken) is { } stored)
        {
            secrets.Remove(stored);
            await unitOfWork.SaveChangesAsync();
        }

        messenger.UseToken(null);
        return Result.Ok();
    }

    /// <summary>The stored token for the bot to start with, or null.</summary>
    public async Task<string?> StoredTokenAsync() =>
        await secrets.FindAsync(ServerSecret.TelegramBotToken) is { } stored ? protector.Unprotect(stored.ProtectedValue) : null;

    // "<bot id>:<secret>" as BotFather hands it out
    [GeneratedRegex(@"^\d{5,15}:[A-Za-z0-9_-]{30,50}$")]
    private static partial Regex TokenFormat();
}
