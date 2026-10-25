using System.Net;
using System.Net.Http.Json;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Lernkicker.Web.Tests;

/// <summary>Parents link a Telegram chat and get results and reminders; the bot is driven through the use cases.</summary>
public sealed class TelegramTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Parent_links_a_chat_with_a_one_time_link()
    {
        var parent = await factory.NewFamilyAsync("telegram-link@example.org");
        var chat = NewChat();

        var status = await LernkickerFactory.Json<TelegramStatusDto>(await parent.GetAsync("/api/account/telegram"));
        Assert.Equal((true, "LernkickerTestBot", null), (status.Enabled, status.BotUsername, status.LinkedAt));

        var code = await LinkCodeAsync(parent);
        var reply = await BotAsync(chat, $"/start {code}");

        Assert.Equal("en", reply!.Language);
        Assert.Equal("telegram-link@example.org", Assert.IsType<LinkedNotification>(reply.Notification).ParentName);
        Assert.NotNull((await LernkickerFactory.Json<TelegramStatusDto>(await parent.GetAsync("/api/account/telegram"))).LinkedAt);

        // The code works once
        var again = await BotAsync(NewChat(), $"/start {code}", "de");
        Assert.Equal("de", again!.Language);
        Assert.IsType<LinkInvalidNotification>(again.Notification);
    }

    [Fact]
    public async Task Unknown_chats_get_a_hint_how_to_connect()
    {
        var chat = NewChat();

        Assert.IsType<WelcomeNotification>((await BotAsync(chat, "/start"))!.Notification);
        Assert.IsType<LinkInvalidNotification>((await BotAsync(chat, "/start not-a-code"))!.Notification);
        Assert.IsType<WelcomeNotification>((await BotAsync(chat, "/next"))!.Notification);
        Assert.IsType<WelcomeNotification>((await BotAsync(chat, "hello"))!.Notification);
        Assert.IsType<LinkInvalidNotification>((await BotAsync(chat, new string('x', 43)))!.Notification);
    }

    [Theory]
    [InlineData("{0}")]
    [InlineData("  {0}\n")]
    [InlineData("https://t.me/LernkickerTestBot?start={0}")]
    public async Task Without_a_start_button_the_code_can_be_sent_as_a_message(string message)
    {
        var parent = await factory.NewFamilyAsync($"telegram-code-{message.Length}@example.org");
        var link = await LernkickerFactory.Json<TelegramLinkDto>(await parent.PostAsync("/api/account/telegram/link", null));
        Assert.EndsWith($"?start={link.Code}", link.Url);

        var reply = await BotAsync(NewChat(), string.Format(message, link.Code));

        Assert.IsType<LinkedNotification>(reply!.Notification);
        Assert.NotNull((await LernkickerFactory.Json<TelegramStatusDto>(await parent.GetAsync("/api/account/telegram"))).LinkedAt);
    }

    [Fact]
    public async Task Parents_get_results_and_dates_and_can_stop()
    {
        var parent = await factory.NewFamilyAsync("telegram-results@example.org");
        var chat = NewChat();
        await BotAsync(chat, $"/start {await LinkCodeAsync(parent)}");
        var child = await TestData.CreateChildAsync(parent);
        var quiz = await TestData.UploadHsuAsync(parent);
        var assignment = await TestData.AssignAsync(parent, child.Id, quiz.QuizId);
        await parent.PostAsJsonAsync($"/api/children/{child.Id}/calendar", new EventRequest("football", "Spiel", Today.AddDays(2), new TimeOnly(10, 0), null, "Zirndorf"));

        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);
        var sheet = Lernkicker.Tests.Examples.Hsu();
        await phone.PostAsJsonAsync($"/api/device/assignments/{assignment.Id}/submit", new Submission(TestData.AllCorrect(sheet)));

        var result = Assert.Single(factory.Messenger.SentTo<ResultNotification>(chat));
        Assert.Equal(("Calvin", quiz.Title, 39, 39, 1), (result.ChildName, result.QuizTitle, result.Points, result.MaxPoints, result.Mark));

        var next = Assert.IsType<DatesNotification>((await BotAsync(chat, "/next@LernkickerTestBot"))!.Notification);
        var dates = Assert.Single(next.Children);
        Assert.Equal(("Calvin", "Spiel"), (dates.ChildName, Assert.Single(dates.Entries).Title));

        Assert.IsType<HelpNotification>((await BotAsync(chat, "what?"))!.Notification);
        Assert.IsType<UnlinkedNotification>((await BotAsync(chat, "/stop"))!.Notification);
        // An unlinked chat only gets the hint how to connect
        Assert.IsType<WelcomeNotification>((await BotAsync(chat, "/next"))!.Notification);
        Assert.Null((await LernkickerFactory.Json<TelegramStatusDto>(await parent.GetAsync("/api/account/telegram"))).LinkedAt);
    }

    [Fact]
    public async Task Evening_reminder_lists_tomorrows_dates_of_the_own_family_only()
    {
        var a = await factory.NewFamilyAsync("reminder-a@example.org");
        var b = await factory.NewFamilyAsync("reminder-b@example.org");
        var chatA = NewChat();
        var chatB = NewChat();
        await BotAsync(chatA, $"/start {await LinkCodeAsync(a)}");
        await BotAsync(chatB, $"/start {await LinkCodeAsync(b)}");
        var childA = await TestData.CreateChildAsync(a, "Calvin");
        var childB = await TestData.CreateChildAsync(b, "Mia", "4321");
        await a.PostAsJsonAsync($"/api/children/{childA.Id}/calendar", new EventRequest("test", "Probe HSU", Today.AddDays(1), null, null, null));
        await a.PostAsJsonAsync($"/api/children/{childA.Id}/calendar", new EventRequest("test", "Not tomorrow", Today.AddDays(3), null, null, null));

        using (var scope = factory.Services.CreateScope())
        {
            var families = await scope.ServiceProvider.GetRequiredService<TelegramUseCases>().FamiliesForRemindersAsync();
            foreach (var familyId in families)
            {
                using var familyScope = factory.Services.CreateScope();
                await familyScope.ServiceProvider.GetRequiredService<TelegramUseCases>().SendRemindersAsync(familyId);
            }
        }

        var reminder = Assert.Single(factory.Messenger.SentTo<DatesNotification>(chatA));
        Assert.Equal(DatesNotification.Period.Tomorrow, reminder.When);
        Assert.Equal(["Probe HSU"], Assert.Single(reminder.Children).Entries.Select(e => e.Title));
        // Family B has no dates tomorrow: no message at all
        Assert.Empty(factory.Messenger.SentTo<DatesNotification>(chatB));
        Assert.NotEqual(childA.Id, childB.Id);
    }

    [Fact]
    public async Task Linking_a_chat_again_moves_it_and_unlinking_works_from_the_page()
    {
        var a = await factory.NewFamilyAsync("relink-a@example.org");
        var b = await factory.NewFamilyAsync("relink-b@example.org");
        var chat = NewChat();
        await BotAsync(chat, $"/start {await LinkCodeAsync(a)}");

        // The same Telegram account links to another parent: the first link goes
        await BotAsync(chat, $"/start {await LinkCodeAsync(b)}");
        Assert.Null((await LernkickerFactory.Json<TelegramStatusDto>(await a.GetAsync("/api/account/telegram"))).LinkedAt);
        Assert.NotNull((await LernkickerFactory.Json<TelegramStatusDto>(await b.GetAsync("/api/account/telegram"))).LinkedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await b.DeleteAsync("/api/account/telegram")).StatusCode);
        // An unlinked chat only gets the hint how to connect
        Assert.IsType<WelcomeNotification>((await BotAsync(chat, "/next"))!.Notification);
    }

    [Fact]
    public async Task Without_a_bot_nothing_can_be_linked()
    {
        using var noBot = new LernkickerFactory { TelegramEnabled = false };
        var parent = await noBot.NewFamilyAsync("no-bot@example.org");

        var status = await LernkickerFactory.Json<TelegramStatusDto>(await parent.GetAsync("/api/account/telegram"));
        var link = await parent.PostAsync("/api/account/telegram/link", null);

        Assert.False(status.Enabled);
        Assert.Equal(ErrorCodes.TelegramDisabled, (await LernkickerFactory.Error(link, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(HttpStatusCode.Unauthorized, (await noBot.Client().GetAsync("/api/account/telegram")).StatusCode);
    }

    [Fact]
    public async Task Before_the_bot_reached_telegram_linking_waits()
    {
        using var starting = new LernkickerFactory();
        starting.Messenger.Username = null;
        var parent = await starting.NewFamilyAsync("starting@example.org");

        var status = await LernkickerFactory.Json<TelegramStatusDto>(await parent.GetAsync("/api/account/telegram"));
        var link = await parent.PostAsync("/api/account/telegram/link", null);

        Assert.True(status.Enabled);
        Assert.Equal(ErrorCodes.TelegramUnavailable, (await LernkickerFactory.Error(link, HttpStatusCode.BadRequest)).Code);
    }

    private static long NewChat() => Random.Shared.NextInt64(1, long.MaxValue);

    private static async Task<string> LinkCodeAsync(HttpClient parent)
    {
        var link = await LernkickerFactory.Json<TelegramLinkDto>(await parent.PostAsync("/api/account/telegram/link", null));
        Assert.StartsWith("https://t.me/LernkickerTestBot?start=", link.Url);
        return link.Url.Split("start=")[1];
    }

    private async Task<BotReply?> BotAsync(long chatId, string text, string language = "de")
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TelegramUseCases>().HandleAsync(chatId, text, language);
    }
}
