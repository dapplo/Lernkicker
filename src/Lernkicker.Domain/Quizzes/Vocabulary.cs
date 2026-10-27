using Lernkicker.Domain.Media;

namespace Lernkicker.Domain.Quizzes;

/// <summary>
/// A word of a vocabulary list: the <c>Word</c> in the language being learned and its <c>Translation</c>. The
/// alternatives are further answers that count as right (e.g. without the article, or a synonym).
/// </summary>
public sealed record VocabularyWord(
    string Word,
    string Translation,
    IReadOnlyList<string>? WordAlternatives = null,
    IReadOnlyList<string>? TranslationAlternatives = null,
    ImageRef? Image = null)
{
    /// <summary>Every answer that counts when the word is asked for (<paramref name="asksWord"/>) or the translation.</summary>
    public IReadOnlyList<string> Accepted(bool asksWord) => asksWord
        ? [Word, .. WordAlternatives ?? []]
        : [Translation, .. TranslationAlternatives ?? []];
}

/// <summary>Which way a vocabulary list is asked.</summary>
public enum VocabularyDirection
{
    /// <summary>Shows the word, the child gives the translation and the other way round.</summary>
    Both,

    /// <summary>Shows the word (e.g. Dutch), the child gives the translation (e.g. German).</summary>
    WordToTranslation,

    /// <summary>Shows the translation, the child gives the word.</summary>
    TranslationToWord,
}

/// <summary>One question of a vocabulary list: the word asked one way. <c>Index</c> numbers the questions.</summary>
public sealed record VocabularyQuestion(int Index, VocabularyWord Word, bool AsksWord)
{
    public string Prompt => AsksWord ? Word.Translation : Word.Word;

    public IReadOnlyList<string> Accepted => Word.Accepted(AsksWord);
}

public static class Vocabulary
{
    /// <summary>
    /// The questions a list makes: each word once per direction. With <see cref="VocabularyDirection.Both"/> the
    /// words are first asked word → translation, then translation → word.
    /// </summary>
    public static IReadOnlyList<VocabularyQuestion> Questions(IReadOnlyList<VocabularyWord> words, VocabularyDirection direction)
    {
        IEnumerable<bool> ways = direction switch
        {
            VocabularyDirection.WordToTranslation => [false],
            VocabularyDirection.TranslationToWord => [true],
            _ => [false, true],
        };
        return ways
            .SelectMany(asksWord => words.Select(word => (word, asksWord)))
            .Select((q, i) => new VocabularyQuestion(i, q.word, q.asksWord))
            .ToList();
    }
}

/// <summary>
/// A vocabulary test: the child types the translation of each word, or the word for each translation, depending on
/// <see cref="Direction"/>. Each question is a part; an answer is right when it is spelled right (see <see cref="Spelling"/>).
/// </summary>
public sealed record VocabularyExercise : Exercise
{
    /// <summary>BCP 47 tag of the words, e.g. "nl" or "en".</summary>
    public required string WordLanguage { get; init; }

    /// <summary>BCP 47 tag of the translations; null means the quiz's language.</summary>
    public string? TranslationLanguage { get; init; }

    public VocabularyDirection Direction { get; init; } = VocabularyDirection.Both;

    public required IReadOnlyList<VocabularyWord> Words { get; init; }

    public IReadOnlyList<VocabularyQuestion> Questions => Vocabulary.Questions(Words, Direction);

    public override int PartCount => Direction == VocabularyDirection.Both ? Words.Count * 2 : Words.Count;

    protected override IEnumerable<ImageUse> PartImages(string path) =>
        Words.Select((w, i) => (w.Image, At: $"{path}/words/{i}/image"))
            .Where(x => x.Image is not null)
            .Select(x => new ImageUse(x.At, x.Image!));
}
