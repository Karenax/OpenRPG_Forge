using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Quests;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Localization;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;

namespace Forge.Application.Projects;

public sealed record AuthoredModuleResult(ProjectState State, IReadOnlyList<ProjectPatch> Patches);

public static class OriginalModuleAuthoringService
{
    private static readonly DateTimeOffset AuthoredAt = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly GuidNamespace Ids = new("e5c6b0b1-37b8-4c25-9935-9ee0e0b7d001");

    public static AuthoredModuleResult CreateVeilleeDesEclats()
    {
        IClock clock = new AuthoringClock(AuthoredAt); var initial = new NewModuleHandler(clock).Handle(new(
            Ids.Command("new"), new(0), Ids.Project("project"), Ids.Module("module"), Ids.Area("area"),
            Ids.Blueprint("floor"), Ids.Entity("floor"), Ids.Blueprint("spawn"), Ids.Entity("spawn"),
            "La Veillée des Éclats", "Au bord d'un observatoire brisé, une veilleuse négocie avec les souvenirs lumineux d'un ciel disparu.", "Parvis des Astres", false));
        if (!initial.Succeeded || initial.State is null) throw new InvalidOperationException("G8_SAMPLE_NEW_MODULE_FAILED");
        var history = new ProjectCommandHistory(initial.State); var patches = new List<ProjectPatch> { initial.Patch! }; var upsert = new UpsertBlueprintHandler(clock); var place = new PlaceEntityHandler(clock);
        var blueprints = new[]
        {
            ("lyra", "Lyra, veilleuse des éclats", BlueprintKind.Creature),
            ("murmure", "Murmure prismatique", BlueprintKind.Creature),
            ("eclat", "Éclat d'azur", BlueprintKind.Item),
            ("brasero", "Brasero stellaire", BlueprintKind.Placeable),
            ("porte", "Porte de l'observatoire", BlueprintKind.Door),
            ("seuil", "Seuil des souvenirs", BlueprintKind.Trigger),
        };
        foreach (var (slug, name, kind) in blueprints) Apply(history, upsert.Handle(history.State, new(Ids.Command($"blueprint:{slug}"), history.State.Revision, Ids.Blueprint(slug), slug, name, kind, false)), patches);
        var setCombat = new SetBlueprintCombatHandler(clock);
        Apply(history, setCombat.Handle(history.State, new(Ids.Command("combat:lyra"), history.State.Revision, Ids.Blueprint("lyra"), new(new CombatStats(24, 8, 4, 3, "veilleuses", "allies")), false)), patches);
        Apply(history, setCombat.Handle(history.State, new(Ids.Command("combat:murmure"), history.State.Revision, Ids.Blueprint("murmure"), new(new CombatStats(18, 6, 5, 2, "souvenirs", "hostiles")), false)), patches);
        var placements = new[] { ("lyra", -2f, 0f), ("murmure", 4f, 3f), ("eclat", 1f, -3f), ("brasero", 0f, 2f), ("porte", 0f, 7f), ("seuil", 0f, 5f) };
        foreach (var (slug, x, z) in placements) Apply(history, place.Handle(history.State, new(Ids.Command($"place:{slug}"), history.State.Revision, Ids.Area("area"), Ids.Entity(slug), Ids.Blueprint(slug), new(new(x, 0, z), QuaternionValue.Identity, Vector3Value.One), false)), patches);

        var entry = Ids.DialogueNode("entry"); var reveal = Ids.DialogueNode("reveal"); var dialogue = new DialogueDefinition(Ids.Dialogue("lyra"), "lyra-veillee", "La promesse de Lyra", entry,
        [
            new(entry, Ids.Blueprint("lyra"), new LocalizedText("dialogue.veillee.entry", "Chaque éclat se souvient d'une étoile. M'aiderez-vous à les réunir ?"), choices:
            [
                new(Ids.DialogueChoice("accept"), new LocalizedText("dialogue.veillee.accept", "Je porterai leur lumière."), actions: [new(DialogueActionKind.StartQuest, "rassembler-les-eclats", "active")], targetNodeId: reveal),
                new(Ids.DialogueChoice("leave"), new LocalizedText("dialogue.veillee.leave", "La nuit peut attendre.")),
            ]),
            new(reveal, Ids.Blueprint("lyra"), new LocalizedText("dialogue.veillee.reveal", "Le seuil s'ouvrira quand trois souvenirs chanteront ensemble."), choices: [new(Ids.DialogueChoice("end"), new LocalizedText("dialogue.veillee.end", "Alors, commençons la veillée."))]),
        ]);
        Apply(history, new SetDialogueHandler(clock).Handle(history.State, new(Ids.Command("dialogue"), history.State.Revision, dialogue, false)), patches);
        var dormant = Ids.QuestState("dormant"); var active = Ids.QuestState("active"); var complete = Ids.QuestState("complete"); var quest = new QuestDefinition(Ids.Quest("eclats"), "rassembler-les-eclats", "Rassembler les éclats", dormant,
        [
            new(dormant, "En attente", new("quest.veillee.dormant", "Écoutez la veilleuse du parvis.")),
            new(active, "La collecte", new("quest.veillee.active", "Réunissez trois éclats et rejoignez le seuil.")),
            new(complete, "Le ciel rendu", new("quest.veillee.complete", "L'observatoire chante de nouveau."), true),
        ],
        [
            new(Ids.QuestTransition("begin"), dormant, active, actions: [new(QuestActionKind.SetVariable, "eclats.collecte", "0")]),
            new(Ids.QuestTransition("finish"), active, complete, [new(QuestConditionKind.VariableEquals, "eclats.collecte", "3")], [new(QuestActionKind.SetVariable, "observatoire.ouvert", "true")]),
        ]);
        Apply(history, new SetQuestHandler(clock).Handle(history.State, new(Ids.Command("quest"), history.State.Revision, quest, false)), patches);
        return new(history.State, patches);
    }

