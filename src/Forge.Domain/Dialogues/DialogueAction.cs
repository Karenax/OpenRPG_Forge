using System.Text.Json.Serialization;

namespace Forge.Domain.Dialogues;

public sealed record DialogueAction
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public DialogueActionKind Kind { get; }

    public string Key { get; }

    public string Value { get; }

    public bool Required { get; }

    [JsonConstructor]
    public DialogueAction(
        Guid id,
        int schemaVersion,
        string provenance,
        DialogueActionKind kind,
        string key,
        string value,
        bool required)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        var normalizedKey = key.Trim();
        var normalizedValue = value.Trim();
        Id = id == Guid.Empty
            ? DialogueParticipant.StableId($"action:{kind}:{normalizedKey}:{normalizedValue}")
            : id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Kind = kind;
        Key = normalizedKey;
        Value = normalizedValue;
        Required = required;
    }

    public DialogueAction(
        DialogueActionKind kind,
        string key,
        string value,
        bool required = true,
        string provenance = "migration-default")
        : this(Guid.Empty, CurrentSchemaVersion, provenance, kind, key, value, required)
    {
    }
}
