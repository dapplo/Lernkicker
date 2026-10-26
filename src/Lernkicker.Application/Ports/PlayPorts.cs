using Lernkicker.Domain.Multiplayer;

namespace Lernkicker.Application.Ports;

/// <summary>
/// Play settings and matches. Matches cross families by design (children of different families play together), so
/// they are not under the family filter; the use cases decide who sees what. Methods ending in AnyFamily read
/// across families on purpose.
/// </summary>
public interface IPlayRepository
{
    /// <summary>The settings of a child of the logged-in family.</summary>
    Task<PlaySettings?> FindSettingsAsync(Guid childId);

    Task<PlaySettings?> FindSettingsAnyFamilyAsync(Guid childId);

    /// <summary>The settings of every child that has at least one game unlocked.</summary>
    Task<IReadOnlyList<PlaySettings>> ListUnlockedAnyFamilyAsync();

    void Add(PlaySettings settings);

    void Add(Match match);

    void Remove(Match match);

    Task<Match?> FindMatchAsync(Guid id);

    Task<IReadOnlyList<Match>> ListOpenTablesAsync();

    /// <summary>The child's open tables and running games, and the games that ended since <paramref name="finishedSince"/>.</summary>
    Task<IReadOnlyList<Match>> ListMatchesOfAsync(Guid childId, DateTimeOffset finishedSince);

    /// <summary>Names to show for players of any family.</summary>
    Task<IReadOnlyList<PlayerInfo>> PlayersAnyFamilyAsync(IReadOnlyCollection<Guid> childIds);

    /// <summary>The other families on this server, for the parents' picker.</summary>
    Task<IReadOnlyList<FamilyName>> OtherFamiliesAsync(Guid familyId);
}

/// <summary>Which children have the play page open right now (kept by the web adapter's live connection).</summary>
public interface IPlayPresence
{
    IReadOnlyCollection<Guid> Online { get; }
}

/// <summary>Tells open play pages that something changed, so they load it again. Fire and forget.</summary>
public interface IPlayNotifier
{
    void LobbyChanged();

    void MatchChanged(Guid matchId, IReadOnlyCollection<Guid> childIds);
}

public sealed record PlayerInfo(Guid ChildId, Guid FamilyId, string Name, string FamilyName);

public sealed record FamilyName(Guid Id, string Name);
