using Lernkicker.Domain.Families;

namespace Lernkicker.Domain.Multiplayer;

/// <summary>The games children play with each other (not to confuse with the lesson games in Domain.Games).</summary>
public static class MultiplayerGames
{
    public const string Battleship = "battleship";

    public static readonly IReadOnlyList<IGameRules> All = [new Battleship()];

    public static IGameRules? Find(string? game) => All.FirstOrDefault(g => g.Game == game);
}

/// <summary>Whom a child may meet in the lobby, besides the own family.</summary>
public static class PlayReach
{
    /// <summary>Only brothers, sisters and the other profiles of the own family.</summary>
    public const string Family = "family";

    /// <summary>The own family plus the families and children a parent picked.</summary>
    public const string Selected = "selected";

    /// <summary>Every child on this server (whose parents allow it as well).</summary>
    public const string Everyone = "everyone";

    public static readonly IReadOnlyList<string> All = [Family, Selected, Everyone];

    public static bool IsValid(string? reach) => reach is not null && All.Contains(reach);
}

/// <summary>
/// What a parent unlocked for a child: which games, and with whom. Two children only meet when both sides allow
/// it (<see cref="CanPlayWith"/>), so one family can never open another family's child to strangers.
/// </summary>
public sealed class PlaySettings : IFamilyOwned
{
    /// <summary>Picked families plus picked children; a school class fits easily.</summary>
    public const int MaxPicks = 200;

    public Guid ChildId { get; private set; }
    public Guid FamilyId { get; private set; }

    public IReadOnlyList<string> Games { get; private set; } = [];
    public string Reach { get; private set; } = PlayReach.Family;
    public IReadOnlyList<Guid> Families { get; private set; } = [];
    public IReadOnlyList<Guid> Children { get; private set; } = [];

    public PlaySettings(Guid familyId, Guid childId)
    {
        FamilyId = familyId;
        ChildId = childId;
    }

    /// <summary>Nothing unlocked, for a child whose parents never opened the games.</summary>
    public static PlaySettings Locked(Guid familyId, Guid childId) => new(familyId, childId);

    public static bool IsValid(IReadOnlyCollection<string> games, string? reach, IReadOnlyCollection<Guid> families, IReadOnlyCollection<Guid> children) =>
        games.All(g => MultiplayerGames.Find(g) is not null) && PlayReach.IsValid(reach) && families.Count + children.Count <= MaxPicks;

    /// <summary>Picks only count for <see cref="PlayReach.Selected"/>; they are kept anyway so switching back restores them.</summary>
    public void Update(IEnumerable<string> games, string reach, IEnumerable<Guid> families, IEnumerable<Guid> children)
    {
        Games = games.Distinct().Order().ToList();
        Reach = reach;
        Families = families.Where(f => f != FamilyId).Distinct().ToList();
        Children = children.Distinct().ToList();
    }

    public bool Unlocked(string game) => Games.Contains(game);

    public bool AnyUnlocked => Games.Count > 0;

    /// <summary>One side of the check: does this child's parent allow meeting that child?</summary>
    public bool Allows(Guid familyId, Guid childId) =>
        familyId == FamilyId || Reach switch
        {
            PlayReach.Everyone => true,
            PlayReach.Selected => Families.Contains(familyId) || Children.Contains(childId),
            _ => false,
        };

    /// <summary>Both children have the game and both sides allow each other.</summary>
    public bool CanPlayWith(PlaySettings other, string game) =>
        other.ChildId != ChildId && Unlocked(game) && other.Unlocked(game) && Allows(other.FamilyId, other.ChildId) && other.Allows(FamilyId, ChildId);

    /// <summary>The games two children could play together.</summary>
    public IReadOnlyList<string> GamesWith(PlaySettings other) => Games.Where(g => CanPlayWith(other, g)).ToList();
}
