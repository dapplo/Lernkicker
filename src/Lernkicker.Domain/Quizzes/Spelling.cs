using System.Text;

namespace Lernkicker.Domain.Quizzes;

/// <summary>How close a typed word is to the right one.</summary>
internal enum SpellingResult
{
    Wrong,

    /// <summary>Nearly: wrong capital letters, a wrong or missing article, or one letter wrong, missing or extra.</summary>
    Almost,

    Right,
}

/// <summary>
/// Checks a typed vocabulary answer. Extra spaces and a full stop, question or exclamation mark at the end don't
/// matter, and a capital first letter is fine (phones capitalize it), but "hund" for "Hund" is only
/// <see cref="SpellingResult.Almost"/>. This file is compiled into the client too, so that lesson practice checks
/// answers the same way as the quiz grader.
/// </summary>
internal static class Spelling
{
    private static readonly string[] Articles =
    [
        "der", "die", "das", "den", "dem", "des", "ein", "eine", "einen",
        "the", "a", "an", "to",
        "de", "het", "een",
    ];

    public static SpellingResult Check(string? input, IEnumerable<string> accepted)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return SpellingResult.Wrong;
        }

        var typed = Normalize(input);
        var best = SpellingResult.Wrong;
        foreach (var answer in accepted.Select(Normalize))
        {
            var result = Compare(typed, answer);
            if (result == SpellingResult.Right)
            {
                return result;
            }

            best = result > best ? result : best;
        }

        return best;
    }

    private static SpellingResult Compare(string typed, string answer)
    {
        if (typed == answer || (typed.Length > 0 && answer.Length > 0 && char.IsLower(answer[0])
            && typed[1..] == answer[1..] && char.ToLowerInvariant(typed[0]) == answer[0]))
        {
            return SpellingResult.Right;
        }

        var lowerTyped = typed.ToLowerInvariant();
        var lowerAnswer = answer.ToLowerInvariant();
        if (lowerTyped == lowerAnswer || WithoutArticle(lowerTyped) == WithoutArticle(lowerAnswer))
        {
            return SpellingResult.Almost;
        }

        return lowerAnswer.Length >= 4 && AtMostOneEdit(lowerTyped, lowerAnswer) ? SpellingResult.Almost : SpellingResult.Wrong;
    }

    private static string WithoutArticle(string text)
    {
        var space = text.IndexOf(' ');
        return space > 0 && Articles.Contains(text[..space]) ? text[(space + 1)..] : text;
    }

    /// <summary>True when one letter replaced, left out or added turns <paramref name="a"/> into <paramref name="b"/>.</summary>
    private static bool AtMostOneEdit(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1)
        {
            return false;
        }

        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
        var i = 0;
        while (i < shorter.Length && shorter[i] == longer[i])
        {
            i++;
        }

        return shorter.Length == longer.Length
            ? shorter[(Math.Min(i + 1, shorter.Length))..] == longer[(Math.Min(i + 1, longer.Length))..]
            : shorter[i..] == longer[(i + 1)..];
    }

    private static string Normalize(string text)
    {
        var words = string.Join(' ', text.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return words.TrimEnd('.', '!', '?', ' ');
    }
}
