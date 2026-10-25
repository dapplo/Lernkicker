using System.Text.RegularExpressions;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Messaging;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Application.UseCases;

/// <summary>
/// Telegram for parents: link a chat with a one-time code, answer the bot's commands, and send results and
/// the evening reminder. The bot only talks to linked chats; an unknown chat can send a link code and otherwise gets a hint how to connect.
/// </summary>
public sealed partial class TelegramUseCases(
    ITelegramRepository telegram,
    IMessenger messenger,
    IParentAccounts parents,
    IChildRepository children,
    CalendarUseCases calendar,
    IFamilyScope familyScope,
    ILocalClock clock,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    /// <summary>How many days /next shows.</summary>
    public const int NextDays = 7;

    public async Task<TelegramStatus> StatusAsync(Guid parentId) =>
        new(messenger.IsEnabled, messenger.BotUsername, (await telegram.FindByParentAsync(parentId))?.LinkedAt);

    /// <summary>A new one-time link to the bot; older codes of the parent stop working.</summary>
    public async Task<Result<TelegramLinkCreated>> CreateLinkAsync(Guid familyId, Guid parentId)
    {
        if (!messenger.IsEnabled)
        {
            return AppError.Invalid(ErrorCodes.TelegramDisabled);
        }

        // The bot learns its name from Telegram on start; until then there is no link to give
        if (messenger.BotUsername is not { } bot)
        {
            return AppError.Invalid(ErrorCodes.TelegramUnavailable);
        }

        var code = Secrets.NewToken();
        var expiresAt = time.GetUtcNow() + TelegramLinkCode.Lifetime;
        await telegram.RemoveCodesAsync(parentId);
        telegram.Add(new TelegramLinkCode(familyId, parentId, Secrets.Hash(code), expiresAt));
        await unitOfWork.SaveChangesAsync();
        return new TelegramLinkCreated($"https://t.me/{bot}?start={code}", code, expiresAt);
    }

    public async Task<Result> UnlinkAsync(Guid parentId)
    {
        if (await telegram.FindByParentAsync(parentId) is { } link)
        {
            telegram.Remove(link);
            await unitOfWork.SaveChangesAsync();
        }

        return Result.Ok();
    }

    /// <summary>
    /// A text message to the bot. Returns the answer, or null to stay silent. <paramref name="language"/> is
    /// the Telegram user's language, used until the chat is linked to a parent.
    /// </summary>
    public async Task<BotReply?> HandleAsync(long chatId, string text, string language)
    {
        var (command, argument) = Command(text);
        if (command == "/start" && argument.Length > 0)
        {
            return await LinkAsync(chatId, argument, language);
        }

        var link = await telegram.FindByChatAnyFamilyAsync(chatId);
        if (link is null)
        {
            // In a chat that already exists Telegram shows no Start button, so the code (or the whole link) can be sent as a message
            return LinkCode(text) is { } code
                ? await LinkAsync(chatId, code, language)
                : new BotReply(language, new WelcomeNotification());
        }

        familyScope.Enter(link.FamilyId);
        if (await parents.FindAsync(link.ParentId) is not { } parent)
        {
            return null;
        }

        switch (command)
        {
            case "/stop":
                telegram.Remove(link);
                await unitOfWork.SaveChangesAsync();
                return new BotReply(parent.Language, new UnlinkedNotification());
            case "/next" or "/termine":
                var today = clock.Today;
                return new BotReply(parent.Language, new DatesNotification(DatesNotification.Period.NextDays, await DatesAsync(today, today.AddDays(NextDays - 1))));
            default:
                return new BotReply(parent.Language, new HelpNotification());
        }
    }

    /// <summary>A child handed in a quiz: tell every linked parent of the family.</summary>
    public async Task ResultAsync(string childName, string quizTitle, QuizResult result)
    {
        if (!messenger.IsEnabled)
        {
            return;
        }

        await SendToFamilyAsync(new ResultNotification(childName, quizTitle, result.Points, result.MaxPoints, result.Percent, result.Mark));
    }

    public async Task<IReadOnlyList<Guid>> FamiliesForRemindersAsync() =>
        messenger.IsEnabled ? await telegram.FamiliesWithLinksAnyFamilyAsync() : [];

    /// <summary>The evening reminder: tomorrow's dates of all children, when there are any.</summary>
    public async Task SendRemindersAsync(Guid familyId)
    {
        familyScope.Enter(familyId);
        var tomorrow = clock.Today.AddDays(1);
        var dates = await DatesAsync(tomorrow, tomorrow);
        if (dates.Count > 0)
        {
            await SendToFamilyAsync(new DatesNotification(DatesNotification.Period.Tomorrow, dates));
        }
    }

    private async Task<BotReply> LinkAsync(long chatId, string code, string language)
    {
        var stored = await telegram.FindCodeAnyFamilyAsync(Secrets.Hash(code));
        if (stored is null || !stored.IsValidAt(time.GetUtcNow()))
        {
            return new BotReply(language, new LinkInvalidNotification());
        }

        familyScope.Enter(stored.FamilyId);
        var parent = await parents.FindAsync(stored.ParentId);
        if (parent is null)
        {
            return new BotReply(language, new LinkInvalidNotification());
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync();
        await telegram.RemoveByChatAnyFamilyAsync(chatId);
        if (await telegram.FindByParentAsync(parent.Id) is { } previous)
        {
            telegram.Remove(previous);
        }

        telegram.Remove(stored);
        telegram.Add(new TelegramLink(stored.FamilyId, parent.Id, chatId, time.GetUtcNow()));
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return new BotReply(parent.Language, new LinkedNotification(parent.DisplayName));
    }

    private async Task SendToFamilyAsync(Notification notification)
    {
        foreach (var link in await telegram.ListAsync())
        {
            if (await parents.FindAsync(link.ParentId) is { } parent)
            {
                messenger.Send(link.ChatId, parent.Language, notification);
            }
        }
    }

    private async Task<IReadOnlyList<ChildDates>> DatesAsync(DateOnly from, DateOnly to)
    {
        var dates = new List<ChildDates>();
        foreach (var child in await children.ListSummariesAsync())
        {
            if (await calendar.ListAsync(child.Id, from, to) is { IsSuccess: true, Value.Count: > 0 } entries)
            {
                dates.Add(new ChildDates(child.Name, entries.Value));
            }
        }

        return dates;
    }

    /// <summary>The code of a link, sent on its own or as the whole link; null for any other text.</summary>
    private static string? LinkCode(string text)
    {
        var match = LinkCodePattern().Match(text.Trim());
        return match.Success ? match.Groups["code"].Value : null;
    }

    [GeneratedRegex(@"^(?:https://t\.me/\w+\?start=)?(?<code>[A-Za-z0-9_-]{43})$")]
    private static partial Regex LinkCodePattern();

    /// <summary>"/start abc" → ("/start", "abc"); "/next@LernkickerBot" → ("/next", "").</summary>
    private static (string Command, string Argument) Command(string text)
    {
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].StartsWith('/'))
        {
            return ("", "");
        }

        var command = parts[0].Split('@')[0].ToLowerInvariant();
        return (command, parts.Length > 1 ? parts[1].Trim() : "");
    }
}
