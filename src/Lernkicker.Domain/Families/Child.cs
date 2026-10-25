namespace Lernkicker.Domain.Families;

public sealed class Child : IFamilyOwned
{
    public const int MaxNameLength = 50;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }

    /// <summary>First name or nickname only (data minimisation, see docs/security.md).</summary>
    public string Name { get; private set; }

    public int? GradeLevel { get; private set; }
    public string? PinHash { get; private set; }

    public Child(Guid familyId, string name, int? gradeLevel)
    {
        FamilyId = familyId;
        Name = name.Trim();
        GradeLevel = gradeLevel;
    }

    public static bool IsValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= MaxNameLength;

    public static bool IsValidGradeLevel(int? gradeLevel) => gradeLevel is null or (>= 1 and <= 13);

    public void Update(string name, int? gradeLevel)
    {
        Name = name.Trim();
        GradeLevel = gradeLevel;
    }

    public void SetPinHash(string pinHash) => PinHash = pinHash;
}

/// <summary>A child's PIN: exactly four digits. It only works on a paired device.</summary>
public static class Pin
{
    public static bool IsValid(string? pin) => pin is { Length: 4 } && pin.All(char.IsAsciiDigit);
}

/// <summary>A child's phone paired via QR code.</summary>
public sealed class ChildDevice : IFamilyOwned
{
    public const int MaxWrongPins = 5;
    public const int MaxLabelLength = 100;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ChildId { get; private set; }

    /// <summary>SHA-256 of the device token stored in the device's cookie.</summary>
    public string TokenHash { get; private set; }

    public string Label { get; private set; }
    public DateTimeOffset PairedAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public int WrongPins { get; private set; }
    public bool Locked { get; private set; }

    public ChildDevice(Guid familyId, Guid childId, string tokenHash, string label, DateTimeOffset pairedAt)
    {
        FamilyId = familyId;
        ChildId = childId;
        TokenHash = tokenHash;
        Label = label;
        PairedAt = pairedAt;
    }

    public static string CleanLabel(string? label, string fallback)
    {
        var trimmed = label?.Trim();
        return string.IsNullOrEmpty(trimmed) ? fallback : trimmed[..Math.Min(trimmed.Length, MaxLabelLength)];
    }

    /// <summary>Counts a wrong PIN; the device locks after <see cref="MaxWrongPins"/> until a parent unlocks it.</summary>
    public void RecordWrongPin()
    {
        WrongPins++;
        Locked = WrongPins >= MaxWrongPins;
    }

    public void RecordLogin(DateTimeOffset now)
    {
        WrongPins = 0;
        LastUsedAt = now;
    }

    public void Unlock()
    {
        WrongPins = 0;
        Locked = false;
    }
}

/// <summary>One-time code a parent shows as QR code to pair a child's device.</summary>
public sealed class PairingCode : IFamilyOwned
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ChildId { get; private set; }
    public string CodeHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public PairingCode(Guid familyId, Guid childId, string codeHash, DateTimeOffset expiresAt)
    {
        FamilyId = familyId;
        ChildId = childId;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
    }

    public bool IsValid(DateTimeOffset now) => ExpiresAt >= now;
}
