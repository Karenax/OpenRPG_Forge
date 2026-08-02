using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Modules;
using Forge.Domain.Quests;

namespace Forge.Domain.Projects;

public sealed record ProjectDefinition
{
    public ProjectId Id { get; }

    public string Title { get; }

    public ModuleDefinition Module { get; }

    public IReadOnlyList<BlueprintDefinition> Blueprints { get; }

    public IReadOnlyList<AreaDefinition> Areas { get; }

    public IReadOnlyList<DialogueDefinition> Dialogues { get; }

    public IReadOnlyList<QuestDefinition> Quests { get; }

    public ProjectDefinition(
        ProjectId id,
        string title,
        ModuleDefinition module,
        IReadOnlyList<BlueprintDefinition> blueprints,
        IReadOnlyList<AreaDefinition> areas,
        IReadOnlyList<DialogueDefinition>? dialogues = null,
        IReadOnlyList<QuestDefinition>? quests = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(blueprints);
        ArgumentNullException.ThrowIfNull(areas);

        var materializedBlueprints = blueprints.ToArray();
        var materializedAreas = areas.ToArray();
        var materializedDialogues = (dialogues ?? []).ToArray();
        var materializedQuests = (quests ?? []).ToArray();

        if (materializedBlueprints.Select(blueprint => blueprint.Id).Distinct().Count() != materializedBlueprints.Length)
        {
            throw new ArgumentException("A project cannot contain duplicate blueprint IDs.", nameof(blueprints));
        }

        if (materializedAreas.Select(area => area.Id).Distinct().Count() != materializedAreas.Length)
        {
            throw new ArgumentException("A project cannot contain duplicate area IDs.", nameof(areas));
        }

        if (materializedDialogues.Select(dialogue => dialogue.Id).Distinct().Count() != materializedDialogues.Length)
        {
            throw new ArgumentException("A project cannot contain duplicate dialogue IDs.", nameof(dialogues));
        }

        if (materializedQuests.Select(quest => quest.Id).Distinct().Count() != materializedQuests.Length)
        {
            throw new ArgumentException("A project cannot contain duplicate quest IDs.", nameof(quests));
        }

        Id = id;
        Title = title;
        Module = module;
        Blueprints = materializedBlueprints;
        Areas = materializedAreas;
        Dialogues = materializedDialogues;
        Quests = materializedQuests;
    }

    public ProjectDefinition(
        ProjectId id,
        string title,
        IReadOnlyList<BlueprintDefinition> blueprints,
        IReadOnlyList<AreaDefinition> areas,
        IReadOnlyList<DialogueDefinition>? dialogues = null,
        IReadOnlyList<QuestDefinition>? quests = null)
        : this(id, title, CreateDefaultModule(id, title, areas), blueprints, areas, dialogues, quests)
    {
    }

    private static ModuleDefinition CreateDefaultModule(
        ProjectId projectId,
        string title,
        IReadOnlyList<AreaDefinition> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var entryArea = areas.FirstOrDefault()
            ?? throw new ArgumentException("A project must contain an entry area.", nameof(areas));
        return new ModuleDefinition(
            ModuleId.From(projectId.Value),
            "main-module",
            title,
            string.Empty,
            entryArea.Id);
    }
}
