using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Forge.Domain.Quests;

public sealed record QuestSimulationEvent
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public string EventName { get; }
    public DateTimeOffset OccurredAt { get; }
    public string ActorId { get; }
    public IReadOnlyDictionary<string, string> Variables { get; }

    [JsonConstructor]
    public QuestSimulationEvent(
        Guid id,
        int schemaVersion,
        string? provenance,
        string eventName,
        DateTimeOffset occurredAt,
        string actorId,
        IReadOnlyDictionary<string, string>? variables = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("A simulation event ID cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        Id = id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        EventName = eventName.Trim();
        OccurredAt = occurredAt;
        ActorId = actorId.Trim();
        Variables = new SortedDictionary<string, string>(
            (variables ?? new Dictionary<string, string>()).ToDictionary(item => item.Key, item => item.Value,
                StringComparer.Ordinal), StringComparer.Ordinal);
    }

    public QuestSimulationEvent(Guid id, string eventName, DateTimeOffset occurredAt, string actorId,
        IReadOnlyDictionary<string, string>? variables = null, string provenance = "author")
        : this(id, CurrentSchemaVersion, provenance, eventName, occurredAt, actorId, variables)
    {
    }
}

public sealed record QuestSimulationProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public int MaximumSteps { get; }
    public IReadOnlyDictionary<string, string> InitialVariables { get; }
    public IReadOnlyList<QuestSimulationEvent> Events { get; }

    [JsonConstructor]
    public QuestSimulationProfile(
        Guid id,
        int schemaVersion,
        string? provenance,
        int maximumSteps,
        IReadOnlyDictionary<string, string>? initialVariables,
        IReadOnlyList<QuestSimulationEvent>? events)
    {
        if (id == Guid.Empty) throw new ArgumentException("A simulation profile ID cannot be empty.", nameof(id));
        Id = id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        MaximumSteps = maximumSteps == 0 ? 100 : maximumSteps;
        InitialVariables = new SortedDictionary<string, string>(
            (initialVariables ?? new Dictionary<string, string>()).ToDictionary(item => item.Key, item => item.Value,
                StringComparer.Ordinal), StringComparer.Ordinal);
        Events = (events ?? []).OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToArray();
    }

    public QuestSimulationProfile(Guid id, int maximumSteps,
        IReadOnlyDictionary<string, string>? initialVariables = null,
        IReadOnlyList<QuestSimulationEvent>? events = null,
        string provenance = "author")
        : this(id, CurrentSchemaVersion, provenance, maximumSteps, initialVariables, events)
    {
    }

    public static QuestSimulationProfile CreateDefault(QuestId questId) => new(
        StableId($"quest-simulation:{questId}"), CurrentSchemaVersion, "migration-default", 100, null, null);

    private static Guid StableId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[6] = (byte)((id[6] & 0x0f) | 0x40);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }
}
