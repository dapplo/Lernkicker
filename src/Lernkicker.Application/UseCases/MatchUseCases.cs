using Lernkicker.Application.Ports;
using Lernkicker.Domain.Multiplayer;

namespace Lernkicker.Application.UseCases;

/// <summary>
/// Children playing with each other: the lobby, open tables, moves. Every check runs here on the server: whether the
/// game is unlocked, whether both sides allow each other, whose turn it is and whether the move follows the rules.
/// </summary>
public sealed class MatchUseCases(IPlayRepository play, IPlayPresence presence, IPlayNotifier notifier, IUnitOfWork unitOfWork, TimeProvider time)
{
    /// <summary>Finished games stay in the child's list this long.</summary>
    public static readonly TimeSpan FinishedListed = TimeSpan.FromDays(7);

    // Two players may move at the same moment (both place their ships): changes to matches run one at a time.
    // Lernkicker runs as one server process, so a lock in memory is enough.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<Lobby> LobbyAsync(Guid childId)
    {
        var me = await SettingsAsync(childId);
        var others = (await play.ListUnlockedAnyFamilyAsync()).Where(s => s.ChildId != childId).ToDictionary(s => s.ChildId);

        var online = presence.Online
            .Select(id => others.GetValueOrDefault(id))
            .OfType<PlaySettings>()
            .Select(s => (s.ChildId, Games: me.GamesWith(s)))
            .Where(o => o.Games.Count > 0)
            .ToList();

        var tables = (await play.ListOpenTablesAsync())
            .Where(t => t.SeatOf(childId) is null && t.Players.Count > 0 && t.Players.All(p => others.TryGetValue(p.ChildId, out var s) && me.CanPlayWith(s, t.Game)))
            .ToList();
        var mine = await play.ListMatchesOfAsync(childId, time.GetUtcNow() - FinishedListed);

        var names = await NamesAsync(online.Select(o => o.ChildId).Concat(tables.Concat(mine).SelectMany(m => m.Players).Select(p => p.ChildId)));
        return new Lobby(
            me.Games,
            online.Where(o => names.ContainsKey(o.ChildId))
                .Select(o => new LobbyPlayer(o.ChildId, names[o.ChildId].Name, names[o.ChildId].FamilyName, o.Games))
                .OrderBy(p => p.Name).ToList(),
            tables.OrderBy(t => t.CreatedAt).Select(t => Summary(t, childId, names)).ToList(),
            mine.Select(m => Summary(m, childId, names))
                .OrderBy(m => m.Status == MatchStatus.Finished).ThenByDescending(m => m.MyTurn).ThenByDescending(m => m.UpdatedAt).ToList());
    }

