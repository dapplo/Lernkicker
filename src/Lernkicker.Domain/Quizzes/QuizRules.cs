namespace Lernkicker.Domain.Quizzes;

/// <summary>
/// A problem found in a quiz file. <c>Code</c> is stable and translated by the UI; <c>Path</c> points into
/// the JSON (e.g. <c>exercises/2/gaps/0</c>); <c>Args</c> fill the placeholders of the translated message.
/// </summary>
public sealed record QuizIssue(string Code, string Path, IReadOnlyDictionary<string, string>? Args = null)
{
    public static QuizIssue Of(string code, string path, params (string Key, object Value)[] args) =>
        new(code, path, args.Length == 0 ? null : args.ToDictionary(a => a.Key, a => Convert.ToString(a.Value, System.Globalization.CultureInfo.InvariantCulture) ?? ""));
}

public static class QuizIssueCodes
{
    public const string TooLarge = "tooLarge";
    public const string InvalidJson = "invalidJson";
    public const string Schema = "schema";
    public const string DuplicateId = "duplicateId";
    public const string GapCount = "gapCount";
    public const string GapNotInWordBank = "gapNotInWordBank";
    public const string AnswerNotInOptions = "answerNotInOptions";
    public const string CorrectOutOfRange = "correctOutOfRange";
    public const string WordNotLetters = "wordNotLetters";
    public const string WordTooLong = "wordTooLong";
    public const string DuplicateWord = "duplicateWord";
    public const string MissingImage = "missingImage";
    public const string ImageType = "imageType";
    public const string ImageTooLarge = "imageTooLarge";
    public const string ImagesTooLarge = "imagesTooLarge";
}

/// <summary>Content rules that JSON Schema cannot express (same rules as tools/validate_quizzes.py).</summary>
public static class QuizRules
{
    public static IReadOnlyList<QuizIssue> Check(Quiz quiz)
    {
        var issues = new List<QuizIssue>();
        var ids = new HashSet<string>();
        for (var i = 0; i < quiz.Exercises.Count; i++)
        {
            var exercise = quiz.Exercises[i];
            var at = $"exercises/{i}";
            if (!ids.Add(exercise.Id))
            {
                issues.Add(QuizIssue.Of(QuizIssueCodes.DuplicateId, $"{at}/id", ("id", exercise.Id)));
            }

            switch (exercise)
            {
                case ClozeExercise cloze:
                    if (cloze.GapsInText != cloze.Gaps.Count)
                    {
                        issues.Add(QuizIssue.Of(QuizIssueCodes.GapCount, $"{at}/gaps", ("inText", cloze.GapsInText), ("entries", cloze.Gaps.Count)));
                    }

                    if (cloze.WordBank is { Count: > 0 } wordBank)
                    {
                        for (var j = 0; j < cloze.Gaps.Count; j++)
                        {
                            if (!cloze.Gaps[j].Answers.Any(wordBank.Contains))
                            {
                                issues.Add(QuizIssue.Of(QuizIssueCodes.GapNotInWordBank, $"{at}/gaps/{j}"));
                            }
                        }
                    }

                    break;
                case MatchingExercise matching:
                    for (var j = 0; j < matching.Rows.Count; j++)
                    {
                        if (!matching.Options.Contains(matching.Rows[j].Answer))
                        {
                            issues.Add(QuizIssue.Of(QuizIssueCodes.AnswerNotInOptions, $"{at}/rows/{j}/answer", ("answer", matching.Rows[j].Answer)));
                        }
                    }

                    break;
                case MultipleChoiceExercise choice:
                    for (var j = 0; j < choice.Questions.Count; j++)
                    {
                        if (choice.Questions[j].Correct.Any(c => c >= choice.Questions[j].Options.Count))
                        {
                            issues.Add(QuizIssue.Of(QuizIssueCodes.CorrectOutOfRange, $"{at}/questions/{j}/correct"));
                        }
                    }

                    break;
                case FallingWordsExercise falling:
                    for (var j = 0; j < falling.Words.Count; j++)
                    {
                        if (!falling.Options.Contains(falling.Words[j].Answer))
                        {
                            issues.Add(QuizIssue.Of(QuizIssueCodes.AnswerNotInOptions, $"{at}/words/{j}/answer", ("answer", falling.Words[j].Answer)));
                        }
                    }

                    break;
                case WordSearchExercise wordSearch:
                    CheckWordSearch(wordSearch, at, issues);
                    break;
            }
        }

        return issues;
    }

    private static void CheckWordSearch(WordSearchExercise wordSearch, string at, List<QuizIssue> issues)
    {
        var maxLength = wordSearch.Size ?? WordSearchExercise.MaxSize;
        var seen = new HashSet<string>();
        for (var j = 0; j < wordSearch.Words.Count; j++)
        {
            var word = wordSearch.Words[j];
            var letters = WordSearchGrid.Letters(word);
            if (!letters.All(char.IsLetter))
            {
                issues.Add(QuizIssue.Of(QuizIssueCodes.WordNotLetters, $"{at}/words/{j}", ("word", word)));
            }
            else if (letters.Length > maxLength)
            {
                issues.Add(QuizIssue.Of(QuizIssueCodes.WordTooLong, $"{at}/words/{j}", ("word", word), ("size", maxLength)));
            }

            if (!seen.Add(letters))
            {
                issues.Add(QuizIssue.Of(QuizIssueCodes.DuplicateWord, $"{at}/words/{j}", ("word", word)));
            }
        }
    }
}
