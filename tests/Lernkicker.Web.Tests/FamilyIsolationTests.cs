using System.Net;
using System.Net.Http.Json;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>No family may read or change another family's data (docs/security.md, section 3).</summary>
public sealed class FamilyIsolationTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    [Fact]
    public async Task Other_family_sees_and_changes_nothing()
    {
        var a = await factory.NewFamilyAsync("a@example.org");
        var b = await factory.NewFamilyAsync("b@example.org");
        var quiz = await TestData.UploadHsuAsync(a);
        var child = await TestData.CreateChildAsync(a);
        var assignment = await TestData.AssignAsync(a, child.Id, quiz.QuizId);

        Assert.Empty(await LernkickerFactory.Json<List<QuizOverview>>(await b.GetAsync("/api/quizzes")));
        Assert.Empty(await LernkickerFactory.Json<List<ChildOverview>>(await b.GetAsync("/api/children")));
        Assert.Empty(await LernkickerFactory.Json<List<AssignmentOverview>>(await b.GetAsync($"/api/children/{child.Id}/assignments")));
        Assert.Empty(await LernkickerFactory.Json<List<AttemptOverview>>(await b.GetAsync($"/api/children/{child.Id}/attempts")));
        Assert.Empty(await LernkickerFactory.Json<List<DeviceOverview>>(await b.GetAsync($"/api/children/{child.Id}/devices")));

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/quizzes/{quiz.QuizId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/quizzes/{quiz.QuizId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/children/{child.Id}", new ChildDetails("Hacker", 4))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/children/{child.Id}/pin", new ChangePin("0000"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/children/{child.Id}/pairing", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/children/{child.Id}/assignments/{assignment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/children/{child.Id}")).StatusCode);

        // Family B assigning its own copy of the quiz to A's child fails as well
        await TestData.UploadHsuAsync(b);
        Assert.Equal(HttpStatusCode.NotFound,
            (await b.PostAsJsonAsync($"/api/children/{child.Id}/assignments", new NewAssignment(quiz.QuizId, null))).StatusCode);

        // A still has everything
        Assert.Single(await LernkickerFactory.Json<List<ChildOverview>>(await a.GetAsync("/api/children")));
        Assert.Single(await LernkickerFactory.Json<List<AssignmentOverview>>(await a.GetAsync($"/api/children/{child.Id}/assignments")));
    }

    [Fact]
    public async Task Child_of_one_family_sees_no_other_assignments()
    {
        var a = await factory.NewFamilyAsync("c@example.org");
        var b = await factory.NewFamilyAsync("d@example.org");
        var quiz = await TestData.UploadHsuAsync(a);
        var childA = await TestData.CreateChildAsync(a);
        var assignment = await TestData.AssignAsync(a, childA.Id, quiz.QuizId);

        var childB = await TestData.CreateChildAsync(b, "Mia", "4321");
        var phoneB = await TestData.ChildPhoneAsync(factory, b, childB.Id, "4321");

        Assert.Empty(await LernkickerFactory.Json<List<MyAssignment>>(await phoneB.GetAsync("/api/device/assignments")));
        Assert.Equal(HttpStatusCode.NotFound, (await phoneB.GetAsync($"/api/device/assignments/{assignment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await phoneB.PostAsJsonAsync($"/api/device/assignments/{assignment.Id}/submit", new Submission([]))).StatusCode);
    }
}
