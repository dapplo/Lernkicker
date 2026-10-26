using Lernkicker.Contracts;

namespace Lernkicker.Client.Services;

/// <summary>The games made from a lesson (see LessonGames on the server), in the order they are offered.</summary>
public static class LessonGameKinds
{
    public static string Icon(string kind) => kind switch
    {
        "fallingWords" => "🌠",
        "memory" => "🃏",
        "crossword" => "✏️",
        "wordSearch" => "🔍",
        _ => "🎮",
    };
}

/// <summary>The rules of the falling words game that do not need the screen.</summary>
public static class FallingWords
{
    /// <summary>How much faster each caught word makes the next one fall, and the fastest it gets.</summary>
    private const double SpeedUp = 0.93;
    private const double Fastest = 0.45;

    /// <summary>The options shown under <paramref name="word"/>: its answer and others, as many as the sheet says, in random order.</summary>
    public static IReadOnlyList<string> Targets(FallingWordsSheetDto sheet, FallingWordDto word, Random random)
    {
        var others = sheet.Options.Where(o => o != word.Answer).Distinct().OrderBy(_ => random.Next()).Take(Math.Max(1, sheet.TargetCount) - 1);
        return others.Append(word.Answer).OrderBy(_ => random.Next()).ToList();
    }

    /// <summary>Milliseconds the next word takes to fall: a little faster for every word caught so far.</summary>
    public static int FallMilliseconds(int fallSeconds, int caught) =>
        (int)(fallSeconds * 1000 * Math.Max(Fastest, Math.Pow(SpeedUp, caught)));

    /// <summary>Whether the word landed on its answer. The targets are the sheet's own option texts, so they compare exactly.</summary>
    public static bool IsRight(FallingWordDto word, string? landedOn) => landedOn == word.Answer;
}
