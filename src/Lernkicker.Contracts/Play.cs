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
/// someone else's open table; <c>Abandoned</c>: a player was deleted, so the game cannot go on.
/// </summary>
public sealed record MatchOverview(
    Guid Id, string Game, string Status, int Seats, IReadOnlyList<SeatDto> Players, int? MySeat, bool MyTurn, int? Winner, bool Abandoned, DateTimeOffset UpdatedAt);

public sealed record LobbyPlayerDto(Guid ChildId, string Name, string FamilyName, IReadOnlyList<string> Games);

/// <summary><c>Games</c>: what the child may play; <c>Online</c>: who could play with the child and is around now.</summary>
public sealed record LobbyDto(IReadOnlyList<string> Games, IReadOnlyList<LobbyPlayerDto> Online, IReadOnlyList<MatchOverview> Tables, IReadOnlyList<MatchOverview> MyMatches);

/// <summary>Opens a table; <c>Seats</c> defaults to the fewest players the game needs.</summary>
public sealed record NewTable(string Game, int? Seats = null);

/// <summary>A game as one player sees it; <c>View</c> is null while the table waits; <c>Resigned</c>: the seat that gave up.</summary>
public sealed record MatchDto(MatchOverview Match, GameViewDto? View, int? Resigned);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BattleshipViewDto), "battleship")]
public abstract record GameViewDto;

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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BattleshipPlaceDto), "battleship.place")]
[JsonDerivedType(typeof(BattleshipShootDto), "battleship.shoot")]
public abstract record MoveDto;

public sealed record BattleshipPlaceDto(IReadOnlyList<ShipDto> Ships) : MoveDto;

public sealed record BattleshipShootDto(int X, int Y) : MoveDto;

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
