using Lernkicker.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Lernkicker.Client.Services;

/// <summary>
/// The live connection of the play pages: the server says when the lobby or a game changed, and the page loads it
/// again. While it is open, the child counts as online in the lobby. The pages hold it (<see cref="AcquireAsync"/>)
/// and let go of it when they close; it stays open a moment longer, so going from the lobby to a game keeps it.
/// Without it the pages still work, they just do not update by themselves.
/// </summary>
public sealed class PlayConnection(NavigationManager navigation) : IAsyncDisposable
{
    private static readonly TimeSpan Linger = TimeSpan.FromSeconds(5);

    private HubConnection? _connection;
    private int _holders;
    private CancellationTokenSource? _closing;

    /// <summary>Someone came or went, or a table opened or closed.</summary>
    public event Func<Task>? LobbyChanged;

    /// <summary>A game of this child changed.</summary>
    public event Func<Guid, Task>? MatchChanged;

    public async Task AcquireAsync()
    {
        _holders++;
        _closing?.Cancel();
        if (_connection is not null)
        {
            return;
        }

        var connection = new HubConnectionBuilder()
            .WithUrl(navigation.ToAbsoluteUri(PlayEvents.Path.TrimStart('/')))
            .WithAutomaticReconnect()
            .Build();
        connection.On(PlayEvents.LobbyChanged, () => Raise(LobbyChanged));
        connection.On<Guid>(PlayEvents.MatchChanged, id => MatchChanged?.Invoke(id) ?? Task.CompletedTask);
        // Changes may have been missed while the connection was down
        connection.Reconnected += _ => Raise(LobbyChanged);
        connection.Closed += _ =>
        {
            if (_connection == connection)
            {
                _connection = null;
            }

            return Task.CompletedTask;
        };
        _connection = connection;
        try
        {
            await connection.StartAsync();
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TimeoutException)
        {
            _connection = null;
            await connection.DisposeAsync();
        }
    }

    public void Release()
    {
        if (--_holders > 0)
        {
            return;
        }

        _closing = new CancellationTokenSource();
        var token = _closing.Token;
        _ = Task.Delay(Linger, token).ContinueWith(async _ =>
        {
            if (_connection is { } connection)
            {
                _connection = null;
                await connection.DisposeAsync();
            }
        }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    private static Task Raise(Func<Task>? handler) => handler?.Invoke() ?? Task.CompletedTask;
}
