using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Placeables;
using DomainVector3 = Forge.Domain.Spatial.Vector3Value;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    internal StudioUx1QSmokeResult RunUx1QContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1q-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000001")),
                "porte-archives",
                "Porte des archives",
                BlueprintKind.Placeable);
            var neighborBlueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000002")),
                "borne-partagee",
                "Borne partagée",
                BlueprintKind.Placeable);
            var keyItem = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000003")),
                "cle-des-archives",
                "Clé des archives",
                BlueprintKind.Item);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000004")),
                blueprint.Id,
                new TransformValue(DomainVector3.Zero, QuaternionValue.Identity, DomainVector3.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000005")),
                blueprint.Id,
                new TransformValue(new(3, 0, 2), QuaternionValue.Identity, DomainVector3.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000006")),
                "atelier-justificatifs",
                "Atelier des justificatifs",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000010")),
                "Studio UX1-Q",
                [blueprint, neighborBlueprint, keyItem],
                [area]);
            var originalProfile = CreatePlaceableTransformParent(
                blueprint,
                Guid.Parse("c1ad0000-0000-4000-8000-000000000011"),
                "test:ux1-q-profile");
            var originalLock = new PlaceableLockProfile(
                Guid.Parse("c1ad0000-0000-4000-8000-000000000012"),
                PlaceableAuthoredLockState.Locked,
                ["legacy:watch"],
                "mechanics",
                12,
                null,
                false,
                true,
                false,
                []);
            originalProfile = originalProfile with
            {
                Credential = new(
                    "legacy:watch",
                    PlaceableWorldProfile.CredentialConsumption.Retain,
                    0,
                    null,
                    false),
                Foundation = originalProfile.Foundation! with
                {
                    Components = originalProfile.Foundation.Components with { HasLock = true },
                },
                Operations = new(
                    Guid.Parse("c1ad0000-0000-4000-8000-000000000013"),
                    1,
                    "test:ux1-q-operations",
                    null,
                    CreateContainerCompatibilityStateMachine(blueprint.Id),
                    null,
                    CreateContainerCompatibilityProtection(blueprint.Id),
                    originalLock),
            };
            var neighbor = CreatePlaceableTransformParent(
                neighborBlueprint,
                Guid.Parse("c1ad0000-0000-4000-8000-000000000014"),
                "test:ux1-q-neighbor");
            var worldFeatures = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-q-catalog",
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
                    "OPENRPG_FORGE_STUDIO_UX1Q_PROJECT_CREATE_FAILED diagnostics=" +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1QSmoke();
            }
            var opened = editing.Open(tempRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX1Q_PROJECT_OPEN_FAILED diagnostics=" +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                return FailedUx1QSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginal = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var loadedNeighbor = originalProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == neighborBlueprint.Id);
            var sourceDraft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var credentialDraft = sourceDraft.Credential with
            {
                ItemBlueprintId = keyItem.Id,
                OtherCredentialId = string.Empty,
                Consumption = PlaceableWorldProfile.CredentialConsumption.Charge,
                Charges = 3,
                VirtualPermission = string.Empty,
                AuditUse = true,
            };
            var draft = sourceDraft with
            {
                HasCredentialPolicy = true,
                Credential = credentialDraft,
                RequireCredentialRuntime = true,
            };
            var unknownItem = BlueprintId.From(Guid.Parse("c1ad0000-0000-4000-8000-000000000099"));
            var invalidResults = new[]
            {
                editing.PreviewPlaceable(draft with
                {
                    Credential = credentialDraft with
                    {
                        ItemBlueprintId = null,
                        OtherCredentialId = string.Empty,
                        Consumption = PlaceableWorldProfile.CredentialConsumption.Retain,
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Credential = credentialDraft with { ItemBlueprintId = unknownItem },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Credential = credentialDraft with { Charges = 0 },
                }),
                editing.PreviewPlaceable(draft with
                {
                    Credential = credentialDraft with
                    {
                        ItemBlueprintId = null,
                        OtherCredentialId = "permission:archives",
                        Consumption = PlaceableWorldProfile.CredentialConsumption.VirtualPermission,
                        Charges = 0,
                        VirtualPermission = string.Empty,
                    },
                }),
                editing.PreviewPlaceable(draft with
                {
                    HasCredentialPolicy = false,
                    RequireCredentialRuntime = true,
                }),
                editing.PreviewPlaceable(draft with
                {
                    Credential = credentialDraft with
                    {
                        ItemBlueprintId = null,
                        OtherCredentialId = "item:c1ad0000-0000-4000-8000-000000000099",
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
            var credentialPreview = editing.PreviewPlaceableCredential(
                draft,
                StudioPlaceableCredentialPreviewMode.PresentCredential,
                3);
            var dryRun = preview.Succeeded && preview.CanApply &&
                         preview.Summary.Contains("Justificatif", StringComparison.Ordinal) &&
                         ReferenceEquals(originalProject, editing.Project);
            var instanceSelection = preview.InstanceIds.SequenceEqual([first.Id, second.Id]);
            var applied = editing.ApplyApprovedPlaceable();
            var appliedProject = editing.Project!;
            var profile = appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                item.BlueprintId == blueprint.Id);
            var credential = profile.Credential;
            var policyModes = Enum.GetValues<PlaceableWorldProfile.CredentialConsumption>().Length == 5 &&
                              Enum.GetValues<PlaceableWorldProfile.CredentialConsumption>().All(mode =>
                              {
                                  var policy = CredentialFor(mode);
                                  return new PlaceableCredentialRuntimeAdapter().Resolve(
                                      profile with { Credential = policy }).Available;
                              });
            var adapter = new PlaceableCredentialRuntimeAdapter();
            var retain = Evaluate(PlaceableWorldProfile.CredentialConsumption.Retain);
            var consume = Evaluate(PlaceableWorldProfile.CredentialConsumption.Consume);
            var charge = Evaluate(PlaceableWorldProfile.CredentialConsumption.Charge);
            var shared = Evaluate(PlaceableWorldProfile.CredentialConsumption.Shared);
            var virtualAllowed = Evaluate(PlaceableWorldProfile.CredentialConsumption.VirtualPermission);
            var virtualDeniedProfile = profile with
            {
                Credential = CredentialFor(PlaceableWorldProfile.CredentialConsumption.VirtualPermission),
            };
            var virtualDeniedHandle = adapter.Resolve(virtualDeniedProfile).Handle!;
            var virtualDenied = adapter.Preview(
                virtualDeniedHandle,
                virtualDeniedProfile,
                new HashSet<string>(),
                new HashSet<string>(),
                3);
            var consumptionDecisions = retain.Allowed && !retain.Consume && retain.RemainingCharges == 3 &&
                                       consume.Allowed && consume.Consume && consume.RemainingCharges == 3 &&
                                       charge.Allowed && charge.Consume && charge.RemainingCharges == 2 &&
                                       shared.Allowed && !shared.Consume && shared.RemainingCharges == 3;
            var virtualPermission = virtualAllowed.Allowed && !virtualAllowed.Consume && !virtualDenied.Allowed;
            var credentialPreviewResolved = credentialPreview.Succeeded && credentialPreview.Allowed &&
                                            credentialPreview.Consume &&
                                            credentialPreview.RemainingCharges == 2 &&
                                            credentialPreview.Summary.Contains(
                                                "charge dépensée", StringComparison.OrdinalIgnoreCase);
            var commandParity = directBuilt &&
                                directProfile?.CredentialRequiredAtRuntime == profile.CredentialRequiredAtRuntime &&
                                CredentialValuesEqual(directProfile?.Credential, credential);
            var preservation = profile.Id == loadedOriginal.Id &&
                               profile.Provenance == loadedOriginal.Provenance &&
                               profile.Foundation?.Id == loadedOriginal.Foundation?.Id &&
                               profile.Operations?.Id == loadedOriginal.Operations?.Id &&
                               ReferenceEquals(
                                   profile.Operations?.StateMachine,
                                   loadedOriginal.Operations?.StateMachine) &&
                               ReferenceEquals(
                                   profile.Operations?.Protection,
                                   loadedOriginal.Operations?.Protection) &&
                               ReferenceEquals(profile.Operations?.Lock, loadedOriginal.Operations?.Lock) &&
                               ReferenceEquals(profile.Reactive, loadedOriginal.Reactive) &&
                               ReferenceEquals(
                                   appliedProject.Module.WorldFeatures.Placeables.Single(item =>
                                       item.BlueprintId == neighborBlueprint.Id),
                                   loadedNeighbor) &&
                               appliedProject.Blueprints.SequenceEqual(originalProject.Blueprints);
            var resolution = adapter.Resolve(profile);
            var runtimePolicy = adapter.Build(resolution.Handle!, profile);
            var runtimeProjection = applied.Succeeded && resolution.Available && credential is not null &&
                                    credential.CredentialId == $"item:{keyItem.Id.Value:D}" &&
                                    credential.Consumption == PlaceableWorldProfile.CredentialConsumption.Charge &&
                                    credential.Charges == 3 && credential.AuditUse &&
                                    profile.CredentialRequiredAtRuntime &&
                                    runtimePolicy == credential;

            var scriptHandle = new WorldFeatureScriptService().Query(
                editing.History!.State,
                "placeable",
                profile.Id.ToString("D"),
                1).Single();
            var scriptRead = scriptHandle.Properties["credentialId"] == $"item:{keyItem.Id.Value:D}" &&
                             scriptHandle.Properties["credentialMode"] == "Charge" &&
                             scriptHandle.Properties["credentialCharges"] == "3" &&
                             scriptHandle.Properties["credentialVirtualPermission"] == string.Empty &&
                             scriptHandle.Properties["credentialAudit"] == "True" &&
                             scriptHandle.Properties["credentialRequiredAtRuntime"] == "True";
            var optionalProfile = profile with
            {
                Credential = null,
                CredentialRequiredAtRuntime = false,
            };
            var requiredProfile = optionalProfile with { CredentialRequiredAtRuntime = true };
            var optional = adapter.Resolve(optionalProfile);
            var required = adapter.Resolve(requiredProfile);
            var optionalRequired = optional.OptionalFallback && optional.DiagnosticCode is null &&
                                   !required.Available &&
                                   required.DiagnosticCode == "WORLD_PLACEABLE_CREDENTIAL_REQUIRED_MISSING" &&
                                   WorldFeatureRuntimeResolver.ResolvePlaceableAccess(
                                       optionalProfile,
                                       new HashSet<string>(),
                                       new HashSet<string>(),
                                       0).Allowed &&
                                   !WorldFeatureRuntimeResolver.ResolvePlaceableAccess(
                                       requiredProfile,
                                       new HashSet<string>(),
                                       new HashSet<string>(),
                                       0).Allowed;

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
                              restored.CredentialRequiredAtRuntime &&
                              CredentialValuesEqual(restored.Credential, credential) &&
                              LockValuesEqual(restored.Operations?.Lock, originalLock) &&
                              restoredNeighbor?.Id == neighbor.Id && reopened.Project?.Blueprints.Count == 3;
            var succeeded = policyModes && consumptionDecisions && virtualPermission &&
                            credentialPreviewResolved && dryRun && commandParity && instanceSelection &&
                            preservation && runtimeProjection && scriptRead && optionalRequired && undoRedo &&
                            persistence && invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                policyModes,
                consumptionDecisions,
                virtualPermission,
                credentialPreviewResolved,
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

            PlaceableWorldProfile.CredentialPolicy CredentialFor(
                PlaceableWorldProfile.CredentialConsumption mode) => new(
                    mode == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
                        ? "permission:archives"
                        : $"item:{keyItem.Id.Value:D}",
                    mode,
                    mode == PlaceableWorldProfile.CredentialConsumption.Charge ? 3 : 0,
                    mode == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
                        ? "archives.enter"
                        : null,
                    true);

            WorldFeatureRuntimeResolver.AccessResult Evaluate(
                PlaceableWorldProfile.CredentialConsumption mode)
            {
                var candidate = profile with { Credential = CredentialFor(mode) };
                var handle = adapter.Resolve(candidate).Handle!;
                return adapter.Preview(
                    handle,
                    candidate,
                    mode == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
                        ? new HashSet<string>()
                        : new HashSet<string> { $"item:{keyItem.Id.Value:D}" },
                    mode == PlaceableWorldProfile.CredentialConsumption.VirtualPermission
                        ? new HashSet<string> { "archives.enter" }
                        : new HashSet<string>(),
                    3);
            }
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1Q_CONTRACT_EXCEPTION type={exception.GetType().Name} message={exception.Message}");
            return FailedUx1QSmoke();
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

    internal StudioPlaceableCredentialPreview PreviewPlaceableCredential(
        StudioPlaceableDraft draft,
        StudioPlaceableCredentialPreviewMode mode,
        int availableCharges)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!draft.HasCredentialPolicy)
        {
            return new(
                false,
                "Politique désactivée · activez Clé consommée avant l’aperçu.",
                "WORLD_PLACEABLE_CREDENTIAL_DISABLED",
                false,
                false,
                availableCharges,
                "access:disabled");
        }
        if (!TryBuildPlaceable(draft, dryRun: true, out _, out var profile, out var message) ||
            profile?.Credential is null)
        {
            return new(
                false,
                message,
                LastPlaceableDiagnosticCode,
                false,
                false,
                availableCharges,
                "access:invalid");
        }

        var adapter = new PlaceableCredentialRuntimeAdapter();
        var resolution = adapter.Resolve(profile);
        if (!resolution.Available || resolution.Handle is null)
        {
            return new(
                false,
                HumanizePlaceable(resolution.DiagnosticCode),
                resolution.DiagnosticCode ?? "WORLD_PLACEABLE_CREDENTIAL_UNAVAILABLE",
                false,
                false,
                availableCharges,
                "access:unavailable");
        }

        var policy = adapter.Build(resolution.Handle, profile);
        IReadOnlySet<string> credentials = mode is StudioPlaceableCredentialPreviewMode.PresentCredential or
            StudioPlaceableCredentialPreviewMode.VirtualPermission
            ? new HashSet<string> { policy.CredentialId }
            : new HashSet<string>();
        IReadOnlySet<string> permissions = mode == StudioPlaceableCredentialPreviewMode.VirtualPermission &&
                                           !string.IsNullOrWhiteSpace(policy.VirtualPermission)
            ? new HashSet<string> { policy.VirtualPermission }
            : new HashSet<string>();
        var charges = mode == StudioPlaceableCredentialPreviewMode.ExhaustedCharges
            ? 0
            : Math.Max(0, availableCharges);
        if (policy.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission &&
            mode == StudioPlaceableCredentialPreviewMode.PresentCredential &&
            !string.IsNullOrWhiteSpace(policy.VirtualPermission))
        {
            permissions = new HashSet<string> { policy.VirtualPermission };
        }
        var result = adapter.Preview(
            resolution.Handle,
            profile,
            credentials,
            permissions,
            charges);
        var decision = result.Allowed
            ? policy.Consumption switch
            {
                PlaceableWorldProfile.CredentialConsumption.Retain => "justificatif conservé",
                PlaceableWorldProfile.CredentialConsumption.Consume => "justificatif consommé",
                PlaceableWorldProfile.CredentialConsumption.Charge =>
                    $"charge dépensée · {result.RemainingCharges} restante(s)",
                PlaceableWorldProfile.CredentialConsumption.Shared => "justificatif partagé et conservé",
                PlaceableWorldProfile.CredentialConsumption.VirtualPermission => "permission virtuelle reconnue",
                _ => "accès autorisé",
            }
            : mode == StudioPlaceableCredentialPreviewMode.ExhaustedCharges
                ? "charges épuisées · accès refusé"
                : "justificatif absent · accès refusé";
        var audit = policy.AuditUse ? "audit prévu" : "sans audit";
        return new(
            true,
            $"Aperçu · {decision} · {audit} · aucun inventaire ni module modifié.",
            result.Allowed
                ? "RPG_PLACEABLE_CREDENTIAL_PREVIEW_ALLOWED"
                : "RPG_PLACEABLE_CREDENTIAL_PREVIEW_DENIED",
            result.Allowed,
            result.Consume,
            result.RemainingCharges,
            result.Audit);
    }

    private static StudioUx1QSmokeResult FailedUx1QSmoke() => new(
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
