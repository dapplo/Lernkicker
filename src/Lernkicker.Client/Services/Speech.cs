using Microsoft.JSInterop;

namespace Lernkicker.Client.Services;

/// <summary>
/// Reads words aloud with the device's own voices (Web Speech API). Whether the device has a voice for a language is
/// asked once per language.
/// </summary>
public sealed class Speech(IJSRuntime js)
{
    private readonly Dictionary<string, Task<bool>> _voices = [];
    private readonly Dictionary<string, Task<string>> _names = [];

    public Task<bool> HasVoice(string language)
    {
        if (!_voices.TryGetValue(language, out var voice))
        {
            _voices[language] = voice = Ask<bool>("lernkicker.hasVoice", language, false);
        }

        return voice;
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

    private async Task<T> Ask<T>(string function, string language, T fallback)
    {
        try
        {
            return await js.InvokeAsync<T>(function, language);
        }
        catch (JSException)
        {
            return fallback;
        }
    }
}
