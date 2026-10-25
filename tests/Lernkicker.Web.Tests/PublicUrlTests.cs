using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>Behind a proxy in another container (TSDProxy) links come from Lernkicker:PublicUrl.</summary>
public sealed class PublicUrlTests : IDisposable
{
    private readonly LernkickerFactory _factory = new() { PublicUrl = "https://lernkicker.example.ts.net" };

    [Fact]
    public async Task Links_use_the_public_url()
    {
        var parent = await _factory.NewFamilyAsync("public-url@example.org");
        var child = await TestData.CreateChildAsync(parent);

        var pairing = await LernkickerFactory.Json<PairingCodeResponse>(await parent.PostAsync($"/api/children/{child.Id}/pairing", null));
        var feed = await LernkickerFactory.Json<FeedResponse>(await parent.PostAsync($"/api/children/{child.Id}/feed", null));

        Assert.StartsWith("https://lernkicker.example.ts.net/pair#", pairing.Link);
        Assert.StartsWith("https://lernkicker.example.ts.net/calendar/", feed.Link);
    }

    public void Dispose() => _factory.Dispose();
}
