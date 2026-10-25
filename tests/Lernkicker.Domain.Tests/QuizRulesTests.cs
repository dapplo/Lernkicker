using Lernkicker.Domain.Quizzes;
using Lernkicker.Tests;

namespace Lernkicker.Domain.Tests;

public class QuizRulesTests
{
    private readonly Quiz _quiz = Examples.Hsu();

    [Fact]
    public void Example_has_no_issues() => Assert.Empty(QuizRules.Check(_quiz));

    [Fact]
    public void Gap_count_must_match_the_text()
    {
        var cloze = (ClozeExercise)_quiz.Exercises[0];
        var quiz = Replace(0, cloze with { Gaps = cloze.Gaps.Take(7).ToList() });

        var issue = Assert.Single(QuizRules.Check(quiz));
        Assert.Equal(QuizIssueCodes.GapCount, issue.Code);
        Assert.Equal("exercises/0/gaps", issue.Path);
        Assert.Equal("8", issue.Args!["inText"]);
        Assert.Equal("7", issue.Args!["entries"]);
    }

    [Fact]
    public void Gap_needs_an_answer_from_the_word_bank()
    {
        var cloze = (ClozeExercise)_quiz.Exercises[0];
        var quiz = Replace(0, cloze with { Gaps = [new Gap(["Blatt"]), .. cloze.Gaps.Skip(1)] });

        var issue = Assert.Single(QuizRules.Check(quiz));
        Assert.Equal(QuizIssueCodes.GapNotInWordBank, issue.Code);
        Assert.Equal("exercises/0/gaps/0", issue.Path);
    }

    [Fact]
    public void Matching_answer_must_be_an_option()
    {
        var matching = (MatchingExercise)_quiz.Exercises[1];
        var quiz = Replace(1, matching with { Rows = [matching.Rows[0] with { Answer = "Mais" }, .. matching.Rows.Skip(1)] });

        var issue = Assert.Single(QuizRules.Check(quiz));
        Assert.Equal(QuizIssueCodes.AnswerNotInOptions, issue.Code);
        Assert.Equal("Mais", issue.Args!["answer"]);
    }

    [Fact]
    public void Correct_index_must_exist()
    {
        var choice = (MultipleChoiceExercise)_quiz.Exercises[7];
        var quiz = Replace(7, choice with { Questions = [choice.Questions[0] with { Correct = [5] }, .. choice.Questions.Skip(1)] });

        Assert.Equal(QuizIssueCodes.CorrectOutOfRange, Assert.Single(QuizRules.Check(quiz)).Code);
    }

    [Fact]
    public void Exercise_ids_are_unique()
    {
        var quiz = Replace(3, _quiz.Exercises[3] with { Id = _quiz.Exercises[0].Id });

        var issue = Assert.Single(QuizRules.Check(quiz));
        Assert.Equal(QuizIssueCodes.DuplicateId, issue.Code);
        Assert.Equal("exercises/3/id", issue.Path);
    }

    private Quiz Replace(int index, Exercise exercise) =>
        _quiz with { Exercises = _quiz.Exercises.Select((e, i) => i == index ? exercise : e).ToList() };
}
