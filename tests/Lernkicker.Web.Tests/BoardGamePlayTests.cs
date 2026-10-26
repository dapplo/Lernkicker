using System.Net;
using System.Net.Http.Json;
using Lernkicker.Application;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>The games after battleship, played over the API: tables for more players, draws and each game's moves.</summary>
public sealed class BoardGamePlayTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    [Fact]
    public async Task Three_siblings_play_mau_mau_after_the_host_starts_early()
    {
        var parent = await factory.NewFamilyAsync("maumau@example.org");
        var children = new[] { await TestData.CreateChildAsync(parent), await TestData.CreateChildAsync(parent, "Lena"), await TestData.CreateChildAsync(parent, "Max") };
        var phones = new List<HttpClient>();
        foreach (var child in children)
        {
            await UnlockAsync(parent, child.Id, "mauMau");
            phones.Add(await TestData.ChildPhoneAsync(factory, parent, child.Id));
        }

        var table = await LernkickerFactory.Json<MatchDto>(await phones[0].PostAsJsonAsync("/api/play/tables", new NewTable("mauMau")));
        Assert.Equal((5, 2), (table.Match.Seats, table.Match.MinPlayers));
        var id = table.Match.Id;

        // Alone at the table the host cannot start yet; a guest cannot start at all
        Assert.Equal(ErrorCodes.TableClosed, (await LernkickerFactory.Error(await phones[0].PostAsync($"/api/play/tables/{id}/start", null), HttpStatusCode.BadRequest)).Code);
        var joined = await LernkickerFactory.Json<MatchDto>(await phones[1].PostAsync($"/api/play/tables/{id}/join", null));
        Assert.Equal("waiting", joined.Match.Status);
        await LernkickerFactory.Json<MatchDto>(await phones[2].PostAsync($"/api/play/tables/{id}/join", null));
        Assert.Equal(ErrorCodes.TableClosed, (await LernkickerFactory.Error(await phones[1].PostAsync($"/api/play/tables/{id}/start", null), HttpStatusCode.BadRequest)).Code);

        var started = await LernkickerFactory.Json<MatchDto>(await phones[0].PostAsync($"/api/play/tables/{id}/start", null));
        Assert.Equal(("playing", 3), (started.Match.Status, started.Match.Seats));
        var first = (MauMauViewDto)started.View!;
        Assert.Equal([5, 5, 5], first.HandSizes);

        // Everyone plays the first card that fits, or draws and passes; giving up ends it if that takes too long
        for (var moves = 0; moves < 300; moves++)
        {
            var any = await LernkickerFactory.Json<MatchDto>(await phones[0].GetAsync($"/api/play/matches/{id}"));
            if (any.Match.Status == "finished")
            {
                break;
            }

            var turn = ((MauMauViewDto)any.View!).Turn;
            var view = (MauMauViewDto)(await LernkickerFactory.Json<MatchDto>(await phones[turn].GetAsync($"/api/play/matches/{id}"))).View!;
            MoveDto move = view.Playable.Count > 0
                ? new MauMauPlayDto(view.Playable[0], view.Playable[0][1..] == "J" ? "H" : null)
                : view.CanPass ? new MauMauPassDto() : new MauMauDrawDto();
            await LernkickerFactory.Json<MatchDto>(await phones[turn].PostAsJsonAsync($"/api/play/matches/{id}/moves", move));
        }

        var state = await LernkickerFactory.Json<MatchDto>(await phones[0].GetAsync($"/api/play/matches/{id}"));
        if (state.Match.Status != "finished")
        {
            await LernkickerFactory.Json<MatchDto>(await phones[1].PostAsync($"/api/play/matches/{id}/resign", null));
            state = await LernkickerFactory.Json<MatchDto>(await phones[2].PostAsync($"/api/play/matches/{id}/resign", null));
            Assert.Equal(0, state.Match.Winner);
        }

        Assert.Equal("finished", state.Match.Status);
        Assert.NotNull(state.Match.Winner);
    }

    [Fact]
    public async Task Four_in_a_row_wins_connect_four()
    {
        var (phones, id, view) = await StartAsync<ConnectFourViewDto>("connect-four@example.org", "connectFour");
        var starter = view.Turn;
        MatchDto state = null!;
        for (var i = 0; i < 7; i++)
        {
            var seat = i % 2 == 0 ? starter : 1 - starter;
            state = await LernkickerFactory.Json<MatchDto>(await phones[seat].PostAsJsonAsync<MoveDto>(
                $"/api/play/matches/{id}/moves", new ConnectFourDropDto(seat == starter ? 0 : 1)));
        }

        var end = (ConnectFourViewDto)state.View!;
        Assert.Equal(("finished", starter), (state.Match.Status, state.Match.Winner));
        Assert.Equal(4, end.Line.Count);
    }

    [Fact]
    public async Task Checkers_offers_the_allowed_moves_and_refuses_others()
    {
        var (phones, id, view) = await StartAsync<CheckersViewDto>("checkers@example.org", "checkers");
        var white = view.IAmWhite ? 0 : 1;
        var mine = (CheckersViewDto)(await LernkickerFactory.Json<MatchDto>(await phones[white].GetAsync($"/api/play/matches/{id}"))).View!;
        Assert.Equal(7, mine.Moves.Count);

        var backwards = new CheckersMoveDto([new CellDto(0, 5), new CellDto(1, 6)]);
        Assert.Equal(ErrorCodes.MoveInvalid, (await LernkickerFactory.Error(
            await phones[white].PostAsJsonAsync<MoveDto>($"/api/play/matches/{id}/moves", backwards), HttpStatusCode.BadRequest)).Code);
        var moved = await LernkickerFactory.Json<MatchDto>(await phones[white].PostAsJsonAsync<MoveDto>(
            $"/api/play/matches/{id}/moves", new CheckersMoveDto(mine.Moves[0])));
        Assert.False(moved.Match.MyTurn);
        Assert.Equal(mine.Moves[0], ((CheckersViewDto)moved.View!).Last);
    }

    [Fact]
    public async Task Fools_mate_ends_chess_in_four_moves()
    {
        var (phones, id, view) = await StartAsync<ChessViewDto>("chess@example.org", "chess");
        var white = phones[view.IAmWhite ? 0 : 1];
        var black = phones[view.IAmWhite ? 1 : 0];

        Assert.Equal(ErrorCodes.ChessMoveInvalid, (await LernkickerFactory.Error(
            await white.PostAsJsonAsync<MoveDto>($"/api/play/matches/{id}/moves", new ChessMoveRequestDto(new(4, 6), new(4, 3))), HttpStatusCode.BadRequest)).Code);
        foreach (var (phone, from, to) in new[]
        {
            (white, new CellDto(5, 6), new CellDto(5, 5)),
            (black, new CellDto(4, 1), new CellDto(4, 3)),
            (white, new CellDto(6, 6), new CellDto(6, 4)),
            (black, new CellDto(3, 0), new CellDto(7, 4)),
        })
        {
            await LernkickerFactory.Json<MatchDto>(await phone.PostAsJsonAsync<MoveDto>($"/api/play/matches/{id}/moves", new ChessMoveRequestDto(from, to)));
        }

        var end = await LernkickerFactory.Json<MatchDto>(await white.GetAsync($"/api/play/matches/{id}"));
        var board = (ChessViewDto)end.View!;
        Assert.Equal(("finished", "checkmate", true), (end.Match.Status, board.Ending, board.Check));
        Assert.Equal(view.IAmWhite ? 1 : 0, end.Match.Winner);
    }

    /// <summary>Two siblings at a table for two: the second one joining starts the game. The view is seat 0's.</summary>
    private async Task<(HttpClient[] Phones, Guid Id, T View)> StartAsync<T>(string email, string game) where T : GameViewDto
    {
        var parent = await factory.NewFamilyAsync(email);
        var calvin = await TestData.CreateChildAsync(parent);
        var lena = await TestData.CreateChildAsync(parent, "Lena");
        await UnlockAsync(parent, calvin.Id, game);
        await UnlockAsync(parent, lena.Id, game);
        var phones = new[] { await TestData.ChildPhoneAsync(factory, parent, calvin.Id), await TestData.ChildPhoneAsync(factory, parent, lena.Id) };

        var table = await LernkickerFactory.Json<MatchDto>(await phones[0].PostAsJsonAsync("/api/play/tables", new NewTable(game)));
        Assert.Equal(2, table.Match.Seats);
        var started = await LernkickerFactory.Json<MatchDto>(await phones[1].PostAsync($"/api/play/tables/{table.Match.Id}/join", null));
        Assert.Equal("playing", started.Match.Status);
        var view = (await LernkickerFactory.Json<MatchDto>(await phones[0].GetAsync($"/api/play/matches/{table.Match.Id}"))).View;
        return (phones, table.Match.Id, Assert.IsType<T>(view));
    }

    private static async Task UnlockAsync(HttpClient parent, Guid childId, string game) =>
        await LernkickerFactory.Json<PlaySettingsDto>(await parent.PutAsJsonAsync($"/api/children/{childId}/play", new PlaySettingsDto([game], "family", [], [])));
}
