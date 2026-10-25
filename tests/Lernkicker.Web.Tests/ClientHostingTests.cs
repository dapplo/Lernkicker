using System.Net;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Tests;

/// <summary>The server hosts the PWA: pages are index.html, framework files are served, unknown API paths stay 404.</summary>
public class ClientHostingTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/parents/children/0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("/pair")]
    public async Task Pages_are_the_app_with_a_strict_content_security_policy(string path)
    {
        var response = await factory.Client().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("_framework/blazor.webassembly.js", await response.Content.ReadAsStringAsync());
        Assert.Equal(SecurityHeaders.Policy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Index_html_has_no_inline_scripts_so_the_policy_needs_no_exceptions()
    {
        var html = await factory.Client().GetStringAsync("/");

        Assert.DoesNotMatch(@"<script(?![^>]*\ssrc=)[^>]*>", html);
        Assert.DoesNotContain(" style=", html);
    }

    [Theory]
    [InlineData("/_framework/blazor.webassembly.js")]
    [InlineData("/js/start.js")]
    [InlineData("/css/app.css")]
    public async Task Client_files_are_served(string path)
    {
        var response = await factory.Client().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_paths_are_not_the_app()
    {
        var response = await factory.Client().GetAsync("/api/does-not-exist");

        var error = await LernkickerFactory.Error(response, HttpStatusCode.NotFound);
        Assert.Equal("notFound", error.Code);
    }
}
