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
    internal StudioUx1PSmokeResult RunUx1PContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1p-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ac0000-0000-4000-8000-000000000001")),
                "coffre-des-veilleurs",
                "Coffre des veilleurs",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ac0000-0000-4000-8000-000000000002")),
                "borne-sans-verrou",
                "Borne sans verrou",
                BlueprintKind.Placeable);
            var keyItem = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ac0000-0000-4000-8000-000000000003")),
                "cle-des-veilleurs",
                "Clé des veilleurs",
                BlueprintKind.Item);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1ac0000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(DomainVector3.Zero, QuaternionValue.Identity, DomainVector3.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1ac0000-0000-4000-8000-000000000005")),
                blueprint.Id,
                new TransformValue(new(4, 0, 2), QuaternionValue.Identity, DomainVector3.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1ac0000-0000-4000-8000-000000000006")),
                "atelier-verrous",
                "Atelier des verrous",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1ac0000-0000-4000-8000-000000000010")),
                "Studio UX1-P",
                [blueprint, neighborBlueprint, keyItem],
                [area]);
            var originalProfile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1ac0000-0000-4000-8000-000000000011"),
                "test:ux1-p-profile");
            var stateMachine = CreateContainerCompatibilityStateMachine(blueprint.Id);
            var protection = CreateContainerCompatibilityProtection(blueprint.Id);
            originalProfile = originalProfile with
            {
                Operations = new(
                    Guid.Parse("c1ac0000-0000-4000-8000-000000000012"),
                    1,
                    "test:ux1-p-operations",
                    null,
                    stateMachine,
                    null,
                    protection,
                    null),
            };
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1ac0000-0000-4000-8000-000000000013"),
                "test:ux1-p-neighbor");
            var worldFeatures = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-p-catalog",
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
                    "OPENRPG_FORGE_STUDIO_UX1P_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1PSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1P_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1PSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var lockDraft = sourceDraft.Lock with
            {
                InitialState = PlaceableAuthoredLockState.Locked,
                KeyItemId = keyItem.Id,
                OtherCredentialIds = ["guild:watch"],
                SkillId = "mechanics",
                Difficulty = 14,
                CodeEnabled = true,
                AccessCode = "4271",
                ExistingCodeHash = null,
                Hackable = true,
                Relockable = true,
                CanJam = true,
                RequiredPermissions = ["lock.use", "lock.admin"],
            };
            var draft = sourceDraft with
            {
                HasLock = true,
                Lock = lockDraft,
                RequireOperationsRuntime = true,
            };
            var unknownItem = BlueprintId.From(Guid.Parse("c1ac0000-0000-4000-8000-000000000099"));
            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with { Lock = lockDraft with { Id = Guid.Empty } }),
                editing.PreviewPlaceable(draft with
                {
                    Lock = lockDraft with
                    {
                        KeyItemId = null,
                        OtherCredentialIds = [],
                        SkillId = string.Empty,
                        CodeEnabled = false,
                        AccessCode = string.Empty,
                        Hackable = false,
                        RequiredPermissions = [],
                    },
                }),
                editing.PreviewPlaceable(draft with { Lock = lockDraft with { KeyItemId = unknownItem } }),
                editing.PreviewPlaceable(draft with
                {
                    Lock = lockDraft with { KeyItemId = null, SkillId = string.Empty, Hackable = true },
                }),
                editing.PreviewPlaceable(draft with { Lock = lockDraft with { Difficulty = -1 } }),
                editing.PreviewPlaceable(draft with
                {
                    Lock = lockDraft with { OtherCredentialIds = ["guild:watch", "guild:watch"] },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Lock = lockDraft with
                    {
                        CodeEnabled = true,
                        AccessCode = string.Empty,
                        ExistingCodeHash = null,
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
            var lockPreview = editing.PreviewPlaceableLock(
                draft,
                StudioPlaceableLockPreviewMode.Credential,
                0);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Verrouillage", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var lockProfile = profile.Operations?.Lock;
            var statesAndMechanisms = applied.Succeeded && lockProfile is not null &&
                                      lockProfile.InitialState == PlaceableAuthoredLockState.Locked &&
                                      lockProfile.CredentialIds.Count == 2 &&
                                      lockProfile.CredentialIds.Contains($"item:{keyItem.Id.Value:D}") &&
                                      lockProfile.SkillId == "mechanics" && lockProfile.Difficulty == 14 &&
                                      lockProfile.CodeHash is not null && lockProfile.Hackable &&
                                      lockProfile.Relockable && lockProfile.CanJam &&
                                      lockProfile.RequiredPermissions.SequenceEqual(["lock.use", "lock.admin"]);
            var commandParity = directBuilt &&
                                directProfile?.OperationsRequiredAtRuntime == profile.OperationsRequiredAtRuntime &&
                                LockValuesEqual(directProfile?.Operations?.Lock, lockProfile);
            var preservation = profile.Id == loadedOriginal.Id &&
                               profile.Provenance == loadedOriginal.Provenance &&
                               profile.Foundation?.Id == loadedOriginal.Foundation?.Id &&
                               ReferenceEquals(profile.Operations?.StateMachine, loadedOriginal.Operations?.StateMachine) &&
                               ReferenceEquals(profile.Operations?.Protection, loadedOriginal.Operations?.Protection) &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor) &&
                               appliedProject.Blueprints.SequenceEqual(originalProject.Blueprints);

            var adapter = new PlaceableLockRuntimeAdapter();
            var resolution = adapter.Resolve(profile);
            var runtime = adapter.Build(resolution.Handle!, profile);
            var attempts = new[]
            {
                Attempt(credentials: new HashSet<string> { $"item:{keyItem.Id.Value:D}" }),
                Attempt(codeHash: lockProfile!.CodeHash),
                Attempt(skill: 14),
                Attempt(skill: 14, hacking: true),
                Attempt(permissions: new HashSet<string> { "lock.use", "lock.admin" }),
                Attempt(gameMaster: true),
            };
            var unlockResults = attempts.Select(attempt =>
                adapter.PreviewUnlock(resolution.Handle!, profile, attempt)).ToArray();
            var unlockMethods = unlockResults.All(result => result.Succeeded) &&
                                unlockResults.Select(result => result.Method).SequenceEqual(
                                    ["credential", "code", "skill", "hacking", "permission", "gm"]);
            var openedLock = unlockResults[0].Lock;
            var relocked = adapter.PreviewRelock(resolution.Handle!, profile, openedLock, false, "player:test");
            var jammed = adapter.PreviewRelock(resolution.Handle!, profile, openedLock, true, "player:test");
            var relockAndJam = relocked.Succeeded && relocked.Lock.State == LockState.Locked &&
                               jammed.Succeeded && jammed.Lock.State == LockState.Jammed;
            var runtimeProjection = resolution.Available && runtime.State == LockState.Locked &&
                                    runtime.Credentials.Count == 2 && runtime.SkillId == "mechanics" &&
                                    runtime.Difficulty == 14 && runtime.CodeHash is not null &&
                                    runtime.Hackable && runtime.Relockable && runtime.CanJam &&
                                    runtime.Permissions.Count == 2;
            var previewResolved = lockPreview.Succeeded && lockPreview.Method == "credential" &&
                                  lockPreview.State == PlaceableAuthoredLockState.Unlocked &&
                                  lockPreview.Summary.Contains("ouverture autorisée", StringComparison.OrdinalIgnoreCase);

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["lockState"] == "Locked" &&
                             scriptHandle.Properties["lockCredentialIds"].Contains(
                                 keyItem.Id.Value.ToString("D"), StringComparison.Ordinal) &&
                             scriptHandle.Properties["lockSkill"] == "mechanics" &&
                             scriptHandle.Properties["lockDifficulty"] == "14" &&
                             scriptHandle.Properties["lockHasCode"] == "True" &&
                             scriptHandle.Properties["lockHackable"] == "True" &&
                             scriptHandle.Properties["lockRelockable"] == "True" &&
                             scriptHandle.Properties["lockCanJam"] == "True" &&
                             scriptHandle.Properties["lockPermissions"] == "lock.admin,lock.use";
            var optionalProfile = profile with
            {
                Operations = profile.Operations! with { Lock = null },
                Foundation = profile.Foundation! with
                {
                    Components = profile.Foundation.Components with { HasLock = false },
                },
            };
            var requiredProfile = optionalProfile with
            {
                Foundation = optionalProfile.Foundation! with
                {
                    Components = optionalProfile.Foundation.Components with { HasLock = true },
                },
            };
            var optional = adapter.Resolve(optionalProfile);
            var required = adapter.Resolve(requiredProfile);
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_LOCK_REQUIRED_MISSING";

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
                              restored.Foundation?.Components.HasLock == true &&
                              LockValuesEqual(restored.Operations?.Lock, lockProfile) &&
                              restoredNeighbor?.Id == neighbor.Id && reopened.Project?.Blueprints.Count == 3;
            var succeeded = statesAndMechanisms && unlockMethods && relockAndJam && previewResolved &&
                            dryRun && commandParity && instanceSelection && preservation && runtimeProjection &&
                            scriptRead && optionalRequired && undoRedo && persistence && invalidBlocked &&
                            humanDiagnostics;
            return new(
                succeeded,
                statesAndMechanisms,
                unlockMethods,
                relockAndJam,
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
                $"OPENRPG_FORGE_STUDIO_UX1P_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1PSmoke();
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

    internal StudioPlaceableLockPreview PreviewPlaceableLock(
        StudioPlaceableDraft draft,
        StudioPlaceableLockPreviewMode mode,
        double skillResult)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasLock)
        {
            return new(false, "Verrou désactivé · activez la section avant l’aperçu.",
                "WORLD_PLACEABLE_LOCK_DISABLED", "disabled", PlaceableAuthoredLockState.Unlocked);
        }
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) ||
            profile?.Operations?.Lock is null)
        {
            return new(false, message, LastPlaceableDiagnosticCode, "invalid", draft.Lock.InitialState);
        }

        var adapter = new PlaceableLockRuntimeAdapter();
        var resolution = adapter.Resolve(profile);
        if (!resolution.Available || resolution.Handle is null)
        {
            return new(false, HumanizePlaceable(resolution.DiagnosticCode),
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_LOCK_UNAVAILABLE", "unavailable",
                draft.Lock.InitialState);
        }
        var component = adapter.Build(resolution.Handle, profile);
        LockResult result;
        if (mode is StudioPlaceableLockPreviewMode.Relock or StudioPlaceableLockPreviewMode.Jam)
        {
            result = adapter.PreviewRelock(
                resolution.Handle,
                profile,
                component with { State = LockState.Unlocked },
                mode == StudioPlaceableLockPreviewMode.Jam,
                "player:preview");
        }
        else
        {
            var attempt = mode switch
            {
                StudioPlaceableLockPreviewMode.Credential => Attempt(
                    credentials: component.Credentials.Take(1).ToHashSet(StringComparer.Ordinal)),
                StudioPlaceableLockPreviewMode.Skill => Attempt(skill: skillResult),
                StudioPlaceableLockPreviewMode.Code => Attempt(codeHash: component.CodeHash),
                StudioPlaceableLockPreviewMode.Hacking => Attempt(skill: skillResult, hacking: true),
                StudioPlaceableLockPreviewMode.Permission => Attempt(permissions: component.Permissions),
                StudioPlaceableLockPreviewMode.GameMaster => Attempt(gameMaster: true),
                _ => Attempt(),
            };
            result = adapter.PreviewUnlock(resolution.Handle, profile, attempt);
        }

        var state = MapAuthoredLockState(result.Lock.State);
        var summary = result.Succeeded
            ? $"Aperçu · {(state == PlaceableAuthoredLockState.Unlocked ? "ouverture autorisée" : HumanizeLockState(state))} · " +
              $"{HumanLockMethod(result.Method)} · le module n’a pas été modifié."
            : $"Aperçu · tentative refusée · {HumanLockMethod(result.Method)} · le module n’a pas été modifié.";
        return new(
            result.Succeeded,
            summary,
            result.Succeeded ? "RPG_PLACEABLE_LOCK_PREVIEW_SUCCEEDED" : "RPG_PLACEABLE_LOCK_PREVIEW_FAILED",
            result.Method,
            state);
    }

    private static LockAttempt Attempt(
        IReadOnlySet<string>? credentials = null,
        IReadOnlySet<string>? permissions = null,
        string? codeHash = null,
        double skill = 0,
        bool hacking = false,
        bool gameMaster = false)
        => new("player:preview", credentials ?? new HashSet<string>(), permissions ?? new HashSet<string>(),
            codeHash, skill, hacking, gameMaster);

    private static PlaceableAuthoredLockState MapAuthoredLockState(LockState state) => state switch
    {
        LockState.Unlocked => PlaceableAuthoredLockState.Unlocked,
        LockState.Locked => PlaceableAuthoredLockState.Locked,
        LockState.Jammed => PlaceableAuthoredLockState.Jammed,
        LockState.Broken => PlaceableAuthoredLockState.Broken,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static string HumanLockMethod(string method) => method switch
    {
        "credential" => "clé ou justificatif reconnu",
        "code" => "code reconnu",
        "skill" => "défi de compétence réussi",
        "hacking" => "piratage réussi",
        "permission" => "droits requis présents",
        "gm" => "dérogation du maître de jeu",
        "relock:player:preview" => "reverrouillage autorisé",
        "jam:player:preview" => "brouillage autorisé",
        "already-unlocked" => "déjà déverrouillé",
        "broken" => "mécanisme brisé",
        "unavailable" => "cycle indisponible",
        "jam-disabled" => "brouillage interdit",
        _ => "aucun moyen d’ouverture accepté",
    };

    private static StudioUx1PSmokeResult FailedUx1PSmoke() => new(
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
