namespace Lernkicker.Domain.Families;

/// <summary>
/// A key a parent creates so a program (e.g. an AI assistant over MCP) can work with the family's quizzes,
/// lessons, assignments and calendar without a login. Only the hash of the key is stored; the key itself is
/// shown once. A read-only key can look at everything the parent sees but change nothing.
/// </summary>
public sealed class ApiKey : IFamilyOwned
{
    public const int MaxNameLength = 60;

    /// <summary>Most keys a family may have at once.</summary>
    public const int MaxPerFamily = 10;

    /// <summary>Every key starts with this, so leaked keys are easy to recognise (e.g. by secret scanners).</summary>
    public const string Prefix = "lk_";

    /// <summary>How often the last use is written at most, so a busy assistant doesn't write on every request.</summary>
    public static readonly TimeSpan UsageResolution = TimeSpan.FromMinutes(1);

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }

    /// <summary>The parent who created the key; deleting the parent removes it.</summary>
    public Guid ParentId { get; private set; }

    public string Name { get; private set; }
    public string TokenHash { get; private set; }

    /// <summary>The last characters of the key, so parents can tell their keys apart.</summary>
    public string Hint { get; private set; }

    public bool ReadOnly { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    public ApiKey(Guid familyId, Guid parentId, string name, string tokenHash, string hint, bool readOnly, DateTimeOffset createdAt)
    {
        FamilyId = familyId;
        ParentId = parentId;
        Name = name.Trim();
        TokenHash = tokenHash;
        Hint = hint;
        ReadOnly = readOnly;
        CreatedAt = createdAt;
    }

    public static bool IsValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= MaxNameLength;

    /// <summary>Notes a use; true when that changed the stored time.</summary>
    public bool Used(DateTimeOffset at)
    {
        if (LastUsedAt is { } last && at - last < UsageResolution)
        {
            return false;
        }

        LastUsedAt = at;
        return true;
    }
}
