using System.Collections.Concurrent;
using Lernkicker.Application.Ports;
using Lernkicker.Contracts;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Lernkicker.Web.Play;

/// <summary>
/// The live connection of a child's play pages. It only pushes "something changed" (<see cref="PlayEvents"/>);
/// everything else goes through the API, with the same checks as always. Who is connected is who is online.
/// </summary>
[Authorize(Policy = Policies.Child)]
public sealed class PlayHub(PlayPresence presence, IPlayNotifier notifier) : Hub
{
    public static string Group(Guid childId) => $"child:{childId}";

    public override async Task OnConnectedAsync()
    {
        var childId = Context.User!.ChildIdOf();
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(childId));
        presence.Add(Context.ConnectionId, childId);
        notifier.LobbyChanged();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        presence.Remove(Context.ConnectionId);
        notifier.LobbyChanged();
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>The children with an open connection; one child may have several (a phone and a tablet).</summary>
public sealed class PlayPresence : IPlayPresence
{
    private readonly ConcurrentDictionary<string, Guid> _connections = new();

    public IReadOnlyCollection<Guid> Online => _connections.Values.Distinct().ToList();

    public void Add(string connectionId, Guid childId) => _connections[connectionId] = childId;

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);
}

/// <summary>Pushes changes to the open play pages. A failed push must never fail the move that caused it.</summary>
public sealed class PlayNotifier(IHubContext<PlayHub> hub, ILogger<PlayNotifier> logger) : IPlayNotifier
{
    public void LobbyChanged() => Send(hub.Clients.All, PlayEvents.LobbyChanged);

    public void MatchChanged(Guid matchId, IReadOnlyCollection<Guid> childIds) =>
        Send(hub.Clients.Groups(childIds.Select(PlayHub.Group).ToList()), PlayEvents.MatchChanged, matchId);

    private async void Send(IClientProxy clients, string method, params object[] args)
    {
        try
        {
            await clients.SendCoreAsync(method, args);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not push {Method} to the play pages", method);
        }
    }
}
