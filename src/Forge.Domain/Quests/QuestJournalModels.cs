using System.Text.Json.Serialization;
using Forge.Domain.Dialogues;

namespace Forge.Domain.Quests;

public enum QuestTerminalKind
{
    None,
    Completed,
    Failed,
    Abandoned,
    Custom,
}

public enum QuestReactivationRule
{
    Never,
    Manual,
    RepeatPolicy,
    Scripted,
}

public enum QuestProgressScope
{
    Player,
    Party,
    World,
}

public sealed record QuestInstancePolicy
{
    public QuestProgressScope DefaultScope { get; }

    public IReadOnlyList<QuestProgressScope> AllowedScopes { get; }

    public bool RequireAuthority { get; }

    [JsonConstructor]
    public QuestInstancePolicy(
        QuestProgressScope defaultScope,
        IReadOnlyList<QuestProgressScope>? allowedScopes,
        bool requireAuthority)
    {
        if (!Enum.IsDefined(defaultScope))
            throw new ArgumentOutOfRangeException(nameof(defaultScope));
        var materialized = (allowedScopes ?? [defaultScope]).Distinct().Order().ToArray();
        if (materialized.Length == 0 || materialized.Any(scope => !Enum.IsDefined(scope)) ||
            !materialized.Contains(defaultScope))
            throw new ArgumentException("Quest instance scopes must include the default scope.", nameof(allowedScopes));
        DefaultScope = defaultScope;
        AllowedScopes = materialized;
        RequireAuthority = requireAuthority;
    }

    public static QuestInstancePolicy FromSharePolicy(QuestSharePolicy? share) => share?.Mode switch
    {
        QuestShareMode.PartyShared or QuestShareMode.OptIn or QuestShareMode.Leader or QuestShareMode.Unanimous =>
            new(QuestProgressScope.Party, [QuestProgressScope.Party], share.RequireServerAuthority),
        _ => new(QuestProgressScope.Player, [QuestProgressScope.Player], share?.RequireServerAuthority ?? false),
    };
}

public enum QuestJournalLinkKind
{
    Location,
    Character,
    Quest,
    Dialogue,
    Entity,
    Custom,
}

public sealed record QuestJournalLink
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public QuestJournalLinkKind Kind { get; }

    public string ReferenceId { get; }

    public LocalizedText? Label { get; }

    [JsonConstructor]
    public QuestJournalLink(
        Guid id,
        int schemaVersion,
        string? provenance,
        QuestJournalLinkKind kind,
        string referenceId,
        LocalizedText? label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceId);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var normalizedReference = referenceId.Trim();
        Id = id == Guid.Empty
            ? DialogueParticipant.StableId($"quest-journal-link:{kind}:{normalizedReference}")
            : id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Kind = kind;
        ReferenceId = normalizedReference;
        Label = label;
    }

    public QuestJournalLink(
        QuestJournalLinkKind kind,
        string referenceId,
        LocalizedText? label = null,
        string provenance = "author")
        : this(Guid.Empty, CurrentSchemaVersion, provenance, kind, referenceId, label)
    {
    }
}
