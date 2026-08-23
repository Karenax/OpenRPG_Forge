using System.Globalization;
using Forge.Domain.Blueprints;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6HProvenance = "studio:ux6-h:script-world-apis";
    private const string Ux6HStartMarker = "// <studio:ux6-h-world-apis>";
    private const string Ux6HEndMarker = "// </studio:ux6-h-world-apis>";

    private static readonly IReadOnlySet<string> SensesUx6H =
        new HashSet<string>(["sight", "hearing", "smell", "magic", "tremor"], StringComparer.Ordinal);

    internal StudioUx6HWorldCatalog WorldCatalogUx6H()
    {
        var project = Project;
        if (project is null) return new([], [], [], [], []);
        var items = project.Blueprints
            .Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioUx6HChoice(item.Slug, item.DisplayName,
                "Objet du catalogue de blueprints du module."))
            .ToArray();
        var actors = project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioUx6HChoice(item.Slug, item.DisplayName,
                item.Kind == BlueprintKind.PlayerSpawn ? "Personnage joueur" : "Créature"))
            .ToArray();
        var quests = project.Quests
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioUx6HQuestChoice(
                item.Slug,
                item.DisplayName,
                item.States.OrderBy(state => state.DisplayOrder)
                    .Select(state => new StudioUx6HChoice(state.Slug, state.DisplayName,
                        state.IsTerminal ? "État terminal" : "État de progression"))
                    .ToArray(),
                (item.Authoring?.Objectives ?? [])
                    .OrderBy(objective => objective.DisplayOrder)
                    .Select(objective => new StudioUx6HChoice(
                        objective.Id.ToString("D", CultureInfo.InvariantCulture),
                        objective.Label.Fallback,
                        $"Objectif {objective.Kind}"))
                    .ToArray()))
            .ToArray();
        var dialogues = project.Dialogues
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioUx6HChoice($"dialogue:{item.Slug}", item.DisplayName,
                $"Graphe · {item.Nodes.Count} ligne(s)"))
            .ToArray();
        var factions = project.Module.Factions.Factions
            .Where(item => !item.Archived)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioUx6HChoice(item.Slug, item.DisplayName,
                "Faction active du module"))
            .ToArray();
        return new(items, actors, quests, dialogues, factions);
    }

    internal StudioUx6HWorldDraft? DefaultWorldDraftUx6H()
    {
        var catalog = WorldCatalogUx6H();
        if (!catalog.Complete) return null;
        var quest = catalog.Quests[0];
        if (quest.States.Count == 0) return null;
        var from = quest.States[0];
        var to = quest.States.FirstOrDefault(item => !string.Equals(item.Key, from.Key,
                     StringComparison.Ordinal)) ?? from;
        return new(
            catalog.Actors[0].Key,
            catalog.Actors[1].Key,
            catalog.Items[0].Key,
            1,
            quest.Key,
            "party:studio",
            from.Key,
            to.Key,
            quest.Objectives.FirstOrDefault()?.Key ?? "progression-principale",
            1,
            catalog.Dialogues[0].Key,
            catalog.Actors[0].Key,
            catalog.Actors[1].Key,
            "accepted",
            catalog.Actors[1].Key,
            catalog.Factions[0].Key,
            25,
            true,
            "module",
            catalog.Actors[0].Key,
            catalog.Actors[1].Key,
            "sight",
            2,
            10,
            true,
            .8,
            98);
    }

    internal StudioUx6HSourcePlan BuildWorldApiSourceUx6H(
        StudioUx6FDraft source,
        StudioUx6HWorldDraft world)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(world);
        var diagnostics = ValidateWorldDraftUx6H(world);
        if (diagnostics.Count > 0)
            return new(false, "Le bloc API monde est incomplet · corrigez les références localisées.",
                string.Empty, source.Source, diagnostics);
        var block = WorldApiBlockUx6H(world);
        var start = source.Source.IndexOf(Ux6HStartMarker, StringComparison.Ordinal);
        var end = source.Source.IndexOf(Ux6HEndMarker, StringComparison.Ordinal);
        string candidate;
        if (start >= 0 || end >= 0)
        {
            if (start < 0 || end < start)
                return new(false, "Le bloc UX6-H existant est incomplet et reste protégé.", string.Empty,
                    source.Source,
                    [new("script.world.block_invalid", "script/source", "error",
                        "Les deux marqueurs du bloc UX6-H sont requis.")]);
            var after = end + Ux6HEndMarker.Length;
            candidate = source.Source[..start] + block + source.Source[after..];
        }
        else
        {
            candidate = source.Source.TrimEnd() + Environment.NewLine + Environment.NewLine + block +
                        Environment.NewLine;
        }
        return new(true,
            "Bloc versionné généré · relisez-le puis insérez-le dans le brouillon avant l’aperçu sec.",
            block,
            candidate,
            []);
    }

    internal StudioUx6HWorldResult PreviewWorldApisUx6H(
        StudioUx6FDraft source,
        StudioUx6HWorldDraft world)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(world);
        var diagnostics = ValidateWorldDraftUx6H(world);
        if (diagnostics.Count > 0)
            return FailedWorldUx6H("Simulation refusée · références ou valeurs invalides.", diagnostics);
        try
        {
            var document = ToDocumentUx6F(source);
            var adapter = new ForgeScriptWorldApiRuntimeAdapter();
            var optional = adapter.Resolve(null, requiredAtRuntime: false).OptionalFallback;
            var required = adapter.Resolve(null, requiredAtRuntime: true).DiagnosticCode ==
                           ForgeScriptWorldApiRuntimeAdapter.RequiredMissingCode;
            var resolution = adapter.Resolve(document, source.RequiredAtRuntime);
            if (!resolution.Available || resolution.Handle is null)
                return FailedWorldUx6H("La source API monde n’est pas disponible.",
                    [new(resolution.DiagnosticCode ?? "script.world.source_missing", "script/source", "error",
                        "Une source canonique est requise pour cette simulation.")]);

            var inventory = new InventoryState(
                new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal)
                {
                    [world.InventoryOwner] = new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [world.ItemId] = Math.Max(2, world.Quantity),
                    },
                    [world.InventoryRecipient] = new Dictionary<string, int>(StringComparer.Ordinal),
                },
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [world.InventoryOwner] = 16,
                    [world.InventoryRecipient] = 8,
                },
                0);
            var questState = new QuestState(world.QuestId, world.QuestScope, world.QuestFromState,
                new Dictionary<string, int>(StringComparer.Ordinal) { [world.QuestObjective] = 0 }, [], 0);
            var quests = new Dictionary<string, QuestState>(StringComparer.Ordinal)
            {
                [world.QuestId] = questState,
            };
            var factions = new FactionState(
                new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal),
                new Dictionary<string, int>(StringComparer.Ordinal),
                0);
            var handle = resolution.Handle;
            var runtime = adapter.Build(handle, document, inventory, quests, factions);
            var transfer = adapter.Transfer(handle, runtime,
                new InventoryTransaction(Guid.Parse("6e000000-0000-4000-8000-000000000081"), "server",
                    "studio-preview", world.InventoryOwner, world.InventoryRecipient, world.ItemId, world.Quantity));
            runtime = transfer.Runtime;
            var inventoryQuery = ScriptWorldService.QueryInventory(runtime.Inventory,
                world.InventoryRecipient, world.ItemId);

            runtime = adapter.TransitionQuest(handle, runtime, world.QuestId,
                new QuestTransition(Guid.Parse("6e000000-0000-4000-8000-000000000082"), "server",
                    world.QuestFromState, world.QuestToState, world.QuestObjective, world.QuestDelta,
                    "studio-preview"));
            var quest = runtime.Quests[world.QuestId];

            var dialogue = adapter.StartDialogue(handle, runtime,
                new DialogueRequest(Guid.Parse("6e000000-0000-4000-8000-000000000083"), "server",
                    world.DialogueId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["speaker"] = world.DialogueSpeaker,
                        ["listener"] = world.DialogueListener,
                    },
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["quest"] = world.QuestId,
                    }));
            runtime = adapter.CompleteDialogue(handle, dialogue.Runtime, dialogue.Session.Id,
                world.DialogueOutcome);
            var completed = runtime.Dialogues[dialogue.Session.Id];
            var interrupted = ScriptWorldService.InterruptDialogue(dialogue.Session, "danger-preview");

            runtime = adapter.ApplyFaction(handle, runtime,
                new FactionTransaction(Guid.Parse("6e000000-0000-4000-8000-000000000084"), "server",
                    world.FactionActor, world.FactionId, world.ReputationDelta, world.JoinFaction,
                    world.FactionScope, "studio-preview"));
            var faction = ScriptWorldService.QueryFaction(runtime.Factions, world.FactionActor,
                world.FactionId, world.FactionScope);

            var perceptionQuery = new PerceptionQuery(world.Observer, world.Target, world.Sense,
                world.Distance, world.Range, world.LineOfSight, world.Signal, world.Seed);
            var perceived = adapter.Perceive(handle, runtime, perceptionQuery);
            var repeated = adapter.Perceive(handle, runtime, perceptionQuery);

            var rejected = adapter.Transfer(handle, runtime,
                new InventoryTransaction(Guid.Parse("6e000000-0000-4000-8000-000000000085"), "client",
                    "unauthorized-preview", world.InventoryRecipient, world.InventoryOwner,
                    world.ItemId, world.Quantity));
            var rollback = !rejected.Result.Committed && ReferenceEquals(rejected.Runtime, runtime);
            var stale = false;
            try
            {
                adapter.Perceive(new ForgeScriptWorldApiHandle(Guid.NewGuid()), runtime, perceptionQuery);
            }
            catch (InvalidOperationException exception)
            {
                stale = exception.Message == "FORGESCRIPT_WORLD_API_HANDLE_STALE";
            }

            var nativeDocument = document with
            {
                Source = NativeWorldApiSourceUx6H(world),
                EventBindings = [],
            };
            var nativeRuntime = adapter.Build(handle, nativeDocument, inventory, quests, factions);
            var native = adapter.Execute(handle, nativeRuntime, new NativeForgeScriptRuntime(),
                new ForgeScriptQuotas(30_000, 1000, 32, 8));
            var nativeEvents = native.Events.Select(item => $"{item.Kind} · {item.Value}").ToArray();
            var nativeSucceeded = native.Status == ForgeScriptExecutionStatus.Completed &&
                                  native.Events.Any(item => item.Kind == "inventory.transfer") &&
                                  native.Events.Any(item => item.Kind == "quest.transition") &&
                                  native.Events.Any(item => item.Kind == "dialogue.started") &&
                                  native.Events.Any(item => item.Kind == "faction.reputation") &&
                                  native.Events.Any(item => item.Kind == "perception.query");
            var deterministic = perceived.Perceived == repeated.Perceived &&
                                perceived.Confidence == repeated.Confidence &&
                                string.Equals(perceived.Trace, repeated.Trace, StringComparison.Ordinal);
            var succeeded = optional && required && transfer.Result.Committed && rollback &&
                            quest.Status == world.QuestToState && completed.State == "completed" &&
                            interrupted.State == "interrupted" && faction.Member == world.JoinFaction &&
                            deterministic && stale && nativeSucceeded;
            var trace = transfer.Result.Audit
                .Concat(quest.History)
                .Concat(completed.Outcomes)
                .Concat(interrupted.Outcomes)
                .Concat(perceived.Stimuli)
                .Append(perceived.Trace)
                .ToArray();
            return new(succeeded,
                succeeded
                    ? "Cinq API monde simulées sur une projection immuable · source et projet inchangés."
                    : "La simulation n’a pas satisfait tous les invariants R098.",
                optional,
                required,
                transfer.Result.Committed,
                rollback,
                inventoryQuery.Quantity,
                inventoryQuery.MaximumCapacity,
                quest.Status == world.QuestToState,
                quest.Objectives.GetValueOrDefault(world.QuestObjective),
                dialogue.Session.State == "running",
                completed.State == "completed",
                interrupted.State == "interrupted",
                faction.Member,
                faction.Relationship,
                perceived.Perceived,
                perceived.Confidence,
                deterministic,
                stale,
                nativeSucceeded,
                nativeEvents,
                trace,
                native.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray());
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or
                                           OverflowException or DllNotFoundException or BadImageFormatException)
        {
            return FailedWorldUx6H($"Simulation refusée : {exception.Message}",
                [new("script.world.preview_failed", "script/worldApis", "error", exception.Message)]);
        }
    }

    internal StudioUx6HSmokeResult RunUx6HContractSmoke()
    {
        var sourceProject = Project;
        if (Session is null || sourceProject is null || History is null) return FailedSmokeUx6H();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-h", Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6HSmokeResult result;
        try
        {
            var evidenceProject = CreateEvidenceProjectUx6H(sourceProject);
            if (!ProjectSession.Create(root, evidenceProject).Succeeded)
                return FailedSmokeUx6H();
            var document = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(document);
            var editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(document.RelativePath, out var initial, out _) ||
                initial is null)
                return FailedSmokeUx6H();
            var catalog = editing.WorldCatalogUx6H();
            var world = editing.DefaultWorldDraftUx6H();
            if (world is null) return FailedSmokeUx6H();
            var plan = editing.BuildWorldApiSourceUx6H(initial, world);
            if (!plan.Succeeded) return FailedSmokeUx6H();
            var candidate = initial with { Source = plan.CandidateSource };
            var worldResult = editing.PreviewWorldApisUx6H(candidate, world);
            var preview = editing.PreviewScriptSourceUx6F(candidate);
            var compilation = editing.CompileScriptSourceUx6F(candidate);
            var sandbox = editing.RunScriptSandboxUx6F(candidate);
            var stale = !editing.ApplyApprovedScriptSourceUx6F(candidate with
            {
                Source = candidate.Source + Environment.NewLine + "// approbation périmée",
            }).Succeeded;
            editing.PreviewScriptSourceUx6F(candidate);
            var applied = editing.ApplyApprovedScriptSourceUx6F(candidate);
            var undone = editing.UndoScriptSourceUx6F();
            var undoDocument = new ForgeScriptSourceStore(root).Load(candidate.RelativePath);
            var redone = editing.RedoScriptSourceUx6F();
            var redoDocument = new ForgeScriptSourceStore(root).Load(candidate.RelativePath);
            var reopened = new StudioSessionController();
            var persistence = reopened.Open(root).Succeeded &&
                              reopened.TryOpenCanonicalSourceUx6F(candidate.RelativePath,
                                  out var reopenedDraft, out _) && reopenedDraft?.Source == candidate.Source;
            reopened.Close();
            var applyUndoRedo = applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                                undoDocument.Source == document.Source && redoDocument.Source == candidate.Source;
            var neighbors = ReferenceEquals(editing.Project, editing.Session?.Project) &&
                            editing.Project?.Areas.Count == evidenceProject.Areas.Count &&
                            editing.Project?.Blueprints.Count == evidenceProject.Blueprints.Count &&
                            editing.Project?.Dialogues.Count == evidenceProject.Dialogues.Count &&
                            editing.Project?.Quests.Count == evidenceProject.Quests.Count &&
                            editing.Project?.Module.Factions.Factions.Count ==
                            evidenceProject.Module.Factions.Factions.Count;
            var inventoryApi = worldResult.InventoryCommitted && worldResult.InventoryQuantity == world.Quantity;
            var questApi = worldResult.QuestTransitioned && worldResult.QuestProgress == world.QuestDelta;
            var dialogueApi = worldResult.DialogueStarted && worldResult.DialogueCompleted &&
                              worldResult.DialogueInterrupted;
            var factionApi = worldResult.FactionJoined && worldResult.Reputation == world.ReputationDelta;
            var perceptionApi = worldResult.DeterministicPerception && worldResult.Confidence >= 0;
            var resolution = worldResult.OptionalFallbackSafe && worldResult.RequiredMissingDiagnostic;
            var rollback = worldResult.InventoryRollback && worldResult.StaleHandleBlocked;
            var succeeded = catalog.Complete && plan.Succeeded && inventoryApi && questApi && dialogueApi &&
                            factionApi && perceptionApi && resolution && rollback &&
                            worldResult.NativeSandboxExecuted && preview.Succeeded && compilation.Succeeded &&
                            sandbox.Succeeded && stale && applyUndoRedo && persistence && neighbors;
            var sharedSourceAuthority = initial.Id == document.Id &&
                                        candidate.Id == document.Id &&
                                        editing.CurrentScriptDraftUx6F()?.Id == document.Id;
            result = new(succeeded, true, sharedSourceAuthority, catalog.Complete,
                plan.ReviewedSource.Contains("inventory_transfer", StringComparison.Ordinal),
                inventoryApi, questApi, dialogueApi, factionApi, perceptionApi, resolution, rollback,
                worldResult.NativeSandboxExecuted, preview.CanonicalDryRun, compilation.Succeeded,
                sandbox.Succeeded, stale, applyUndoRedo, persistence, neighbors, false);
            editing.Close();
        }
        finally
        {
            released = DeleteUx6FTemporaryWorkspace(root);
        }
        return result with
        {
            Succeeded = result.Succeeded && released,
            TemporaryWorkspaceReleased = released,
        };
    }

    internal static ProjectDefinition CreateEvidenceProjectUx6H(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Module.Factions.Factions.Any(item => !item.Archived)) return project;
        var faction = new FactionDefinition(
            Guid.Parse("6e000000-0000-4000-8000-000000000080"),
            1,
            Ux6HProvenance,
            "forest-wardens",
            "Gardiens de la forêt",
            false,
            [],
            ["guardian", "forest"]);
        var source = project.Module.Factions;
        var factions = source with { Factions = [.. source.Factions, faction] };
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithFactions(factions),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
    }

    private IReadOnlyList<StudioUx6FDiagnostic> ValidateWorldDraftUx6H(StudioUx6HWorldDraft world)
    {
        var catalog = WorldCatalogUx6H();
        var diagnostics = new List<StudioUx6FDiagnostic>();
        Add(!catalog.Items.Any(item => item.Key == world.ItemId), "script.world.inventory.item_missing",
            "world/inventory/item", "Choisissez un objet réel du projet.");
        Add(!catalog.Actors.Any(item => item.Key == world.InventoryOwner),
            "script.world.inventory.owner_missing", "world/inventory/owner",
            "Choisissez un propriétaire réel.");
        Add(!catalog.Actors.Any(item => item.Key == world.InventoryRecipient) ||
            world.InventoryOwner == world.InventoryRecipient, "script.world.inventory.recipient_invalid",
            "world/inventory/recipient", "Le destinataire doit être un autre acteur réel.");
        Add(world.Quantity is < 1 or > 999, "script.world.inventory.quantity_invalid",
            "world/inventory/quantity", "La quantité doit rester entre 1 et 999.");
        var quest = catalog.Quests.FirstOrDefault(item => item.Key == world.QuestId);
        Add(quest is null, "script.world.quest.missing", "world/quest", "Choisissez une quête réelle.");
        if (quest is not null)
        {
            Add(!quest.States.Any(item => item.Key == world.QuestFromState),
                "script.world.quest.from_missing", "world/quest/from", "Choisissez un état source réel.");
            Add(!quest.States.Any(item => item.Key == world.QuestToState),
                "script.world.quest.to_missing", "world/quest/to", "Choisissez un état cible réel.");
        }
        Add(string.IsNullOrWhiteSpace(world.QuestScope) || string.IsNullOrWhiteSpace(world.QuestObjective),
            "script.world.quest.scope_invalid", "world/quest/scope",
            "Portée et objectif sont requis.");
        Add(!catalog.Dialogues.Any(item => item.Key == world.DialogueId),
            "script.world.dialogue.missing", "world/dialogue", "Choisissez un dialogue réel.");
        Add(!catalog.Actors.Any(item => item.Key == world.DialogueSpeaker) ||
            !catalog.Actors.Any(item => item.Key == world.DialogueListener) ||
            world.DialogueSpeaker == world.DialogueListener || string.IsNullOrWhiteSpace(world.DialogueOutcome),
            "script.world.dialogue.participants_invalid", "world/dialogue/participants",
            "Locuteur, interlocuteur distinct et résultat sont requis.");
        Add(!catalog.Factions.Any(item => item.Key == world.FactionId),
            "script.world.faction.missing", "world/faction", "Créez ou choisissez une faction réelle via UX4.");
        Add(!catalog.Actors.Any(item => item.Key == world.FactionActor) ||
            string.IsNullOrWhiteSpace(world.FactionScope) || world.ReputationDelta is < -200 or > 200,
            "script.world.faction.values_invalid", "world/faction/transaction",
            "Acteur, portée et delta de réputation -200..200 sont requis.");
        Add(!catalog.Actors.Any(item => item.Key == world.Observer) ||
            !catalog.Actors.Any(item => item.Key == world.Target) || world.Observer == world.Target,
            "script.world.perception.actors_invalid", "world/perception/actors",
            "Observateur et cible doivent être deux acteurs réels distincts.");
        Add(!SensesUx6H.Contains(world.Sense) || !double.IsFinite(world.Distance) ||
            !double.IsFinite(world.Range) || !double.IsFinite(world.Signal) || world.Distance < 0 ||
            world.Range < 0 || world.Signal is < 0 or > 1,
            "script.world.perception.values_invalid", "world/perception",
            "Sens, distance, portée et signal doivent respecter le contrat de perception.");
        return diagnostics;

        void Add(bool condition, string code, string path, string message)
        {
            if (condition) diagnostics.Add(new(code, path, "error", message));
        }
    }

    private static string WorldApiBlockUx6H(StudioUx6HWorldDraft world) =>
        $"{Ux6HStartMarker}{Environment.NewLine}" +
        "// API ForgeScript 1.0.0 · bloc auteur revu; les identités restent celles du projet.\n" +
        "void StudioWorldApiScenario()\n" +
        "{\n" +
        $"    EntityRef inventoryOwner = spawn(\"{QuoteUx6H(world.InventoryOwner)}\");\n" +
        $"    EntityRef inventoryRecipient = spawn(\"{QuoteUx6H(world.InventoryRecipient)}\");\n" +
        $"    InventoryResultRef inventoryResult = inventory_transfer(inventoryOwner, inventoryRecipient, \"{QuoteUx6H(world.ItemId)}\", {world.Quantity});\n" +
        $"    QuestResultRef questResult = quest_transition(\"{QuoteUx6H(world.QuestId)}\", \"{QuoteUx6H(world.QuestScope)}\", \"{QuoteUx6H(world.QuestFromState)}\", \"{QuoteUx6H(world.QuestToState)}\", \"{QuoteUx6H(world.QuestObjective)}\", {world.QuestDelta}, \"studio-preview\");\n" +
        $"    EntityRef dialogueSpeaker = spawn(\"{QuoteUx6H(world.DialogueSpeaker)}\");\n" +
        $"    EntityRef dialogueListener = spawn(\"{QuoteUx6H(world.DialogueListener)}\");\n" +
        $"    DialogueSessionRef dialogueSession = dialogue_start(\"{QuoteUx6H(world.DialogueId)}\", dialogueSpeaker, dialogueListener);\n" +
        $"    dialogue_complete(dialogueSession, \"{QuoteUx6H(world.DialogueOutcome)}\");\n" +
        $"    FactionResultRef factionResult = faction_reputation(dialogueListener, \"{QuoteUx6H(world.FactionId)}\", {world.ReputationDelta}, {world.JoinFaction.ToString().ToLowerInvariant()}, \"{QuoteUx6H(world.FactionScope)}\", \"studio-preview\");\n" +
        $"    PerceptionResultRef perception = perception_query(dialogueSpeaker, dialogueListener, \"{QuoteUx6H(world.Sense)}\", {FloatUx6H(world.Distance)}, {FloatUx6H(world.Range)}, {world.LineOfSight.ToString().ToLowerInvariant()}, {FloatUx6H(world.Signal)}, {world.Seed});\n" +
        "}\n" + Ux6HEndMarker;

    private static string NativeWorldApiSourceUx6H(StudioUx6HWorldDraft world) =>
        WorldApiBlockUx6H(world)
            .Replace(Ux6HStartMarker + Environment.NewLine +
                     "// API ForgeScript 1.0.0 · bloc auteur revu; les identités restent celles du projet.\n" +
                     "void StudioWorldApiScenario()", "void main()", StringComparison.Ordinal)
            .Replace(Environment.NewLine + Ux6HEndMarker, string.Empty, StringComparison.Ordinal);

    private static string QuoteUx6H(string value) => (value ?? string.Empty)
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    private static string FloatUx6H(double value)
    {
        var literal = value.ToString("0.######", CultureInfo.InvariantCulture);
        return literal.Contains(".", StringComparison.Ordinal) ? literal + "f" : literal + ".0f";
    }

    private static StudioUx6HWorldResult FailedWorldUx6H(
        string message,
        IReadOnlyList<StudioUx6FDiagnostic> diagnostics) =>
        new(false, message, false, false, false, false, 0, 0, false, 0, false, false, false,
            false, 0, false, 0, false, false, false, [], [], diagnostics);

    private static StudioUx6HSmokeResult FailedSmokeUx6H() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false);
}
