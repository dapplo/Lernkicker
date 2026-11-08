using Lernkicker.Client.Resources;
using Lernkicker.Contracts;
using Microsoft.Extensions.Localization;

namespace Lernkicker.Client.Services;

/// <summary>How the games for several players look in the lists (names are <c>Play.Game.{game}</c>).</summary>
public static class PlayGames
{
    /// <summary>The games for several players the server knows (MultiplayerGames on the server).</summary>
    public static readonly IReadOnlyList<string> All = ["battleship", "connectFour", "checkers", "mauMau", "chess", "numberTowers"];

    public static string Icon(string game) => game switch
    {
        "battleship" => "🚢",
        "connectFour" => "🔴",
        "checkers" => "⚪",
        "mauMau" => "🃏",
        "chess" => "♞",
        "numberTowers" => "🗼",
        _ => "🎲",
    };

    /// <summary>How many can play (IGameRules.MinPlayers and MaxPlayers on the server), for the rules.</summary>
    public static (int Min, int Max) Seats(string game) => game switch
    {
        "mauMau" => (2, 5),
        "numberTowers" => (2, 4),
        _ => (2, 2),
    };

    public static string Name(SeatDto player) => player.Name;

    /// <summary>With the family, for children of other families (two may be called Max).</summary>
    public static string NameWithFamily(SeatDto player) => $"{player.Name} ({player.FamilyName})";

    /// <summary>One line about where a game stands, from the child's side.</summary>
    public static string Status(IStringLocalizer<Strings> l, MatchOverview match) => match switch
    {
        { Status: "waiting" } => l["Play.WaitingForPlayers"],
        { Abandoned: true } => l["Play.Abandoned"],
        { Status: "finished", Winner: { } winner } when winner == match.MySeat => l["Play.YouWon"],
        { Status: "finished", Winner: { } winner } => l["Play.Won", match.Players.FirstOrDefault(p => p.Seat == winner)?.Name ?? "?"],
        { Status: "finished" } => l["Play.Draw"],
        { MyTurn: true } => l["Play.YourTurn"],
        _ => l["Play.TheirTurn"],
    };
}
