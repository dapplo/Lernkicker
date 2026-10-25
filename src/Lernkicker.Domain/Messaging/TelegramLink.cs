using Lernkicker.Domain.Families;

namespace Lernkicker.Domain.Messaging;

/// <summary>A parent's Telegram chat: the bot sends results and reminders there and answers commands from it.</summary>
public sealed class TelegramLink : IFamilyOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ParentId { get; private set; }
    public long ChatId { get; private set; }
    public DateTimeOffset LinkedAt { get; private set; }

    public TelegramLink(Guid familyId, Guid parentId, long chatId, DateTimeOffset linkedAt)
    {
        FamilyId = familyId;
        ParentId = parentId;
        ChatId = chatId;
        LinkedAt = linkedAt;
    }
}

/// <summary>
/// A one-time code in the link <c>t.me/&lt;bot&gt;?start=&lt;code&gt;</c>. Only its hash is stored; it is valid for
/// <see cref="Lifetime"/> and used once.
/// </summary>
public sealed class TelegramLinkCode : IFamilyOwned
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ParentId { get; private set; }
    public string CodeHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public TelegramLinkCode(Guid familyId, Guid parentId, string codeHash, DateTimeOffset expiresAt)
    {
        FamilyId = familyId;
        ParentId = parentId;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
    }

    public bool IsValidAt(DateTimeOffset now) => now < ExpiresAt;
}
