using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1MSmokeResult RunUx1MContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1m-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a90000-0000-4000-8000-000000000001")),
                "obelisque-du-serment",
                "Obélisque du serment",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a90000-0000-4000-8000-000000000002")),
                "socle-voisin",
                "Socle voisin",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1a90000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1a90000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(new(4, 0, 2), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1a90000-0000-4000-8000-000000000005")),
                "atelier-protection-plot",
                "Atelier de protection plot",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1a90000-0000-4000-8000-000000000006")),
                "Studio UX1-M",
                [blueprint, neighborBlueprint],
                [area]);

            var stateMachine = new PlaceableVisualStateMachineProfile(
                Guid.Parse("c1a90000-0000-4000-8000-000000000010"),
                "closed",
                Ux1KStateProfiles("ux1-m-legacy"),
                [
                    new(
                        Guid.Parse("c1a90000-0000-4000-8000-000000000011"),
                        "closed",
                        "broken",
                        "destroyed",
                        .25,
                        null),
                ]);
            var container = new PlaceableContainerProfile(
                Guid.Parse("c1a90000-0000-4000-8000-000000000012"),
                12,
                90,
                "party",
                ["container.open"],
                true,
                "loot:obelisque-du-serment",
                "ui:placeable-chest",
                true,
                "inventory:obelisque-du-serment");
            var damageable = new PlaceableDamageableProfile(
                Guid.Parse("c1a90000-0000-4000-8000-000000000013"),
                100,
                100,
                5,
                new Dictionary<string, double> { ["physical"] = .1 },
                [.75, .5, .25],
                "vfx:legacy-break",
                "debris:legacy-obelisque",
                true,
                10);
            var originalProtection = new PlaceableProtectionProfile(
                Guid.Parse("c1a90000-0000-4000-8000-000000000014"),
                ["move"],
                ["placeable.maintain"],
                "Déplacement réservé à la maintenance.",
                false,
                "PlaceableMaintenanceOverride");
            var lockProfile = new PlaceableLockProfile(
                Guid.Parse("c1a90000-0000-4000-8000-000000000015"),
                PlaceableAuthoredLockState.Locked,
                ["key:serment"],
                "mechanics",
                9,
                null,
                true,
                true,
                false,
                ["lock.admin"]);
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Guid.Parse("c1a90000-0000-4000-8000-000000000016"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-m-foundation",
                blueprint.Slug,
                [new("fr-FR", blueprint.DisplayName, "Examiner l’obélisque")],
                ["obélisque", "plot"],
                "Politique narrative à modifier sans toucher aux opérations voisines.",
                new(
                    "asset:obelisque-du-serment",
                    ["mesh:obelisque-du-serment"],
                    ["material:pierre"],
                    "asset:obelisque-icon",
                    "asset:obelisque-portrait",
                    ["vfx:obelisque-idle"]),
                new("box", new(1, 2, 1), true, true, "world"),
                null,
                new(true, true, true, false, true),
                new(
                    true,
                    true,
                    true,
                    false,
                    false,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["onProtected"] = "scripts/obelisque_protected",
                    }),
                new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, .25,
                    null, true, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["variant"] = "serment",
                },
                Validation: new(
                    stateMachine.States.Select(state => state.AnimationId).ToArray(),
                    ["lock:serment"],
                    [],
                    ["inventory:obelisque-du-serment"],
                    [],
                    4,
                    2));
            var operations = new PlaceableOperationsProfile(
                Guid.Parse("c1a90000-0000-4000-8000-000000000017"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-m-operations",
                container,
                stateMachine,
                damageable,
                originalProtection,
                lockProfile);
            var originalProfile = new PlaceableWorldProfile(
                Guid.Parse("c1a90000-0000-4000-8000-000000000018"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-m-profile",
                blueprint.Id,
                "neutral",
                null,
                null,
                new([], new(false, 2, 2, true, "protected")),
                null,
                foundation,
                true,
                operations,
                true);
            var neighborId = Guid.Parse("c1a90000-0000-4000-8000-000000000019");
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                neighborId,
                "test:ux1-m-neighbor");
            var seededCatalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-m-catalog",
                placeables: [originalProfile, neighbor]);
            project = new ProjectDefinition(
                project.Id,
                project.Title,
                project.Module.WithWorldFeatures(seededCatalog),
                project.Blueprints,
                project.Areas,
                project.Dialogues,
                project.Quests);
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1M_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1MSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1M_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1MSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var loadedFoundation = loadedOriginal.Foundation!;
            var loadedOperations = loadedOriginal.Operations!;
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var protectionDraft = sourceDraft.Protection with
            {
                ProtectedActions = ["destroy", "move", "use"],
                RequiredPermissions = ["placeable.plot.override", "placeable.maintain"],
                Feedback = "Cet objet est protégé par le scénario principal.",
                AllowGameMasterOverride = true,
                AuditBinding = "PlaceableProtectionOverride",
            };
            var draft = sourceDraft with
            {
                HasProtection = true,
                Protection = protectionDraft,
                RequireOperationsRuntime = true,
            };

            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with
                {
                    Protection = protectionDraft with { Id = Guid.Empty },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Protection = protectionDraft with { ProtectedActions = [] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Protection = protectionDraft with { ProtectedActions = [string.Empty] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Protection = protectionDraft with { ProtectedActions = ["destroy", "destroy"] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Protection = protectionDraft with { RequiredPermissions = [] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Protection = protectionDraft with { RequiredPermissions = [string.Empty] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Protection = protectionDraft with
                    {
                        RequiredPermissions = ["placeable.plot.override", "placeable.plot.override"],
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Protection = protectionDraft with { Feedback = string.Empty },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Protection = protectionDraft with { AuditBinding = string.Empty },
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
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Protection plot : 3 action(s) protégée(s)",
                             StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var protection = profile.Operations?.Protection;
            var protectedActions = applied.Succeeded && protection?.ProtectedActions.SequenceEqual(
                ["destroy", "move", "use"]) == true;
            var permissions = protection?.RequiredPermissions.SequenceEqual(
                ["placeable.plot.override", "placeable.maintain"]) == true;
            var feedback = protection?.Feedback == "Cet objet est protégé par le scénario principal.";
            var commandParity = directBuilt &&
                                directProfile?.OperationsRequiredAtRuntime == profile.OperationsRequiredAtRuntime &&
                                ProtectionValuesEqual(directProfile?.Operations?.Protection, protection);
            var preservation = profile.Id == loadedOriginal.Id && profile.Provenance == loadedOriginal.Provenance &&
                               profile.FactionId == loadedOriginal.FactionId &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               profile.Operations?.Id == loadedOperations.Id &&
                               profile.Operations?.Provenance == loadedOperations.Provenance &&
                               ReferenceEquals(profile.Operations?.Container, loadedOperations.Container) &&
                               ReferenceEquals(profile.Operations?.StateMachine, loadedOperations.StateMachine) &&
                               ReferenceEquals(profile.Operations?.Damageable, loadedOperations.Damageable) &&
                               ReferenceEquals(profile.Operations?.Lock, loadedOperations.Lock) &&
                               profile.Foundation?.Id == loadedFoundation.Id &&
                               profile.Foundation.Slug == loadedFoundation.Slug &&
                               profile.Foundation.Texts.SequenceEqual(loadedFoundation.Texts) &&
                               profile.Foundation.Tags.SequenceEqual(loadedFoundation.Tags) &&
                               SamePlaceableVisual(profile.Foundation.Visual, loadedFoundation.Visual) &&
                               profile.Foundation.Collision == loadedFoundation.Collision &&
                               profile.Foundation.StaticProp == loadedFoundation.StaticProp &&
                               SamePlaceableComponents(profile.Foundation.Components, loadedFoundation.Components) &&
                               profile.Foundation.Overrides.OrderBy(item => item.Key)
                                   .SequenceEqual(loadedFoundation.Overrides.OrderBy(item => item.Key)) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor);

            var adapter = new PlaceableOperationsRuntimeAdapter();
            var resolution = adapter.Resolve(profile);
            var blocked = adapter.Authorize(
                resolution.Handle!, profile, "destroy", new HashSet<string>(), false,
                "player:1", string.Empty);
            var authorized = adapter.Authorize(
                resolution.Handle!, profile, "destroy",
                new HashSet<string> { "placeable.plot.override", "placeable.maintain" }, false,
                "builder:1", string.Empty);
            var overridden = adapter.Authorize(
                resolution.Handle!, profile, "destroy", new HashSet<string>(), true,
                "gm:karen", "restaurer la mise en scène");
            var unprotected = adapter.Authorize(
                resolution.Handle!, profile, "inspect", new HashSet<string>(), false,
                "player:1", string.Empty);
            var runtimeEnforcement = resolution.Available && !blocked.Allowed &&
                                     blocked.Code == "RPG_PLACEABLE_PROTECTED" &&
                                     blocked.Feedback == protection?.Feedback;
            var policyPreview = authorized.Allowed &&
                                authorized.Code == "RPG_PLACEABLE_PERMISSION_ALLOWED" &&
                                unprotected.Allowed && unprotected.Code == "RPG_PLACEABLE_ACTION_ALLOWED";
            var gameMasterOverride = overridden.Allowed &&
                                     overridden.Code == "RPG_PLACEABLE_GM_OVERRIDE" &&
                                     protection?.AllowGameMasterOverride == true;
            var auditTrail = profile.Operations?.Protection?.AuditBinding == "PlaceableProtectionOverride" &&
                             overridden.AuditEntry.Contains("gm:karen", StringComparison.Ordinal) &&
                             overridden.AuditEntry.Contains("restaurer la mise en scène", StringComparison.Ordinal);
            var runtime = adapter.Build(resolution.Handle!, profile);
            runtimeEnforcement = runtimeEnforcement && runtime.Protection is not null &&
                                 runtime.Protection.ProtectedActions.SetEquals(
                                     new HashSet<string> { "destroy", "move", "use" }) &&
                                 adapter.Validate(resolution.Handle!, profile).Count == 0;

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["protectedActions"] == "destroy,move,use" &&
                             scriptHandle.Properties["protectionPermissions"] ==
                             "placeable.maintain,placeable.plot.override" &&
                             scriptHandle.Properties["protectionFeedback"] ==
                             "Cet objet est protégé par le scénario principal." &&
                             scriptHandle.Properties["allowGameMasterOverride"] == "True" &&
                             scriptHandle.Properties["protectionAudit"] == "PlaceableProtectionOverride";
            var optional = adapter.Resolve(profile with
            {
                Operations = null,
                OperationsRequiredAtRuntime = false,
            });
            var required = adapter.Resolve(profile with
            {
                Operations = null,
                OperationsRequiredAtRuntime = true,
            });
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING";

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
                              restored.OperationsRequiredAtRuntime &&
                              ProtectionValuesEqual(restored.Operations?.Protection, protection) &&
                              restored.Operations?.Container?.Id == container.Id &&
                              StateMachineValuesEqual(restored.Operations?.StateMachine, stateMachine) &&
                              restored.Operations?.Damageable?.Id == damageable.Id &&
                              restored.Operations?.Lock?.Id == lockProfile.Id &&
                              restoredNeighbor?.Id == neighborId;
            var succeeded = protectedActions && permissions && feedback && gameMasterOverride && auditTrail &&
                            policyPreview && dryRun && commandParity && instanceSelection && preservation &&
                            runtimeEnforcement && scriptRead && optionalRequired && undoRedo && persistence &&
                            invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                protectedActions,
                permissions,
                feedback,
                gameMasterOverride,
                auditTrail,
                policyPreview,
                dryRun,
                commandParity,
                instanceSelection,
                preservation,
                runtimeEnforcement,
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
                $"OPENRPG_FORGE_STUDIO_UX1M_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1MSmoke();
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

    private static StudioUx1MSmokeResult FailedUx1MSmoke() => new(
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
        false,
        false);
}
