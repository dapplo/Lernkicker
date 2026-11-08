using Lernkicker.Client.Resources;
using Lernkicker.Contracts;
using Microsoft.Extensions.Localization;

namespace Lernkicker.Client.Services;

/// <summary>How the games for several players look in the lists (names are <c>Play.Game.{game}</c>).</summary>
public static class PlayGames
{
    /// <summary>The games for several players the server knows (MultiplayerGames on the server).</summary>
    public static readonly IReadOnlyList<string> All = ["battleship", "connectFour", "checkers", "mauMau", "chess", "moles", "balloons", "reflex"];

    /// <summary>The games that can be played alone as well (everyone plays the same round, the points count).</summary>
    public static bool CanPlayAlone(string game) => Lernkicker.Client.Reaction.ReactionRounds.Games.Contains(game);

    public static string Icon(string game) => game switch
    {
        "battleship" => "🚢",
        "connectFour" => "🔴",
        "checkers" => "⚪",
        "mauMau" => "🃏",
        "chess" => "♞",
        "moles" => "🔨",
        "balloons" => "🎈",
        "reflex" => "⚡",
        _ => "🎲",
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
