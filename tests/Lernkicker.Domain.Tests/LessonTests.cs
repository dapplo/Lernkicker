using Lernkicker.Domain.Lessons;

namespace Lernkicker.Domain.Tests;

public class LessonTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Practice_counts_and_keeps_only_existing_blocks()
    {
        var assignment = new LessonAssignment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Now);

        assignment.Practised(["poem", "unknown", "facts", "poem"], ["tips", "poem", "facts"], Now);
        assignment.Practised(["tips"], ["tips", "poem", "facts"], Now.AddHours(1));

        Assert.Equal(2, assignment.PracticeCount);
        Assert.Equal(Now.AddHours(1), assignment.LastPracticedAt);
        Assert.Equal(["tips"], assignment.KnownBlocks);
    }

    [Fact]
    public void Known_blocks_are_distinct_and_sorted()
    {
        var assignment = new LessonAssignment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Now);

        assignment.Practised(["poem", "facts", "poem"], ["poem", "facts"], Now);

        Assert.Equal(["facts", "poem"], assignment.KnownBlocks);
    }

    [Fact]
    public void Duplicate_block_ids_break_the_rules()
    {
        var lesson = new Lesson
        {
            Id = "x-lesson", Title = "X", Subject = "german", GradeLevel = 4,
            Blocks = [new FactsBlock { Id = "a", Items = ["1"] }, new FactsBlock { Id = "a", Items = ["2"] }],
        };

        var issue = Assert.Single(LessonRules.Check(lesson));

        Assert.Equal("blocks/1/id", issue.Path);
    }
}
