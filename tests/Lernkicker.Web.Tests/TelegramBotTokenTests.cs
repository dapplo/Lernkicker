using System.Net;
using System.Net.Http.Json;
using Lernkicker.Application;
using Lernkicker.Contracts;
using Lernkicker.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lernkicker.Web.Tests;

/// <summary>The admin sets the bot token in the app: checked with Telegram, stored encrypted, never sent back.</summary>
public sealed class TelegramBotTokenTests
{
    private const string Token = "1234567890:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsaw";

    [Fact]
    public async Task Admin_sets_and_removes_the_token()
    {
        using var server = NoTokenServer();
        server.Messenger.KnownTokens[Token] = "LernkickerKromBot";
        var admin = await server.AdminAsync();

        Assert.Equal(new TelegramBotDto(false, false, null, null), await SettingsAsync(admin));
        Assert.False((await StatusAsync(admin)).Enabled);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/admin/telegram", new SetBotToken($"  {Token}\n"))).StatusCode);
        var settings = await SettingsAsync(admin);
        Assert.True(settings.Configured);
        Assert.Equal("LernkickerKromBot", settings.BotUsername);
        Assert.NotNull(settings.SetAt);
        Assert.Equal(Token, server.Messenger.Token);
        Assert.True((await StatusAsync(admin)).Enabled);

        // Encrypted in the database, and the API never returns it
        using (var scope = server.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<LernkickerDbContext>().ServerSecrets.SingleAsync();
            Assert.DoesNotContain(Token, stored.ProtectedValue);
        }

        Assert.DoesNotContain(Token, await (await admin.GetAsync("/api/admin/telegram")).Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/admin/telegram")).StatusCode);
        Assert.Equal(new TelegramBotDto(false, false, null, null), await SettingsAsync(admin));
        Assert.Null(server.Messenger.Token);
        Assert.False((await StatusAsync(admin)).Enabled);
    }

    [Theory]
    [InlineData("not a token", ErrorCodes.TelegramTokenInvalid)]
    [InlineData("9876543210:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsaw", ErrorCodes.TelegramTokenRejected)]
    public async Task Wrong_tokens_are_refused(string token, string code)
    {
        using var server = NoTokenServer();
        server.Messenger.KnownTokens[Token] = "LernkickerKromBot";
        var admin = await server.AdminAsync();

        var response = await admin.PutAsJsonAsync("/api/admin/telegram", new SetBotToken(token));

        Assert.Equal(code, (await LernkickerFactory.Error(response, HttpStatusCode.BadRequest)).Code);
        Assert.False((await SettingsAsync(admin)).Configured);
        Assert.Null(server.Messenger.Token);
    }

    [Fact]
    public async Task A_token_is_not_saved_while_telegram_cannot_be_reached()
    {
        using var server = NoTokenServer();
        server.Messenger.Reachable = false;
        var admin = await server.AdminAsync();

        var response = await admin.PutAsJsonAsync("/api/admin/telegram", new SetBotToken(Token));

        Assert.Equal(ErrorCodes.TelegramUnreachable, (await LernkickerFactory.Error(response, HttpStatusCode.BadRequest)).Code);
        Assert.False((await SettingsAsync(admin)).Configured);
    }

    [Fact]
    public async Task A_token_from_the_file_cannot_be_changed_in_the_app()
    {
        using var server = new LernkickerFactory();
        var admin = await server.AdminAsync();

        var settings = await SettingsAsync(admin);
        var put = await admin.PutAsJsonAsync("/api/admin/telegram", new SetBotToken(Token));
        var delete = await admin.DeleteAsync("/api/admin/telegram");

        Assert.Equal(new TelegramBotDto(true, true, "LernkickerTestBot", null), settings);
        Assert.Equal(ErrorCodes.TelegramTokenFromFile, (await LernkickerFactory.Error(put, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(ErrorCodes.TelegramTokenFromFile, (await LernkickerFactory.Error(delete, HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Only_the_admin_sets_the_token()
    {
        using var server = NoTokenServer();
        var parent = await server.NewFamilyAsync("bot-token-parent@example.org");

        Assert.Equal(HttpStatusCode.Forbidden, (await parent.PutAsJsonAsync("/api/admin/telegram", new SetBotToken(Token))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync("/api/admin/telegram")).StatusCode);
    }

    private static LernkickerFactory NoTokenServer()
    {
        var server = new LernkickerFactory { TelegramEnabled = false };
        server.Messenger.TokenFromConfiguration = false;
        return server;
    }

    private static async Task<TelegramBotDto> SettingsAsync(HttpClient admin) =>
        await LernkickerFactory.Json<TelegramBotDto>(await admin.GetAsync("/api/admin/telegram"));

    private static async Task<TelegramStatusDto> StatusAsync(HttpClient parent) =>
        await LernkickerFactory.Json<TelegramStatusDto>(await parent.GetAsync("/api/account/telegram"));
}
