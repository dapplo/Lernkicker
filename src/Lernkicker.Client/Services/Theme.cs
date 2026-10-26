namespace Lernkicker.Client.Services;

/// <summary>
/// A look of the app (a profile's theme, see ChildThemes on the server). <c>Id</c> is the CSS class suffix
/// (<c>theme-{Id}</c> in app.css) and the key of its name (<c>Theme.{Id}</c>); <c>Icon</c> replaces the ball in the header.
/// </summary>
public sealed record Theme(string Id, string Icon)
{
    public static readonly IReadOnlyList<Theme> All = [new("football", "⚽"), new("space", "🚀"), new("unicorn", "🦄")];

    public static Theme Find(string? id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}

/// <summary>
/// The look of the app right now: on a paired device the child's profile theme, elsewhere the default. The layout
/// asks for it once; the home page passes on every newer status it loads (e.g. after pairing).
/// </summary>
public sealed class CurrentTheme(Api api)
{
    private Task? _loading;

    public Theme Value { get; private set; } = Theme.All[0];

    public event Action? Changed;

    public Task LoadAsync() => _loading ??= Load();

    public void Set(string? id)
    {
        var theme = Theme.Find(id);
        if (theme != Value)
        {
            Value = theme;
            Changed?.Invoke();
        }
    }

    private async Task Load()
    {
        try
        {
            Set((await api.DeviceStatus()).Theme);
        }
        catch (Exception e) when (e is ApiException or HttpRequestException)
        {
            // Offline or not reachable: keep the default look
        }
    }
}
