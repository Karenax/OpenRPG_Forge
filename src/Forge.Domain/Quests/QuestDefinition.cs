using System.Text.Json.Serialization;
using Forge.Domain.Dialogues;

namespace Forge.Domain.Quests;

public sealed record QuestDefinition
{
    public const int CurrentSchemaVersion = 1;

    public QuestId Id { get; }

    public int SchemaVersion { get; }

    public string Provenance { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public LocalizedText Title { get; }

    public LocalizedText Description { get; }

    public string Category { get; }

    public int Priority { get; }

    public string Owner { get; }

    public bool RequiredAtRuntime { get; }

    public QuestInstancePolicy InstancePolicy { get; }

    public QuestStateId InitialStateId { get; }

    public IReadOnlyList<QuestStateDefinition> States { get; }

    public IReadOnlyList<QuestTransition> Transitions { get; }

    public QuestOperationsProfile? Operations { get; }

    public QuestSimulationProfile Simulation { get; }

    [JsonConstructor]
    public QuestDefinition(
        QuestId id,
        int schemaVersion,
        string? provenance,
        string slug,
        string displayName,
        LocalizedText? title,
        LocalizedText? description,
        string? category,
        int priority,
        string? owner,
        bool requiredAtRuntime,
        QuestInstancePolicy? instancePolicy,
        QuestStateId initialStateId,
        IReadOnlyList<QuestStateDefinition> states,
        IReadOnlyList<QuestTransition>? transitions = null,
        QuestOperationsProfile? operations = null,
        QuestSimulationProfile? simulation = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(initialStateId);
        ArgumentNullException.ThrowIfNull(states);
        var materializedStates = states.Select((state, index) => state.DisplayOrder > 0
            ? state
            : state.WithOrdering(index + 1, state.LegacyJournalIndex ?? index)).ToArray();
        var materializedTransitions = (transitions ?? []).Select((transition, index) =>
            transition.DisplayOrder > 0 ? transition : transition.WithOrdering(index + 1)).ToArray();
        if (materializedStates.Select(state => state.Id).Distinct().Count() != materializedStates.Length)
        {
            throw new ArgumentException("A quest cannot contain duplicate state IDs.", nameof(states));
        }

        if (materializedTransitions.Select(transition => transition.Id).Distinct().Count() !=
            materializedTransitions.Length)
        {
            throw new ArgumentException("A quest cannot contain duplicate transition IDs.", nameof(transitions));
        }

        Id = id;
        SchemaVersion = schemaVersion == 0 ? CurrentSchemaVersion : schemaVersion;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "migration-default" : provenance.Trim();
        Slug = slug.Trim();
        DisplayName = displayName.Trim();
        Title = title ?? new LocalizedText($"quest.{Slug}.title", DisplayName);
        Description = description ?? new LocalizedText($"quest.{Slug}.description", DisplayName);
        Category = string.IsNullOrWhiteSpace(category) ? "journal" : category.Trim();
        Priority = priority == 0 ? operations?.Journal.Priority ?? 50 : priority;
        Owner = string.IsNullOrWhiteSpace(owner) ? "module" : owner.Trim();
        RequiredAtRuntime = requiredAtRuntime;
        InstancePolicy = instancePolicy ?? QuestInstancePolicy.FromSharePolicy(operations?.Share);
        InitialStateId = initialStateId;
        States = materializedStates;
        Transitions = materializedTransitions;
        Operations = operations;
        Simulation = simulation ?? QuestSimulationProfile.CreateDefault(id);
    }

    public QuestDefinition(
        QuestId id,
        string slug,
        string displayName,
        QuestStateId initialStateId,
        IReadOnlyList<QuestStateDefinition> states,
        IReadOnlyList<QuestTransition>? transitions = null,
        QuestOperationsProfile? operations = null,
        QuestSimulationProfile? simulation = null)
        : this(id, CurrentSchemaVersion, "migration-default", slug, displayName, null, null, null, 0, null,
            false, null, initialStateId, states, transitions, operations, simulation)
    {
    }

    public QuestDefinition WithOperations(QuestOperationsProfile? operations) => new(
        Id, SchemaVersion, Provenance, Slug, DisplayName, Title, Description, Category, Priority, Owner,
        RequiredAtRuntime, InstancePolicy, InitialStateId, States, Transitions, operations, Simulation);
}
