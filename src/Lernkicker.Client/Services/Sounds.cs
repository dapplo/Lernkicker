using Microsoft.JSInterop;

namespace Lernkicker.Client.Services;

/// <summary>
/// Sounds in games (wwwroot/js/sounds.js makes them with the Web Audio API). A child can turn them off; the choice is
/// kept on the device for all games.
/// </summary>
public sealed class Sounds(IJSRuntime js)
{
    private bool? _on;

    public async Task<bool> IsOn() => _on ??= await Ask("lernkickerSounds.isOn", true);

    public async Task Toggle()
    {
        _on = await Ask("lernkickerSounds.setOn", !await IsOn(), !await IsOn());
    }

    /// <summary>Plays sounds by name ("shot", "splash", "hit", "sink", "place", "win", "lose"), each after its delay.</summary>
    public async Task Play(params (string Name, double Delay)[] steps)
    {
        if (!await IsOn())
        {
            return;
        }

        try
        {
            await js.InvokeVoidAsync("lernkickerSounds.play", [steps.Select(s => new object[] { s.Name, s.Delay }).ToArray()]);
        }
        catch (JSException)
        {
            // No audio on this device; the game goes on silently
        }
    }

    private async Task<T> Ask<T>(string function, T fallback, params object[] args)
    {
        try
        {
            return await js.InvokeAsync<T>(function, args);
        }
        catch (JSException)
        {
            return fallback;
        }
    }
}
