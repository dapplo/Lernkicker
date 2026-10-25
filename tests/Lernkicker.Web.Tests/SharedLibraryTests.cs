using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Lernkicker.Application;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>Families share quizzes and lessons; others preview and copy them, and nothing else crosses families.</summary>
public sealed class SharedLibraryTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    [Fact]
    public async Task Shared_material_can_be_previewed_and_copied_by_other_families()
    {
        var a = await factory.NewFamilyAsync("share-a@example.org");
        var b = await factory.NewFamilyAsync("share-b@example.org");
        var quiz = await UploadAsync<QuizOverview>(a, "quizzes", Lernkicker.Tests.Examples.HsuJson(), "hsu-" + Guid.NewGuid().ToString("N")[..8]);
        var lesson = await UploadAsync<LessonOverview>(a, "lessons", Lernkicker.Tests.Examples.PoemJson(), "poem-" + Guid.NewGuid().ToString("N")[..8]);
        Assert.False(quiz.Shared);

        var shared = await LernkickerFactory.Json<QuizOverview>(await a.PutAsJsonAsync($"/api/quizzes/{quiz.QuizId}/shared", new ShareRequest(true)));
        Assert.True(shared.Shared);
        await a.PutAsJsonAsync($"/api/lessons/{lesson.LessonId}/shared", new ShareRequest(true));
        Assert.Empty(await LibraryAsync(a));

        var library = await LibraryAsync(b);
        Assert.Equal(["lesson", "quiz"], library.Select(s => s.Kind).Order());
        var sharedQuiz = library.Single(s => s.Kind == "quiz");
        var sharedLesson = library.Single(s => s.Kind == "lesson");
        Assert.Equal((quiz.QuizId, 39, false), (sharedQuiz.ItemId, sharedQuiz.Size, sharedQuiz.InMyLibrary));
        Assert.Equal(4, sharedLesson.Size);

        var sheet = await LernkickerFactory.Json<SheetDto>(await b.GetAsync($"/api/library/quizzes/{sharedQuiz.Id}"));
        Assert.Equal((quiz.Title, 8), (sheet.Title, sheet.Exercises.Count));
        var poem = await LernkickerFactory.Json<LessonDto>(await b.GetAsync($"/api/library/lessons/{sharedLesson.Id}"));
        Assert.Equal(4, poem.Blocks.Count);

        var copied = await b.PostAsync($"/api/library/quizzes/{sharedQuiz.Id}/copy", null);
        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        var copy = await LernkickerFactory.Json<QuizOverview>(copied);
        Assert.Equal((quiz.QuizId, 1, false), (copy.QuizId, copy.Version, copy.Shared));
        Assert.Equal(HttpStatusCode.Created, (await b.PostAsync($"/api/library/lessons/{sharedLesson.Id}/copy", null)).StatusCode);
        Assert.All(await LibraryAsync(b), s => Assert.True(s.InMyLibrary));

        // Copying again brings a new version, like uploading the file again
        Assert.Equal(2, (await LernkickerFactory.Json<QuizOverview>(await b.PostAsync($"/api/library/quizzes/{sharedQuiz.Id}/copy", null))).Version);

        // Making it private again hides it; the copies stay with family B
        await a.PutAsJsonAsync($"/api/quizzes/{quiz.QuizId}/shared", new ShareRequest(false));
        await a.PutAsJsonAsync($"/api/lessons/{lesson.LessonId}/shared", new ShareRequest(false));
        Assert.Empty(await LibraryAsync(b));
        Assert.Equal(ErrorCodes.SharedNotFound, (await LernkickerFactory.Error(await b.GetAsync($"/api/library/quizzes/{sharedQuiz.Id}"), HttpStatusCode.NotFound)).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/library/lessons/{sharedLesson.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/library/quizzes/{sharedQuiz.Id}/copy", null)).StatusCode);
        Assert.Single(await LernkickerFactory.Json<List<QuizOverview>>(await b.GetAsync("/api/quizzes")));
        Assert.Single(await LernkickerFactory.Json<List<LessonOverview>>(await b.GetAsync("/api/lessons")));
    }

    [Fact]
    public async Task Private_material_and_own_material_cannot_be_reached_through_the_library()
    {
        var a = await factory.NewFamilyAsync("private-a@example.org");
        var b = await factory.NewFamilyAsync("private-b@example.org");
        var quiz = await UploadAsync<QuizOverview>(a, "quizzes", Lernkicker.Tests.Examples.HsuJson(), "hsu-" + Guid.NewGuid().ToString("N")[..8]);
        await a.PutAsJsonAsync($"/api/quizzes/{quiz.QuizId}/shared", new ShareRequest(true));
        var id = (await LibraryAsync(b)).Single().Id;

        // Family A cannot copy its own quiz through the library
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsync($"/api/library/quizzes/{id}/copy", null)).StatusCode);
        // A shared quiz id is not a lesson id
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/library/lessons/{id}")).StatusCode);
        // Sharing someone else's quiz is not possible
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/quizzes/{quiz.QuizId}/shared", new ShareRequest(false))).StatusCode);
        Assert.Single(await LibraryAsync(b));
        // Children and anonymous callers have no access
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Client().GetAsync("/api/library")).StatusCode);
    }

    [Fact]
    public async Task A_new_version_stays_shared()
    {
        var a = await factory.NewFamilyAsync("version-a@example.org");
        var b = await factory.NewFamilyAsync("version-b@example.org");
        var quiz = await UploadAsync<QuizOverview>(a, "quizzes", Lernkicker.Tests.Examples.HsuJson(), "hsu-" + Guid.NewGuid().ToString("N")[..8]);
        await a.PutAsJsonAsync($"/api/quizzes/{quiz.QuizId}/shared", new ShareRequest(true));
        var first = (await LibraryAsync(b)).Single();

        var second = await UploadAsync<QuizOverview>(a, "quizzes", Lernkicker.Tests.Examples.HsuJson(), quiz.QuizId);

        Assert.Equal((2, true), (second.Version, second.Shared));
        var entry = (await LibraryAsync(b)).Single();
        Assert.NotEqual(first.Id, entry.Id);
        Assert.Equal(first.SharedAt, entry.SharedAt);
    }

    // The tests share one server, so each test uploads under its own ids and only looks at those
    private readonly HashSet<string> _ids = [];

    private async Task<List<SharedOverview>> LibraryAsync(HttpClient parent) =>
        (await LernkickerFactory.Json<List<SharedOverview>>(await parent.GetAsync("/api/library"))).Where(s => _ids.Contains(s.ItemId)).ToList();

    private async Task<T> UploadAsync<T>(HttpClient parent, string kind, string json, string id)
    {
        var file = JsonNode.Parse(json)!.AsObject();
        file["id"] = id;
        _ids.Add(id);
        return await LernkickerFactory.Json<T>(await parent.PostAsync($"/api/{kind}", TestData.AsJson(file.ToJsonString())));
    }

    [Fact]
    public async Task Parents_preview_their_own_material()
    {
        var parent = await factory.NewFamilyAsync("preview@example.org");
        var quiz = await TestData.UploadHsuAsync(parent);
        var lesson = await TestData.UploadPoemAsync(parent);

        var sheet = await LernkickerFactory.Json<SheetDto>(await parent.GetAsync($"/api/quizzes/{quiz.QuizId}/preview"));
        var poem = await LernkickerFactory.Json<LessonDto>(await parent.GetAsync($"/api/lessons/{lesson.LessonId}/preview"));

        Assert.Equal(39, sheet.MaxPoints);
        Assert.Equal((Guid.Empty, "Gedicht: Septembermorgen"), (poem.AssignmentId, poem.Title));
        Assert.Equal(HttpStatusCode.NotFound, (await parent.GetAsync("/api/quizzes/nope/preview")).StatusCode);
    }
}