    /// <summary>
    /// Opens a table in the lobby; <paramref name="seats"/> defaults to the most players the game takes. The game
    /// starts when every seat is taken, or earlier when the host says so (<see cref="StartAsync"/>).
    /// </summary>
    public async Task<Result<MatchState>> OpenTableAsync(Guid childId, string game, int? seats)
    {
        var rules = MultiplayerGames.Find(game);
        if (rules is null || seats is { } n && (n < rules.MinPlayers || n > rules.MaxPlayers))
        {
            return AppError.Invalid(ErrorCodes.PlayGameUnknown);
        }

        var me = await SettingsAsync(childId);
        if (!me.Unlocked(game))
        {
            return AppError.Invalid(ErrorCodes.PlayNotUnlocked);
        }

        await Gate.WaitAsync();
        try
        {
            var mine = await play.ListMatchesOfAsync(childId, DateTimeOffset.MaxValue);
            if (mine.Count(m => m.Status == MatchStatus.Waiting && m.Host.ChildId == childId) >= Match.MaxOpenTables)
            {
                return AppError.Invalid(ErrorCodes.TooManyTables);
            }

            var match = Match.Open(rules, seats ?? rules.MaxPlayers, childId, me.FamilyId, time.GetUtcNow());
            play.Add(match);
            await unitOfWork.SaveChangesAsync();
            notifier.LobbyChanged();
            return await StateAsync(match, childId);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<Result<MatchState>> JoinAsync(Guid childId, Guid matchId, Random random)
    {
        var me = await SettingsAsync(childId);
        await Gate.WaitAsync();
        try
        {
            var match = await play.FindMatchAsync(matchId);
            if (match is null || match.SeatOf(childId) is not null)
            {
                return AppError.NotFound(ErrorCodes.MatchNotFound);
            }

            if (!me.Unlocked(match.Game))
            {
                return AppError.Invalid(ErrorCodes.PlayNotUnlocked);
            }

            foreach (var player in match.Players)
            {
                if (await play.FindSettingsAnyFamilyAsync(player.ChildId) is not { } other || !me.CanPlayWith(other, match.Game))
                {
                    // Looks the same as a table that does not exist: nobody learns about children they may not meet
                    return AppError.NotFound(ErrorCodes.MatchNotFound);
                }
            }

            if (match.Status != MatchStatus.Waiting || match.IsFull || match.Players.Count == 0)
            {
                return AppError.Invalid(ErrorCodes.TableClosed);
            }

            var running = await play.ListMatchesOfAsync(childId, DateTimeOffset.MaxValue);
            if (running.Count(m => m.Status == MatchStatus.Playing) >= Match.MaxRunning)
            {
                return AppError.Invalid(ErrorCodes.TooManyMatches);
            }

            match.Join(MultiplayerGames.Find(match.Game)!, childId, me.FamilyId, random, time.GetUtcNow());
            await unitOfWork.SaveChangesAsync();
            Changed(match);
            return await StateAsync(match, childId);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>The host starts a game for more players with whoever sits at the table already.</summary>
    public async Task<Result<MatchState>> StartAsync(Guid childId, Guid matchId, Random random)
    {
        await Gate.WaitAsync();
        try
        {
            var match = await play.FindMatchAsync(matchId);
            if (match?.SeatOf(childId) is null)
            {
                return AppError.NotFound(ErrorCodes.MatchNotFound);
            }

            var rules = MultiplayerGames.Find(match.Game)!;
            if (match.Host.ChildId != childId || !match.CanStartEarly(rules))
            {
                return AppError.Invalid(ErrorCodes.TableClosed);
            }

            match.StartEarly(rules, random, time.GetUtcNow());
            await unitOfWork.SaveChangesAsync();
            Changed(match);
            return await StateAsync(match, childId);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>The host closes the table, another player leaves it; only before the game starts.</summary>
    public async Task<Result> LeaveTableAsync(Guid childId, Guid matchId)
    {
        await Gate.WaitAsync();
        try
        {
            var match = await play.FindMatchAsync(matchId);
            if (match?.SeatOf(childId) is null)
            {
                return AppError.NotFound(ErrorCodes.MatchNotFound);
            }

            if (match.Status != MatchStatus.Waiting)
            {
                return AppError.Invalid(ErrorCodes.TableClosed);
            }

            var players = match.Players.Select(p => p.ChildId).ToList();
            if (!match.Leave(childId, time.GetUtcNow()))
            {
                play.Remove(match);
            }

            await unitOfWork.SaveChangesAsync();
            notifier.LobbyChanged();
            notifier.MatchChanged(match.Id, players);
            return Result.Ok();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<Result<MatchState>> MatchAsync(Guid childId, Guid matchId)
    {
        var match = await play.FindMatchAsync(matchId);
        return match?.SeatOf(childId) is null ? AppError.NotFound(ErrorCodes.MatchNotFound) : await StateAsync(match, childId);
    }

    public Task<Result<MatchState>> MoveAsync(Guid childId, Guid matchId, GameMove move) =>
        ChangeAsync(childId, matchId, (rules, state, seat) => rules.Play(state, seat, move), resigning: false);

    public Task<Result<MatchState>> ResignAsync(Guid childId, Guid matchId) =>
        ChangeAsync(childId, matchId, (rules, state, seat) => rules.Resign(state, seat), resigning: true);

    /// <summary>A random battleship fleet that follows the rules, for the "mix" button.</summary>
    public static IReadOnlyList<Ship> RandomFleet(Random random) => Battleship.RandomFleet(random);

    private async Task<Result<MatchState>> ChangeAsync(Guid childId, Guid matchId, Func<IGameRules, string, int, GameStep> change, bool resigning)
    {
        var me = await SettingsAsync(childId);
        await Gate.WaitAsync();
        try
        {
            var match = await play.FindMatchAsync(matchId);
            if (match?.SeatOf(childId) is not { } seat)
            {
                return AppError.NotFound(ErrorCodes.MatchNotFound);
            }

            // A parent who locks the game again stops the child's running games too (giving up still works)
            if (!resigning && !me.Unlocked(match.Game))
            {
                return AppError.Invalid(ErrorCodes.PlayNotUnlocked);
            }

            if (match.Status != MatchStatus.Playing || match.State is null || match.IsAbandoned)
            {
                return AppError.Invalid(ErrorCodes.MatchOver);
            }

            var step = change(MultiplayerGames.Find(match.Game)!, match.State, seat);
            if (!step.IsValid)
            {
                return AppError.Invalid(step.Error!);
            }

            match.Apply(step, time.GetUtcNow(), resigning ? seat : null);
            await unitOfWork.SaveChangesAsync();
            Changed(match);
            return await StateAsync(match, childId);
        }
        finally
        {
            Gate.Release();
        }
    }

    private void Changed(Match match)
    {
        notifier.MatchChanged(match.Id, match.Players.Select(p => p.ChildId).ToList());
        notifier.LobbyChanged();
    }

    private async Task<PlaySettings> SettingsAsync(Guid childId) =>
        await play.FindSettingsAsync(childId) ?? PlaySettings.Locked(Guid.Empty, childId);

    private async Task<MatchState> StateAsync(Match match, Guid childId)
    {
        var names = await NamesAsync(match.Players.Select(p => p.ChildId));
        var seat = match.SeatOf(childId)!.Value;
        var view = match.State is null ? null : MultiplayerGames.Find(match.Game)!.View(match.State, seat);
        return new MatchState(Summary(match, childId, names), view, match.Resigned);
    }

    private async Task<Dictionary<Guid, PlayerInfo>> NamesAsync(IEnumerable<Guid> childIds) =>
        (await play.PlayersAnyFamilyAsync(childIds.Distinct().ToList())).ToDictionary(p => p.ChildId);

    private static MatchSummary Summary(Match m, Guid childId, IReadOnlyDictionary<Guid, PlayerInfo> names)
    {
        var rules = MultiplayerGames.Find(m.Game)!;
        var seat = m.SeatOf(childId);
        return new MatchSummary(
            m.Id, m.Game, m.Status, m.Seats, rules.MinPlayers,
            m.Players.OrderBy(p => p.Seat)
                .Select(p => names.TryGetValue(p.ChildId, out var n) ? new SeatInfo(p.Seat, p.ChildId, n.Name, n.FamilyName) : null)
                .OfType<SeatInfo>().ToList(),
            seat,
            seat is { } s && m.ToMove(rules).Contains(s),
            m.Winner, m.IsAbandoned, m.UpdatedAt);
    }
}
