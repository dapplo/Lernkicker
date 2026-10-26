using System.Text.Json.Serialization;

namespace Lernkicker.Contracts;

// Playing with others: the parents' settings, the lobby and the games.

/// <summary>
/// What a parent unlocked for a child. <c>Reach</c>: "family", "selected" (plus the picked <c>Families</c> and
/// <c>Children</c>) or "everyone". Two children only meet when both sides allow it.
/// </summary>
public sealed record PlaySettingsDto(IReadOnlyList<string> Games, string Reach, IReadOnlyList<Guid> Families, IReadOnlyList<Guid> Children);

public sealed record PlayFamilyDto(Guid Id, string Name);

public sealed record PlayChildDto(Guid Id, string Name, string FamilyName);

/// <summary>Whom a parent can pick: other families, and children whose parents opened them to this child.</summary>
public sealed record PlayPartnersDto(IReadOnlyList<PlayFamilyDto> Families, IReadOnlyList<PlayChildDto> Children);

public sealed record SeatDto(int Seat, Guid ChildId, string Name, string FamilyName);

/// <summary>
/// A table or game. <c>Status</c>: "waiting" (an open table), "playing" or "finished". <c>MySeat</c> is null at
/// someone else's open table; <c>Abandoned</c>: a player was deleted, so the game cannot go on. With
/// <c>MinPlayers</c> at the table, the host may start before all <c>Seats</c> are taken. A finished game without a
/// <c>Winner</c> is a draw.
/// </summary>
public sealed record MatchOverview(
    Guid Id, string Game, string Status, int Seats, int MinPlayers, IReadOnlyList<SeatDto> Players, int? MySeat, bool MyTurn, int? Winner, bool Abandoned, DateTimeOffset UpdatedAt);

public sealed record LobbyPlayerDto(Guid ChildId, string Name, string FamilyName, IReadOnlyList<string> Games);

/// <summary><c>Games</c>: what the child may play; <c>Online</c>: who could play with the child and is around now.</summary>
public sealed record LobbyDto(IReadOnlyList<string> Games, IReadOnlyList<LobbyPlayerDto> Online, IReadOnlyList<MatchOverview> Tables, IReadOnlyList<MatchOverview> MyMatches);

/// <summary>Opens a table; <c>Seats</c> defaults to the fewest players the game needs.</summary>
public sealed record NewTable(string Game, int? Seats = null);

/// <summary>A game as one player sees it; <c>View</c> is null while the table waits; <c>Resigned</c>: the seat that gave up.</summary>
public sealed record MatchDto(MatchOverview Match, GameViewDto? View, int? Resigned);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BattleshipViewDto), "battleship")]
[JsonDerivedType(typeof(ConnectFourViewDto), "connectFour")]
[JsonDerivedType(typeof(CheckersViewDto), "checkers")]
[JsonDerivedType(typeof(MauMauViewDto), "mauMau")]
[JsonDerivedType(typeof(ChessViewDto), "chess")]
public abstract record GameViewDto;

/// <summary>A square on a board: column and row (row 0 is the top).</summary>
public sealed record CellDto(int X, int Y);

public sealed record ShipDto(int X, int Y, int Length, bool Horizontal);

public sealed record ShotDto(int X, int Y, bool Hit);

/// <summary>
/// Battleship for one player. <c>Phase</c>: "placing", "shooting" or "over"; <c>Turn</c> is the seat that shoots;
/// <c>MyShots</c> are on the other sea, <c>TheirShots</c> on mine; <c>SunkShips</c> are the enemy ships I sank (all
/// of them once the game is over).
/// </summary>
public sealed record BattleshipViewDto(
    string Phase, int Size, IReadOnlyList<int> Fleet, int Turn, IReadOnlyList<ShipDto>? MyShips, bool OpponentPlaced,
    IReadOnlyList<ShotDto> MyShots, IReadOnlyList<ShotDto> TheirShots, IReadOnlyList<ShipDto> SunkShips) : GameViewDto;

/// <summary>
/// Connect Four: <c>Cells[row][column]</c> is the seat of the disc or -1, row 0 is the top; <c>Line</c> are the
/// cells that won.
/// </summary>
public sealed record ConnectFourViewDto(
    IReadOnlyList<IReadOnlyList<int>> Cells, int Turn, IReadOnlyList<CellDto> Line, CellDto? Last, bool Over) : GameViewDto;

