using Lernkicker.Domain.Messaging;

namespace Lernkicker.Domain.Tests;

public class TelegramLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_link_code_is_valid_until_it_expires()
    {
        var code = new TelegramLinkCode(Guid.NewGuid(), Guid.NewGuid(), "hash", Now + TelegramLinkCode.Lifetime);

        Assert.True(code.IsValidAt(Now));
        Assert.True(code.IsValidAt(Now + TelegramLinkCode.Lifetime - TimeSpan.FromSeconds(1)));
        Assert.False(code.IsValidAt(Now + TelegramLinkCode.Lifetime));
    }
}
