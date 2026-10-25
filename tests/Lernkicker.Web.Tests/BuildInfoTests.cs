using System.Net;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>The admin sees which build runs, so an update on the NAS can be checked.</summary>
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
