namespace Forge.Domain.Dialogues;

public sealed record DialogueDefinition
{
    public const int CurrentSchemaVersion = 1;

    public DialogueId Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public DialogueNodeId EntryNodeId { get; }

    public IReadOnlyList<DialogueNode> Nodes { get; }

    public DialoguePresentationProfile? Presentation { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public IReadOnlyList<DialogueParticipant> Participants { get; }

    public IReadOnlyList<DialogueEntryPoint> EntryPoints { get; }

    public IReadOnlyList<DialogueGraphEdge> Edges { get; }

    public DialogueAuthoringProfile? Authoring { get; }

    public DialogueExperienceProfile? Experience { get; }

    public DialogueProductionProfile? Production { get; }

    public DialogueDefinition(
        DialogueId id,
        string slug,
        string displayName,
        DialogueNodeId entryNodeId,
        IReadOnlyList<DialogueNode> nodes,
        DialoguePresentationProfile? presentation = null,
        int schemaVersion = CurrentSchemaVersion,
        string provenance = "migration-default",
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyList<DialogueParticipant>? participants = null,
        IReadOnlyList<DialogueEntryPoint>? entryPoints = null,
        IReadOnlyList<DialogueGraphEdge>? edges = null,
        DialogueAuthoringProfile? authoring = null,
        DialogueExperienceProfile? experience = null,
        DialogueProductionProfile? production = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(entryNodeId);
        ArgumentNullException.ThrowIfNull(nodes);
        var materializedNodes = nodes.ToArray();
        if (materializedNodes.Select(node => node.Id).Distinct().Count() != materializedNodes.Length)
        {
            throw new ArgumentException("A dialogue cannot contain duplicate node IDs.", nameof(nodes));
        }

        var choiceIds = materializedNodes.SelectMany(node => node.Choices).Select(choice => choice.Id).ToArray();
        if (choiceIds.Distinct().Count() != choiceIds.Length)
        {
            throw new ArgumentException("Choice IDs must be unique inside a dialogue.", nameof(nodes));
        }

        var materializedParticipants = (participants ?? materializedNodes
                .Select(node => DialogueParticipant.FromSpeaker(node.SpeakerBlueprintId))
                .DistinctBy(item => item.Id))
            .OrderBy(item => item.Role, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToArray();
        var materializedEntries = (entryPoints ??
            [new DialogueEntryPoint(
                DialogueParticipant.StableId($"{id}:entry:default"),
                DialogueEntryPoint.CurrentSchemaVersion,
                "default",
                entryNodeId,
                0,
                "migration-default")]).ToArray();
        var derivedEdges = materializedNodes.SelectMany(node => node.Choices
                .Select((choice, index) => (choice, index))
                .Where(item => item.choice.TargetNodeId is not null)
                .Select(item => new DialogueGraphEdge(
                    item.choice.Id,
                    node.Id,
                    item.choice.TargetNodeId!,
                    item.choice.EdgeKind,
                    item.index,
                    item.choice.Priority)))
            .OrderBy(item => item.SourceNodeId.Value)
            .ThenBy(item => item.Order)
            .ToArray();

        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Slug = slug.Trim();
        DisplayName = displayName.Trim();
        EntryNodeId = entryNodeId;
        Nodes = materializedNodes;
        Presentation = presentation;
        Metadata = (metadata ?? new Dictionary<string, string> { ["kind"] = "conversation" })
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        Participants = materializedParticipants;
        EntryPoints = materializedEntries;
        Edges = (edges ?? derivedEdges).ToArray();
        Authoring = authoring;
        Experience = experience;
        Production = production;
    }

    public DialogueDefinition WithPresentation(DialoguePresentationProfile? presentation) =>
        new(Id, Slug, DisplayName, EntryNodeId, Nodes, presentation, SchemaVersion, Provenance,
            Metadata, Participants, EntryPoints, Edges, Authoring, Experience, Production);

    public DialogueDefinition WithAuthoring(DialogueAuthoringProfile? authoring) =>
        new(Id, Slug, DisplayName, EntryNodeId, Nodes, Presentation, SchemaVersion, Provenance,
            Metadata, Participants, EntryPoints, Edges, authoring, Experience, Production);

    public DialogueDefinition WithExperience(DialogueExperienceProfile? experience) =>
        new(Id, Slug, DisplayName, EntryNodeId, Nodes, Presentation, SchemaVersion, Provenance,
            Metadata, Participants, EntryPoints, Edges, Authoring, experience, Production);

    public DialogueDefinition WithProduction(DialogueProductionProfile? production) =>
        new(Id, Slug, DisplayName, EntryNodeId, Nodes, Presentation, SchemaVersion, Provenance,
            Metadata, Participants, EntryPoints, Edges, Authoring, Experience, production);
}
