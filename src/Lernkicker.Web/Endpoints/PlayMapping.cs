using Lernkicker.Application;
using Lernkicker.Contracts;
using Lernkicker.Domain.Multiplayer;

namespace Lernkicker.Web.Endpoints;

/// <summary>Maps the play models and the games' views and moves to the HTTP contracts and back.</summary>
public static class PlayMapping
{
    public static PlaySettingsDto ToDto(this PlaySettingsSummary s) => new(s.Games, s.Reach, s.Families, s.Children);

    public static PlayPartnersDto ToDto(this PlayPartners p) =>
        new(p.Families.Select(f => new PlayFamilyDto(f.Id, f.Name)).ToList(),
            p.Children.Select(c => new PlayChildDto(c.ChildId, c.Name, c.FamilyName)).ToList());

    public static LobbyDto ToDto(this Lobby l) =>
        new(l.Games,
            l.Online.Select(p => new LobbyPlayerDto(p.ChildId, p.Name, p.FamilyName, p.Games)).ToList(),
            l.Tables.Select(ToDto).ToList(),
            l.MyMatches.Select(ToDto).ToList());

    public static MatchOverview ToDto(this MatchSummary m) =>
        new(m.Id, m.Game, m.Status, m.Seats, m.MinPlayers, m.Players.Select(p => new SeatDto(p.Seat, p.ChildId, p.Name, p.FamilyName)).ToList(),
            m.MySeat, m.MyTurn, m.Winner, m.Abandoned, m.UpdatedAt);

    public static MatchDto ToDto(this MatchState s) => new(s.Match.ToDto(), s.View?.ToDto(), s.Resigned);

    public static GameViewDto ToDto(this GameView view) => view switch
    {
        BattleshipView b => new BattleshipViewDto(
            b.Phase, b.Size, b.Fleet, b.Turn, b.MyShips?.Select(ToDto).ToList(), b.OpponentPlaced,
            b.MyShots.Select(ToDto).ToList(), b.TheirShots.Select(ToDto).ToList(), b.SunkShips.Select(ToDto).ToList()),
        ConnectFourView c => new ConnectFourViewDto(c.Cells, c.Turn, c.Line.Select(ToDto).ToList(), c.Last?.ToDto(), c.Over),
        CheckersView c => new CheckersViewDto(
            c.Board, c.IAmWhite, c.WhiteToMove, c.Moves.Select(m => (IReadOnlyList<CellDto>)m.Select(ToDto).ToList()).ToList(),
            c.Last.Select(ToDto).ToList(), c.Over),
        MauMauView m => new MauMauViewDto(
            m.Hand, m.Playable, m.HandSizes, m.Top, m.Suit, m.PendingDraw, m.Turn, m.DeckSize, m.CanPass, m.Drawn, m.Out,
            m.Last is { } a ? new MauMauActionDto(a.Seat, a.Kind, a.Card, a.Count, a.Suit) : null, m.Over),
        ChessView c => new ChessViewDto(
            c.Board, c.IAmWhite, c.WhiteToMove, c.Moves.Select(ToDto).ToList(), c.Check, c.Last?.ToDto(), c.Ending, c.Over),
        _ => throw new NotSupportedException(view.GetType().Name),
    };

    public static ShipDto ToDto(this Ship s) => new(s.X, s.Y, s.Length, s.Horizontal);

    private static ShotDto ToDto(this Shot s) => new(s.X, s.Y, s.Hit);

    private static CellDto ToDto(this Cell c) => new(c.X, c.Y);

    private static ChessMoveDto ToDto(this ChessMove m) => new(m.From.ToDto(), m.To.ToDto(), m.Promotion);

    private static Cell ToDomain(this CellDto c) => new(c.X, c.Y);

    /// <summary>Null for a move the server does not know or that lacks its parts.</summary>
    public static GameMove? ToDomain(this MoveDto? move) => move switch
    {
        BattleshipPlaceDto { Ships: { } ships } => new BattleshipPlacement(ships.Select(s => new Ship(s.X, s.Y, s.Length, s.Horizontal)).ToList()),
        BattleshipShootDto shot => new BattleshipShot(shot.X, shot.Y),
        ConnectFourDropDto drop => new ConnectFourDrop(drop.Column),
        CheckersMoveDto { Path: { } path } when path.All(c => c is not null) => new CheckersMove(path.Select(ToDomain).ToList()),
        MauMauPlayDto { Card: { } card } play => new MauMauPlay(card, play.Suit),
        MauMauDrawDto => new MauMauDraw(),
        MauMauPassDto => new MauMauPass(),
        ChessMoveRequestDto { From: { } from, To: { } to } chess => new ChessMove(from.ToDomain(), to.ToDomain(), chess.Promotion),
        _ => null,
    };
}
