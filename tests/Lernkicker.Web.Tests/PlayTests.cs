using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Lernkicker.Application;
using Lernkicker.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.SignalR.Client;

namespace Lernkicker.Web.Tests;

/// <summary>Children of one or several families playing with each other, as far as their parents allow it.</summary>
public sealed class PlayTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    private const string Battleship = "battleship";

    [Fact]
    public async Task Parents_unlock_games_and_partners_for_their_own_children_only()
    {
        var a = await factory.NewFamilyAsync("play-settings-a@example.org");
        var b = await factory.NewFamilyAsync("play-settings-b@example.org");
        var calvin = await TestData.CreateChildAsync(a);

        var settings = await LernkickerFactory.Json<PlaySettingsDto>(await a.GetAsync($"/api/children/{calvin.Id}/play"));
        Assert.Empty(settings.Games);
        Assert.Equal("family", settings.Reach);

        var invalid = await a.PutAsJsonAsync($"/api/children/{calvin.Id}/play", new PlaySettingsDto(["go"], "family", [], []));
        Assert.Equal(ErrorCodes.PlaySettingsInvalid, (await LernkickerFactory.Error(invalid, HttpStatusCode.BadRequest)).Code);

        var saved = await LernkickerFactory.Json<PlaySettingsDto>(
            await a.PutAsJsonAsync($"/api/children/{calvin.Id}/play", new PlaySettingsDto([Battleship], "everyone", [], [])));
        Assert.Equal([Battleship], saved.Games);
        Assert.Equal("everyone", saved.Reach);

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/children/{calvin.Id}/play")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/children/{calvin.Id}/play", new PlaySettingsDto([], "family", [], []))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/children/{calvin.Id}/play/partners")).StatusCode);
    }

    [Fact]
    public async Task Parents_can_pick_only_children_that_are_open_to_their_child()
    {
        var a = await factory.NewFamilyAsync("partners-a@example.org");
        var b = await factory.NewFamilyAsync("partners-b@example.org");
        var c = await factory.NewFamilyAsync("partners-c@example.org");
        var calvin = await TestData.CreateChildAsync(a);
        var lena = await TestData.CreateChildAsync(b, "Lena");
        var max = await TestData.CreateChildAsync(c, "Max");
        var familyA = (await LernkickerFactory.Json<PlayPartnersDto>(await b.GetAsync($"/api/children/{lena.Id}/play/partners"))).Families
            .Single(f => f.Name == "Family partners-a@example.org").Id;

        await UnlockAsync(b, lena.Id, "selected", families: [familyA]);
        await UnlockAsync(c, max.Id, "family");

        var partners = await LernkickerFactory.Json<PlayPartnersDto>(await a.GetAsync($"/api/children/{calvin.Id}/play/partners"));
        Assert.Contains(partners.Children, p => p.Id == lena.Id && p.Name == "Lena" && p.FamilyName == "Family partners-b@example.org");
        Assert.DoesNotContain(partners.Children, p => p.Id == max.Id);
        Assert.Contains(partners.Families, f => f.Name == "Family partners-c@example.org");
        Assert.DoesNotContain(partners.Families, f => f.Id == familyA);
    }

    [Fact]
    public async Task Two_families_meet_in_the_lobby_and_play_battleship_to_the_end()
    {
        var a = await factory.NewFamilyAsync("game-a@example.org");
        var b = await factory.NewFamilyAsync("game-b@example.org");
        var c = await factory.NewFamilyAsync("game-c@example.org");
        var calvin = await TestData.CreateChildAsync(a);
        var lena = await TestData.CreateChildAsync(b, "Lena");
        var max = await TestData.CreateChildAsync(c, "Max");
        await UnlockAsync(a, calvin.Id, "everyone");
        await UnlockAsync(b, lena.Id, "selected", children: [calvin.Id]);
        await UnlockAsync(c, max.Id, "everyone");
        var calvinPhone = await TestData.ChildPhoneAsync(factory, a, calvin.Id);
        var lenaPhone = await TestData.ChildPhoneAsync(factory, b, lena.Id);
        var maxPhone = await TestData.ChildPhoneAsync(factory, c, max.Id);

        var table = await LernkickerFactory.Json<MatchDto>(await lenaPhone.PostAsJsonAsync("/api/play/tables", new NewTable(Battleship)));
        Assert.Equal(("waiting", 0, null), (table.Match.Status, table.Match.MySeat, table.View));
        var id = table.Match.Id;

        // Lena only opened up to Calvin: Max neither sees nor joins her table
        Assert.Contains((await LobbyAsync(calvinPhone)).Tables, t => t.Id == id && t.Players.Single().Name == "Lena");
        Assert.DoesNotContain((await LobbyAsync(maxPhone)).Tables, t => t.Id == id);
        Assert.Equal(HttpStatusCode.NotFound, (await maxPhone.PostAsync($"/api/play/tables/{id}/join", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await maxPhone.GetAsync($"/api/play/matches/{id}")).StatusCode);

        var joined = await LernkickerFactory.Json<MatchDto>(await calvinPhone.PostAsync($"/api/play/tables/{id}/join", null));
        Assert.Equal(("playing", 1, true), (joined.Match.Status, joined.Match.MySeat, joined.Match.MyTurn));
        Assert.Equal(["Lena", "Calvin"], joined.Match.Players.Select(p => p.Name));
        Assert.DoesNotContain((await LobbyAsync(calvinPhone)).Tables, t => t.Id == id);
        Assert.Equal(ErrorCodes.TableClosed,
            (await LernkickerFactory.Error(await calvinPhone.DeleteAsync($"/api/play/tables/{id}"), HttpStatusCode.BadRequest)).Code);

        var bad = await calvinPhone.PostAsJsonAsync<MoveDto>($"/api/play/matches/{id}/moves", new BattleshipPlaceDto([new ShipDto(0, 0, 5, true)]));
        Assert.Equal(ErrorCodes.FleetInvalid, (await LernkickerFactory.Error(bad, HttpStatusCode.BadRequest)).Code);
        foreach (var phone in new[] { lenaPhone, calvinPhone })
        {
            var fleet = await LernkickerFactory.Json<List<ShipDto>>(await phone.GetAsync("/api/play/battleship/random-fleet"));
            await LernkickerFactory.Json<MatchDto>(await phone.PostAsJsonAsync<MoveDto>($"/api/play/matches/{id}/moves", new BattleshipPlaceDto(fleet)));
        }

        // Each takes their turn, cell by cell, until one fleet is gone
        var phones = new[] { lenaPhone, calvinPhone };
        var shots = new[] { 0, 0 };
        MatchDto state;
        while (true)
        {
            state = await LernkickerFactory.Json<MatchDto>(await lenaPhone.GetAsync($"/api/play/matches/{id}"));
            if (state.Match.Status == "finished")
            {
                break;
            }

            var view = (BattleshipViewDto)state.View!;
            Assert.Equal("shooting", view.Phase);
            var other = 1 - view.Turn;
            Assert.Equal(ErrorCodes.NotYourTurn, (await LernkickerFactory.Error(
                await phones[other].PostAsJsonAsync<MoveDto>($"/api/play/matches/{id}/moves", new BattleshipShootDto(0, 0)), HttpStatusCode.BadRequest)).Code);

            var cell = shots[view.Turn]++;
            await LernkickerFactory.Json<MatchDto>(
                await phones[view.Turn].PostAsJsonAsync<MoveDto>($"/api/play/matches/{id}/moves", new BattleshipShootDto(cell % 10, cell / 10)));
        }

        var end = (BattleshipViewDto)state.View!;
        Assert.Equal("over", end.Phase);
        Assert.Equal(5, end.SunkShips.Count);
        Assert.NotNull(state.Match.Winner);
        Assert.Contains((await LobbyAsync(calvinPhone)).MyMatches, m => m.Id == id && m.Status == "finished");
        Assert.Equal(ErrorCodes.MatchOver, (await LernkickerFactory.Error(
            await calvinPhone.PostAsync($"/api/play/matches/{id}/resign", null), HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Children_play_only_what_their_parents_unlocked()
    {
        var a = await factory.NewFamilyAsync("locked-a@example.org");
        var calvin = await TestData.CreateChildAsync(a);
        var robin = await TestData.CreateChildAsync(a, "Robin");
        var calvinPhone = await TestData.ChildPhoneAsync(factory, a, calvin.Id);
        var robinPhone = await TestData.ChildPhoneAsync(factory, a, robin.Id);

        Assert.Empty((await LobbyAsync(calvinPhone)).Games);
        var refused = await calvinPhone.PostAsJsonAsync("/api/play/tables", new NewTable(Battleship));
        Assert.Equal(ErrorCodes.PlayNotUnlocked, (await LernkickerFactory.Error(refused, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync("/api/play/lobby")).StatusCode);

        // Within the family it is enough to unlock the game: e.g. a parent's own profile against the child
        await UnlockAsync(a, calvin.Id, "family");
        await UnlockAsync(a, robin.Id, "family");
        var table = await LernkickerFactory.Json<MatchDto>(await robinPhone.PostAsJsonAsync("/api/play/tables", new NewTable(Battleship)));
        var game = await LernkickerFactory.Json<MatchDto>(await calvinPhone.PostAsync($"/api/play/tables/{table.Match.Id}/join", null));
        Assert.Equal("playing", game.Match.Status);

        // Locking the game again stops the running game, but giving up still works
        await UnlockAsync(a, calvin.Id, "family", games: []);
        var fleet = await LernkickerFactory.Json<List<ShipDto>>(await calvinPhone.GetAsync("/api/play/battleship/random-fleet"));
        Assert.Equal(ErrorCodes.PlayNotUnlocked, (await LernkickerFactory.Error(
            await calvinPhone.PostAsJsonAsync<MoveDto>($"/api/play/matches/{table.Match.Id}/moves", new BattleshipPlaceDto(fleet)), HttpStatusCode.BadRequest)).Code);
        var resigned = await LernkickerFactory.Json<MatchDto>(await calvinPhone.PostAsync($"/api/play/matches/{table.Match.Id}/resign", null));
        Assert.Equal((0, 1), (resigned.Match.Winner, resigned.Resigned));
    }

    [Fact]
    public async Task The_live_connection_shows_who_is_online_and_tells_about_changes()
    {
        var a = await factory.NewFamilyAsync("live-a@example.org");
        var calvin = await TestData.CreateChildAsync(a);
        var lena = await TestData.CreateChildAsync(a, "Lena");
        await UnlockAsync(a, calvin.Id, "family");
        await UnlockAsync(a, lena.Id, "family");
        var calvinCookies = new System.Net.CookieContainer();
        var calvinPhone = await PhoneWithCookiesAsync(a, calvin.Id, calvinCookies);
        var lenaPhone = await TestData.ChildPhoneAsync(factory, a, lena.Id);

        Assert.DoesNotContain((await LobbyAsync(lenaPhone)).Online, p => p.ChildId == calvin.Id);

        var events = Channel.CreateUnbounded<string>();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(HubUrl, o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => new CookieContainerHandler(calvinCookies) { InnerHandler = factory.Server.CreateHandler() };
            })
            .Build();
        connection.On(PlayEvents.LobbyChanged, () => events.Writer.TryWrite("lobby"));
        connection.On<Guid>(PlayEvents.MatchChanged, id => events.Writer.TryWrite($"match {id}"));
        await connection.StartAsync();

        var online = (await LobbyAsync(lenaPhone)).Online.Single(p => p.ChildId == calvin.Id);
        Assert.Equal("Calvin", online.Name);
        Assert.Equal([Battleship], online.Games);

        var table = await LernkickerFactory.Json<MatchDto>(await calvinPhone.PostAsJsonAsync("/api/play/tables", new NewTable(Battleship)));
        await LernkickerFactory.Json<MatchDto>(await lenaPhone.PostAsync($"/api/play/tables/{table.Match.Id}/join", null));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (await events.Reader.ReadAsync(timeout.Token) != $"match {table.Match.Id}")
        {
        }

        await connection.StopAsync();
        Assert.DoesNotContain((await LobbyAsync(lenaPhone)).Online, p => p.ChildId == calvin.Id);

        // Without a child's login there is no connection
        var anonymous = new HubConnectionBuilder()
            .WithUrl(HubUrl, o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => anonymous.StartAsync());
    }

    // The cookies are secure, so the connection goes to https like the browser's
    private static readonly Uri HubUrl = new("https://localhost" + PlayEvents.Path);

    private static async Task UnlockAsync(HttpClient parent, Guid childId, string reach, Guid[]? families = null, Guid[]? children = null, string[]? games = null) =>
        await LernkickerFactory.Json<PlaySettingsDto>(await parent.PutAsJsonAsync(
            $"/api/children/{childId}/play", new PlaySettingsDto(games ?? [Battleship], reach, families ?? [], children ?? [])));

    private static async Task<LobbyDto> LobbyAsync(HttpClient phone) =>
        await LernkickerFactory.Json<LobbyDto>(await phone.GetAsync("/api/play/lobby"));

    /// <summary>Like <see cref="TestData.ChildPhoneAsync"/>, with cookies the live connection can use as well.</summary>
    private async Task<HttpClient> PhoneWithCookiesAsync(HttpClient parent, Guid childId, System.Net.CookieContainer cookies)
    {
        var pairing = await LernkickerFactory.Json<PairingCodeResponse>(await parent.PostAsync($"/api/children/{childId}/pairing", null));
        var phone = factory.CreateDefaultClient(new Uri("https://localhost"), new CookieContainerHandler(cookies));
        await LernkickerFactory.Json<DeviceStatusResponse>(await phone.PostAsJsonAsync("/api/device/pair", new PairDevice(pairing.Code, "Test phone")));
        Assert.Equal(HttpStatusCode.NoContent, (await phone.PostAsJsonAsync("/api/device/login", new PinLogin("1234"))).StatusCode);
        return phone;
    }
}
