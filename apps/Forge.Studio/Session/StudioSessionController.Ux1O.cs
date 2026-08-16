using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1OSmokeResult RunUx1OContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1o-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ab0000-0000-4000-8000-000000000001")),
                "autel-des-voix",
                "Autel des voix",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ab0000-0000-4000-8000-000000000002")),
                "borne-silencieuse",
                "Borne silencieuse",
                BlueprintKind.Placeable);
            var ritualItem = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ab0000-0000-4000-8000-000000000003")),
                "sceau-du-veilleur",
                "Sceau du veilleur",
                BlueprintKind.Item);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1ab0000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1ab0000-0000-4000-8000-000000000005")),
                blueprint.Id,
                new TransformValue(new(3, 0, 2), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1ab0000-0000-4000-8000-000000000006")),
                "atelier-conversations",
                "Atelier des conversations",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var greeting = Dialogue(
                "c1ab0000-0000-4000-8000-000000000010",
                "c1ab0000-0000-4000-8000-000000000011",
                "salut-de-lautell",
                "Salut de l’autel",
                "Qui vient troubler mon sommeil ?",
                ["default", "warning"]);
            var secret = Dialogue(
                "c1ab0000-0000-4000-8000-000000000020",
                "c1ab0000-0000-4000-8000-000000000021",
                "secret-du-veilleur",
                "Secret du veilleur",
                "Le sceau reconnaît son porteur.",
                ["default", "initiated"]);
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1ab0000-0000-4000-8000-000000000030")),
                "Studio UX1-O",
                [blueprint, neighborBlueprint, ritualItem],
                [area],
                [greeting, secret],
                []);
            var originalProfile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1ab0000-0000-4000-8000-000000000031"),
                "test:ux1-o-profile") with
            {
                DialogueId = greeting.Id.Value,
            };
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1ab0000-0000-4000-8000-000000000032"),
                "test:ux1-o-neighbor");
            var worldFeatures = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-o-catalog",
                placeables: [originalProfile, neighbor]);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module.WithWorldFeatures(worldFeatures),
                project.Blueprints,
                project.Areas,
                project.Dialogues,
                project.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1O_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1OSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1O_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1OSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var ids = Enumerable.Range(40, 6)
                .Select(index => Guid.Parse($"c1ab0000-0000-4000-8000-{index:000000000000}"))
                .ToArray();
            var bindings = new StudioPlaceableConversationBindingDraft[]
            {
                new(ids[0], greeting.Id.Value, PlaceableConversationTrigger.Interaction,
                    "examiner", null, "default", 50, true),
                new(ids[1], greeting.Id.Value, PlaceableConversationTrigger.State,
                    "brisé", null, "warning", 40, false),
                new(ids[2], secret.Id.Value, PlaceableConversationTrigger.User,
                    "tag:initié", null, "initiated", 30, false),
                new(ids[3], secret.Id.Value, PlaceableConversationTrigger.Item,
                    string.Empty, ritualItem.Id, "default", 20, false),
                new(ids[4], greeting.Id.Value, PlaceableConversationTrigger.Event,
                    "OnDisturbed", null, "warning", 10, false),
                new(ids[5], secret.Id.Value, PlaceableConversationTrigger.Interaction,
                    "examiner", null, "default", 5, false),
            };
            var draft = sourceDraft with
            {
                HasConversation = true,
                Conversation = sourceDraft.Conversation with { Bindings = bindings },
                RequireConversationRuntime = true,
            };
            var unknownDialogue = Guid.Parse("c1ab0000-0000-4000-8000-000000000099");
            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with
                {
                    Conversation = draft.Conversation with { Id = Guid.Empty },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Conversation = draft.Conversation with
                    {
                        Bindings = [bindings[0] with { Id = Guid.Empty }, bindings[1] with { Id = Guid.Empty }],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Conversation = draft.Conversation with
                    {
                        Bindings = [bindings[0] with { DialogueId = unknownDialogue }],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Conversation = draft.Conversation with
                    {
                        Bindings = [bindings[0] with { EntryPointKey = "missing" }],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Conversation = draft.Conversation with
                    {
                        Bindings = [bindings[0] with { Selector = "bad?selector" }],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Conversation = draft.Conversation with
                    {
                        Bindings = [bindings[3] with { ItemBlueprintId = null }],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Conversation = draft.Conversation with
                    {
                        Bindings = [bindings[0] with { Priority = 1_001 }],
                    },
                }),
            };
            var invalidBlocked = invalidResults.All(result => !result.Succeeded);
            var humanDiagnostics = invalidBlocked && invalidResults.All(result =>
                IsHumanPlaceableMessage(result.Message));

            var directBuilt = editing.TryBuildPlaceable(
                draft,
                dryRun: true,
                out _,
                out var directProfile,
                out _);
            var preview = editing.PreviewPlaceable(draft);
            var conversationPreview = editing.PreviewPlaceableConversation(
                draft,
                bindings[0].Id,
                alreadyUsed: false);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Conversation", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var conversation = profile.Conversation;
            var fiveTriggers = applied.Succeeded && conversation is not null &&
                               Enum.GetValues<PlaceableConversationTrigger>().All(trigger =>
                                   conversation.Bindings.Any(binding => binding.Trigger == trigger));
            var dialogueAndEntryPoints = conversation?.Bindings.Count == 6 &&
                                         conversation.Bindings.Select(binding => binding.DialogueId)
                                             .Distinct().Order().SequenceEqual(
                                                 new[] { greeting.Id.Value, secret.Id.Value }.Order()) &&
                                         conversation.Bindings.Any(binding => binding.EntryPointKey == "initiated");
            var commandParity = directBuilt &&
                                directProfile?.ConversationRequiredAtRuntime ==
                                profile.ConversationRequiredAtRuntime &&
                                ConversationValuesEqual(directProfile?.Conversation, conversation);
            var preservation = profile.Id == loadedOriginal.Id &&
                               profile.Provenance == loadedOriginal.Provenance &&
                               profile.DialogueId == greeting.Id.Value &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               profile.Foundation?.Id == loadedOriginal.Foundation?.Id &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor) &&
                               appliedProject.Dialogues.SequenceEqual(originalProject.Dialogues);

            var adapter = new PlaceableConversationRuntimeAdapter();
            var resolution = adapter.Resolve(profile, appliedProject.Dialogues, appliedProject.Blueprints);
            var runtime = adapter.Build(
                resolution.Handle!, profile, appliedProject.Dialogues, appliedProject.Blueprints);
            var interaction = adapter.Evaluate(
                resolution.Handle!,
                profile,
                new(PlaceableConversationTrigger.Interaction, "examiner", null, new HashSet<Guid>()));
            var interactionReplay = adapter.Evaluate(
                resolution.Handle!,
                profile,
                new(PlaceableConversationTrigger.Interaction, "examiner", null,
                    new HashSet<Guid> { bindings[0].Id }));
            var itemDecision = adapter.Evaluate(
                resolution.Handle!,
                profile,
                new(PlaceableConversationTrigger.Item, string.Empty, ritualItem.Id, new HashSet<Guid>()));
            var priorityAndOnce = interaction.BindingId == bindings[0].Id && interaction.OncePerUser &&
                                  interactionReplay.BindingId == bindings[5].Id;
            var runtimeProjection = resolution.Available && runtime.Bindings.Count == 6 &&
                                    runtime.Bindings[0].Priority == 50 &&
                                    itemDecision.DialogueId == secret.Id.Value &&
                                    itemDecision.EntryPointKey == "default";
            var previewResolved = conversationPreview.Succeeded &&
                                  conversationPreview.DialogueId == greeting.Id.Value &&
                                  conversationPreview.BindingId == bindings[0].Id &&
                                  conversationPreview.Summary.Contains("lancée", StringComparison.Ordinal);

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["dialogue"] == greeting.Id.ToString() &&
                             scriptHandle.Properties["conversationBindingCount"] == "6" &&
                             scriptHandle.Properties["conversationDialogueIds"].Contains(
                                 secret.Id.ToString(), StringComparison.Ordinal) &&
                             scriptHandle.Properties["conversationTriggers"] ==
                             "Event,Interaction,Item,State,User" &&
                             scriptHandle.Properties["conversationEntryPoints"] ==
                             "default,initiated,warning" &&
                             scriptHandle.Properties["conversationRequiredAtRuntime"] == "True";
            var optional = adapter.Resolve(profile with
            {
                Conversation = null,
                ConversationRequiredAtRuntime = false,
            }, appliedProject.Dialogues, appliedProject.Blueprints);
            var required = adapter.Resolve(profile with
            {
                Conversation = null,
                ConversationRequiredAtRuntime = true,
            }, appliedProject.Dialogues, appliedProject.Blueprints);
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_CONVERSATION_REQUIRED_MISSING";

            var undo = editing.Undo();
            var undone = ReferenceEquals(originalProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undo.Succeeded && undone && redo.Succeeded && redone;
            var save = editing.Save();
            editing.Close();
            var reopen = reopened.Open(tempRoot);
            var restored = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == blueprint.Id);
            var restoredNeighbor = reopened.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var persistence = save.Succeeded && reopen.Succeeded && restored is not null &&
                              restored.ConversationRequiredAtRuntime &&
                              ConversationValuesEqual(restored.Conversation, conversation) &&
                              restored.DialogueId == greeting.Id.Value &&
                              restoredNeighbor?.Id == neighbor.Id &&
                              reopened.Project?.Dialogues.Count == 2;
            var succeeded = fiveTriggers && dialogueAndEntryPoints && priorityAndOnce && previewResolved &&
                            dryRun && commandParity && instanceSelection && preservation && runtimeProjection &&
                            scriptRead && optionalRequired && undoRedo && persistence && invalidBlocked &&
                            humanDiagnostics;
            return new(
                succeeded,
                fiveTriggers,
                dialogueAndEntryPoints,
                priorityAndOnce,
                previewResolved,
                dryRun,
                commandParity,
                instanceSelection,
                preservation,
                runtimeProjection,
                scriptRead,
                optionalRequired,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1O_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1OSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    internal StudioPlaceableConversationPreview PreviewPlaceableConversation(
        StudioPlaceableDraft draft,
        Guid? bindingId,
        bool alreadyUsed)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasConversation)
        {
            return new(false, "Conversation désactivée · activez la section avant l’aperçu.",
                "WORLD_PLACEABLE_CONVERSATION_DISABLED", null, null);
        }
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) ||
            profile?.Conversation is null || Project is null)
        {
            return new(false, message, LastPlaceableDiagnosticCode, null, null);
        }
        var binding = profile.Conversation.Bindings.FirstOrDefault(candidate => candidate.Id == bindingId) ??
                      profile.Conversation.Bindings.FirstOrDefault();
        if (binding is null)
        {
            return new(false, "Aucune liaison à prévisualiser.",
                "WORLD_PLACEABLE_CONVERSATION_BINDING_MISSING", null, null);
        }

        var adapter = new PlaceableConversationRuntimeAdapter();
        var resolution = adapter.Resolve(profile, Project.Dialogues, Project.Blueprints);
        if (!resolution.Available || resolution.Handle is null)
        {
            return new(false, HumanizePlaceable(resolution.DiagnosticCode),
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_CONVERSATION_UNAVAILABLE", null, null);
        }
        var completed = alreadyUsed ? new HashSet<Guid> { binding.Id } : new HashSet<Guid>();
        var decision = adapter.Evaluate(
            resolution.Handle,
            profile,
            new(binding.Trigger, binding.Selector, binding.ItemBlueprintId, completed));
        var dialogue = Project.Dialogues.FirstOrDefault(candidate => candidate.Id.Value == binding.DialogueId);
        var dialogueName = dialogue?.DisplayName ?? "Dialogue indisponible";
        var trigger = HumanConversationTrigger(binding.Trigger);
        if (decision.Matched)
        {
            return new(
                true,
                $"Conversation lancée · {dialogueName} · {HumanConversationEntry(binding.EntryPointKey)} · " +
                $"{trigger} · priorité {binding.Priority} · " +
                $"{(binding.OncePerUser ? "une fois par utilisateur" : "réutilisable")} · le module n’a pas été modifié.",
                decision.Code,
                decision.BindingId,
                decision.DialogueId);
        }
        var summary = decision.Code == "RPG_PLACEABLE_CONVERSATION_ALREADY_USED"
            ? $"Conversation déjà jouée · {dialogueName} ne sera pas relancé pour cet utilisateur · le module n’a pas été modifié."
            : $"Aucune conversation ne correspond à {trigger.ToLowerInvariant()} · le module n’a pas été modifié.";
        return new(false, summary, decision.Code, null, null);
    }

    private static DialogueDefinition Dialogue(
        string dialogueId,
        string nodeId,
        string slug,
        string displayName,
        string fallback,
        IReadOnlyList<string> entryPointKeys)
    {
        var node = DialogueNodeId.From(Guid.Parse(nodeId));
        var id = DialogueId.From(Guid.Parse(dialogueId));
        return new(
            id,
            slug,
            displayName,
            node,
            [new(node, null, new($"dialogue.{slug}", fallback))],
            provenance: "test:ux1-o",
            entryPoints: entryPointKeys.Select((key, index) => new DialogueEntryPoint(
                StablePlaceableId(BlueprintId.From(id.Value), $"entry:{key}"),
                DialogueEntryPoint.CurrentSchemaVersion,
                key,
                node,
                index,
                "test:ux1-o")).ToArray());
    }

    private static string HumanConversationTrigger(PlaceableConversationTrigger trigger) => trigger switch
    {
        PlaceableConversationTrigger.Interaction => "Interaction avec le placeable",
        PlaceableConversationTrigger.State => "État du placeable",
        PlaceableConversationTrigger.User => "Profil de l’utilisateur",
        PlaceableConversationTrigger.Item => "Objet présenté",
        PlaceableConversationTrigger.Event => "Événement reçu",
        _ => "Déclencheur",
    };

    private static string HumanConversationEntry(string entry) => entry switch
    {
        "default" => "entrée principale",
        "warning" => "entrée d’avertissement",
        "initiated" => "entrée des initiés",
        _ => $"entrée {entry.Replace('-', ' ').Replace('_', ' ')}",
    };

    private static StudioUx1OSmokeResult FailedUx1OSmoke() => new(
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);
}
