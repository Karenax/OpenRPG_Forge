namespace Forge.Domain.Assistant;

public sealed record AssistantProviderDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Key { get; }
    public string DisplayName { get; }
    public AssistantProviderKind Kind { get; }
    public string Endpoint { get; }
    public string Model { get; }
    public string CredentialEnvironmentVariable { get; }
    public bool Enabled { get; }
    public bool LocalOnly { get; }
    public int MaximumInputTokens { get; }
    public int MaximumOutputTokens { get; }
    public decimal InputCostPerMillionTokens { get; }
    public decimal OutputCostPerMillionTokens { get; }
    public string Provenance { get; }

    public AssistantProviderDefinition(
        Guid id,
        int schemaVersion,
        string key,
        string displayName,
        AssistantProviderKind kind,
        string endpoint,
        string model,
        string credentialEnvironmentVariable,
        bool enabled,
        bool localOnly,
        int maximumInputTokens,
        int maximumOutputTokens,
        decimal inputCostPerMillionTokens,
        decimal outputCostPerMillionTokens,
        string provenance)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Key = (key ?? string.Empty).Trim().ToLowerInvariant();
        DisplayName = (displayName ?? string.Empty).Trim();
        Kind = kind;
        Endpoint = (endpoint ?? string.Empty).Trim().TrimEnd('/');
        Model = (model ?? string.Empty).Trim();
        CredentialEnvironmentVariable = (credentialEnvironmentVariable ?? string.Empty).Trim();
        Enabled = enabled;
        LocalOnly = localOnly;
        MaximumInputTokens = maximumInputTokens;
        MaximumOutputTokens = maximumOutputTokens;
        InputCostPerMillionTokens = inputCostPerMillionTokens;
        OutputCostPerMillionTokens = outputCostPerMillionTokens;
        Provenance = (provenance ?? string.Empty).Trim();
    }
}