/// <summary>
/// Checkers: <c>Board[row]</c> has eight squares, '.' empty, 'w'/'b' a piece, 'W'/'B' a king; black starts at the
/// top. <c>Moves</c> are the paths the player may take now (start square first).
/// </summary>
public sealed record CheckersViewDto(
    IReadOnlyList<string> Board, bool IAmWhite, bool WhiteToMove, IReadOnlyList<IReadOnlyList<CellDto>> Moves,
    IReadOnlyList<CellDto> Last, bool Over) : GameViewDto;

/// <summary>What happened last in Mau-Mau. <c>Kind</c>: "play", "draw", "pass" or "resign".</summary>
public sealed record MauMauActionDto(int Seat, string Kind, string? Card, int Count, string? Suit);

/// <summary>
/// Mau-Mau for one player. Cards are suit (C, S, H, D) plus rank (7, 8, 9, T, J, Q, K, A), e.g. "HT". <c>Suit</c>
/// is the wished suit after a jack; <c>PendingDraw</c> what the sevens add up to; <c>Drawn</c> the card just
/// drawn, which may still be played.
/// </summary>
public sealed record MauMauViewDto(
    IReadOnlyList<string> Hand, IReadOnlyList<string> Playable, IReadOnlyList<int> HandSizes, string Top, string? Suit,
    int PendingDraw, int Turn, int DeckSize, bool CanPass, string? Drawn, IReadOnlyList<int> Out, MauMauActionDto? Last,
    bool Over) : GameViewDto;

public sealed record ChessMoveDto(CellDto From, CellDto To, string? Promotion);

/// <summary>
/// Chess: <c>Board[row]</c> has eight squares, row 0 is rank 8; "KQRBNP" white, "kqrbnp" black, '.' empty.
/// <c>Ending</c>: "checkmate", "stalemate", "repetition", "fiftyMoves" or "material".
/// </summary>
public sealed record ChessViewDto(
    IReadOnlyList<string> Board, bool IAmWhite, bool WhiteToMove, IReadOnlyList<ChessMoveDto> Moves, bool Check,
    ChessMoveDto? Last, string? Ending, bool Over) : GameViewDto;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BattleshipPlaceDto), "battleship.place")]
[JsonDerivedType(typeof(BattleshipShootDto), "battleship.shoot")]
[JsonDerivedType(typeof(ConnectFourDropDto), "connectFour.drop")]
[JsonDerivedType(typeof(CheckersMoveDto), "checkers.move")]
[JsonDerivedType(typeof(MauMauPlayDto), "mauMau.play")]
[JsonDerivedType(typeof(MauMauDrawDto), "mauMau.draw")]
[JsonDerivedType(typeof(MauMauPassDto), "mauMau.pass")]
[JsonDerivedType(typeof(ChessMoveRequestDto), "chess.move")]
public abstract record MoveDto;

public sealed record BattleshipPlaceDto(IReadOnlyList<ShipDto> Ships) : MoveDto;

public sealed record BattleshipShootDto(int X, int Y) : MoveDto;

public sealed record ConnectFourDropDto(int Column) : MoveDto;

public sealed record CheckersMoveDto(IReadOnlyList<CellDto> Path) : MoveDto;

/// <summary>A jack needs the wished <c>Suit</c>.</summary>
public sealed record MauMauPlayDto(string Card, string? Suit = null) : MoveDto;

public sealed record MauMauDrawDto : MoveDto;

public sealed record MauMauPassDto : MoveDto;

public sealed record ChessMoveRequestDto(CellDto From, CellDto To, string? Promotion = null) : MoveDto;

/// <summary>
/// The live connection of the play pages (SignalR). The server only says that something changed; the page then
/// loads it again over the API.
/// </summary>
public static class PlayEvents
{
    public const string Path = "/hubs/play";

    /// <summary>Someone came or went, or a table opened or closed. No arguments.</summary>
    public const string LobbyChanged = "lobby";

    /// <summary>A game of this child changed. Argument: the match id.</summary>
    public const string MatchChanged = "match";
}
