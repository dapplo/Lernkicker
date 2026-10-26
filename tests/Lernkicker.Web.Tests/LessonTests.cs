using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Lernkicker.Application;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

public sealed class LessonTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    private static readonly DateOnly Soon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

    [Fact]
    public async Task Child_learns_a_poem_and_the_parent_sees_the_progress()
    {
        var parent = await factory.NewFamilyAsync("poem@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var lesson = await TestData.UploadPoemAsync(parent);
        Assert.Equal(("poem-septembermorgen", 4), (lesson.LessonId, lesson.Blocks));

        var assignment = await TestData.AssignLessonAsync(parent, child.Id, lesson.LessonId, Soon);
        Assert.Equal((0, 4, Soon), (assignment.PracticeCount, assignment.Blocks, assignment.DueOn));

        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);
        var mine = Assert.Single(await LernkickerFactory.Json<List<MyLesson>>(await phone.GetAsync("/api/device/lessons")));
        Assert.Equal((assignment.Id, "Gedicht: Septembermorgen", 0, 4, "learn"), (mine.AssignmentId, mine.Title, mine.KnownBlocks, mine.Blocks, mine.Mode));

        var toLearn = await LernkickerFactory.Json<LessonDto>(await phone.GetAsync($"/api/device/lessons/{assignment.Id}"));
        Assert.Equal(["text", "poem", "flashcards", "facts"], toLearn.Blocks.Select(b => b.Type));
        var poem = toLearn.Blocks[1];
        Assert.Equal(("Eduard Mörike", "Im Nebel ruhet noch die Welt,"), (poem.Author, poem.Stanzas![0][0]));
        Assert.Equal("Strophe", toLearn.Blocks[2].Cards![1].Front);

        var practised = await LernkickerFactory.Json<LessonDto>(
            await phone.PostAsJsonAsync($"/api/device/lessons/{assignment.Id}/practice", new LessonPractice(["poem", "made-up"])));
        Assert.Equal(["poem"], practised.KnownBlocks);

        var progress = Assert.Single(await LernkickerFactory.Json<List<LessonAssignmentOverview>>(await parent.GetAsync($"/api/children/{child.Id}/lessons")));
        Assert.Equal(1, progress.PracticeCount);
        Assert.Equal(["poem"], progress.KnownBlocks);
        Assert.NotNull(progress.LastPracticedAt);
    }

    [Fact]
    public async Task Reference_lesson_reaches_the_child_with_its_mode_and_entries()
    {
        var parent = await factory.NewFamilyAsync("reference@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var lesson = await LernkickerFactory.Json<LessonOverview>(await parent.PostAsync("/api/lessons", TestData.AsJson(Lernkicker.Tests.Examples.ReferenceJson())));
        var assignment = await TestData.AssignLessonAsync(parent, child.Id, lesson.LessonId);
        Assert.Equal("reference", assignment.Mode);

        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);
        var mine = Assert.Single(await LernkickerFactory.Json<List<MyLesson>>(await phone.GetAsync("/api/device/lessons")));
        Assert.Equal("reference", mine.Mode);

        var toLookUp = await LernkickerFactory.Json<LessonDto>(await phone.GetAsync($"/api/device/lessons/{assignment.Id}"));
        Assert.Equal(("reference", "entries"), (toLookUp.Mode, toLookUp.Blocks[1].Type));
        Assert.Equal(("8", "Der Nächste muss **aussetzen**."), (toLookUp.Blocks[1].Entries![1].Name, toLookUp.Blocks[1].Entries![1].Text));
    }

    [Fact]
    public async Task Uploading_the_same_id_replaces_the_lesson_and_keeps_progress()
    {
        var parent = await factory.NewFamilyAsync("replace-lesson@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var lesson = await TestData.UploadPoemAsync(parent);
        var assignment = await TestData.AssignLessonAsync(parent, child.Id, lesson.LessonId);
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);
        await phone.PostAsJsonAsync($"/api/device/lessons/{assignment.Id}/practice", new LessonPractice(["poem"]));

        var json = JsonNode.Parse(Lernkicker.Tests.Examples.PoemJson())!.AsObject();
        json["title"] = "Septembermorgen (neu)";
        var replaced = await parent.PostAsync("/api/lessons", TestData.AsJson(json.ToJsonString()));

        Assert.Equal(HttpStatusCode.Created, replaced.StatusCode);
        Assert.Single(await LernkickerFactory.Json<List<LessonOverview>>(await parent.GetAsync("/api/lessons")));
        var progress = Assert.Single(await LernkickerFactory.Json<List<LessonAssignmentOverview>>(await parent.GetAsync($"/api/children/{child.Id}/lessons")));
        Assert.Equal("Septembermorgen (neu)", progress.Title);
        Assert.Equal(["poem"], progress.KnownBlocks);
        Assert.Contains("(neu)", await parent.GetStringAsync($"/api/lessons/{lesson.LessonId}"));
    }

    [Fact]
    public async Task Invalid_and_too_large_lessons_are_rejected()
    {
        var parent = await factory.NewFamilyAsync("bad-lesson@example.org");

        var invalid = await LernkickerFactory.Error(await parent.PostAsync("/api/lessons", TestData.AsJson(TestData.HsuJson())), HttpStatusCode.BadRequest);
        var tooLarge = await parent.PostAsync("/api/lessons", TestData.AsJson(new string(' ', 300 * 1024)));

        Assert.Equal(ErrorCodes.LessonInvalid, invalid.Code);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
    }

    [Fact]
    public async Task Due_date_is_in_the_calendar_and_deleting_removes_everything()
    {
        var parent = await factory.NewFamilyAsync("lesson-due@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var lesson = await TestData.UploadPoemAsync(parent);
        var assignment = await TestData.AssignLessonAsync(parent, child.Id, lesson.LessonId, Soon);

        var entry = Assert.Single(await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync($"/api/children/{child.Id}/calendar")));
        Assert.Equal(($"lesson-{assignment.Id}", "lesson", "lesson", Soon, lesson.Title, lesson.LessonId), (entry.Uid, entry.Kind, entry.Source, entry.Date, entry.Title, entry.ItemId));

        Assert.Equal(HttpStatusCode.NoContent, (await parent.DeleteAsync($"/api/lessons/{lesson.LessonId}")).StatusCode);
        Assert.Empty(await LernkickerFactory.Json<List<LessonAssignmentOverview>>(await parent.GetAsync($"/api/children/{child.Id}/lessons")));
        Assert.Empty(await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync($"/api/children/{child.Id}/calendar")));
        Assert.Equal(ErrorCodes.LessonNotFound, (await LernkickerFactory.Error(await parent.DeleteAsync($"/api/lessons/{lesson.LessonId}"), HttpStatusCode.NotFound)).Code);
    }

    [Fact]
    public async Task Assigning_again_changes_the_due_date_and_unassigning_removes_it()
    {
        var parent = await factory.NewFamilyAsync("lesson-again@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var lesson = await TestData.UploadPoemAsync(parent);
        var first = await TestData.AssignLessonAsync(parent, child.Id, lesson.LessonId, Soon);

        var second = await TestData.AssignLessonAsync(parent, child.Id, lesson.LessonId, null);

        Assert.Equal(first.Id, second.Id);
        Assert.Null(second.DueOn);
        Assert.Equal(HttpStatusCode.NoContent, (await parent.DeleteAsync($"/api/children/{child.Id}/lessons/{first.Id}")).StatusCode);
        Assert.Empty(await LernkickerFactory.Json<List<LessonAssignmentOverview>>(await parent.GetAsync($"/api/children/{child.Id}/lessons")));
    }

    [Fact]
    public async Task Other_family_cannot_see_or_use_a_lesson()
    {
        var a = await factory.NewFamilyAsync("lesson-a@example.org");
        var b = await factory.NewFamilyAsync("lesson-b@example.org");
        var lesson = await TestData.UploadPoemAsync(a);
        var childA = await TestData.CreateChildAsync(a);
        var assignment = await TestData.AssignLessonAsync(a, childA.Id, lesson.LessonId);
        var childB = await TestData.CreateChildAsync(b, "Mia", "4321");
        var phoneB = await TestData.ChildPhoneAsync(factory, b, childB.Id, "4321");

        Assert.Empty(await LernkickerFactory.Json<List<LessonOverview>>(await b.GetAsync("/api/lessons")));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/lessons/{lesson.LessonId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/lessons/{lesson.LessonId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/children/{childA.Id}/lessons")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/children/{childA.Id}/lessons/{assignment.Id}")).StatusCode);
        await TestData.UploadPoemAsync(b);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PostAsJsonAsync($"/api/children/{childA.Id}/lessons", new NewLessonAssignment(lesson.LessonId, null))).StatusCode);

        Assert.Empty(await LernkickerFactory.Json<List<MyLesson>>(await phoneB.GetAsync("/api/device/lessons")));
        Assert.Equal(HttpStatusCode.NotFound, (await phoneB.GetAsync($"/api/device/lessons/{assignment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await phoneB.PostAsJsonAsync($"/api/device/lessons/{assignment.Id}/practice", new LessonPractice([]))).StatusCode);

        Assert.Single(await LernkickerFactory.Json<List<LessonAssignmentOverview>>(await a.GetAsync($"/api/children/{childA.Id}/lessons")));
    }

    [Fact]
    public async Task Games_come_from_the_lessons_flashcards()
    {
        var parent = await factory.NewFamilyAsync("games@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var json = File.ReadAllText(Lernkicker.Tests.Examples.LessonPathOf("science/grain-terms.json"));
        var lesson = await LernkickerFactory.Json<LessonOverview>(await parent.PostAsync("/api/lessons", TestData.AsJson(json)));
        var assignment = await TestData.AssignLessonAsync(parent, child.Id, lesson.LessonId);
        var rules = await LernkickerFactory.Json<LessonOverview>(await parent.PostAsync("/api/lessons", TestData.AsJson(Lernkicker.Tests.Examples.ReferenceJson())));
        var reference = await TestData.AssignLessonAsync(parent, child.Id, rules.LessonId);
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);

        var toLearn = await LernkickerFactory.Json<LessonDto>(await phone.GetAsync($"/api/device/lessons/{assignment.Id}"));
        Assert.Equal(["fallingWords", "memory", "crossword", "wordSearch"], toLearn.Games);

        var falling = await LernkickerFactory.Json<LessonGameDto>(await phone.GetAsync($"/api/device/lessons/{assignment.Id}/games/fallingWords"));
        Assert.IsType<FallingWordsSheetDto>(falling.Exercise);
        var memory = await LernkickerFactory.Json<LessonGameDto>(await phone.GetAsync($"/api/device/lessons/{assignment.Id}/games/memory"));
        Assert.Equal(12, memory.Cards!.Count);
        var crossword = await LernkickerFactory.Json<LessonGameDto>(await phone.GetAsync($"/api/device/lessons/{assignment.Id}/games/crossword"));
        Assert.NotEmpty(crossword.Crossword!.Words);
        var search = await LernkickerFactory.Json<LessonGameDto>(await phone.GetAsync($"/api/device/lessons/{assignment.Id}/games/wordSearch"));
        Assert.IsType<WordSearchSheetDto>(search.Exercise);

        // The card game rules have long texts and names that are no words: not enough for a game
        Assert.Empty((await LernkickerFactory.Json<LessonDto>(await phone.GetAsync($"/api/device/lessons/{reference.Id}"))).Games);
        Assert.Equal(ErrorCodes.GameNotAvailable,
            (await LernkickerFactory.Error(await phone.GetAsync($"/api/device/lessons/{reference.Id}/games/memory"), HttpStatusCode.NotFound)).Code);
    }
}
