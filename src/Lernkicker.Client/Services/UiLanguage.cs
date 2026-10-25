using System.Globalization;
using Microsoft.JSInterop;

namespace Lernkicker.Client.Services;

/// <summary>
/// The UI language. js/start.js picks it before Blazor starts; switching stores the choice on this device
/// and reloads, so every text and date is rendered in the new culture.
/// </summary>
public sealed class UiLanguage(IJSRuntime js)
{
    public static readonly IReadOnlyList<(string Code, string Name)> Available = [("de", "Deutsch"), ("en", "English")];

    public static string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public ValueTask SwitchTo(string code) => js.InvokeVoidAsync("lernkicker.setLanguage", code);
}
