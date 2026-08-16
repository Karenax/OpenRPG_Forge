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
    internal StudioUx1HSmokeResult RunUx1HContractSmoke()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux1h-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        try
        {
            var blueprint = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("c1a40000-0000-4000-8000-000000000001")),
                "coffre-expedition",
                "Coffre d’expédition",
                BlueprintKind.Placeable);
            var first = new EntityInstance(
                EntityId.From(Guid.Parse("c1a40000-0000-4000-8000-000000000002")),
                blueprint.Id,
                new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One));
            var second = new EntityInstance(
                EntityId.From(Guid.Parse("c1a40000-0000-4000-8000-000000000003")),
                blueprint.Id,
                new TransformValue(new(4, 0, 0), QuaternionValue.Identity, Vector3Value.One));
            var area = new AreaDefinition(
                AreaId.From(Guid.Parse("c1a40000-0000-4000-8000-000000000004")),
                "atelier-conteneur",
                "Atelier du conteneur",
                [first, second],
                AreaKind.Interior,
                new AreaDimensions(32, 32));
            var project = new ProjectDefinition(
                ProjectId.From(Guid.Parse("c1a40000-0000-4000-8000-000000000005")),
                "Studio UX1-H",
                [blueprint],
                [area]);
            var verbId = Guid.Parse("c1a40000-0000-4000-8000-000000000006");
            var foundation = new PlaceableWorldProfile.FoundationPolicy(
                Guid.Parse("c1a40000-0000-4000-8000-000000000007"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-h-foundation",
                blueprint.Slug,
                [
                    new("en-US", "Expedition chest", "Open the chest"),
                    new("fr-FR", blueprint.DisplayName, "Ouvrir le coffre"),
                ],
                ["conteneur", "expédition"],
                "Inventaire de groupe avec politique personnalisée conservée.",
                new(
                    "asset:coffre-expedition",
                    ["mesh:coffre"],
                    ["material:bois"],
                    "asset:coffre-icon",
                    "asset:coffre-portrait",
                    ["vfx:coffre-ouvert"]),
                new("box", new(1.4f, 1, .8f), true, true, "interaction"),
                new(
                    [new(verbId, "coffre-expedition.verb.ouvrir", "container-opened", "player.can.open")],
                    2.5,
                    120,
                    true,
                    "coffre-expedition.use",
                    .4,
                    "server",
                    ["player.alive"]),
                null,
                new(true, true, true, false, false,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["onInspect"] = "scripts/coffre_inspect",
                    }),
                new(new(.13f, 0, -.13f), Vector3Value.Zero, Vector3Value.One, new(0, .5f, 0), .25,
                    null, true, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["lore"] = "expedition",
                },
                Validation: new(
                    [],
                    ["lock:coffre-expedition"],
                    [],
                    ["inventory:coffre-ancien", "inventory:réserve-secrète"],
                    [],
                    2,
                    2));
            var stateMachine = new PlaceableVisualStateMachineProfile(
                Guid.Parse("c1a40000-0000-4000-8000-000000000010"),
                "closed",
                Ux1HStateProfiles(),
                [new(Guid.Parse("c1a40000-0000-4000-8000-000000000017"), "closed", "open", "open", .5, null)]);
            var damageable = new PlaceableDamageableProfile(
                Guid.Parse("c1a40000-0000-4000-8000-000000000018"),
                80,
                80,
                5,
                new Dictionary<string, double> { ["fire"] = .25 },
                [.75, .5, .25],
                "vfx:wood-break",
                "debris:wood",
                true,
                10);
            var protection = new PlaceableProtectionProfile(
                Guid.Parse("c1a40000-0000-4000-8000-000000000019"),
                ["destroy"],
                ["placeable.override"],
                "Ce coffre appartient au groupe.",
                true,
                "ContainerProtectionOverride");
            var lockProfile = new PlaceableLockProfile(
                Guid.Parse("c1a40000-0000-4000-8000-00000000001a"),
                PlaceableAuthoredLockState.Locked,
                ["key:expedition"],
                "mechanics",
                10,
                null,
                true,
                true,
                true,
                ["lock.admin"]);
            var originalContainer = new PlaceableContainerProfile(
                Guid.Parse("c1a40000-0000-4000-8000-00000000001b"),
                12,
                80,
                "owner",
                ["container.open", "container.audit"],
                false,
                "loot:coffre-ancien",
                "ui:placeable-legacy",
                false,
                "inventory:coffre-ancien");
            var operations = new PlaceableOperationsProfile(
                Guid.Parse("c1a40000-0000-4000-8000-00000000001c"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-h-operations",
                originalContainer,
                stateMachine,
                damageable,
                protection,
                lockProfile);
            var originalProfile = new PlaceableWorldProfile(
                Guid.Parse("c1a40000-0000-4000-8000-00000000001d"),
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-h-profile",
                blueprint.Id,
                "expedition",
                null,
                new("credential:expedition", PlaceableWorldProfile.CredentialConsumption.Retain, 0, null, true),
                new(
                    [new("heartbeat", "scripts/coffre_heartbeat", false, true)],
                    new(true, 10, 6, true, "container")),
                null,
                foundation,
                true,
                operations,
                true);
            var seededCatalog = new WorldFeatureCatalog(
                project.Module.Id.Value,
                WorldFeatureCatalog.CurrentSchemaVersion,
                "test:ux1-h-catalog",
                placeables: [originalProfile]);
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
                return FailedUx1HSmoke();
            }

            var originalProject = editing.Project!;
            var loadedOriginalProfile = originalProject.Module.WorldFeatures.Placeables.Single();
            var loadedOriginalFoundation = loadedOriginalProfile.Foundation!;
            var loadedOriginalOperations = loadedOriginalProfile.Operations!;
            var draft = editing.CreatePlaceableDraft(blueprint.Id)!;
            var editedContainer = draft.Container with
            {
                CapacitySlots = 36,
                CapacityMass = 240,
                OwnershipPolicy = "party",
                AccessPermissions =
                ["container.open", "container.deposit", "container.withdraw", "container.audit"],
                Persistent = true,
                LootPolicyId = "loot:coffre-expedition",
                UiProfileId = "ui:placeable-chest",
                Replicated = true,
                InventoryId = "inventory:coffre-expedition",
            };
            draft = draft with
            {
                HasContainer = true,
                Container = editedContainer,
                RequireOperationsRuntime = true,
            };

            var invalidSlots = editing.PreviewPlaceable(draft with
            {
                Container = editedContainer with { CapacitySlots = 0 },
            });
            var invalidAccess = editing.PreviewPlaceable(draft with
            {
                Container = editedContainer with { AccessPermissions = [] },
            });
            var invalidReferences = editing.PreviewPlaceable(draft with
            {
                Container = editedContainer with { InventoryId = string.Empty },
            });
            var invalidBlocked = !invalidSlots.Succeeded && !invalidAccess.Succeeded && !invalidReferences.Succeeded;
            var humanDiagnostics = invalidBlocked &&
                                   IsHumanPlaceableMessage(invalidSlots.Message) &&
                                   IsHumanPlaceableMessage(invalidAccess.Message) &&
                                   IsHumanPlaceableMessage(invalidReferences.Message);

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
            var appliedContainer = profile.Operations?.Container;
            var containerComponent = applied.Succeeded && profile.Foundation?.Components.HasContainer == true &&
                                     appliedContainer is not null;
            var capacity = appliedContainer is { CapacitySlots: 36, CapacityMass: 240 };
            var ownershipAccess = appliedContainer?.OwnershipPolicy == "party" &&
                                  appliedContainer.AccessPermissions.SequenceEqual(
                                      ["container.open", "container.deposit", "container.withdraw", "container.audit"]);
            var persistenceReplication = appliedContainer is { Persistent: true, Replicated: true } &&
                                         profile.OperationsRequiredAtRuntime;
            var references = appliedContainer is
            {
                InventoryId: "inventory:coffre-expedition",
                LootPolicyId: "loot:coffre-expedition",
                UiProfileId: "ui:placeable-chest",
            };
            var directContainer = directProfile?.Operations?.Container;
            var commandParity = directBuilt && directProfile?.OperationsRequiredAtRuntime ==
                                profile.OperationsRequiredAtRuntime &&
                                ContainerValuesEqual(directContainer, appliedContainer);
            var preservation = profile.Id == loadedOriginalProfile.Id && profile.FactionId == "expedition" &&
                               ReferenceEquals(profile.Credential, loadedOriginalProfile.Credential) &&
                               ReferenceEquals(profile.Reactive, loadedOriginalProfile.Reactive) &&
                               profile.Operations?.Id == loadedOriginalOperations.Id &&
                               profile.Operations?.Provenance == "test:ux1-h-operations" &&
                               ReferenceEquals(profile.Operations?.StateMachine, loadedOriginalOperations.StateMachine) &&
                               ReferenceEquals(profile.Operations?.Damageable, loadedOriginalOperations.Damageable) &&
                               ReferenceEquals(profile.Operations?.Protection, loadedOriginalOperations.Protection) &&
                               ReferenceEquals(profile.Operations?.Lock, loadedOriginalOperations.Lock) &&
                               profile.Foundation?.Texts.Any(item => item.Locale == "en-US" &&
                                   item.Name == "Expedition chest" && item.Prompt == "Open the chest") == true &&
                               profile.Foundation.Visual.VisualEffectIds.SequenceEqual(
                                   loadedOriginalFoundation.Visual.VisualEffectIds) &&
                               profile.Foundation.Components.ScriptBindings["onInspect"] == "scripts/coffre_inspect" &&
                               profile.Foundation.Overrides["lore"] == "expedition" &&
                               profile.Foundation.Validation is { } appliedValidation &&
                               appliedValidation.AnimationIds.SequenceEqual(
                                   loadedOriginalFoundation.Validation!.AnimationIds) &&
                               appliedValidation.LockIds.SequenceEqual(loadedOriginalFoundation.Validation.LockIds) &&
                               appliedValidation.TrapIds.SequenceEqual(loadedOriginalFoundation.Validation.TrapIds) &&
                               appliedValidation.PhysicsProfileIds.SequenceEqual(
                                   loadedOriginalFoundation.Validation.PhysicsProfileIds) &&
                               appliedValidation.DrawCallBudget == loadedOriginalFoundation.Validation.DrawCallBudget &&
                               appliedValidation.ScriptBudget == loadedOriginalFoundation.Validation.ScriptBudget &&
                               appliedValidation.InventoryIds.SequenceEqual(
                                   ["inventory:coffre-expedition", "inventory:réserve-secrète"]);

            var adapter = new PlaceableOperationsRuntimeAdapter();
            var resolution = adapter.Resolve(profile);
            var runtime = adapter.Build(resolution.Handle!, profile);
            var runtimeProjection = resolution.Available && runtime.Profile.Container is
            {
                CapacitySlots: 36,
                CapacityMass: 240,
                OwnershipPolicy: "party",
                Persistent: true,
                Replicated: true,
                LootPolicyId: "loot:coffre-expedition",
                UiProfileId: "ui:placeable-chest",
            } && runtime.Profile.Container.AccessPermissions.Contains("container.audit") &&
                                    runtime.Profile.InventoryReferences.SequenceEqual(
                                        ["inventory:coffre-expedition"]) &&
                                    adapter.Validate(resolution.Handle!, profile).Count == 0;
            var scriptHandle = new WorldFeatureScriptService().Query(editing.History!.State,
                "placeable", profile.Id.ToString("D"), 1).Single();
            var scriptRead = scriptHandle.Properties["container"] ==
                             "slots:36;mass:240;owner:party;persistent:True;replicated:True" &&
                             scriptHandle.Properties["containerAccess"] ==
                             "container.audit,container.deposit,container.open,container.withdraw" &&
                             scriptHandle.Properties["inventory"] == "inventory:coffre-expedition" &&
                             scriptHandle.Properties["lootPolicy"] == "loot:coffre-expedition" &&
                             scriptHandle.Properties["containerUi"] == "ui:placeable-chest";
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
            var persistence = save.Succeeded && reopen.Succeeded && restored?.Foundation?.Components.HasContainer == true &&
                              restored.OperationsRequiredAtRuntime &&
                              restored.Operations?.Container is
                              {
                                  CapacitySlots: 36,
                                  CapacityMass: 240,
                                  OwnershipPolicy: "party",
                                  Persistent: true,
                                  Replicated: true,
                                  InventoryId: "inventory:coffre-expedition",
                                  LootPolicyId: "loot:coffre-expedition",
                                  UiProfileId: "ui:placeable-chest",
                              } && restored.Operations.Container.AccessPermissions.SequenceEqual(
                                  ["container.open", "container.deposit", "container.withdraw", "container.audit"]) &&
                              restored.Operations.StateMachine?.Id == stateMachine.Id &&
                              restored.Operations.Damageable?.Id == damageable.Id &&
                              restored.Operations.Protection?.Id == protection.Id &&
                              restored.Operations.Lock?.Id == lockProfile.Id &&
                              restored.Foundation.Validation?.InventoryIds.SequenceEqual(
                                  ["inventory:coffre-expedition", "inventory:réserve-secrète"]) == true &&
                              restored.Foundation.Texts.Any(item => item.Locale == "en-US" &&
                                  item.Name == "Expedition chest");

            var succeeded = containerComponent && capacity && ownershipAccess && persistenceReplication &&
                            references && dryRun && commandParity && instanceSelection && preservation &&
                            runtimeProjection && scriptRead && optionalRequired && undoRedo && persistence &&
                            invalidBlocked && humanDiagnostics;
            return new(
                succeeded,
                containerComponent,
                capacity,
                ownershipAccess,
                persistenceReplication,
                references,
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

    private static IReadOnlyList<PlaceableVisualStateProfile> Ux1HStateProfiles()
    {
        var states = new[] { "idle", "open", "closed", "broken", "active", "custom" };
        return states.Select((state, index) => new PlaceableVisualStateProfile(
            Guid.Parse($"c1a40000-0000-4000-8000-{index + 20:D12}"),
            state,
            $"animation:{state}",
            $"visual:{state}",
            state != "broken")).ToArray();
    }

    private static bool ContainerValuesEqual(
        PlaceableContainerProfile? first,
        PlaceableContainerProfile? second) =>
        first is not null && second is not null &&
        first.Id == second.Id &&
        first.CapacitySlots == second.CapacitySlots &&
        first.CapacityMass == second.CapacityMass &&
        first.OwnershipPolicy == second.OwnershipPolicy &&
        first.AccessPermissions.SequenceEqual(second.AccessPermissions) &&
        first.Persistent == second.Persistent &&
        first.LootPolicyId == second.LootPolicyId &&
        first.UiProfileId == second.UiProfileId &&
        first.Replicated == second.Replicated &&
        first.InventoryId == second.InventoryId;

    private static StudioUx1HSmokeResult FailedUx1HSmoke() => new(
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