    private static void Apply(ProjectCommandHistory history, CommandResult result, List<ProjectPatch> patches)
    {
        if (!result.Succeeded) throw new InvalidOperationException($"G8_SAMPLE_COMMAND_FAILED:{string.Join('|', result.Diagnostics.Select(item => item.Code))}"); history.Record(result); patches.Add(result.Patch!);
    }

    private sealed class AuthoringClock(DateTimeOffset value) : IClock { public DateTimeOffset UtcNow { get; } = value; }
    private sealed class GuidNamespace(string seed)
    {
        private readonly Guid _seed = Guid.Parse(seed);
        private Guid Value(string scope) { var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{_seed:D}:{scope}")); return new Guid(bytes[..16]); }
        public CommandId Command(string value) => CommandId.From(Value($"command:{value}")); public ProjectId Project(string value) => ProjectId.From(Value($"project:{value}")); public ModuleId Module(string value) => ModuleId.From(Value($"module:{value}")); public AreaId Area(string value) => AreaId.From(Value($"area:{value}")); public BlueprintId Blueprint(string value) => BlueprintId.From(Value($"blueprint:{value}")); public EntityId Entity(string value) => EntityId.From(Value($"entity:{value}")); public DialogueId Dialogue(string value) => DialogueId.From(Value($"dialogue:{value}")); public DialogueNodeId DialogueNode(string value) => DialogueNodeId.From(Value($"dialogue-node:{value}")); public DialogueChoiceId DialogueChoice(string value) => DialogueChoiceId.From(Value($"dialogue-choice:{value}")); public QuestId Quest(string value) => QuestId.From(Value($"quest:{value}")); public QuestStateId QuestState(string value) => QuestStateId.From(Value($"quest-state:{value}")); public QuestTransitionId QuestTransition(string value) => QuestTransitionId.From(Value($"quest-transition:{value}"));
    }
}
