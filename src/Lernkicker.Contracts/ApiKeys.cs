namespace Lernkicker.Contracts;

/// <summary>An API key as the parents' page lists it: <c>Hint</c> is the end of the key, the key itself is never returned again.</summary>
public sealed record ApiKeyOverview(Guid Id, string Name, string Hint, bool ReadOnly, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

public sealed record NewApiKey(string Name, bool ReadOnly);

/// <summary>A new key: <c>Token</c> is shown once; <c>McpUrl</c> is the address an AI assistant connects to.</summary>
public sealed record ApiKeyCreatedResponse(ApiKeyOverview Key, string Token, string McpUrl);
