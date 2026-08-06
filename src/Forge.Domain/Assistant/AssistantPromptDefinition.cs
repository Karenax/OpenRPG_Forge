namespace Forge.Domain.Assistant;

public sealed record AssistantPromptDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Key { get; }
    public string Version { get; }
    public string DisplayName { get; }
    public string SystemTemplate { get; }
    public string UserTemplate { get; }
    public string ResponseJsonSchema { get; }
    public IReadOnlyList<string> RequiredCapabilities { get; }
    public IReadOnlyList<AssistantPromptTestCase> Tests { get; }
    public IReadOnlyList<AssistantPromptChange> Changelog { get; }
    public string Provenance { get; }

    public AssistantPromptDefinition(
        Guid id,
        int schemaVersion,
        string key,
        string version,
        string displayName,
        string systemTemplate,
        string userTemplate,
        string responseJsonSchema,
        IReadOnlyList<string>? requiredCapabilities,
        IReadOnlyList<AssistantPromptTestCase>? tests,
        IReadOnlyList<AssistantPromptChange>? changelog,
        string provenance)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Key = (key ?? string.Empty).Trim().ToLowerInvariant();
        Version = (version ?? string.Empty).Trim();
        DisplayName = (displayName ?? string.Empty).Trim();
        SystemTemplate = (systemTemplate ?? string.Empty).Trim();
        UserTemplate = (userTemplate ?? string.Empty).Trim();
        ResponseJsonSchema = (responseJsonSchema ?? string.Empty).Trim();
        RequiredCapabilities = (requiredCapabilities ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Tests = (tests ?? []).OrderBy(test => test.Name, StringComparer.Ordinal).ToArray();
        Changelog = (changelog ?? []).ToArray();
        Provenance = (provenance ?? string.Empty).Trim();
    }
}
