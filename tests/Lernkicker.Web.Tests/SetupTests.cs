using System.Net;
using System.Net.Http.Json;
using Lernkicker.Application;
using Lernkicker.Contracts;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Tests;

/// <summary>A fresh server: the first visitor inside the tailnet creates the admin, once.</summary>
public sealed class SetupTests
{
    private static SetupRequest Request(string? publicUrl = "https://lernkicker.example.ts.net/parents", string password = "admin-password-123") =>
        new("first@example.org", password, "Robin", "Familie Krom", "de", publicUrl);

    [Fact]
    public async Task The_first_visitor_becomes_admin()
    {
        using var server = new LernkickerFactory { SeedAdmin = false };
        var browser = server.Client();

        Assert.True((await LernkickerFactory.Json<SetupStatus>(await browser.GetAsync("/api/setup"))).Needed);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/api/setup", Request())).StatusCode);

        // Logged in as admin of the new family
        var me = await LernkickerFactory.Json<Me>(await browser.GetAsync("/api/account/me"));
        Assert.True(me.IsAdmin);
        Assert.Equal("Familie Krom", me.Family);
        Assert.False((await LernkickerFactory.Json<SetupStatus>(await browser.GetAsync("/api/setup"))).Needed);

        // Links now use the address from the browser, without a configured PublicUrl
        var invitation = await LernkickerFactory.Json<InvitationCreatedResponse>(await browser.PostAsJsonAsync("/api/admin/invitations", new NewInvitation(null, "Familie Huber")));
        Assert.StartsWith("https://lernkicker.example.ts.net/invitation#", invitation.Link);

        // Only once
        var again = await server.Client().PostAsJsonAsync("/api/setup", Request() with { Email = "second@example.org" });
        Assert.Equal(ErrorCodes.SetupDone, (await LernkickerFactory.Error(again, HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Not_through_funnel()
    {
        using var server = new LernkickerFactory { SeedAdmin = false };
        var viaFunnel = new HttpRequestMessage(HttpMethod.Post, "/api/setup") { Content = JsonContent.Create(Request()) };
        viaFunnel.Headers.Add(TailnetOnlyFilter.FunnelHeader, "?1");

        Assert.Equal(HttpStatusCode.NotFound, (await server.Client().SendAsync(viaFunnel)).StatusCode);
        Assert.True((await LernkickerFactory.Json<SetupStatus>(await server.Client().GetAsync("/api/setup"))).Needed);
    }

    [Fact]
    public async Task A_failed_setup_can_be_repeated()
    {
        using var server = new LernkickerFactory { SeedAdmin = false };
        var browser = server.Client();

        var weak = await browser.PostAsJsonAsync("/api/setup", Request(password: "short"));
        var noFamily = await browser.PostAsJsonAsync("/api/setup", Request() with { FamilyName = " " });

        Assert.StartsWith(ErrorCodes.AccountPrefix, (await LernkickerFactory.Error(weak, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(ErrorCodes.SetupFamilyInvalid, (await LernkickerFactory.Error(noFamily, HttpStatusCode.BadRequest)).Code);
        Assert.True((await LernkickerFactory.Json<SetupStatus>(await browser.GetAsync("/api/setup"))).Needed);

        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/api/setup", Request())).StatusCode);
        var families = await LernkickerFactory.Json<List<FamilyOverview>>(await browser.GetAsync("/api/admin/families"));
        Assert.Equal(["Familie Krom"], families.Select(f => f.Name));
    }

    [Fact]
    public async Task An_http_address_is_not_kept_for_links()
    {
        using var server = new LernkickerFactory { SeedAdmin = false };
        var browser = server.Client();

        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsJsonAsync("/api/setup", Request("http://nas.local:8085"))).StatusCode);

        var invitation = await LernkickerFactory.Json<InvitationCreatedResponse>(await browser.PostAsJsonAsync("/api/admin/invitations", new NewInvitation(null, "Familie Huber")));
        Assert.StartsWith("https://localhost/invitation#", invitation.Link);
    }

    [Fact]
    public async Task A_seeded_admin_means_no_setup()
    {
        using var server = new LernkickerFactory();

        Assert.False((await LernkickerFactory.Json<SetupStatus>(await server.Client().GetAsync("/api/setup"))).Needed);
        var setup = await server.Client().PostAsJsonAsync("/api/setup", Request());
        Assert.Equal(ErrorCodes.SetupDone, (await LernkickerFactory.Error(setup, HttpStatusCode.BadRequest)).Code);
    }
}
