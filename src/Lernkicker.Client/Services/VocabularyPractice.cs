using Lernkicker.Contracts;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Client.Services;

/// <summary>How a typed vocabulary answer compares to the right one.</summary>
public enum AnswerCheck
{
    Wrong,

    /// <summary>Wrong capital letters, a wrong or missing article, or one letter off.</summary>
    Almost,

    Right,
}

/// <summary>A word asked one way: the child sees <see cref="Prompt"/> and answers with <see cref="Answer"/>.</summary>
public sealed record VocabularyCard(VocabularyWordDto Word, bool AsksWord)
{
    public string Prompt => AsksWord ? Word.Translation : Word.Word;

    public string Answer => AsksWord ? Word.Word : Word.Translation;

    public IReadOnlyList<string> Accepted => AsksWord
        ? [Word.Word, .. Word.WordAlternatives ?? []]
        : [Word.Translation, .. Word.TranslationAlternatives ?? []];
}

/// <summary>
/// Practising a vocabulary list with cards or by typing: every word once per round, in random order. With direction
/// "both" each word is asked a random way, and a word that comes again may come the other way.
/// </summary>
public sealed class VocabularyPractice
{
    private readonly IReadOnlyList<VocabularyWordDto> _words;
    private readonly string _direction;
    private readonly Random _random;
    private List<VocabularyCard> _queue = [];

    public VocabularyPractice(IReadOnlyList<VocabularyWordDto> words, string? direction, Random random)
    {
        _words = words;
        _direction = direction ?? "both";
        _random = random;
        Restart();
    }

    public VocabularyCard? Current => _queue.Count > 0 ? _queue[0] : null;

    public int Left => _queue.Count;

    public int Total => _words.Count;

    public void Restart() =>
        _queue = _words.OrderBy(_ => _random.Next()).Select(Card).ToList();

    /// <summary>Done with the current card: a known one is finished, the others come again at the end.</summary>
    public void Next(bool known)
    {
        if (Current is not { } card)
        {
            return;
        }

        _queue.RemoveAt(0);
        if (!known)
        {
            _queue.Add(Card(card.Word));
        }
    }

    /// <summary>Checks a typed answer like the quiz grader does.</summary>
    public static AnswerCheck Check(string? typed, VocabularyCard card) => Spelling.Check(typed, card.Accepted) switch
    {
        SpellingResult.Right => AnswerCheck.Right,
        SpellingResult.Almost => AnswerCheck.Almost,
        _ => AnswerCheck.Wrong,
    };

    private VocabularyCard Card(VocabularyWordDto word) => new(word, _direction switch
    {
        "wordToTranslation" => false,
        "translationToWord" => true,
        _ => _random.Next(2) == 1,
    });
}
