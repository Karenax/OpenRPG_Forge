using System.Text.Json.Serialization;

namespace Forge.Domain.Dialogues;

public sealed record DialogueCondition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public DialogueConditionKind Kind { get; }

    public string Key { get; }

    public string ExpectedValue { get; }

    public bool Negated { get; }

    public string EventBinding { get; }

    public int ForgeScriptApiVersion { get; }

    [JsonConstructor]
    public DialogueCondition(
        Guid id,
        int schemaVersion,
        string provenance,
        DialogueConditionKind kind,
        string key,
        string expectedValue,
        bool negated,
        string eventBinding,
        int forgeScriptApiVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(expectedValue);
        var normalizedKey = key.Trim();
        var normalizedExpectedValue = expectedValue.Trim();
        Id = id == Guid.Empty
            ? DialogueParticipant.StableId($"condition:{kind}:{normalizedKey}:{normalizedExpectedValue}:{negated}")
            : id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Kind = kind;
        Key = normalizedKey;
        ExpectedValue = normalizedExpectedValue;
        Negated = negated;
        EventBinding = string.IsNullOrWhiteSpace(eventBinding) ? "on-choice-visible" : eventBinding.Trim();
        ForgeScriptApiVersion = forgeScriptApiVersion == 0 ? 1 : forgeScriptApiVersion;
    }

    public DialogueCondition(
        DialogueConditionKind kind,
        string key,
        string expectedValue,
        bool negated = false,
        string eventBinding = "on-choice-visible",
        int forgeScriptApiVersion = 1,
        string provenance = "migration-default")
        : this(Guid.Empty, CurrentSchemaVersion, provenance, kind, key, expectedValue, negated, eventBinding,
            forgeScriptApiVersion)
    {
    }
}
