using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;
using DomainVector3 = Forge.Domain.Spatial.Vector3Value;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1RSmokeResult RunUx1RContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1r-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ae0000-0000-4000-8000-000000000001")),
                "coffre-epines",
                "Coffre d’épines",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ae0000-0000-4000-8000-000000000002")),
                "borne-voisine",
                "Borne voisine",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1ae0000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(DomainVector3.Zero, QuaternionValue.Identity, DomainVector3.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1ae0000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(4, 0, 1), QuaternionValue.Identity, DomainVector3.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1ae0000-0000-4000-8000-000000000005")),
                "atelier-pieges",
                "Atelier des pièges",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1ae0000-0000-4000-8000-000000000006")),
                "Studio UX1-R",
                [blueprint, neighborBlueprint],
                [area]);
            var originalProfile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1ae0000-0000-4000-8000-000000000010"),
                "test:ux1-r-profile");
            var originalTrap = new PlaceableTrapProfile(
                Guid.Parse("c1ae0000-0000-4000-8000-000000000011"),
                "interaction:use",
                "damage:old-thorns",
                8,
                9,
                20,
                "anciens-gardiens",
                "vfx:old-thorns",
                "anciens-gardiens",
                true,
                false);
            var hiddenContainerEvents = new PlaceableContainerStateEventsProfile(
                Guid.Parse("c1ae0000-0000-4000-8000-000000000012"),
                "1.0.0",
                "ContainerOpened",
                "ContainerClosed",
                true,
                true,
                true,
                true);
            originalProfile = originalProfile with
            {
                Foundation = originalProfile.Foundation! with
                {
                    Components = originalProfile.Foundation.Components with { HasTrap = true },
                },
                SecurityEvents = new(
                    Guid.Parse("c1ae0000-0000-4000-8000-000000000013"),
                    1,
                    "test:ux1-r-security-events",
                    Trap: originalTrap,
                    ContainerEvents: hiddenContainerEvents),
                SecurityEventsRequiredAtRuntime = true,
            };
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1ae0000-0000-4000-8000-000000000014"),
                "test:ux1-r-neighbor");
            var worldFeatures = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-r-catalog",
                placeables: [originalProfile, neighbor]);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module.WithWorldFeatures(worldFeatures),
                project.Blueprints,
                project.Areas);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1R_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1RSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1R_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1RSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var trapDraft = sourceDraft.Trap with
            {
                TriggerId = "interaction:open",
                PayloadId = "damage:thorn-burst",
                DetectionDifficulty = 14,
                DisarmDifficulty = 16,
                ResetDelaySeconds = 45,
                OwnerId = "gardiens-forestiers",
                VisualEffectId = "vfx:thorn-burst",
                FriendlyPolicy = "gardiens-forestiers",
                Armed = true,
                OneShot = true,
            };
            var draft = sourceDraft with
            {
                HasTrap = true,
                Trap = trapDraft,
                RequireTrapRuntime = true,
            };
            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with { Trap = trapDraft with { TriggerId = string.Empty } }),
                editing.PreviewPlaceable(draft with { Trap = trapDraft with { PayloadId = string.Empty } }),
                editing.PreviewPlaceable(draft with { Trap = trapDraft with { DetectionDifficulty = -1 } }),
                editing.PreviewPlaceable(draft with { Trap = trapDraft with { DisarmDifficulty = 1001 } }),
                editing.PreviewPlaceable(draft with { Trap = trapDraft with { ResetDelaySeconds = 86401 } }),
                editing.PreviewPlaceable(draft with { Trap = trapDraft with { VisualEffectId = string.Empty } }),
                editing.PreviewPlaceable(draft with { HasTrap = false, RequireTrapRuntime = true }),
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
            var trapPreview = editing.PreviewPlaceableTrap(
                draft,
                StudioPlaceableTrapPreviewMode.TriggerHostile);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Piège", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var trap = profile.SecurityEvents?.Trap;
            var commandParity = directBuilt &&
                                directProfile?.TrapRequiredAtRuntime == profile.TrapRequiredAtRuntime &&
                                TrapValuesEqual(directProfile?.SecurityEvents?.Trap, trap);
            var preservation = profile.Id == loadedOriginal.Id &&
                               profile.Provenance == loadedOriginal.Provenance &&
                               profile.Foundation?.Id == loadedOriginal.Foundation?.Id &&
                               profile.Operations == loadedOriginal.Operations &&
                               profile.SecurityEvents?.Id == loadedOriginal.SecurityEvents?.Id &&
                               profile.SecurityEvents?.Provenance == loadedOriginal.SecurityEvents?.Provenance &&
                               profile.SecurityEventsRequiredAtRuntime ==
                               loadedOriginal.SecurityEventsRequiredAtRuntime &&
                               ReferenceEquals(
                                   profile.SecurityEvents?.ContainerEvents,
                                   loadedOriginal.SecurityEvents?.ContainerEvents) &&
                               profile.SecurityEvents?.Loot is null &&
                               profile.SecurityEvents?.LockEvents is null &&
                               profile.SecurityEvents?.ContentsChanged is null &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor) &&
                               appliedProject.Blueprints.SequenceEqual(originalProject.Blueprints);

            var adapter = new PlaceableSecurityEventsRuntimeAdapter();
            var resolution = adapter.ResolveTrap(profile);
            var runtimeTrap = adapter.BuildTrap(resolution.Handle!, profile);
            var hidden = adapter.Detect(resolution.Handle!, profile, 13, "adversaires");
            var detected = adapter.Detect(resolution.Handle!, profile, 14, "adversaires");
            var failedDisarm = adapter.Disarm(resolution.Handle!, profile, 15);
            var disarmed = adapter.Disarm(resolution.Handle!, profile, 16);
            var friendly = adapter.Trigger(resolution.Handle!, profile, "gardiens-forestiers");
            var hostile = adapter.Trigger(resolution.Handle!, profile, "adversaires");
            var reset = adapter.Reset(resolution.Handle!, profile, hostile.Trap);
            var triggerAndPayload = trap is not null && trap.TriggerId == "interaction:open" &&
                                    trap.PayloadId == "damage:thorn-burst";
            var detectAndDisarm = !hidden.Succeeded && hidden.EventName == "TrapHidden" &&
                                  detected.Succeeded && detected.EventName == "TrapDetected" &&
                                  !failedDisarm.Succeeded && disarmed.Succeeded && !disarmed.Trap.Armed;
            var resetAndOneShot = runtimeTrap.OneShot && hostile.Succeeded && !hostile.Trap.Armed && reset.Armed;
            var ownershipVfxFriendly = runtimeTrap.OwnerId == "gardiens-forestiers" &&
                                       runtimeTrap.VisualEffectId == "vfx:thorn-burst" &&
                                       runtimeTrap.FriendlyPolicy == "gardiens-forestiers" &&
                                       !friendly.Succeeded && friendly.EventName == "TrapIgnored";
            var trapPreviewResolved = trapPreview.Succeeded && trapPreview.OperationSucceeded &&
                                      trapPreview.EventName == "TrapTriggered" &&
                                      trapPreview.Summary.Contains("déclenché", StringComparison.OrdinalIgnoreCase);
            var runtimeProjection = applied.Succeeded && resolution.Available &&
                                    profile.TrapRequiredAtRuntime && runtimeTrap.Id == trap?.Id &&
                                    runtimeTrap.DetectionDifficulty == 14 && runtimeTrap.DisarmDifficulty == 16 &&
                                    runtimeTrap.ResetDelay == TimeSpan.FromSeconds(45) && runtimeTrap.Armed;

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["trapTrigger"] == "interaction:open" &&
                             scriptHandle.Properties["trapPayload"] == "damage:thorn-burst" &&
                             scriptHandle.Properties["trapDetection"] == "14" &&
                             scriptHandle.Properties["trapDisarm"] == "16" &&
                             scriptHandle.Properties["trapResetSeconds"] == "45" &&
                             scriptHandle.Properties["trapOwner"] == "gardiens-forestiers" &&
                             scriptHandle.Properties["trapVisualEffect"] == "vfx:thorn-burst" &&
                             scriptHandle.Properties["trapFriendlyPolicy"] == "gardiens-forestiers" &&
                             scriptHandle.Properties["trapArmed"] == "True" &&
                             scriptHandle.Properties["trapOneShot"] == "True" &&
                             scriptHandle.Properties["trapRequiredAtRuntime"] == "True";
            var optionalProfile = profile with
            {
                SecurityEvents = profile.SecurityEvents! with { Trap = null },
                TrapRequiredAtRuntime = false,
            };
            var requiredProfile = optionalProfile with { TrapRequiredAtRuntime = true };
            var optional = adapter.ResolveTrap(optionalProfile);
            var required = adapter.ResolveTrap(requiredProfile);
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_TRAP_REQUIRED_MISSING";

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
                              restored.TrapRequiredAtRuntime &&
                              TrapValuesEqual(restored.SecurityEvents?.Trap, trap) &&
                              restored.SecurityEventsRequiredAtRuntime &&
                              restored.SecurityEvents?.ContainerEvents?.OpenedBinding == "ContainerOpened" &&
                              restored.SecurityEvents?.Loot is null && restored.SecurityEvents?.LockEvents is null &&
                              restored.SecurityEvents?.ContentsChanged is null && restored.Operations is null &&
                              restoredNeighbor?.Id == neighbor.Id && reopened.Project?.Blueprints.Count == 2;
            var succeeded = triggerAndPayload && detectAndDisarm && resetAndOneShot && ownershipVfxFriendly &&
                            trapPreviewResolved && dryRun && commandParity && instanceSelection && preservation &&
                            runtimeProjection && scriptRead && optionalRequired && undoRedo && persistence &&
                            invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                triggerAndPayload,
                detectAndDisarm,
                resetAndOneShot,
                ownershipVfxFriendly,
                trapPreviewResolved,
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
                $"OPENRPG_FORGE_STUDIO_UX1R_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1RSmoke();
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

    internal StudioPlaceableTrapPreview PreviewPlaceableTrap(
        StudioPlaceableDraft draft,
        StudioPlaceableTrapPreviewMode mode)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasTrap)
        {
            return new(
                false,
                "Piège désactivé · activez le composant avant l’aperçu.",
                "WORLD_PLACEABLE_TRAP_DISABLED",
                false,
                false,
                "TrapUnavailable",
                "disabled");
        }
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) ||
            profile?.SecurityEvents?.Trap is null)
        {
            return new(
                false,
                message,
                LastPlaceableDiagnosticCode,
                false,
                false,
                "TrapInvalid",
                "invalid");
        }

        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var resolution = adapter.ResolveTrap(profile);
        if (!resolution.Available || resolution.Handle is null)
        {
            return new(
                false,
                HumanizePlaceable(resolution.DiagnosticCode),
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_TRAP_UNAVAILABLE",
                false,
                false,
                "TrapUnavailable",
                "unavailable");
        }

        var trap = adapter.BuildTrap(resolution.Handle, profile);
        var hostileFaction = string.Equals(trap.FriendlyPolicy, "adversaires", StringComparison.Ordinal)
            ? "intrus"
            : "adversaires";
        TrapResult result = mode switch
        {
            StudioPlaceableTrapPreviewMode.DetectHidden =>
                adapter.Detect(resolution.Handle, profile, Math.Max(0, trap.DetectionDifficulty - 1), hostileFaction),
            StudioPlaceableTrapPreviewMode.DetectSuccess =>
                adapter.Detect(resolution.Handle, profile, trap.DetectionDifficulty, hostileFaction),
            StudioPlaceableTrapPreviewMode.DisarmFailure =>
                adapter.Disarm(resolution.Handle, profile, Math.Max(0, trap.DisarmDifficulty - 1)),
            StudioPlaceableTrapPreviewMode.DisarmSuccess =>
                adapter.Disarm(resolution.Handle, profile, trap.DisarmDifficulty),
            StudioPlaceableTrapPreviewMode.TriggerFriendly =>
                adapter.Trigger(resolution.Handle, profile, trap.FriendlyPolicy),
            StudioPlaceableTrapPreviewMode.TriggerHostile =>
                adapter.Trigger(resolution.Handle, profile, hostileFaction),
            StudioPlaceableTrapPreviewMode.Reset => new(
                adapter.Reset(resolution.Handle, profile, trap with { Armed = false }),
                true,
                "TrapReset",
                "rearmed"),
            _ => adapter.Detect(resolution.Handle, profile, trap.DetectionDifficulty, hostileFaction),
        };
        var decision = result.EventName switch
        {
            "TrapHidden" => "reste caché",
            "TrapDetected" => "est détecté",
            "TrapDisarmFailed" => "résiste au désamorçage",
            "TrapDisarmed" => "est désamorcé",
            "TrapIgnored" or "TrapFriendlyIgnored" => "ignore l’allié",
            "TrapTriggered" => "est déclenché",
            "TrapReset" => "est réarmé",
            "TrapInactive" or "TrapAlreadyDisarmed" => "est déjà inactif",
            _ => "est évalué",
        };
        return new(
            true,
            $"Aperçu · le piège {decision} · état {(result.Trap.Armed ? "armé" : "désarmé")} · aucun effet ni module modifié.",
            $"RPG_PLACEABLE_{result.EventName.ToUpperInvariant()}",
            result.Succeeded,
            result.Trap.Armed,
            result.EventName,
            result.Detail);
    }

    private static StudioUx1RSmokeResult FailedUx1RSmoke() => new(
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
        false,
        false);
}
