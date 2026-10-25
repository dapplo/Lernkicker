namespace Lernkicker.Domain.Settings;

/// <summary>A setting of the whole installation that the app keeps itself, such as the public address from the first-run setup.</summary>
public sealed class ServerSetting
{
    public const string PublicUrl = "publicUrl";

    public string Name { get; private set; }
    public string Value { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public ServerSetting(string name, string value, DateTimeOffset updatedAt)
    {
        Name = name;
        Value = value;
        UpdatedAt = updatedAt;
    }

    public void Change(string value, DateTimeOffset at)
    {
        Value = value;
        UpdatedAt = at;
    }
}
