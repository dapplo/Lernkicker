namespace Lernkicker.Domain.Families;

/// <summary>Everything belonging to one family carries its id; families never see each other's data.</summary>
public interface IFamilyOwned
{
    Guid FamilyId { get; }
}

public sealed class Family
{
    public const int MaxNameLength = 100;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Family(string name, DateTimeOffset createdAt)
    {
        Name = name.Trim();
        CreatedAt = createdAt;
    }
}

/// <summary>Invitation link for a new parent, created by the admin. Joins an existing family or creates a new one.</summary>
public sealed class Invitation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Only the hash of the code is stored; the code itself travels in the link.</summary>
    public string CodeHash { get; private set; }

    /// <summary>Set to join an existing family; null creates a new family named <see cref="NewFamilyName"/>.</summary>
    public Guid? FamilyId { get; private set; }

    public string? NewFamilyName { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RedeemedAt { get; private set; }

    public Invitation(string codeHash, Guid? familyId, string? newFamilyName, DateTimeOffset expiresAt)
    {
        CodeHash = codeHash;
        FamilyId = familyId;
        NewFamilyName = familyId is null ? newFamilyName?.Trim() : null;
        ExpiresAt = expiresAt;
    }

    public bool IsUsable(DateTimeOffset now) => RedeemedAt is null && ExpiresAt >= now;

    public void Redeem(DateTimeOffset now) => RedeemedAt = now;
}
