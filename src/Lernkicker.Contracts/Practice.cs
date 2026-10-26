using System.Text.Json.Serialization;

namespace Lernkicker.Contracts;

/// <summary>A quiz as the child's device receives it: no solutions. Parts carry their original <c>Index</c>.</summary>
public sealed record SheetDto(
    string Id, string Title, string Subject, int? GradeLevel, string Language, string? Topic, int? DurationMinutes, int MaxPoints, IReadOnlyList<ExerciseSheetDto> Exercises);

public sealed record SheetItemDto(int Index, string Text, ImageDto? Image = null);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ClozeSheetDto), "cloze")]
[JsonDerivedType(typeof(MatchingSheetDto), "matching")]
[JsonDerivedType(typeof(OrderingSheetDto), "ordering")]
[JsonDerivedType(typeof(TrueFalseSheetDto), "trueFalse")]
[JsonDerivedType(typeof(FreeTextSheetDto), "freeText")]
[JsonDerivedType(typeof(MultipleChoiceSheetDto), "multipleChoice")]
public abstract record ExerciseSheetDto(string Id, string Instruction, string? Hint, int MaxPoints)
{
    public ImageDto? Image { get; init; }
}

/// <summary>Gap i sits between Segments[i] and Segments[i + 1].</summary>
public sealed record ClozeSheetDto(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<string> Segments, IReadOnlyList<string>? WordBank)
    : ExerciseSheetDto(Id, Instruction, Hint, MaxPoints);

public sealed record MatchingSheetDto(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<string> Options, IReadOnlyList<SheetItemDto> Rows)
    : ExerciseSheetDto(Id, Instruction, Hint, MaxPoints);

public sealed record OrderingSheetDto(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<SheetItemDto> Steps)
    : ExerciseSheetDto(Id, Instruction, Hint, MaxPoints);

public sealed record TrueFalseSheetDto(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<SheetItemDto> Statements)
    : ExerciseSheetDto(Id, Instruction, Hint, MaxPoints);

public sealed record FreeTextSheetDto(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<string?> FieldLabels)
    : ExerciseSheetDto(Id, Instruction, Hint, MaxPoints);

public sealed record MultipleChoiceSheetDto(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<QuestionSheetDto> Questions)
    : ExerciseSheetDto(Id, Instruction, Hint, MaxPoints);

/// <summary>An option's <c>Text</c> is empty when the option is a picture alone.</summary>
public sealed record QuestionSheetDto(int Index, string Text, IReadOnlyList<SheetItemDto> Options, bool MultipleAnswers, ImageDto? Image = null);

/// <summary>A child's answer to one exercise; all indices refer to the original order (see <see cref="SheetItemDto.Index"/>).</summary>
public sealed record AnswerDto
{
    /// <summary>cloze, matching, freeText: one entry per gap, row or field.</summary>
    public IReadOnlyList<string?>? Texts { get; init; }

    /// <summary>trueFalse: one entry per statement, null = not answered.</summary>
    public IReadOnlyList<bool?>? Truths { get; init; }

    /// <summary>ordering: step indices in the order the child tapped them.</summary>
    public IReadOnlyList<int>? Order { get; init; }

    /// <summary>multipleChoice: per question the selected option indices.</summary>
    public IReadOnlyList<IReadOnlyList<int>?>? Choices { get; init; }
}

/// <summary><c>Solution</c> is quiz content; for trueFalse it is null and <c>ExpectedTruth</c> holds the answer.</summary>
public sealed record PartResultDto(int Index, bool Correct, string? Solution, string? Explanation, bool? ExpectedTruth);

public sealed record ExerciseResultDto(string Id, string Type, string Instruction, int Points, int MaxPoints, IReadOnlyList<PartResultDto> Parts, string? Explanation, string? SolutionText);

/// <summary><c>Mark</c> is a rough German school mark (1 best, 6 worst); null for an adult.</summary>
public sealed record QuizResultDto(int Points, int MaxPoints, int Percent, int? Mark, IReadOnlyList<ExerciseResultDto> Exercises);
