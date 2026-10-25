using System.Text.RegularExpressions;

namespace Lernkicker.Client.Services;

/// <summary>
/// Hides more and more words of a poem while the child learns it. Level 0 shows everything, then every third
/// word, every second word, all but the first word of a line, and finally every word is hidden.
/// </summary>
public static partial class PoemHelp
{
    public const int Levels = 5;

    /// <summary>A word of a line. A hidden word keeps its punctuation and shows a gap as long as the word.</summary>
    public sealed record Word(string Leading, string Text, string Trailing, bool Hidden);

    public static IReadOnlyList<Word> Words(string line, int level, int lineIndex)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Select((w, i) =>
        {
            var m = Parts().Match(w);
            var hidden = m.Groups["text"].Length > 0 && IsHidden(level, lineIndex, i);
            return new Word(m.Groups["lead"].Value, m.Groups["text"].Value, m.Groups["trail"].Value, hidden);
        }).ToList();
    }

    /// <summary>The gap shown for a hidden word: one underscore per letter, at least two.</summary>
    public static string Gap(string text) => new('_', Math.Max(2, text.Length));

    private static bool IsHidden(int level, int lineIndex, int wordIndex) => level switch
    {
        <= 0 => false,
        1 => (wordIndex + lineIndex) % 3 == 2,
        2 => (wordIndex + lineIndex) % 2 == 1,
        3 => wordIndex > 0,
        _ => true,
    };

    [GeneratedRegex(@"^(?<lead>[^\p{L}\p{N}]*)(?<text>.*?)(?<trail>[^\p{L}\p{N}]*)$")]
    private static partial Regex Parts();
}
