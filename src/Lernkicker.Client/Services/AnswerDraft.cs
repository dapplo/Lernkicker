using Lernkicker.Contracts;

namespace Lernkicker.Client.Services;

/// <summary>What the child has entered for one exercise so far. Indices are the original part indices.</summary>
public sealed class AnswerDraft
{
    private AnswerDraft(ExerciseSheetDto exercise, int parts)
    {
        Exercise = exercise;
        Texts = new string?[parts];
        Truths = new bool?[parts];
        Choices = Enumerable.Range(0, parts).Select(_ => new HashSet<int>()).ToArray();
    }

    public ExerciseSheetDto Exercise { get; }
    public string?[] Texts { get; }
    public bool?[] Truths { get; }
    public List<int> Order { get; } = [];
    public HashSet<int>[] Choices { get; }

    /// <summary>wordSearch: the cells of each word found (by word index), to mark them in the grid.</summary>
    public Dictionary<int, IReadOnlyList<Cell>> Found { get; } = [];

    /// <summary>wordSearch: the first letter tapped, waiting for the last one.</summary>
    public Cell? Start { get; set; }

    /// <summary>wordSearch: the last line tapped that is not a word, shown briefly.</summary>
    public IReadOnlyList<Cell>? Missed { get; set; }

    /// <summary>fallingWords: the words that have fallen already (by word index), whether caught or crashed.</summary>
    public HashSet<int> Fallen { get; } = [];

    public static AnswerDraft For(ExerciseSheetDto exercise) => new(exercise, exercise switch
    {
        ClozeSheetDto c => c.Segments.Count - 1,
        MatchingSheetDto m => m.Rows.Count,
        OrderingSheetDto o => o.Steps.Count,
        TrueFalseSheetDto t => t.Statements.Count,
        FreeTextSheetDto f => f.FieldLabels.Count,
        MultipleChoiceSheetDto mc => mc.Questions.Count,
        WordSearchSheetDto w => w.Words.Count,
        FallingWordsSheetDto f => f.Words.Count,
        VocabularySheetDto v => v.Questions.Count,
        _ => 0,
    });

    /// <summary>Number of parts the child has not answered yet.</summary>
    public int Open => Exercise switch
    {
        ClozeSheetDto or MatchingSheetDto or FreeTextSheetDto or WordSearchSheetDto or VocabularySheetDto => Texts.Count(string.IsNullOrWhiteSpace),
        TrueFalseSheetDto => Truths.Count(t => t is null),
        OrderingSheetDto o => o.Steps.Count - Order.Count,
        MultipleChoiceSheetDto => Choices.Count(c => c.Count == 0),
        // A crashed word was played, just not caught
        FallingWordsSheetDto f => f.Words.Count - Fallen.Count,
        _ => 0,
    };

    public AnswerDto ToAnswer() => Exercise switch
    {
        ClozeSheetDto or MatchingSheetDto or FreeTextSheetDto or WordSearchSheetDto or FallingWordsSheetDto or VocabularySheetDto => new AnswerDto { Texts = Texts },
        TrueFalseSheetDto => new AnswerDto { Truths = Truths },
        OrderingSheetDto => new AnswerDto { Order = Order.ToList() },
        MultipleChoiceSheetDto => new AnswerDto { Choices = Choices.Select(c => (IReadOnlyList<int>?)c.ToList()).ToList() },
        _ => new AnswerDto(),
    };
}
