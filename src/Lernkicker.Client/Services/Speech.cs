using Microsoft.JSInterop;

namespace Lernkicker.Client.Services;

/// <summary>
/// Reads words aloud with the device's own voices (Web Speech API). The read-aloud button shows whenever the browser
/// can speak: the device picks a voice from the language tag even when it does not list one (Chrome on Android).
/// </summary>
public sealed class Speech(IJSRuntime js)
{
    private readonly Dictionary<string, Task<bool?>> _voices = [];
    private readonly Dictionary<string, Task<string>> _names = [];
    private Task<bool>? _canSpeak;

    public Task<bool> CanSpeak() => _canSpeak ??= Ask("lernkicker.canSpeak", false);

    /// <summary>
    /// Whether the device lists a voice for the language; null when it lists none at all, so it cannot tell. A "no"
    /// is asked again next time, because some devices list their voices only after a while.
    /// </summary>
    public async Task<bool?> HasVoice(string language)
    {
        if (!_voices.TryGetValue(language, out var voice) || (voice.IsCompleted && voice.Result != true))
        {
            _voices[language] = voice = Ask<bool?>("lernkicker.hasVoice", null, language);
        }

        return await voice;
    }

    public async Task Speak(string text, string language)
    {
        try
        {
            await js.InvokeVoidAsync("lernkicker.speak", text, language);
        }
        catch (JSException)
        {
            // The browser refused (e.g. speech blocked); the button just does nothing
        }
    }

    /// <summary>The language's name in the UI language, e.g. "Niederländisch" for "nl".</summary>
    public Task<string> LanguageName(string language)
    {
        if (!_names.TryGetValue(language, out var name))
        {
            _names[language] = name = Ask("lernkicker.languageName", language, language);
        }

        return name;
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
