using System.Text.RegularExpressions;
using Lernkicker.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Lernkicker.Client.Services;

/// <summary>
/// Keeps the app on this device in step with the server. Every server answer names the server's build
/// (<see cref="BuildInfoDto.Header"/>, read by <see cref="BuildCheck"/>); when it is not the build of this app, the new
/// version is loaded: at once, or once the child has left the quiz or game they are in, so no answers are lost.
/// js/start.js (lernkicker.update) lets the new service worker take over and reloads.
/// </summary>
public sealed partial class AppUpdate : IDisposable
{
    private readonly IJSRuntime _js;
    private readonly NavigationManager _navigation;

    public AppUpdate(IJSRuntime js, NavigationManager navigation)
    {
        _js = js;
        _navigation = navigation;
        _navigation.LocationChanged += OnLocationChanged;
    }

    /// <summary>The build of this app.</summary>
    public static readonly BuildInfoDto Current = BuildInfoDto.Of(typeof(AppUpdate).Assembly);

    public event Action? Changed;

    /// <summary>The server's build, when it is not this app's.</summary>
    public string? Server { get; private set; }

    /// <summary>The new version is being loaded; the page reloads in a moment.</summary>
    public bool Loading { get; private set; }

    /// <summary>Loading it on its own did not work (the page came back with the old version); the parent or child can retry.</summary>
    public bool Stuck { get; private set; }

    /// <summary>Waiting until the child is done with the quiz or game on this page.</summary>
    public bool Waiting => Server is not null && !Loading && !Stuck;

    /// <summary>Pages that hold answers only in memory: a quiz in progress and a lesson game.</summary>
    public static bool IsBusy(string relativePath) => BusyPath().IsMatch(relativePath);

    /// <summary>Called with the build named in a server answer.</summary>
    public void ServerIs(string build)
    {
        if (build == Server || (build == Current.Id && Server is null))
        {
            return;
        }

        Server = build == Current.Id ? null : build;
        Stuck = false;
        Changed?.Invoke();
        _ = LoadIfIdle();
    }

    /// <summary>Loads the new version now, even in the middle of a quiz.</summary>
    public Task LoadNow() => Load(force: Stuck);

    private Task LoadIfIdle() => Server is null || Loading || Stuck || IsBusy(_navigation.ToBaseRelativePath(_navigation.Uri)) ? Task.CompletedTask : Load(force: false);

    private async Task Load(bool force)
    {
        if (Server is not { } build)
        {
            return;
        }

        Loading = true;
        Changed?.Invoke();
        // True: the page reloads. False: this session already reloaded once for this build and still has the old app.
        var reloading = await _js.InvokeAsync<bool>("lernkicker.update", build, force);
        if (!reloading)
        {
            Loading = false;
            Stuck = true;
            Changed?.Invoke();
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => _ = LoadIfIdle();

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;

    [GeneratedRegex(@"^(practice/[^/?#]+|learn/[^/?#]+/play/[^/?#]+)(?:[/?#]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex BusyPath();
}

/// <summary>Hands the build that each server answer names to <see cref="AppUpdate"/>.</summary>
public sealed class BuildCheck(AppUpdate update) : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues(BuildInfoDto.Header, out var values) && values.FirstOrDefault() is { Length: > 0 } build)
        {
            update.ServerIs(build);
        }

        return response;
    }
}
