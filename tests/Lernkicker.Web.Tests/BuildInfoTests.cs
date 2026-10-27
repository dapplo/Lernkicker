using System.Net;
using Lernkicker.Client.Services;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>The admin sees which build runs, so an update on the NAS can be checked; the app compares it with its own.</summary>
public sealed class BuildInfoTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    [Fact]
    public async Task Admin_sees_the_server_version()
    {
        var admin = await factory.AdminAsync();

        var build = await LernkickerFactory.Json<BuildInfoDto>(await admin.GetAsync("/api/admin/version"));

        Assert.Equal(BuildInfoDto.Of(typeof(Program).Assembly), build);
        Assert.False(string.IsNullOrEmpty(build.Version));
    }

    [Fact]
    public async Task Version_is_only_for_the_admin()
    {
        var parent = await factory.NewFamilyAsync("version@example.org");
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync("/api/admin/version")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Client().GetAsync("/api/admin/version")).StatusCode);
    }

    [Theory]
    [InlineData("/api/status")]
    [InlineData("/api/account/me")]
    [InlineData("/")]
    public async Task Every_answer_names_the_build_even_without_login(string path)
    {
        var response = await factory.Client().GetAsync(path);

        Assert.Equal(BuildInfoDto.Of(typeof(Program).Assembly).Id, response.Headers.GetValues(BuildInfoDto.Header).Single());
    }

    [Fact]
    public void Server_and_app_are_the_same_build() =>
        Assert.Equal(BuildInfoDto.Of(typeof(Program).Assembly).Id, AppUpdate.Current.Id);

    [Fact]
    public void The_id_tells_builds_apart_without_naming_them()
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(1761428400);
        var build = new BuildInfoDto("main", "abc", at);

        Assert.Matches("^[0-9a-f]{16}$", build.Id);
        Assert.DoesNotContain("main", build.Id);
        Assert.Equal(build.Id, new BuildInfoDto("main", "abc", at).Id);
        Assert.NotEqual(build.Id, new BuildInfoDto("main", "abd", at).Id);
        Assert.NotEqual(build.Id, new BuildInfoDto("main", "abc", at.AddMinutes(5)).Id);
        Assert.NotEqual(build.Id, new BuildInfoDto("1.2.0", "abc", at).Id);
    }

    [Fact]
    public void The_id_is_not_part_of_the_json() =>
        Assert.DoesNotContain("\"id\"", System.Text.Json.JsonSerializer.Serialize(new BuildInfoDto("main", "abc", null), ApiJson.Options));

    [Theory]
    [InlineData("practice/0f8fad5b-d9cb-469f-a165-70867728950e", true)]
    [InlineData("learn/0f8fad5b-d9cb-469f-a165-70867728950e/play/memory", true)]
    [InlineData("learn/0f8fad5b-d9cb-469f-a165-70867728950e/play/crossword?x=1", true)]
    [InlineData("learn/0f8fad5b-d9cb-469f-a165-70867728950e", false)]
    [InlineData("", false)]
    [InlineData("parents/quizzes", false)]
    [InlineData("play/0f8fad5b-d9cb-469f-a165-70867728950e", false)]
    public void A_new_version_waits_while_answers_are_only_on_the_page(string path, bool busy) =>
        Assert.Equal(busy, AppUpdate.IsBusy(path));

    [Theory]
    [InlineData("1.2.0+8f86d6d832f6bdd732e008a9d4fb05d0257c5889", "2026-10-25T21:40:00.000Z", "1.2.0", "8f86d6d832f6bdd732e008a9d4fb05d0257c5889", true)]
    [InlineData("main+abc", null, "main", "abc", false)]
    [InlineData("dev", "", "dev", null, false)]
    [InlineData("dev+", "not a date", "dev", null, false)]
    [InlineData(null, null, "dev", null, false)]
    public void Parses_what_the_build_stamped(string? informational, string? date, string version, string? commit, bool hasDate)
    {
        var build = BuildInfoDto.Parse(informational, date);

        Assert.Equal(version, build.Version);
        Assert.Equal(commit, build.Commit);
        Assert.Equal(hasDate, build.BuiltAt is not null);
    }
}
