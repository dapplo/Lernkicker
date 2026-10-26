using System.Net;
using System.Net.Http.Json;
using Lernkicker.Application;
using Lernkicker.Contracts;
using Lernkicker.Domain.Families;
using Lernkicker.Domain.Quizzes;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Tests;

public sealed class FlowTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    [Fact]
    public async Task Admin_is_created_on_start()
    {
        var admin = await factory.AdminAsync();
        var me = await LernkickerFactory.Json<Me>(await admin.GetAsync("/api/account/me"));

        Assert.Equal("Robin", me.Name);
        Assert.Equal("Family Admin", me.Family);
        Assert.True(me.IsAdmin);
        Assert.Equal("de", me.Language);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_with_a_code()
    {
        var response = await factory.Client().PostAsJsonAsync("/api/account/login", new Login(LernkickerFactory.AdminEmail, "wrong-wrong-wrong"));

        Assert.Equal(ErrorCodes.LoginFailed, (await LernkickerFactory.Error(response, HttpStatusCode.Unauthorized)).Code);
    }

    [Fact]
    public async Task Invitation_works_only_once_and_keeps_the_language()
    {
        var admin = await factory.AdminAsync();
        var invitation = await LernkickerFactory.Json<InvitationCreatedResponse>(await admin.PostAsJsonAsync("/api/admin/invitations", new NewInvitation(null, "Family X")));
        Assert.Contains("/invitation#", invitation.Link);

        var first = factory.Client();
        var firstResponse = await first.PostAsJsonAsync("/api/account/invitation", new RedeemInvitationRequest(invitation.Code, "x1@example.org", "long-password-1", "X1", true, "en"));
        var second = await factory.Client().PostAsJsonAsync("/api/account/invitation", new RedeemInvitationRequest(invitation.Code, "x2@example.org", "long-password-2", "X2", true, "en"));

        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        Assert.Equal(ErrorCodes.InvitationInvalid, (await LernkickerFactory.Error(second, HttpStatusCode.BadRequest)).Code);
        var me = await LernkickerFactory.Json<Me>(await first.GetAsync("/api/account/me"));
        Assert.Equal(("Family X", "en"), (me.Family, me.Language));
    }

    [Fact]
    public async Task Invitation_needs_consent_and_a_long_password()
    {
        var admin = await factory.AdminAsync();
        var invitation = await LernkickerFactory.Json<InvitationCreatedResponse>(await admin.PostAsJsonAsync("/api/admin/invitations", new NewInvitation(null, null)));

        var noConsent = await factory.Client().PostAsJsonAsync("/api/account/invitation", new RedeemInvitationRequest(invitation.Code, "y@example.org", "long-password-1", "Y", false, null));
        var shortPassword = await factory.Client().PostAsJsonAsync("/api/account/invitation", new RedeemInvitationRequest(invitation.Code, "y@example.org", "short", "Y", true, null));

        Assert.Equal(ErrorCodes.ConsentRequired, (await LernkickerFactory.Error(noConsent, HttpStatusCode.BadRequest)).Code);
        Assert.Equal("account.PasswordTooShort", (await LernkickerFactory.Error(shortPassword, HttpStatusCode.BadRequest)).Code);

        // The failed attempt left no family behind and the invitation still works
        var families = await LernkickerFactory.Json<List<FamilyOverview>>(await admin.GetAsync("/api/admin/families"));
        Assert.DoesNotContain(families, f => f.Name == "Y");
        var ok = await factory.Client().PostAsJsonAsync("/api/account/invitation", new RedeemInvitationRequest(invitation.Code, "y@example.org", "long-password-1", "Y", true, null));
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
    }

    [Fact]
    public async Task Parent_can_change_the_language()
    {
        var parent = await factory.NewFamilyAsync("lang@example.org");

        Assert.Equal(HttpStatusCode.NoContent, (await parent.PutAsJsonAsync("/api/account/language", new SetLanguage("de"))).StatusCode);
        Assert.Equal("de", (await LernkickerFactory.Json<Me>(await parent.GetAsync("/api/account/me"))).Language);
        Assert.Equal(ErrorCodes.LanguageInvalid,
            (await LernkickerFactory.Error(await parent.PutAsJsonAsync("/api/account/language", new SetLanguage("xx")), HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Admin_area_is_closed_via_funnel_and_for_parents()
    {
        var admin = await factory.AdminAsync();
        var viaFunnel = new HttpRequestMessage(HttpMethod.Get, "/api/admin/families");
        viaFunnel.Headers.Add(TailnetOnlyFilter.FunnelHeader, "?1");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.SendAsync(viaFunnel)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/families")).StatusCode);

        var parent = await factory.NewFamilyAsync("e@example.org");
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync("/api/admin/families")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Client().GetAsync("/api/admin/families")).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_delete_the_own_family()
    {
        var admin = await factory.AdminAsync();
        var own = (await LernkickerFactory.Json<List<FamilyOverview>>(await admin.GetAsync("/api/admin/families"))).Single(f => f.Name == "Family Admin");

        Assert.Equal(ErrorCodes.FamilyIsOwn, (await LernkickerFactory.Error(await admin.DeleteAsync($"/api/admin/families/{own.Id}"), HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Invalid_quiz_is_rejected_with_issues()
    {
        var parent = await factory.NewFamilyAsync("f@example.org");

        var broken = await parent.PostAsync("/api/quizzes", TestData.AsJson(TestData.HsuJson().Replace("\"science\"", "\"physics\"")));
        var tooLarge = await parent.PostAsync("/api/quizzes", TestData.AsJson(new string(' ', QuizLimits.MaxBytes + 10)));

        var error = await LernkickerFactory.Error(broken, HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.QuizInvalid, error.Code);
        var issue = Assert.Single(error.Issues!);
        Assert.Equal((QuizIssueCodes.Schema, "subject"), (issue.Code, issue.Path));
        Assert.Equal(ErrorCodes.QuizTooLarge, (await LernkickerFactory.Error(tooLarge, HttpStatusCode.RequestEntityTooLarge)).Code);
    }

    [Fact]
    public async Task Child_practises_and_parents_see_the_result()
    {
        var parent = await factory.NewFamilyAsync("g@example.org");
        var quiz = await TestData.UploadHsuAsync(parent);
        var child = await TestData.CreateChildAsync(parent);
        await TestData.AssignAsync(parent, child.Id, quiz.QuizId, new DateOnly(2026, 11, 3));
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);

        var assignment = Assert.Single(await LernkickerFactory.Json<List<MyAssignment>>(await phone.GetAsync("/api/device/assignments")));
        Assert.Equal((0, "science", new DateOnly(2026, 11, 3)), (assignment.Stars, assignment.Subject, assignment.DueOn!.Value));

        var sheetJson = await (await phone.GetAsync($"/api/device/assignments/{assignment.AssignmentId}")).Content.ReadAsStringAsync();
        // The phone never receives solutions
        Assert.DoesNotContain("\"answers\"", sheetJson);
        Assert.DoesNotContain("\"correct\"", sheetJson);
        Assert.DoesNotContain("\"isTrue\"", sheetJson);
        Assert.DoesNotContain("explanation", sheetJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Getreide wird im Sommer geerntet", sheetJson);
        var sheet = System.Text.Json.JsonSerializer.Deserialize<SheetDto>(sheetJson, ApiJson.Options)!;
        Assert.Equal((8, "de"), (sheet.Exercises.Count, sheet.Language));

        var result = await LernkickerFactory.Json<QuizResultDto>(await phone.PostAsJsonAsync(
            $"/api/device/assignments/{assignment.AssignmentId}/submit", new Submission(TestData.AllCorrect(Lernkicker.Tests.Examples.Hsu())), ApiJson.Options));
        Assert.Equal((39, 1), (result.Points, result.Mark));

        var after = Assert.Single(await LernkickerFactory.Json<List<MyAssignment>>(await phone.GetAsync("/api/device/assignments")));
        Assert.Equal((3, 1), (after.Stars, after.Attempts));

        var attempt = Assert.Single(await LernkickerFactory.Json<List<AttemptOverview>>(await parent.GetAsync($"/api/children/{child.Id}/attempts")));
        Assert.Equal(100, attempt.Percent);
        var details = await LernkickerFactory.Json<QuizResultDto>(await parent.GetAsync($"/api/children/{child.Id}/attempts/{attempt.Id}"));
        Assert.Equal(8, details.Exercises.Count);
        Assert.Equal(true, details.Exercises.Single(e => e.Type == "trueFalse").Parts[1].ExpectedTruth);
    }

    [Fact]
    public async Task Adult_learns_material_without_grade_and_gets_no_mark()
    {
        var parent = await factory.NewFamilyAsync("adult@example.org");
        var json = System.Text.Json.Nodes.JsonNode.Parse(TestData.HsuJson())!.AsObject();
        json.Remove("gradeLevel");
        var quiz = await LernkickerFactory.Json<QuizOverview>(await parent.PostAsync("/api/quizzes", TestData.AsJson(json.ToJsonString())));
        Assert.Null(quiz.GradeLevel);

        Assert.Equal(ErrorCodes.GradeLevelInvalid,
            (await LernkickerFactory.Error(await parent.PostAsJsonAsync("/api/children", new NewChild("Robin", 4, "1234", IsAdult: true)), HttpStatusCode.BadRequest)).Code);
        var adult = await LernkickerFactory.Json<ChildOverview>(await parent.PostAsJsonAsync("/api/children", new NewChild("Robin", null, "1234", IsAdult: true)));
        Assert.True(adult.IsAdult);
        await TestData.AssignAsync(parent, adult.Id, quiz.QuizId);
        var phone = await TestData.ChildPhoneAsync(factory, parent, adult.Id);

        var assignment = Assert.Single(await LernkickerFactory.Json<List<MyAssignment>>(await phone.GetAsync("/api/device/assignments")));
        var result = await LernkickerFactory.Json<QuizResultDto>(await phone.PostAsJsonAsync(
            $"/api/device/assignments/{assignment.AssignmentId}/submit", new Submission(TestData.AllCorrect(Lernkicker.Tests.Examples.Hsu())), ApiJson.Options));
        Assert.Equal((39, 100, (int?)null), (result.Points, result.Percent, result.Mark));

        var attempt = Assert.Single(await LernkickerFactory.Json<List<AttemptOverview>>(await parent.GetAsync($"/api/children/{adult.Id}/attempts")));
        Assert.Null(attempt.Mark);

        // Back to a child: a grade level is allowed again
        Assert.Equal(HttpStatusCode.NoContent, (await parent.PutAsJsonAsync($"/api/children/{adult.Id}", new ChildDetails("Robin", 4))).StatusCode);
        Assert.False(Assert.Single(await LernkickerFactory.Json<List<ChildOverview>>(await parent.GetAsync("/api/children"))).IsAdult);
    }

    [Fact]
    public async Task Roles_are_separate()
    {
        var parent = await factory.NewFamilyAsync("h@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/children")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/quizzes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await parent.GetAsync("/api/device/assignments")).StatusCode);
    }

    [Fact]
    public async Task Pin_only_works_on_a_paired_phone_and_locks_after_five_errors()
    {
        var parent = await factory.NewFamilyAsync("i@example.org");
        var child = await TestData.CreateChildAsync(parent);

        var strangerPhone = factory.Client();
        Assert.Equal(ErrorCodes.DeviceNotPaired,
            (await LernkickerFactory.Error(await strangerPhone.PostAsJsonAsync("/api/device/login", new PinLogin("1234")), HttpStatusCode.Unauthorized)).Code);

        var pairing = await LernkickerFactory.Json<PairingCodeResponse>(await parent.PostAsync($"/api/children/{child.Id}/pairing", null));
        Assert.Contains("/pair#", pairing.Link);
        var phone = factory.Client();
        var status = await LernkickerFactory.Json<DeviceStatusResponse>(await phone.PostAsJsonAsync("/api/device/pair", new PairDevice(pairing.Code.ToLowerInvariant(), null)));
        Assert.Equal("Calvin", status.ChildName);

        // The code works only once
        Assert.Equal(ErrorCodes.PairingCodeInvalid,
            (await LernkickerFactory.Error(await factory.Client().PostAsJsonAsync("/api/device/pair", new PairDevice(pairing.Code, null)), HttpStatusCode.BadRequest)).Code);

        for (var i = 1; i < ChildDevice.MaxWrongPins; i++)
        {
            Assert.Equal(ErrorCodes.PinWrong, (await LernkickerFactory.Error(await phone.PostAsJsonAsync("/api/device/login", new PinLogin("0000")), HttpStatusCode.Unauthorized)).Code);
        }

        Assert.Equal(ErrorCodes.DeviceLocked, (await LernkickerFactory.Error(await phone.PostAsJsonAsync("/api/device/login", new PinLogin("0000")), HttpStatusCode.Locked)).Code);
        Assert.Equal(HttpStatusCode.Locked, (await phone.PostAsJsonAsync("/api/device/login", new PinLogin("1234"))).StatusCode);

        var device = Assert.Single(await LernkickerFactory.Json<List<DeviceOverview>>(await parent.GetAsync($"/api/children/{child.Id}/devices")));
        Assert.True(device.Locked);
        Assert.Equal("Phone", device.Label);
        Assert.Equal(HttpStatusCode.NoContent, (await parent.PostAsync($"/api/children/{child.Id}/devices/{device.Id}/unlock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await phone.PostAsJsonAsync("/api/device/login", new PinLogin("1234"))).StatusCode);
    }

    [Fact]
    public async Task Removed_phone_is_logged_out_at_once()
    {
        var parent = await factory.NewFamilyAsync("j@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/device/assignments")).StatusCode);

        var device = Assert.Single(await LernkickerFactory.Json<List<DeviceOverview>>(await parent.GetAsync($"/api/children/{child.Id}/devices")));
        await parent.DeleteAsync($"/api/children/{child.Id}/devices/{device.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/device/assignments")).StatusCode);
    }

    [Fact]
    public async Task New_version_takes_over_assignments_and_delete_archives_with_results()
    {
        var parent = await factory.NewFamilyAsync("k@example.org");
        var v1 = await TestData.UploadHsuAsync(parent);
        var child = await TestData.CreateChildAsync(parent);
        await TestData.AssignAsync(parent, child.Id, v1.QuizId);
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);
        var assignment = Assert.Single(await LernkickerFactory.Json<List<MyAssignment>>(await phone.GetAsync("/api/device/assignments")));
        await phone.PostAsJsonAsync($"/api/device/assignments/{assignment.AssignmentId}/submit", new Submission([]), ApiJson.Options);

        var v2 = await LernkickerFactory.Json<QuizOverview>(
            await parent.PostAsync("/api/quizzes", TestData.AsJson(TestData.HsuJson().Replace("Vom Korn zum Brot & Wahlen", "Korn und Wahlen"))));
        Assert.Equal(2, v2.Version);
        var listed = Assert.Single(await LernkickerFactory.Json<List<QuizOverview>>(await parent.GetAsync("/api/quizzes")));
        Assert.Equal("Korn und Wahlen", listed.Title);
        var moved = Assert.Single(await LernkickerFactory.Json<List<AssignmentOverview>>(await parent.GetAsync($"/api/children/{child.Id}/assignments")));
        Assert.Equal(2, moved.Version);
        // Progress counts over all versions
        Assert.Equal(1, Assert.Single(await LernkickerFactory.Json<List<MyAssignment>>(await phone.GetAsync("/api/device/assignments"))).Attempts);

        var deleted = await LernkickerFactory.Json<QuizDeletedResponse>(await parent.DeleteAsync($"/api/quizzes/{v1.QuizId}"));
        Assert.True(deleted.Archived);
        Assert.Empty(await LernkickerFactory.Json<List<QuizOverview>>(await parent.GetAsync("/api/quizzes")));
        Assert.Empty(await LernkickerFactory.Json<List<AssignmentOverview>>(await parent.GetAsync($"/api/children/{child.Id}/assignments")));
        Assert.Single(await LernkickerFactory.Json<List<AttemptOverview>>(await parent.GetAsync($"/api/children/{child.Id}/attempts")));
    }

    [Fact]
    public async Task Quiz_without_results_is_removed_completely()
    {
        var parent = await factory.NewFamilyAsync("l@example.org");
        var quiz = await TestData.UploadHsuAsync(parent);

        var deleted = await LernkickerFactory.Json<QuizDeletedResponse>(await parent.DeleteAsync($"/api/quizzes/{quiz.QuizId}"));

        Assert.False(deleted.Archived);
        Assert.Equal(HttpStatusCode.NotFound, (await parent.GetAsync($"/api/quizzes/{quiz.QuizId}")).StatusCode);
        // Uploading again starts over at version 1
        Assert.Equal(1, (await TestData.UploadHsuAsync(parent)).Version);
    }

    [Fact]
    public async Task Child_validation_returns_codes()
    {
        var parent = await factory.NewFamilyAsync("m@example.org");

        Assert.Equal(ErrorCodes.ChildNameInvalid,
            (await LernkickerFactory.Error(await parent.PostAsJsonAsync("/api/children", new NewChild(" ", 4, "1234")), HttpStatusCode.BadRequest)).Code);
        Assert.Equal(ErrorCodes.GradeLevelInvalid,
            (await LernkickerFactory.Error(await parent.PostAsJsonAsync("/api/children", new NewChild("Calvin", 14, "1234")), HttpStatusCode.BadRequest)).Code);
        Assert.Equal(ErrorCodes.PinInvalid,
            (await LernkickerFactory.Error(await parent.PostAsJsonAsync("/api/children", new NewChild("Calvin", 4, "12")), HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Unknown_api_paths_are_404_with_a_code()
    {
        var error = await LernkickerFactory.Error(await factory.Client().GetAsync("/api/does-not-exist"), HttpStatusCode.NotFound);

        Assert.Equal(ErrorCodes.NotFound, error.Code);
    }
}

public sealed class RateLimitTests
{
    [Fact]
    public async Task Too_many_login_attempts_are_throttled()
    {
        await using var factory = new LernkickerFactory { LimitPerMinute = 3 };
        var client = factory.Client();

        var status = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            status.Add((await client.PostAsJsonAsync("/api/account/login", new Login("x@example.org", "wrong-wrong-wrong"))).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], status);
    }
}
