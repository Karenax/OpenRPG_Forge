using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1GSmokeResult RunUx1GContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1g-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a30000-0000-4000-8000-000000000001")),
                "orbe-des-veilleurs",
                "Orbe des veilleurs",
                BlueprintKind.Placeable);
            var parentBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a30000-0000-4000-8000-00000000000c")),
                "socle-des-veilleurs",
                "Socle des veilleurs",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1a30000-0000-4000-8000-000000000002")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1a30000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(new(3, 0, 0), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1a30000-0000-4000-8000-000000000004")),
                "atelier-objet-utilisable",
                "Atelier de l’objet utilisable",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1a30000-0000-4000-8000-000000000005")),
                "Studio UX1-G",
                [blueprint, parentBlueprint],
                [area]);
            var inspectVerbId = Guid.Parse("c1a30000-0000-4000-8000-000000000006");
            var activateVerbId = Guid.Parse("c1a30000-0000-4000-8000-000000000007");
            var parentId = Guid.Parse("c1a30000-0000-4000-8000-000000000008");
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Guid.Parse("c1a30000-0000-4000-8000-000000000009"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-g-foundation",
                blueprint.Slug,
                [
                    new("en-US", "Watcher orb", "Inspect the orb"),
                    new("fr-FR", blueprint.DisplayName, "Examiner l’orbe"),
                ],
                ["interactif", "magie"],
                "Un objet utilisable avec deux actions gouvernées.",
                new(
                    "asset:orbe-veilleurs",
                    ["mesh:orbe", "mesh:socle"],
                    ["material:cristal", "material:pierre"],
                    "asset:orbe-icon",
                    "asset:orbe-portrait",
                    ["vfx:orbe-lueur"]),
                new("sphere", new(1.2f, 1.2f, 1.2f), true, true, "interaction"),
                new(
                    [
                        new(inspectVerbId, "orbe-des-veilleurs.verb.examiner", "placeable-inspected", "player.can.inspect"),
                        new(activateVerbId, "orbe-des-veilleurs.verb.activer", "placeable-activated", "power.available"),
                    ],
                    2,
                    120,
                    true,
                    "orbe-des-veilleurs.use",
                    .25,
                    "owner",
                    ["player.alive"]),
                null,
                new(false, false, false, false, true,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["onGlow"] = "scripts/orbe_glow",
                    }),
                new(new(.13f, 0, -.13f), Vector3Value.Zero, Vector3Value.One, new(0, .6f, 0), .25,
                    parentId, true, "animation:orbe-awaken"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["lore"] = "watchers",
                },
                new(
                    Guid.Parse("c1a30000-0000-4000-8000-00000000000a"),
                    WorldFeatureCatalog.CurrentSchemaVersion,
                    "test:ux1-g-on-used",
                    "1.0.0",
                    "InteractionExecuted",
                    "scripts/orbe_before",
                    "scripts/orbe_after"),
                new(
                    ["animation:orbe-awaken"],
                    [],
                    [],
                    [],
                    ["physics:orbe"],
                    3,
                    2),
                true);
            var originalProfile = new PlaceableWorldProfile(
                Guid.Parse("c1a30000-0000-4000-8000-00000000000b"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-g-profile",
                blueprint.Id,
                "watchers",
                null,
                new("credential:watcher", PlaceableWorldProfile.CredentialConsumption.Retain, 0, null, true),
                new(
                    [new("heartbeat", "scripts/orbe_heartbeat", false, true)],
                    new(true, 10, 6, true, "watcher")),
                new(PlaceableWorldProfile.BodyMode.Kinematic, 12, ["rotation"], true, 2, false, "server"),
                foundation,
                true);
            var parentProfile = CreatePlaceableTransformParent(
                parentBlueprint,
                parentId,
                "test:ux1-g-parent");
            var seededCatalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-g-catalog",
                placeables: [originalProfile, parentProfile]);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module.WithWorldFeatures(seededCatalog),
                project.Blueprints,
                project.Areas,
                project.Dialogues,
                project.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded || !editing.Open(tempRoot).Succeeded)
            {
                return FailedUx1GSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginalProfile = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedOriginalFoundation = loadedOriginalProfile.Foundation!;
            var draft = editing.CreatePlaceableDraft(blueprint.Id)! with
            {
                Role = StudioPlaceableRole.Interactable,
                Prompt = "Choisir une action",
                InteractionVerbs =
                [
                    new(inspectVerbId, "Examiner", "placeable-inspected", "player.can.inspect",
                        "Examiner l’orbe", "orbe-des-veilleurs.verb.examiner"),
                    new(activateVerbId, "Activer", "placeable-activated", "power.available",
                        "Activer", "orbe-des-veilleurs.verb.activer"),
                ],
                InteractionDistance = 3.25,
                InteractionAngle = 90,
                RequiresFocus = true,
                InteractionDuration = .75,
                InteractionAuthority = "server",
                InteractionConditionsText = "player.conscious, line_of_sight",
                OnUsedEnabled = true,
                BeforeUseScript = "scripts/orbe_before_use",
                AfterUseScript = "scripts/orbe_after_use",
                RequireOnUsedRuntime = true,
            };

            var noVerb = editing.PreviewPlaceable(draft with { InteractionVerbs = [] });
            var invalidRange = editing.PreviewPlaceable(draft with { InteractionDistance = 0 });
            var missingRequiredEvent = editing.PreviewPlaceable(draft with
            {
                OnUsedEnabled = false,
                RequireOnUsedRuntime = true,
            });
            var invalidBlocked = !noVerb.Succeeded && !invalidRange.Succeeded && !missingRequiredEvent.Succeeded;
            var humanDiagnostics = invalidBlocked &&
                                   IsHumanPlaceableMessage(noVerb.Message) &&
                                   IsHumanPlaceableMessage(invalidRange.Message) &&
                                   IsHumanPlaceableMessage(missingRequiredEvent.Message);

            var directBuilt = editing.TryBuildPlaceable(
                draft,
                dryRun: true,
                out _,
                out var directProfile,
                out _);
            var preview = editing.PreviewPlaceable(draft);
            var dryRun = preview.Succeeded && preview.CanApply && ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item => item.BlueprintId == blueprint.Id);
            var appliedFoundation = profile.Foundation;
            var appliedInteraction = appliedFoundation?.Interaction;
            var interactableRole = applied.Succeeded && appliedFoundation is
            {
                StaticProp: null,
                Interaction: not null,
            };
            var verbsValid = appliedInteraction?.Verbs.Count == 2 &&
                             appliedInteraction.Verbs[0] is
                             {
                                 Id: var firstVerbId,
                                 EventName: "placeable-inspected",
                                 Condition: "player.can.inspect",
                             } &&
                             firstVerbId == inspectVerbId &&
                             appliedInteraction.Verbs[1] is
                             {
                                 Id: var secondVerbId,
                                 EventName: "placeable-activated",
                                 Condition: "power.available",
                             } &&
                             secondVerbId == activateVerbId;
            var rangeFocus = appliedInteraction is
            {
                MaximumDistance: 3.25,
                MaximumAngleDegrees: 90,
                RequiresFocus: true,
                DurationSeconds: .75,
            };
            var authorityConditions = appliedInteraction?.Authority == "server" &&
                                      appliedInteraction.Conditions.SequenceEqual(
                                          ["player.conscious", "line_of_sight"]);
            var onUsed = appliedFoundation?.OnUsed is
            {
                EventBinding: "InteractionExecuted",
                BeforeScriptId: "scripts/orbe_before_use",
                AfterScriptId: "scripts/orbe_after_use",
            } && appliedFoundation.OnUsedRequiredAtRuntime;
            var directFoundation = directProfile?.Foundation;
            var directInteraction = directFoundation?.Interaction;
            var commandParity = directBuilt && directInteraction is not null && appliedInteraction is not null &&
                                directInteraction.Verbs.SequenceEqual(appliedInteraction.Verbs) &&
                                directInteraction.MaximumDistance == appliedInteraction.MaximumDistance &&
                                directInteraction.MaximumAngleDegrees == appliedInteraction.MaximumAngleDegrees &&
                                directInteraction.RequiresFocus == appliedInteraction.RequiresFocus &&
                                directInteraction.DurationSeconds == appliedInteraction.DurationSeconds &&
                                directInteraction.Authority == appliedInteraction.Authority &&
                                directInteraction.Conditions.SequenceEqual(appliedInteraction.Conditions) &&
                                directFoundation?.OnUsed == appliedFoundation?.OnUsed &&
                                directFoundation?.OnUsedRequiredAtRuntime == appliedFoundation?.OnUsedRequiredAtRuntime;
            var preservation = profile.Id == loadedOriginalProfile.Id && profile.FactionId == "watchers" &&
                               ReferenceEquals(profile.Credential, loadedOriginalProfile.Credential) &&
                               ReferenceEquals(profile.Reactive, loadedOriginalProfile.Reactive) &&
                               ReferenceEquals(profile.Physics, loadedOriginalProfile.Physics) &&
                               appliedFoundation?.Texts.Any(item => item.Locale == "en-US" &&
                                   item.Name == "Watcher orb" && item.Prompt == "Inspect the orb") == true &&
                               appliedFoundation.Visual.MeshIds.SequenceEqual(loadedOriginalFoundation.Visual.MeshIds) &&
                               appliedFoundation.Visual.MaterialIds.SequenceEqual(loadedOriginalFoundation.Visual.MaterialIds) &&
                               appliedFoundation.Visual.VisualEffectIds.SequenceEqual(
                                   loadedOriginalFoundation.Visual.VisualEffectIds) &&
                               appliedFoundation.Collision.PhysicsLayer == "interaction" &&
                               appliedFoundation.Components.ScriptBindings["onGlow"] == "scripts/orbe_glow" &&
                               appliedFoundation.Transform.ParentId == parentId &&
                               appliedFoundation.Transform.TransformAnimationId == "animation:orbe-awaken" &&
                               appliedFoundation.Overrides["lore"] == "watchers" &&
                               ReferenceEquals(appliedFoundation.Validation, loadedOriginalFoundation.Validation) &&
                               appliedFoundation.OnUsed?.Id == loadedOriginalFoundation.OnUsed?.Id &&
                               appliedFoundation.OnUsed?.Provenance == "test:ux1-g-on-used";

            var eventId = Guid.Parse("c1a30000-0000-4000-8000-00000000000c");
            var executed = WorldFeatureRuntimeResolver.ExecutePlaceableUse(
                profile,
                eventId,
                inspectVerbId,
                "player:ilyne",
                new(1, 2, 3),
                "success",
                DateTimeOffset.UnixEpoch);
            var runtimeEvent = executed.EventId == eventId && executed.PlaceableId == profile.Id &&
                               executed.VerbId == inspectVerbId && executed.ActorId == "player:ilyne" &&
                               executed.TargetPoint == new Vector3Value(1, 2, 3) && executed.Result == "success" &&
                               executed.EventBinding == "InteractionExecuted" &&
                               executed.ForgeScriptApiVersion == "1.0.0" &&
                               executed.BeforeScriptId == "scripts/orbe_before_use" &&
                               executed.AfterScriptId == "scripts/orbe_after_use";
            var optionalFailure = PlaceableUseFailure(profile with
            {
                Foundation = appliedFoundation! with
                {
                    OnUsed = null,
                    OnUsedRequiredAtRuntime = false,
                },
            }, inspectVerbId);
            var requiredFailure = PlaceableUseFailure(profile with
            {
                Foundation = appliedFoundation! with
                {
                    OnUsed = null,
                    OnUsedRequiredAtRuntime = true,
                },
            }, inspectVerbId);
            var optionalRequired = optionalFailure == "WORLD_PLACEABLE_ON_USED_UNAVAILABLE" &&
                                   requiredFailure == "WORLD_PLACEABLE_ON_USED_REQUIRED_MISSING";

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
            var restoredFoundation = restored?.Foundation;
            var persistence = save.Succeeded && reopen.Succeeded && restoredFoundation is
            {
                Interaction:
                {
                    Verbs.Count: 2,
                    MaximumDistance: 3.25,
                    MaximumAngleDegrees: 90,
                    RequiresFocus: true,
                    DurationSeconds: .75,
                    Authority: "server",
                },
                OnUsed:
                {
                    BeforeScriptId: "scripts/orbe_before_use",
                    AfterScriptId: "scripts/orbe_after_use",
                },
                OnUsedRequiredAtRuntime: true,
            } && restoredFoundation.Interaction.Conditions.SequenceEqual(
                ["player.conscious", "line_of_sight"]) &&
                              restoredFoundation.Texts.Any(item => item.Locale == "en-US" &&
                                  item.Name == "Watcher orb") &&
                              restoredFoundation.Transform.ParentId == parentId;

            var succeeded = interactableRole && verbsValid && rangeFocus && authorityConditions && onUsed && dryRun &&
                            commandParity && instanceSelection && preservation && runtimeEvent && optionalRequired &&
                            undoRedo && persistence && invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                interactableRole,
                verbsValid,
                rangeFocus,
                authorityConditions,
                onUsed,
                dryRun,
                commandParity,
                instanceSelection,
                preservation,
                runtimeEvent,
                optionalRequired,
                undoRedo,
                persistence,
                invalidBlocked,
                humanDiagnostics);
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

    private static string PlaceableUseFailure(PlaceableWorldProfile profile, Guid verbId)
    {
        try
        {
            WorldFeatureRuntimeResolver.ExecutePlaceableUse(
                profile,
                Guid.Parse("c1a30000-0000-4000-8000-00000000000d"),
                verbId,
                "player:test",
                Vector3Value.Zero,
                "success",
                DateTimeOffset.UnixEpoch);
            return string.Empty;
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }
    }

    private static StudioUx1GSmokeResult FailedUx1GSmoke() => new(
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
