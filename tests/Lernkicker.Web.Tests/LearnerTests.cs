using System.Net;
using System.Net.Http.Json;
using Lernkicker.Application;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>A parent learning with their own adult profile, logged in through the parent login instead of a device.</summary>
public sealed class LearnerTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    [Fact]
    public async Task Parent_learns_without_pairing_or_PIN()
    {
        var parent = await factory.NewFamilyAsync("learner@example.org");
        var quiz = await TestData.UploadHsuAsync(parent);

        Assert.Equal(HttpStatusCode.NoContent, (await parent.PostAsync("/api/children/learn", null)).StatusCode);

        var status = await LernkickerFactory.Json<DeviceStatusResponse>(await parent.GetAsync("/api/device/status"));
        Assert.True(status is { Paired: true, Learner: true, ChildName: "learner@example.org" });
        var own = Assert.Single(await LernkickerFactory.Json<List<ChildOverview>>(await parent.GetAsync("/api/children")));
        Assert.True(own is { IsAdult: true, Mine: true, OtherLearner: false, HasPin: false });

        await TestData.AssignAsync(parent, own.Id, quiz.QuizId);
        var assignment = Assert.Single(await LernkickerFactory.Json<List<MyAssignment>>(await parent.GetAsync("/api/device/assignments")));
        Assert.Equal(quiz.Title, assignment.Title);

        // A second time opens the same profile
        Assert.Equal(HttpStatusCode.NoContent, (await parent.PostAsync("/api/children/learn", null)).StatusCode);
        Assert.Single(await LernkickerFactory.Json<List<ChildOverview>>(await parent.GetAsync("/api/children")));

        // Without the parent login the learning session is gone too
        await parent.PostAsync("/api/account/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await parent.GetAsync("/api/device/assignments")).StatusCode);
        Assert.False((await LernkickerFactory.Json<DeviceStatusResponse>(await parent.GetAsync("/api/device/status"))).Paired);
    }

    [Fact]
    public async Task Only_a_free_adult_profile_can_become_a_parents_own()
    {
        var parent = await factory.NewFamilyAsync("own@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var adult = await LernkickerFactory.Json<ChildOverview>(await parent.PostAsJsonAsync("/api/children", new NewChild("Mum", null, "1234", IsAdult: true)));

        Assert.Equal(ErrorCodes.ChildNotOwnProfile,
            (await LernkickerFactory.Error(await parent.PostAsync($"/api/children/{child.Id}/learn", null), HttpStatusCode.BadRequest)).Code);
        Assert.Equal(HttpStatusCode.NoContent, (await parent.PostAsync($"/api/children/{adult.Id}/learn", null)).StatusCode);
        Assert.Equal("Mum", (await LernkickerFactory.Json<DeviceStatusResponse>(await parent.GetAsync("/api/device/status"))).ChildName);

        // Learn myself now opens that profile instead of creating one
        Assert.Equal(HttpStatusCode.NoContent, (await parent.PostAsync("/api/children/learn", null)).StatusCode);
        Assert.Equal(2, (await LernkickerFactory.Json<List<ChildOverview>>(await parent.GetAsync("/api/children"))).Count);

        // Another family does not see it at all
        var stranger = await factory.NewFamilyAsync("stranger-own@example.org");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/children/{adult.Id}/learn", null)).StatusCode);
    }

    [Fact]
    public async Task A_child_phone_is_not_affected()
    {
        var parent = await factory.NewFamilyAsync("phone-learner@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);

        var status = await LernkickerFactory.Json<DeviceStatusResponse>(await phone.GetAsync("/api/device/status"));
        Assert.True(status is { Paired: true, Learner: false, ChildName: "Calvin" });
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.PostAsync("/api/children/learn", null)).StatusCode);
    }
}
