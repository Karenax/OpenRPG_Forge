using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.Tests.WorldFeatures;

public sealed class WorldFeatureCatalogWorkflowTests
{
    [Fact]
    public void Command_supports_preview_apply_history_and_typed_script_query()
    {
        var state = CreateState();
        var catalog = Catalog(state.Project.Module.Id.Value);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), state.Revision, catalog, true);

        var preview = handler.Handle(state, command);
        var applied = handler.Handle(state, command with { DryRun = false });
        var history = new ProjectCommandHistory(state);
        history.Record(applied);
        var script = new WorldFeatureScriptService();
        var handles = script.Query(history.State, "script");

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.Contains("flags=1", preview.Patch!.Summary);
        Assert.True(applied.Succeeded);
        Assert.Equal(state.Revision.Next(), applied.NewRevision);
        Assert.Single(handles);
        Assert.Equal("1", handles[0].Properties["flags"]);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.WorldFeatures.Scripts.ModuleFlags);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.WorldFeatures.Scripts.ModuleFlags);
    }

    [Fact]
    public void Command_rejects_revision_identity_and_invalid_script_contracts()
    {
        var state = CreateState();
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var catalog = Catalog(state.Project.Module.Id.Value);
        var stale = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision.Next(), catalog, false));
        var wrongId = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            Catalog(Guid.Parse("14000000-0000-4000-8000-000000000099")), false));
        var invalidScripts = catalog.Scripts with
        {
            ModuleFlags = [catalog.Scripts.ModuleFlags[0], catalog.Scripts.ModuleFlags[0]],
        };
        var invalid = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            new WorldFeatureCatalog(catalog.Id, 1, "test", scripts: invalidScripts), false));

        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");
        Assert.Contains(wrongId.Diagnostics, item => item.Code == "world.catalog.id_mismatch");
        Assert.Contains(invalid.Diagnostics, item => item.Code == "world.script.flag_invalid");
    }

    [Fact]
    public void R041_DoorValidationReportsNavigationCollisionAndTransitionLoopBeforeApply()
    {
        var state = CreateState();
        var doorBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000010")),
            "validation-door", "Validation door", BlueprintKind.Door);
        var validDoor = Door(doorBlueprint.Id);
        var invalidDoor = validDoor with
        {
            Foundation = validDoor.Foundation! with
            {
                Links = [new(Guid.Parse("14000000-0000-4000-8000-000000000011"), validDoor.Id, true, string.Empty)],
                Navigation = validDoor.Foundation.Navigation! with
                {
                    ObstacleId = string.Empty,
                    BlockingStateIds = ["closed", "open"],
                    TraversableStateIds = ["open"],
                },
            },
        };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r041", doors: [invalidDoor]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [doorBlueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.door.navigation_invalid", codes);
        Assert.Contains("world.door.navigation_state_mismatch", codes);
        Assert.Contains("world.door.transition_loop", codes);
    }

    [Fact]
    public void Ux1Aj_DoorRuntimeValidationPolicyIsTransactionalProjectedAndStructurallyGuarded()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000012")),
            "runtime-validation-door", "Runtime validation door", BlueprintKind.Door);
        var door = Door(blueprint.Id) with
        {
            Validation = new([], ["animation:open", "animation:closed", "animation:opening"],
                ["collision:door"], [], []),
            ValidationRequiredAtRuntime = true,
        };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:ux1-aj", doors: [door]);
        var configured = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);
        var configuredState = new ProjectState(configured, state.Revision);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));

        var preview = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, true));
        var applied = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, false));
        var script = new WorldFeatureScriptService().Query(applied.State, "door", door.Id.ToString("D"), 1)
            .Single();
        var invalidPolicy = door with
        {
            Validation = door.Validation! with
            {
                KnownDestinationIds = [Guid.Empty],
                KnownAnimationIds = [string.Empty, string.Empty],
            },
        };
        var invalidProject = new ProjectDefinition(configured.Id, configured.Title,
            configured.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-aj",
                doors: [invalidPolicy])), configured.Blueprints, configured.Areas);
        var missingFoundation = door with { Foundation = null };
        var missingFoundationProject = new ProjectDefinition(configured.Id, configured.Title,
            configured.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-aj",
                doors: [missingFoundation])), configured.Blueprints, configured.Areas);
        var missingRequired = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-aj", doors:
            [door with { Validation = null, ValidationRequiredAtRuntime = true }]), true));
        var semanticInvalidCatalog = new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-aj", doors:
        [door with { Validation = door.Validation! with { KnownAnimationIds = [] } }]);
        var semanticPreview = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            semanticInvalidCatalog, true));
        var semanticApply = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            semanticInvalidCatalog, false));

        Assert.True(preview.Succeeded);
        Assert.Same(configuredState, preview.State);
        Assert.True(applied.Succeeded);
        Assert.Equal("True", script.Properties["validation"]);
        Assert.Equal("True", script.Properties["validationRequiredAtRuntime"]);
        Assert.Equal("animation:closed,animation:open,animation:opening", script.Properties["knownAnimations"]);
        Assert.Equal("collision:door", script.Properties["knownCollisionProfiles"]);
        Assert.Equal("collision:door", script.Properties["requiredCollisionProfile"]);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(invalidProject),
            item => item.Code == "world.door.validation_context_invalid");
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingFoundationProject),
            item => item.Code == "world.door.validation_foundation_missing");
        Assert.Contains(missingRequired.Diagnostics,
            item => item.Code == "world.door.validation_required_missing");
        Assert.False(semanticPreview.Succeeded);
        Assert.False(semanticApply.Succeeded);
        Assert.Same(configuredState, semanticPreview.State);
        Assert.Same(configuredState, semanticApply.State);
        Assert.Contains(semanticPreview.Diagnostics, item => item.Code == "RPG_DOOR_ANIMATION_MISSING");
    }

    [Fact]
    public void R042_EncounterValidationRejectsGeometryPoolReferencesBudgetAnchorsRewardsAndCap()
    {
        var state = CreateState();
        var creatureBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000030")),
            "encounter-creature", "Encounter creature", BlueprintKind.Creature);
        var valid = Encounter(state.Project.Areas[0].Id, creatureBlueprint.Id);
        var missingBlueprint = BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000099"));
        var invalid = valid with
        {
            Foundation = valid.Foundation! with
            {
                Volume = valid.Foundation.Volume with { Points = [], Height = 0, ThreatRadius = -1 },
                Candidates = [valid.Foundation.Candidates[0] with
                {
                    BlueprintId = missingBlueprint,
                    Weight = 0,
                    Cost = 0,
                    Conditions = [string.Empty],
                    MaximumAlive = 5,
                }],
                SpawnBudget = 0,
                SpawnAnchorIds = [],
                RewardIds = [string.Empty],
                NetworkActorCap = 1,
            },
        };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r042", encounters: [invalid]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [creatureBlueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.encounter.volume_invalid", codes);
        Assert.Contains("world.encounter.candidate_missing", codes);
        Assert.Contains("world.encounter.candidate_invalid", codes);
        Assert.Contains("world.encounter.budget_invalid", codes);
        Assert.Contains("world.encounter.anchors_invalid", codes);
        Assert.Contains("world.encounter.rewards_invalid", codes);
        Assert.Contains("world.encounter.network_cap_invalid", codes);
    }

    [Fact]
    public void R045_MerchantValidationCoversBindingsStockPricesRestrictionsTreasuryLocalizationScriptsAndRuntimeRequirement()
    {
        var state = CreateState();
        var merchantBlueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000040")),
            "merchant", "Merchant", BlueprintKind.Creature);
        var itemBlueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000041")),
            "item", "Item", BlueprintKind.Item);
        var profile = new MerchantWorldProfile(Guid.Parse("14000000-0000-4000-8000-000000000042"), 1, "test:r045",
            merchantBlueprint.Id, [new("gold", "currency.gold", 1, true, false)],
            [new("gold", 100, 0, 0, 1, 200)], new(50, 100, new Dictionary<string, decimal>(), -1, true),
            new(false, [], false, false, 0, "crime"), new(24, "restock", null, true, true),
            new("bargain", 1, .1, .1, 0, true), null, [], new(true, 30, 100, true, true, true),
            new(Guid.Empty, 2, string.Empty, string.Empty, string.Empty,
                [new(Guid.Empty, BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000099")),
                    string.Empty, -1, -1, "missing", ["unknown"])], [], [string.Empty],
                new Dictionary<string, string> { ["onOpen"] = string.Empty }, 0, 0, false), true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r045", merchants: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [merchantBlueprint, itemBlueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.merchant.vendor_binding_invalid", codes);
        Assert.Contains("world.merchant.authority_invalid", codes);
        Assert.Contains("world.merchant.capacity_invalid", codes);
        Assert.Contains("world.merchant.localization_invalid", codes);
        Assert.Contains("world.merchant.restrictions_invalid", codes);
        Assert.Contains("world.merchant.scripts_invalid", codes);
        Assert.Contains("world.merchant.stock_identity_invalid", codes);
        Assert.Contains("world.merchant.stock_item_invalid", codes);
        Assert.Contains("world.merchant.stock_values_invalid", codes);
        Assert.Contains("world.merchant.stock_restriction_invalid", codes);

        var requiredWithoutPolicy = profile with { Transactions = null };
        var missingCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r045", merchants: [requiredWithoutPolicy]);
        var missingProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(missingCatalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingProject),
            item => item.Code == "world.merchant.transactions_required_missing");
    }

    [Fact]
    public void R091_MerchantFoundationValidationLocalizesStoreWizardVendorStockPricingQueryAndRequirementFailures()
    {
        var state = CreateState();
        var merchantBlueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("c2910000-0000-4000-8000-000000000021")),
            "merchant", "Merchant", BlueprintKind.Creature);
        var itemBlueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("c2910000-0000-4000-8000-000000000022")),
            "item", "Item", BlueprintKind.Item);
        var foundation = new MerchantFoundationProfile(Guid.Empty, 2, string.Empty, string.Empty, string.Empty, [],
            [new(Guid.Empty, BlueprintId.From(Guid.NewGuid()), Guid.NewGuid(), string.Empty, string.Empty, string.Empty)],
            new(Guid.Empty, 2, string.Empty, false, false, false, false, false, string.Empty),
            [new(Guid.Empty, BlueprintId.From(Guid.NewGuid()), string.Empty, MerchantSupplyPolicy.Finite, 0,
                null, -1, -1, [string.Empty], -1, "missing")],
            new(Guid.Empty, 2, string.Empty, -1, -1, [new(-1, 0)], [],
                new Dictionary<string, decimal> { ["missing"] = 0 }, -1, -1, -1, -1, -1, -2),
            new(Guid.Empty, 2, string.Empty, [], ["rare"], ["rare"], -1, -2, [string.Empty], -1,
                null, string.Empty));
        var profile = new MerchantWorldProfile(Guid.NewGuid(), 1, "test:r091", merchantBlueprint.Id,
            [new("gold", "currency.gold", 1, true, false)], [new("gold", 100, 0, 0, 1, 200)],
            new(50, 100, new Dictionary<string, decimal>(), -1, true),
            new(false, [], false, false, 0, "crime"), new(24, "restock", null, true, true),
            new("bargain", 1, .1, .1, 0, true), null, [], new(true, 30, 100, true, true, true),
            null, false, foundation, true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r091", merchants: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [merchantBlueprint, itemBlueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.merchant.foundation_identity_invalid", codes);
        Assert.Contains("world.merchant.vendor_identity_invalid", codes);
        Assert.Contains("world.merchant.vendor_reference_missing", codes);
        Assert.Contains("world.merchant.dialogue_reference_missing", codes);
        Assert.Contains("world.merchant.wizard_incomplete", codes);
        Assert.Contains("world.merchant.foundation_stock_item_missing", codes);
        Assert.Contains("world.merchant.foundation_stock_values_invalid", codes);
        Assert.Contains("world.merchant.finite_stock_empty", codes);
        Assert.Contains("world.merchant.foundation_pricing_invalid", codes);
        Assert.Contains("world.merchant.foundation_query_invalid", codes);

        var missing = profile with { Foundation = null };
        var missingProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:r091", merchants: [missing])),
            project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingProject),
            item => item.Code == "world.merchant.foundation_required_missing");
    }

    [Fact]
    public void R092_MerchantExperienceValidationLocalizesSessionUiIdentityAndRequirementFailures()
    {
        var state = CreateState();
        var merchantBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("c2920000-0000-4000-8000-000000000021")),
            "merchant", "Merchant", BlueprintKind.Creature);
        var experience = new MerchantStoreExperienceProfile(Guid.Empty, 2, string.Empty,
            new(Guid.Empty, 2, string.Empty, string.Empty, string.Empty, string.Empty,
                false, false, false, false),
            new(Guid.Empty, 2, string.Empty, false, false, false, false, false, false, false, false,
                [string.Empty, string.Empty]));
        var profile = new MerchantWorldProfile(Guid.NewGuid(), 1, "test:r092", merchantBlueprint.Id,
            [new("gold", "currency.gold", 1, true, false)], [new("gold", 100, 0, 0, 1, 200)],
            new(50, 100, new Dictionary<string, decimal>(), -1, true),
            new(false, [], false, false, 0, "crime"), new(24, "restock", null, true, true),
            new("bargain", 1, .1, .1, 0, true), null, [], new(true, 30, 100, true, true, true),
            null, false, null, false, experience, true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r092",
            merchants: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [merchantBlueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.profile.identity_invalid", codes);
        Assert.Contains("world.merchant.session_events_invalid", codes);
        Assert.Contains("world.merchant.store_ui_invalid", codes);
        Assert.Contains("world.merchant.experience_identity_conflict", codes);

        var missing = profile with { Experience = null };
        var missingProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:r092",
                merchants: [missing])), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingProject),
            item => item.Code == "world.merchant.experience_required_missing");
    }

    [Fact]
    public void R093_PlaceableOperationsValidationLocalizesContainerStateDamageProtectionLockAndRequirementFailures()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("c2930000-0000-4000-8000-000000000030")),
            "placeable", "Placeable", BlueprintKind.Placeable);
        var operations = new PlaceableOperationsProfile(Guid.Empty, 2, string.Empty,
            new(Guid.Empty, 0, -1, string.Empty, [string.Empty, string.Empty], true,
                string.Empty, string.Empty, true, string.Empty),
            new(Guid.Empty, string.Empty, [],
                [new(Guid.Empty, string.Empty, string.Empty, string.Empty, -1, string.Empty)]),
            new(Guid.Empty, 0, -1, -1,
                new Dictionary<string, double> { [string.Empty] = -1 }, [-1, -1],
                string.Empty, string.Empty, true, -1),
            new(Guid.Empty, [string.Empty, string.Empty], [string.Empty, string.Empty],
                string.Empty, true, string.Empty),
            new(Guid.Empty, PlaceableAuthoredLockState.Locked, [string.Empty, string.Empty],
                string.Empty, -1, string.Empty, true, true, true, [string.Empty, string.Empty]));
        var profile = new PlaceableWorldProfile(Guid.NewGuid(), 1, "test:r093", blueprint.Id,
            null, null, null, new([], new(false, 1, 1, true, "placeable")), null,
            Operations: operations, OperationsRequiredAtRuntime: true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r093",
            placeables: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.profile.identity_invalid", codes);
        Assert.Contains("world.placeable.operations_foundation_missing", codes);
        Assert.Contains("world.placeable.container_invalid", codes);
        Assert.Contains("world.placeable.state_machine_invalid", codes);
        Assert.Contains("world.placeable.damageable_invalid", codes);
        Assert.Contains("world.placeable.protection_invalid", codes);
        Assert.Contains("world.placeable.lock_invalid", codes);
        Assert.Contains("world.placeable.operations_identity_conflict", codes);

        var unguarded = profile with
        {
            Operations = operations with
            {
                Protection = new(
                    Guid.NewGuid(),
                    ["destroy"],
                    [],
                    "Protected",
                    true,
                    "PlaceableProtectionOverride"),
            },
        };
        var unguardedProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:r093",
                placeables: [unguarded])), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(unguardedProject),
            item => item.Code == "world.placeable.protection_invalid");

        var unknownKey = profile with
        {
            Operations = operations with
            {
                Lock = new(
                    Guid.NewGuid(),
                    PlaceableAuthoredLockState.Locked,
                    ["item:c2930000-0000-4000-8000-000000000099"],
                    null,
                    0,
                    null,
                    false,
                    true,
                    false,
                    []),
            },
        };
        var unknownKeyProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:r093",
                placeables: [unknownKey])), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(unknownKeyProject),
            item => item.Code == "world.placeable.lock_invalid");

        var missing = profile with { Operations = null };
        var missingProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:r093",
                placeables: [missing])), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingProject),
            item => item.Code == "world.placeable.operations_required_missing");
    }

    [Fact]
    public void PlaceableCredentialValidationReportsRequiredItemAndVirtualPermissionFailures()
    {
        var state = CreateState();
        var placeableBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("c2130000-0000-4000-8000-000000000001")),
            "credential-placeable", "Credential placeable", BlueprintKind.Placeable);
        var itemBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("c2130000-0000-4000-8000-000000000002")),
            "credential-key", "Credential key", BlueprintKind.Item);
        var profile = new PlaceableWorldProfile(
            Guid.Parse("c2130000-0000-4000-8000-000000000003"),
            1,
            "test:ux1-q",
            placeableBlueprint.Id,
            null,
            null,
            null,
            new([], new(false, 1, 1, true, "placeable")),
            null,
            CredentialRequiredAtRuntime: true);
        ProjectDefinition ProjectFor(PlaceableWorldProfile candidate) => new(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module.WithWorldFeatures(new WorldFeatureCatalog(
                state.Project.Module.Id.Value,
                1,
                "test:ux1-q",
                placeables: [candidate])),
            [placeableBlueprint, itemBlueprint],
            state.Project.Areas);

        var requiredCodes = WorldFeatureCatalogValidator.Validate(ProjectFor(profile))
            .Select(item => item.Code).ToHashSet();
        var itemCodes = WorldFeatureCatalogValidator.Validate(ProjectFor(profile with
        {
            Credential = new(
                "item:c2130000-0000-4000-8000-000000000099",
                PlaceableWorldProfile.CredentialConsumption.Consume,
                0,
                null,
                true),
        })).Select(item => item.Code).ToHashSet();
        var virtualCodes = WorldFeatureCatalogValidator.Validate(ProjectFor(profile with
        {
            Credential = new(
                "permission:archive",
                PlaceableWorldProfile.CredentialConsumption.VirtualPermission,
                0,
                null,
                true),
        })).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.placeable.credential_required_missing", requiredCodes);
        Assert.Contains("world.placeable.credential_item_missing", itemCodes);
        Assert.Contains("world.placeable.credential_virtual_permission_missing", virtualCodes);
    }

    [Fact]
    public void PlaceableFactionOwnershipValidationLocalizesMembershipReferenceIdentityAndRequirementFailures()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("c2100000-0000-4000-8000-000000000001")),
            "faction-placeable", "Faction placeable", BlueprintKind.Placeable);
        var factionId = Guid.Parse("c2100000-0000-4000-8000-000000000002");
        var unknownFactionId = Guid.Parse("c2100000-0000-4000-8000-000000000003");
        var factions = new FactionCatalog(
            [new(factionId, 1, "test:ux1-n", "sentinels", "Sentinelles", false, [], ["guard"])],
            [],
            [],
            []);
        var profile = new PlaceableWorldProfile(
            Guid.Parse("c2100000-0000-4000-8000-000000000004"),
            1,
            "test:ux1-n",
            blueprint.Id,
            null,
            null,
            null,
            new([], new(false, 1, 1, true, "placeable")),
            null,
            FactionOwnership: new(
                Guid.Parse("c2100000-0000-4000-8000-000000000005"),
                1,
                "test:ux1-n",
                factionId,
                [factionId, unknownFactionId, unknownFactionId],
                true,
                true,
                true,
                true),
            FactionOwnershipRequiredAtRuntime: true);
        var catalog = new WorldFeatureCatalog(
            state.Project.Module.Id.Value,
            1,
            "test:ux1-n",
            placeables: [profile]);
        var project = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module.WithFactions(factions).WithWorldFeatures(catalog),
            [blueprint],
            state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.placeable.faction_memberships_invalid", codes);
        Assert.Contains("world.placeable.faction_reference_missing", codes);

        var conflicting = profile with { FactionOwnership = profile.FactionOwnership! with { Id = profile.Id } };
        var conflictingProject = new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(
                catalog.Id,
                1,
                "test:ux1-n",
                placeables: [conflicting])),
            project.Blueprints,
            project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(conflictingProject),
            item => item.Code == "world.placeable.faction_ownership_identity_conflict");

        var missing = profile with { FactionOwnership = null };
        var missingProject = new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(
                catalog.Id,
                1,
                "test:ux1-n",
                placeables: [missing])),
            project.Blueprints,
            project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingProject),
            item => item.Code == "world.placeable.faction_ownership_required_missing");
    }

    [Fact]
    public void PlaceableConversationValidationLocalizesDialogueEntryItemSelectorIdentityAndRequirementFailures()
    {
        var state = CreateState();
        var placeableBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("c2111000-0000-4000-8000-000000000001")),
            "talking-placeable", "Talking placeable", BlueprintKind.Placeable);
        var itemBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("c2111000-0000-4000-8000-000000000002")),
            "ritual-seal", "Ritual seal", BlueprintKind.Item);
        var dialogueId = DialogueId.From(Guid.Parse("c2111000-0000-4000-8000-000000000003"));
        var nodeId = DialogueNodeId.From(Guid.Parse("c2111000-0000-4000-8000-000000000004"));
        var dialogue = new DialogueDefinition(
            dialogueId,
            "altar-voice",
            "Altar voice",
            nodeId,
            [new(nodeId, null, new("dialogue.altar", "Who approaches?"))],
            provenance: "test:ux1-o",
            entryPoints:
            [new(Guid.Parse("c2111000-0000-4000-8000-000000000005"), 1, "default", nodeId, 0,
                "test:ux1-o")]);
        var binding = new PlaceableConversationBinding(
            Guid.Parse("c2111000-0000-4000-8000-000000000006"),
            dialogueId.Value,
            PlaceableConversationTrigger.Interaction,
            "examine",
            null,
            "default",
            10,
            true);
        var profile = new PlaceableWorldProfile(
            Guid.Parse("c2111000-0000-4000-8000-000000000007"),
            1,
            "test:ux1-o",
            placeableBlueprint.Id,
            null,
            dialogueId.Value,
            null,
            new([], new(false, 1, 1, true, "placeable")),
            null,
            Conversation: new(
                Guid.Parse("c2111000-0000-4000-8000-000000000008"),
                PlaceableConversationProfile.CurrentSchemaVersion,
                "test:ux1-o",
                [binding]),
            ConversationRequiredAtRuntime: true);
        var catalog = new WorldFeatureCatalog(
            state.Project.Module.Id.Value,
            1,
            "test:ux1-o",
            placeables: [profile]);
        var project = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog),
            [placeableBlueprint, itemBlueprint],
            state.Project.Areas,
            [dialogue],
            state.Project.Quests);

        Assert.DoesNotContain(WorldFeatureCatalogValidator.Validate(project), diagnostic =>
            diagnostic.Code.StartsWith("world.placeable.conversation_", StringComparison.Ordinal));

        var invalidConversation = profile.Conversation! with
        {
            Id = profile.Id,
            Bindings =
            [
                binding with
                {
                    Id = Guid.Empty,
                    DialogueId = Guid.Parse("c2111000-0000-4000-8000-000000000099"),
                    EntryPointKey = "missing",
                    Selector = "bad?selector",
                    Priority = 1_001,
                },
                binding with
                {
                    Id = Guid.Empty,
                    Trigger = PlaceableConversationTrigger.Item,
                    Selector = "not-empty",
                    ItemBlueprintId = BlueprintId.From(
                        Guid.Parse("c2111000-0000-4000-8000-000000000098")),
                },
            ],
        };
        var invalidProject = new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(
                catalog.Id,
                1,
                "test:ux1-o",
                placeables: [profile with { Conversation = invalidConversation }])),
            project.Blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests);
        var codes = WorldFeatureCatalogValidator.Validate(invalidProject)
            .Select(diagnostic => diagnostic.Code)
            .ToHashSet();

        Assert.Contains("world.placeable.conversation_binding_uuid_invalid", codes);
        Assert.Contains("world.placeable.conversation_dialogue_missing", codes);
        Assert.Contains("world.placeable.conversation_priority_invalid", codes);
        Assert.Contains("world.placeable.conversation_selector_invalid", codes);
        Assert.Contains("world.placeable.conversation_item_missing", codes);
        Assert.Contains("world.placeable.conversation_identity_conflict", codes);

        var entryPointProject = new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(
                catalog.Id,
                1,
                "test:ux1-o",
                placeables: [profile with
                {
                    Conversation = profile.Conversation with
                    {
                        Bindings = [binding with { EntryPointKey = "missing" }],
                    },
                }])),
            project.Blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(entryPointProject), diagnostic =>
            diagnostic.Code == "world.placeable.conversation_entry_point_missing");

        var missingProject = new ProjectDefinition(
            project.Id,
            project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(
                catalog.Id,
                1,
                "test:ux1-o",
                placeables: [profile with { Conversation = null }])),
            project.Blueprints,
            project.Areas,
            project.Dialogues,
            project.Quests);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingProject), diagnostic =>
            diagnostic.Code == "world.placeable.conversation_required_missing");
    }

    [Fact]
    public void R094_PlaceableSecurityEventsValidationLocalizesTrapLootLifecycleDeltaAndRequirementFailures()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("c2940000-0000-4000-8000-000000000030")),
            "placeable", "Placeable", BlueprintKind.Placeable);
        var operations = new PlaceableOperationsProfile(Guid.NewGuid(), 1, "test:r094", null, null, null,
            null, new(Guid.NewGuid(), PlaceableAuthoredLockState.Locked, ["key:ember"], "mechanics", 12,
                null, false, true, false, []));
        var securityEvents = new PlaceableSecurityEventsProfile(Guid.Empty, 2, string.Empty,
            new(Guid.Empty, string.Empty, string.Empty, -1, -1, -1, string.Empty, string.Empty,
                string.Empty, true, false),
            new(Guid.Empty, Guid.Empty, PlaceableAuthoredLootSpawnTiming.Open, 0, true,
                [new(Guid.Empty, 0, -1, string.Empty)], -1,
                new(Guid.Empty, 2, string.Empty,
                    [new(Guid.NewGuid(), string.Empty, -1, -1, -2, -1, string.Empty, false)])),
            new(Guid.Empty, string.Empty, string.Empty, string.Empty, false, false, false, false),
            new(Guid.Empty, string.Empty, string.Empty, string.Empty, false, false, false, false),
            new(Guid.Empty, string.Empty, string.Empty, false, false, false, false));
        var profile = new PlaceableWorldProfile(Guid.NewGuid(), 1, "test:r094", blueprint.Id,
            null, null, null, new([], new(false, 1, 1, true, "placeable")), null,
            Operations: operations, SecurityEvents: securityEvents, SecurityEventsRequiredAtRuntime: true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r094",
            placeables: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.profile.identity_invalid", codes);
        Assert.Contains("world.placeable.security_events_foundation_missing", codes);
        Assert.Contains("world.placeable.trap_invalid", codes);
        Assert.Contains("world.placeable.loot_spawner_invalid", codes);
        Assert.Contains("world.placeable.loot_item_reference_missing", codes);
        Assert.Contains("world.placeable.container_events_invalid", codes);
        Assert.Contains("world.placeable.lock_events_invalid", codes);
        Assert.Contains("world.placeable.contents_changed_invalid", codes);
        Assert.Contains("world.placeable.security_events_identity_conflict", codes);

        var missing = profile with
        {
            SecurityEvents = null,
            TrapRequiredAtRuntime = true,
            LootRequiredAtRuntime = true,
            ContainerEventsRequiredAtRuntime = true,
            LockEventsRequiredAtRuntime = true,
        };
        var missingProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:r094",
                placeables: [missing])), project.Blueprints, project.Areas);
        var missingCodes = WorldFeatureCatalogValidator.Validate(missingProject)
            .Select(item => item.Code).ToHashSet();
        Assert.Contains("world.placeable.security_events_required_missing", missingCodes);
        Assert.Contains("world.placeable.trap_required_missing", missingCodes);
        Assert.Contains("world.placeable.loot_required_missing", missingCodes);
        Assert.Contains("world.placeable.container_events_required_missing", missingCodes);
        Assert.Contains("world.placeable.lock_events_required_missing", missingCodes);

        var trapOnly = profile with
        {
            SecurityEvents = new(
                Guid.NewGuid(),
                1,
                "test:ux1-r",
                new(Guid.NewGuid(), "on-open", "damage:ember", 10, 12, 30,
                    "ash-watch", "vfx:ember", "ash-watch", true, false)),
            SecurityEventsRequiredAtRuntime = false,
            TrapRequiredAtRuntime = true,
        };
        var trapOnlyProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-r",
                placeables: [trapOnly])), project.Blueprints, project.Areas);
        var trapOnlyCodes = WorldFeatureCatalogValidator.Validate(trapOnlyProject)
            .Select(item => item.Code).ToHashSet();
        Assert.DoesNotContain("world.placeable.trap_invalid", trapOnlyCodes);
        Assert.DoesNotContain("world.placeable.loot_spawner_invalid", trapOnlyCodes);
        Assert.DoesNotContain("world.placeable.container_events_invalid", trapOnlyCodes);
        Assert.DoesNotContain("world.placeable.lock_events_invalid", trapOnlyCodes);
        Assert.DoesNotContain("world.placeable.contents_changed_invalid", trapOnlyCodes);

        var requiredLootMissing = trapOnly with { LootRequiredAtRuntime = true };
        var requiredLootProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-s",
                placeables: [requiredLootMissing])), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredLootProject), diagnostic =>
            diagnostic.Code == "world.placeable.loot_required_missing");

        var requiredContainerEventsMissing = trapOnly with { ContainerEventsRequiredAtRuntime = true };
        var requiredContainerEventsProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-t",
                placeables: [requiredContainerEventsMissing])), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredContainerEventsProject), diagnostic =>
            diagnostic.Code == "world.placeable.container_events_required_missing");

        var requiredLockEventsMissing = trapOnly with { LockEventsRequiredAtRuntime = true };
        var requiredLockEventsProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-u",
                placeables: [requiredLockEventsMissing])), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredLockEventsProject), diagnostic =>
            diagnostic.Code == "world.placeable.lock_events_required_missing");
    }

    [Fact]
    public void R095_PlaceableCompositionValidationLocalizesDamageSocketAttachmentVerbAndRequirementFailures()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("c2950000-0000-4000-8000-000000000001")),
            "placeable", "Placeable", BlueprintKind.Placeable);
        var invalidTransform = new PlaceableWorldProfile.TransformPolicy(Vector3Value.Zero,
            Vector3Value.Zero, Vector3Value.Zero, Vector3Value.Zero, -1, null, false, null);
        var composition = new PlaceableCompositionEventsProfile(Guid.Empty, 2, string.Empty,
            new(Guid.Empty, string.Empty, string.Empty, string.Empty, false, false, false, false,
                false, false),
            [
                new(Guid.Empty, "socket", string.Empty, invalidTransform, [string.Empty], true, 1),
                new(Guid.Empty, "socket", string.Empty, invalidTransform, [string.Empty], true, 1),
            ],
            [
                new(Guid.Empty, "socket", BlueprintId.From(Guid.NewGuid()), ["other"], invalidTransform,
                    string.Empty),
                new(Guid.Empty, "socket", BlueprintId.From(Guid.NewGuid()), ["other"], invalidTransform,
                    string.Empty),
            ],
            new(Guid.Empty,
                [new(Guid.Empty, "inspect", string.Empty, 0, ["same"], ["same"], string.Empty)]));
        var profile = new PlaceableWorldProfile(Guid.NewGuid(), 1, "test:r095", blueprint.Id,
            null, null, null, new([], new(false, 1, 1, true, "placeable")), null,
            CompositionEvents: composition, CompositionEventsRequiredAtRuntime: true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r095",
            placeables: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.profile.identity_invalid", codes);
        Assert.Contains("world.placeable.composition_events_foundation_missing", codes);
        Assert.Contains("world.placeable.damage_events_damageable_missing", codes);
        Assert.Contains("world.placeable.damage_events_invalid", codes);
        Assert.Contains("world.placeable.attachment_sockets_invalid", codes);
        Assert.Contains("world.placeable.attachment_invalid", codes);
        Assert.Contains("world.placeable.attachment_socket_capacity_exceeded", codes);
        Assert.Contains("world.placeable.multi_verb_invalid", codes);
        Assert.Contains("world.placeable.composition_events_identity_conflict", codes);

        var missing = profile with { CompositionEvents = null };
        var missingProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:r095",
                placeables: [missing])), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingProject),
            item => item.Code == "world.placeable.composition_events_required_missing");

        var canonicalDual = profile with { DamageEvents = composition.DamageEvents };
        var canonicalDualProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-v-dual",
                placeables: [canonicalDual])), project.Blueprints, project.Areas);
        Assert.DoesNotContain(WorldFeatureCatalogValidator.Validate(canonicalDualProject),
            item => item.Code == PlaceableDamageEventsResolver.CatalogDivergenceDiagnosticCode);

        var collidingDamage = new PlaceableDamageEventsProfile(
            Guid.NewGuid(), "1.0.0", "DamageApplied", "EntityDestroyed",
            true, true, true, true, true, true);
        var collidingOperations = new PlaceableOperationsProfile(
            collidingDamage.Id,
            1,
            "test:ux1-v-dual-collision",
            null,
            null,
            new(Guid.NewGuid(), 100, 100, 0, new Dictionary<string, double>(), [.5],
                "vfx:destroy", "debris:placeable", true, 0),
            null,
            null);
        var collidingDual = canonicalDual with
        {
            Operations = collidingOperations,
            CompositionEvents = composition with { DamageEvents = collidingDamage },
            DamageEvents = collidingDamage,
        };
        var collidingDualProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1,
                "test:ux1-v-dual-collision", placeables: [collidingDual])),
            project.Blueprints,
            project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(collidingDualProject),
            item => item.Code == "world.placeable.damage_events_identity_conflict" &&
                    item.Path.EndsWith("/damageEvents", StringComparison.Ordinal));

        var divergentDual = canonicalDual with
        {
            DamageEvents = composition.DamageEvents with { DestroyedBinding = "DifferentDestroyedBinding" },
        };
        var divergentDualProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(new WorldFeatureCatalog(catalog.Id, 1, "test:ux1-v-divergent",
                placeables: [divergentDual])), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(divergentDualProject),
            item => item.Code == PlaceableDamageEventsResolver.CatalogDivergenceDiagnosticCode &&
                    item.Path.EndsWith("/damageEvents", StringComparison.Ordinal));
    }

    [Fact]
    public void R046_PlaceableValidationCoversIdentityVisualRoleCollisionInteractionStaticTransformScriptsAndRequirement()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000050")),
            "placeable", "Placeable", BlueprintKind.Placeable);
        var profileId = Guid.Parse("14000000-0000-4000-8000-000000000051");
        var profile = new PlaceableWorldProfile(profileId, 1, "test:r046", blueprint.Id, null, null, null,
            new([], new(false, 1, 1, true, "placeable")), null,
            new(Guid.Empty, 2, string.Empty, string.Empty, [new(string.Empty, string.Empty, string.Empty)],
                [string.Empty, string.Empty], string.Empty, new(string.Empty, [], [], string.Empty, string.Empty, []),
                new(string.Empty, Vector3Value.Zero, false, true, string.Empty),
                new([new(Guid.Empty, string.Empty, string.Empty, string.Empty)], 0, 0, true, string.Empty, -1,
                    string.Empty, []), new(false, false, true, true, true),
                new(false, false, false, false, false,
                    new Dictionary<string, string> { [string.Empty] = string.Empty }),
                new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.Zero, Vector3Value.Zero, -1, profileId,
                    true, string.Empty), new Dictionary<string, string> { [string.Empty] = string.Empty }), true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r046", placeables: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.placeable.identity_invalid", codes);
        Assert.Contains("world.placeable.visual_invalid", codes);
        Assert.Contains("world.placeable.metadata_invalid", codes);
        Assert.Contains("world.placeable.collision_invalid", codes);
        Assert.Contains("world.placeable.role_invalid", codes);
        Assert.Contains("world.placeable.static_invalid", codes);
        Assert.Contains("world.placeable.interaction_invalid", codes);
        Assert.Contains("world.placeable.transform_invalid", codes);
        Assert.Contains("world.placeable.parent_cycle", codes);
        Assert.Contains("world.placeable.animation_reference_missing", codes);
        Assert.Contains("world.placeable.scripts_invalid", codes);

        var requiredCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r046",
            placeables: [profile with { Foundation = null }]);
        var requiredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(requiredCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredProject),
            item => item.Code == "world.placeable.foundation_required_missing");
    }

    [Fact]
    public void R047_PlaceableValidationCoversOnUsedAnimationLocksTrapsInventoryPhysicsAndBudgets()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000060")),
            "validated-placeable", "Validated placeable", BlueprintKind.Placeable);
        var foundation = new PlaceableWorldProfile.FoundationPolicy(
            Guid.Parse("14000000-0000-4000-8000-000000000061"), 1, "test:r047", "validated-placeable",
            [new("fr-FR", "Placeable", "Utiliser")], ["test"], "notes",
            new("assets/placeable.glb", ["mesh:placeable"], [], "assets/icon.png", "assets/portrait.png", []),
            new("box", new(1, 1, 1), true, true, "world"),
            new([new(Guid.Parse("14000000-0000-4000-8000-000000000062"), "placeable.use", "placeable-used", "true")],
                2, 120, true, "placeable.use", 1, "server", ["true"]), null,
            new(true, true, true, true, true, new Dictionary<string, string> { ["onUsed"] = "scripts/use" }),
            new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, Vector3Value.Zero, .25, null, true,
                "animation:rise"), new Dictionary<string, string>(),
            new(Guid.Parse("14000000-0000-4000-8000-000000000063"), 1, "test:r047", string.Empty,
                string.Empty, null, null), new([], [], [], [], [], 0, 0), true);
        var profile = new PlaceableWorldProfile(Guid.Parse("14000000-0000-4000-8000-000000000064"), 1,
            "test:r047", blueprint.Id, null, null, null, new([], new(false, 1, 1, true, "placeable")), null,
            foundation, true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r047", placeables: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.placeable.on_used_invalid", codes);
        Assert.Contains("world.placeable.animation_invalid", codes);
        Assert.Contains("world.placeable.lock_reference_missing", codes);
        Assert.Contains("world.placeable.trap_reference_missing", codes);
        Assert.Contains("world.placeable.inventory_reference_missing", codes);
        Assert.Contains("world.placeable.physics_profile_missing", codes);
        Assert.Contains("world.placeable.budget_exceeded", codes);

        var requiredCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r047",
            placeables: [profile with { Foundation = foundation with { OnUsed = null } }]);
        var requiredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(requiredCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredProject),
            item => item.Code == "world.placeable.on_used_required_missing");
    }

    [Fact]
    public void R048_TriggerValidationCoversIdentityGeometryPolicyActionsPortalTransformEventsAndRequirement()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000070")),
            "trigger", "Trigger", BlueprintKind.Trigger);
        var profileId = Guid.Parse("14000000-0000-4000-8000-000000000071");
        var foundation = new TriggerWorldProfile.FoundationPolicy(Guid.Empty, 2, string.Empty, string.Empty,
            new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D, [Vector3Value.Zero], [[Vector3Value.Zero]],
                Vector3Value.Zero, 0, -1, true),
            new([string.Empty], [string.Empty], [], []), [string.Empty], -1,
            TriggerWorldProfile.FirePolicy.Once, [],
            new(TriggerWorldProfile.DestinationKind.Waypoint, Guid.Empty, string.Empty, Vector3Value.Zero, true,
                [string.Empty]),
            new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.Zero, profileId),
            new Dictionary<string, string> { [string.Empty] = string.Empty },
            new(Guid.Empty, 2, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            new(Guid.Empty, 2, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
        var profile = new TriggerWorldProfile(profileId, 1, "test:r048", blueprint.Id, state.Project.Areas[0].Id,
            null, null, null, string.Empty, null, null, null, null, null, foundation, true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r048", triggers: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.trigger.identity_invalid", codes);
        Assert.Contains("world.trigger.geometry_invalid", codes);
        Assert.Contains("world.trigger.policy_invalid", codes);
        Assert.Contains("world.trigger.actions_invalid", codes);
        Assert.Contains("world.trigger.portal_invalid", codes);
        Assert.Contains("world.trigger.transform_invalid", codes);
        Assert.Contains("world.trigger.event_invalid", codes);

        var requiredCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r048",
            triggers: [profile with { Foundation = null }]);
        var requiredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(requiredCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredProject),
            item => item.Code == "world.trigger.foundation_required_missing");
    }

    [Fact]
    public void R049_TriggerValidationRejectsSelfIntersectionContradictoryFiltersTargetsPortalScriptsAndAccessibility()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000080")),
            "validated-trigger", "Validated trigger", BlueprintKind.Trigger);
        var destination = Guid.Parse("14000000-0000-4000-8000-000000000081");
        var foundation = new TriggerWorldProfile.FoundationPolicy(
            Guid.Parse("14000000-0000-4000-8000-000000000082"), 1, "test:r049", "validated-trigger",
            new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                [new(0, 0, 0), new(2, 0, 2), new(0, 0, 2), new(2, 0, 0)], [], new(2, 2, 2), 2, .25, true),
            new(["creature"], ["hero"], ["hero"], ["characters"]), [], -1,
            TriggerWorldProfile.FirePolicy.Repeat,
            [new(Guid.Parse("14000000-0000-4000-8000-000000000083"), "script", "missing-target",
                new Dictionary<string, string> { ["script"] = "missing-script" })],
            new(TriggerWorldProfile.DestinationKind.Waypoint, destination, "anchor:exit", Vector3Value.Zero,
                true, ["portal.travel"]), new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
            new Dictionary<string, string>(),
            new(Guid.Parse("14000000-0000-4000-8000-000000000084"), 1, "test:r049", "1.0.0",
                "VolumeEntered", null, "scripts/enter"),
            new(Guid.Parse("14000000-0000-4000-8000-000000000085"), 1, "test:r049", "1.0.0",
                "VolumeExited", null, "scripts/exit"),
            new([Guid.Parse("14000000-0000-4000-8000-000000000086")], ["other-target"],
                ["other-script"], ["vehicles"]), true);
        var profile = new TriggerWorldProfile(Guid.Parse("14000000-0000-4000-8000-000000000087"), 1,
            "test:r049", blueprint.Id, state.Project.Areas[0].Id, null,
            new("inspect", "cursor", "outline", "prompt", "scripts/click", true, "trigger.use"), null,
            "trigger", null, null, null, null, null, foundation, true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r049", triggers: [profile]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var codes = WorldFeatureCatalogValidator.Validate(project).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.trigger.geometry_self_intersection", codes);
        Assert.Contains("world.trigger.filter_contradiction", codes);
        Assert.Contains("world.trigger.action_target_missing", codes);
        Assert.Contains("world.trigger.portal_destination_missing", codes);
        Assert.Contains("world.trigger.policy_invalid", codes);
        Assert.Contains("world.trigger.script_reference_missing", codes);
        Assert.Contains("world.trigger.inaccessible", codes);

        var requiredCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r049",
            triggers: [profile with { Foundation = foundation with { Validation = null } }]);
        var requiredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(requiredCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredProject),
            item => item.Code == "world.trigger.validation_required_missing");
    }

    [Fact]
    public void R096_AdvancedTriggerUsesCanonicalCommandScriptProjectionAndStableValidationDiagnostics()
    {
        var state = CreateState();
        var factionId = Guid.Parse("14000000-0000-4000-8000-000000000190");
        var factions = new FactionCatalog(
            [new FactionDefinition(factionId, 1, "test:r096", "ash-watch", "Ash Watch", false, [], [])],
            [],
            [],
            []);
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000180")),
            "advanced-trigger", "Advanced trigger", BlueprintKind.Trigger);
        var triggerId = Guid.Parse("14000000-0000-4000-8000-000000000181");
        var destination = Guid.Parse("14000000-0000-4000-8000-000000000182");
        var foundation = new TriggerWorldProfile.FoundationPolicy(
            Guid.Parse("14000000-0000-4000-8000-000000000183"), 1, "test:r096", "advanced-trigger",
            new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)], [], new(4, 3, 4),
                3, .25, true),
            new(["creature"], ["player"], ["undead"], ["characters"]), [], 5,
            TriggerWorldProfile.FirePolicy.OncePerEntity,
            [new(Guid.Parse("14000000-0000-4000-8000-000000000184"), "script", "encounter",
                new Dictionary<string, string>())],
            new(TriggerWorldProfile.DestinationKind.Waypoint, destination, "anchor", new(0, 180, 0), true,
                ["portal.travel"]), new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
            new Dictionary<string, string> { ["editor.overlay"] = "volume" },
            new(Guid.Parse("14000000-0000-4000-8000-000000000185"), 1, "test:r096", "1.0.0",
                "VolumeEntered", null, null),
            new(Guid.Parse("14000000-0000-4000-8000-000000000186"), 1, "test:r096", "1.0.0",
                "VolumeExited", null, null));
        var volumes = Enum.GetValues<TriggerGameplayVolumeKind>().Select((kind, index) =>
            new TriggerGameplayVolumeProfile(
                Guid.Parse($"14000000-0000-4000-8000-{(200 + index):000000000000}"), kind,
                new Dictionary<string, double> { [kind.ToString().ToLowerInvariant()] = index + 1 },
                new Dictionary<string, string>(), $"{kind}VolumeEntered", $"{kind}VolumeExited", true)).ToArray();
        var advanced = new TriggerAdvancedProfile(
            Guid.Parse("14000000-0000-4000-8000-000000000187"), 1, "test:r096", "1.0.0",
            new(Guid.Parse("14000000-0000-4000-8000-000000000188"), "enter", "damage:ember", 10,
                12, 30, "ash-watch", "vfx:ember", "ash-watch", true, false, true, "#ff7a45",
                "TrapDetected", "TrapDisarmed", "TrapTriggered", "TrapReset"),
            new(Guid.Parse("14000000-0000-4000-8000-000000000189"), true, ["player"], ["undead"],
                [factionId], ["inventory"], "party", "heroes",
                "quest==active"), volumes,
            new(Guid.Parse("14000000-0000-4000-8000-000000000191"), true, true, true, true, true, true,
                100));
        var trigger = new TriggerWorldProfile(triggerId, 1, "test:r096", blueprint.Id,
            state.Project.Areas[0].Id, null, null, null, "trigger.advanced", null, null, null, null, null,
            foundation, true, advanced, true);
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r096", triggers: [trigger]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithFactions(factions).WithWorldFeatures(catalog), [blueprint], state.Project.Areas);
        var configuredState = new ProjectState(project, state.Revision);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), state.Revision, catalog,
            false);
        var applied = handler.Handle(configuredState, command);
        var history = new ProjectCommandHistory(configuredState);
        history.Record(applied);
        var handle = Assert.Single(new WorldFeatureScriptService().Query(applied.State, "trigger"));

        Assert.True(applied.Succeeded);
        Assert.Equal("True", handle.Properties["advanced"]);
        Assert.Contains("damage:ember", handle.Properties["trap"]);
        Assert.Contains("Damage", handle.Properties["gameplayVolumes"]);
        Assert.Contains("maximum:100", handle.Properties["occupancyDebug"]);
        Assert.True(history.Undo().Succeeded);
        Assert.True(history.Redo().Succeeded);

        var duplicate = Guid.Parse("14000000-0000-4000-8000-000000000192");
        var invalidAdvanced = advanced with
        {
            Id = duplicate,
            ForgeScriptApiVersion = string.Empty,
            Trap = advanced.Trap with
            {
                Id = duplicate,
                TriggerId = string.Empty,
                DetectionDifficulty = -1,
                VisualizationColor = "red",
                DetectedBinding = string.Empty,
            },
            TargetFilter = advanced.TargetFilter with
            {
                Id = duplicate,
                RequiredTags = ["hero"],
                ExcludedTags = ["hero"],
                FactionIds = [Guid.Empty],
                SandboxPredicate = "unsafe",
            },
            GameplayVolumes = [volumes[0] with { Id = duplicate, Enabled = false, EnterBinding = "wrong" }],
            OccupancyDebug = advanced.OccupancyDebug with
            {
                Id = duplicate,
                Enabled = false,
                ShowEventTrace = false,
                MaximumTraceEntries = 0,
            },
        };
        var invalidCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r096",
            triggers: [trigger with { Advanced = invalidAdvanced }]);
        var invalidProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(invalidCatalog), project.Blueprints, project.Areas);
        var codes = WorldFeatureCatalogValidator.Validate(invalidProject).Select(item => item.Code).ToHashSet();

        Assert.Contains("world.trigger.advanced_api_invalid", codes);
        Assert.Contains("world.trigger.trap_invalid", codes);
        Assert.Contains("world.trigger.target_filter_invalid", codes);
        Assert.Contains("world.trigger.gameplay_volumes_invalid", codes);
        Assert.Contains("world.trigger.occupancy_debug_invalid", codes);
        Assert.Contains("world.trigger.advanced_uuid_duplicate", codes);
        var requiredCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r096",
            triggers: [trigger with { Advanced = null, AdvancedRequiredAtRuntime = true }]);
        var requiredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(requiredCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(requiredProject),
            item => item.Code == "world.trigger.advanced_required");

        var missingFactionId = Guid.Parse("14000000-0000-4000-8000-000000000193");
        var missingFactionCatalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r096",
            triggers:
            [
                trigger with
                {
                    Advanced = advanced with
                    {
                        TargetFilter = advanced.TargetFilter with { FactionIds = [factionId, missingFactionId] },
                    },
                },
            ]);
        var missingFactionProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(missingFactionCatalog), project.Blueprints, project.Areas);
        Assert.Contains(WorldFeatureCatalogValidator.Validate(missingFactionProject), item =>
            item.Code == "world.trigger.faction_reference_missing" &&
            item.Path.EndsWith(missingFactionId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void R081_CommandAndTypedScriptExposeCanonicalDoorAccessEvents()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000090")),
            "ash-gate", "Ash gate", BlueprintKind.Door);
        var door = Door(blueprint.Id) with
        {
            AccessEvents = Access(Guid.Parse("14000000-0000-4000-8000-000000000091")),
            AccessEventsRequiredAtRuntime = true,
        };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r081", doors: [door]);
        var configured = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);
        var configuredState = new ProjectState(configured, state.Revision);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));

        var preview = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, true));
        var applied = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, false));
        var generic = new WorldFeatureScriptService().Query(applied.State, "door", door.Id.ToString("D"), 1)
            .Single();
        var typed = new DoorAccessEventScriptService().Get(applied.State, door.Id);

        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal("True", generic.Properties["accessEvents"]);
        Assert.Equal("PortalUseRequested", generic.Properties["portalRequested"]);
        Assert.Equal("key:ember", Assert.Single(typed.Lock.CredentialIds));
        Assert.Equal("DoorClosed", typed.StateEvents.AfterOrCompleted);
    }

    [Fact]
    public void R081_ProjectValidationRejectsDoorAccessComponentUuidReuseAcrossProfiles()
    {
        var state = CreateState();
        var firstBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000092")),
            "first-door", "First door", BlueprintKind.Door);
        var secondBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000093")),
            "second-door", "Second door", BlueprintKind.Door);
        var sharedAccess = Access(Guid.Parse("14000000-0000-4000-8000-000000000094"));
        var first = Door(firstBlueprint.Id) with { AccessEvents = sharedAccess };
        var second = Door(secondBlueprint.Id) with
        {
            Id = Guid.Parse("14000000-0000-4000-8000-000000000095"),
            AccessEvents = Access(Guid.Parse("14000000-0000-4000-8000-000000000096")) with
            {
                Trap = sharedAccess.Trap,
            },
        };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r081",
            doors: [first, second]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [firstBlueprint, secondBlueprint], state.Project.Areas);

        var diagnostics = WorldFeatureCatalogValidator.Validate(project);

        Assert.Contains(diagnostics, item => item.Code == "world.door.access_events_uuid_duplicate" &&
                                             item.Path.Contains(sharedAccess.Trap.Id.ToString("D"),
                                                 StringComparison.Ordinal));
    }

    [Fact]
    public void R082_CommandAndTypedScriptExposeValidatedDoorLockEvents()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-000000000097")),
            "lock-event-door", "Lock event door", BlueprintKind.Door);
        var access = Access(Guid.Parse("14000000-0000-4000-8000-000000000098"));
        access = access with
        {
            LockStateEvents = new(Stable(access.Id, 6), 1, "test:r082", "1.0.0", "DoorLocked",
                "DoorUnlocked"),
            LockStateEventsRequiredAtRuntime = true,
        };
        var door = Door(blueprint.Id) with { AccessEvents = access, AccessEventsRequiredAtRuntime = true };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r082", doors: [door]);
        var configured = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);
        var configuredState = new ProjectState(configured, state.Revision);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));

        var preview = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, true));
        var applied = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, false));
        var generic = new WorldFeatureScriptService().Query(applied.State, "door", door.Id.ToString("D"), 1)
            .Single();
        var typed = new DoorAccessEventScriptService().Get(applied.State, door.Id);
        var invalidCatalog = new WorldFeatureCatalog(catalog.Id, 1, "test:r082", doors:
        [door with
        {
            AccessEvents = access with
            {
                LockStateEvents = access.LockStateEvents! with { LockedBinding = "locked" },
            },
        }]);
        var invalid = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            invalidCatalog, true));

        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal("DoorLocked", typed.LockStateEvents!.LockedBinding);
        Assert.True(typed.LockStateEventsRequiredAtRuntime);
        Assert.Equal("DoorUnlocked", generic.Properties["doorUnlocked"]);
        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Diagnostics, item => item.Code == "world.door.lock_events_invalid");
    }

    [Fact]
    public void R083_CommandAndTypedScriptExposeEncounterComposition()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-0000000000a0")),
            "ash-warden", "Ash warden", BlueprintKind.Creature);
        var encounter = WithComposition(Encounter(state.Project.Areas[0].Id, blueprint.Id));
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r083",
            encounters: [encounter]);
        var configured = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);
        var configuredState = new ProjectState(configured, state.Revision);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));

        var preview = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, true));
        var applied = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, false));
        var typed = new EncounterCompositionScriptService().Get(applied.State, encounter.Id);
        var generic = new WorldFeatureScriptService().Query(applied.State, "encounter",
            encounter.Id.ToString("D"), 1).Single();

        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal(36, typed.Difficulty.EffectiveBudget);
        Assert.Equal((2, 4), (typed.Groups[0].Minimum, typed.Groups[0].Maximum));
        Assert.Equal("PopulationMaintained", typed.Activation.Mode);
        Assert.Equal("True", generic.Properties["composition"]);
        Assert.Equal("party:forge-generic", generic.Properties["partyProvider"]);
        Assert.Equal("True", generic.Properties["compositionRequiredAtRuntime"]);
    }

    [Fact]
    public void R083_ProjectValidationRejectsEncounterCompositionComponentUuidReuse()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-0000000000a1")),
            "ash-warden", "Ash warden", BlueprintKind.Creature);
        var first = WithComposition(Encounter(state.Project.Areas[0].Id, blueprint.Id));
        var secondSource = WithComposition(Encounter(state.Project.Areas[0].Id, blueprint.Id));
        var second = secondSource with
        {
            Id = Guid.Parse("14000000-0000-4000-8000-0000000000a2"),
            Foundation = secondSource.Foundation! with
            {
                Candidates = [secondSource.Foundation.Candidates[0] with
                { Id = Guid.Parse("14000000-0000-4000-8000-0000000000a3") }],
            },
            Composition = secondSource.Composition! with
            {
                Id = Guid.Parse("14000000-0000-4000-8000-0000000000a4"),
                Difficulty = secondSource.Composition.Difficulty with
                { Id = Guid.Parse("14000000-0000-4000-8000-0000000000a5") },
                Selection = secondSource.Composition.Selection with
                { Id = Guid.Parse("14000000-0000-4000-8000-0000000000a6") },
                Activation = secondSource.Composition.Activation with
                { Id = Guid.Parse("14000000-0000-4000-8000-0000000000a7") },
            },
        };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r083",
            encounters: [first, second]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var diagnostics = WorldFeatureCatalogValidator.Validate(project);

        Assert.Contains(diagnostics, item => item.Code == "world.encounter.composition_uuid_duplicate" &&
                                             item.Path.Contains(first.Composition!.Groups[0].Id.ToString("D"),
                                                 StringComparison.Ordinal));
    }

    [Fact]
    public void R084_CommandAndTypedScriptExposeManualAnchorRestrictionsLifecycleAndWaveGraph()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-0000000000c0")),
            "ash-warden", "Ash warden", BlueprintKind.Creature);
        var encounter = WithScenario(WithComposition(Encounter(state.Project.Areas[0].Id, blueprint.Id)));
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r084",
            encounters: [encounter]);
        var configured = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);
        var configuredState = new ProjectState(configured, state.Revision);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));

        var preview = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, true));
        var applied = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, false));
        var typed = new EncounterScenarioScriptService().Get(applied.State, encounter.Id);
        var generic = new WorldFeatureScriptService().Query(applied.State, "encounter",
            encounter.Id.ToString("D"), 1).Single();

        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal("anchor:test", Assert.Single(typed.SpawnAnchors).AnchorId);
        Assert.Equal("Server", typed.Activator.RequiredAuthority);
        Assert.Equal("EncounterEntered", typed.Lifecycle.EnteredBinding);
        Assert.Equal(2, Assert.Single(typed.Phases).Waves.Count);
        Assert.Equal("True", generic.Properties["scenario"]);
        Assert.Equal("EncounterExhausted", generic.Properties["encounterExhausted"]);
        Assert.Equal("2", generic.Properties["waves"]);
        Assert.Equal("True", generic.Properties["scenarioRequiredAtRuntime"]);
    }

    [Fact]
    public void R084_ProjectValidationRejectsScenarioUuidReuseBeforeBuild()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-0000000000c1")),
            "ash-warden", "Ash warden", BlueprintKind.Creature);
        var first = WithScenario(WithComposition(Encounter(state.Project.Areas[0].Id, blueprint.Id)));
        var second = first with { Id = Guid.Parse("14000000-0000-4000-8000-0000000000c2") };
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r084",
            encounters: [first, second]);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);

        var diagnostics = WorldFeatureCatalogValidator.Validate(project);

        Assert.Contains(diagnostics, item => item.Code == "world.encounter.scenario_uuid_duplicate");
    }

    [Fact]
    public void R085_CommandTypedScriptAndGenericResourceExposeStatisticsAndRejectInvalidConfiguration()
    {
        var state = CreateState();
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("14000000-0000-4000-8000-0000000000e0")),
            "ash-warden", "Ash warden", BlueprintKind.Creature);
        var encounter = WithStatistics(WithScenario(WithComposition(
            Encounter(state.Project.Areas[0].Id, blueprint.Id))));
        var catalog = new WorldFeatureCatalog(state.Project.Module.Id.Value, 1, "test:r085",
            encounters: [encounter]);
        var configured = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithWorldFeatures(catalog), [blueprint], state.Project.Areas);
        var configuredState = new ProjectState(configured, state.Revision);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));

        var preview = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, true));
        var applied = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog, false));
        var typed = new EncounterStatisticsScriptService().Get(applied.State, encounter.Id);
        var generic = new WorldFeatureScriptService().Query(applied.State, "encounter",
            encounter.Id.ToString("D"), 1).Single();
        var invalidCatalog = new WorldFeatureCatalog(catalog.Id, 1, "test:r085", encounters:
        [encounter with { Statistics = encounter.Statistics! with { Runs = 0, Percentiles = [1] } }]);
        var invalid = handler.Handle(configuredState, new(CommandId.From(Guid.NewGuid()), state.Revision,
            invalidCatalog, true));

        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal(64, typed.Runs);
        Assert.Equal(840, typed.BaseSeed);
        Assert.Equal([.5, .9, .99], typed.Percentiles);
        Assert.Equal("True", generic.Properties["statistics"]);
        Assert.Equal("64", generic.Properties["statisticsRuns"]);
        Assert.Equal("0.5,0.9,0.99", generic.Properties["statisticsPercentiles"]);
        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Diagnostics,
            item => item.Code == "world.encounter.statistics_configuration_invalid");
        Assert.Contains(invalid.Diagnostics,
            item => item.Code == "world.encounter.statistics_percentiles_invalid");
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("14000000-0000-4000-8000-000000000001")),
            "world", "World", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("14000000-0000-4000-8000-000000000002")),
            "World features", [], [area]);
        return new(project, new ProjectRevision(7));
    }

    private static WorldFeatureCatalog Catalog(Guid moduleId)
    {
        var scripts = new ScriptAutomationProfile(moduleId, 1, "test:g13-aa-ab",
            new([], "restore-camera-and-control", true, true), new(false, true, []),
            [new("rest.enabled", ScriptAutomationProfile.SettingKind.Boolean, "true",
                ScriptAutomationProfile.FlagScope.Module, "Allows safe rest.", 1)],
            [new("combat.aggression", ScriptAutomationProfile.SettingKind.Choice, "balanced",
                ["passive", "balanced", "aggressive"], "Balanced combat behavior.", true)]);
        return new(moduleId, 1, "test:g13-n-ab", scripts: scripts);
    }

    private static DoorWorldProfile Door(BlueprintId blueprintId)
    {
        var id = Guid.Parse("14000000-0000-4000-8000-000000000020");
        return new(id, 1, "test:r041", blueprintId, null, null, [], null, null, null,
            new(DoorWorldProfile.PlacementMode.FreePlacement, null, null, "closed",
                [
                    new("open", DoorWorldProfile.StateKind.Open, "animation:open", false, true),
                    new("closed", DoorWorldProfile.StateKind.Closed, "animation:closed", true, true),
                    new("opening", DoorWorldProfile.StateKind.Opening, "animation:opening", false, false),
                ],
                [new("closed", "opening", "interact", 1, null)],
                new(2, "both", 1, 2, 1, "server", 2, "door.unavailable"), null, [],
                new Dictionary<string, string>
                {
                    [DoorWorldProfile.CollisionProfileOverrideKey] = "collision:door",
                },
                new("nav-obstacle:door", "nav-link:door", ["closed"], ["open"], .75, .25, true)));
    }

    private static DoorAccessEventProfile Access(Guid id) => new(id, 1, "test:r081",
        "assets/doors/ash-gate.glb", TransformValue.Identity,
        new(Stable(id, 1), DoorAccessLockState.Locked, ["key:ember"], "mechanics", 12, null, true, true,
            true, true, ["door.unlock"]),
        new(Stable(id, 2), "1.0.0", "DoorOpenRejected",
            [new("fr-FR", "Verrouillée."), new("en-US", "Locked.")], "audio:locked", "animation:rattle",
            "scripts/rejected"),
        new(Stable(id, 3), "door:open", "damage:ember", 10, 12, 30, "ash-watch", "vfx:ember",
            "ash-watch", true, false, ["front"], ["interact"]),
        new(Stable(id, 4), "1.0.0", "PortalUseRequested", "PortalUseCompleted"),
        new(Stable(id, 5), "1.0.0", "DoorOpened", "DoorClosed"), new Dictionary<string, string>());

    private static Guid Stable(Guid id, byte suffix)
    {
        var bytes = id.ToByteArray();
        bytes[^1] = suffix;
        return new Guid(bytes);
    }

    private static EncounterWorldProfile Encounter(AreaId areaId, BlueprintId blueprintId) => new(
        Guid.Parse("14000000-0000-4000-8000-000000000040"), 1, "test:r042", areaId,
        null, null, null, null, new(EncounterWorldProfile.VisibilityMode.Hidden, 0, false, string.Empty),
        new(0, 0, 0, true, true),
        new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                [new(0, 0, 0), new(4, 0, 0), new(4, 0, 4), new(0, 0, 4)], Vector3Value.Zero,
                4, 4, ["player"], ["party"], true, 12, "scripts/encounter", true),
            [new(Guid.Parse("14000000-0000-4000-8000-000000000041"), blueprintId,
                EncounterWorldProfile.CandidateRole.Controller, 3, 8, ["area.occupied"], ["ember"], 4)],
            12, ["anchor:test"], ["reward:test"], 8));

    private static EncounterWorldProfile WithComposition(EncounterWorldProfile encounter)
    {
        var candidateId = encounter.Foundation!.Candidates[0].Id;
        return encounter with
        {
            Foundation = encounter.Foundation with { SpawnBudget = 36 },
            Composition = new(Guid.Parse("14000000-0000-4000-8000-0000000000b0"), 1, "test:r083",
                "ash-ambush", "Ash ambush", "ambush", "ash-crypt",
                EncounterWorldProfile.CandidateRole.Controller,
                new(Guid.Parse("14000000-0000-4000-8000-0000000000b1"), "ruleset:forge",
                    "party:forge-generic", EncounterDifficultyBand.Hard, 4, 12, 20, 1.2, 1.5),
                [new(Guid.Parse("14000000-0000-4000-8000-0000000000b2"), "wave:warden", 2, 4, 12,
                    [candidateId])],
                new(Guid.Parse("14000000-0000-4000-8000-0000000000b3"), "party:forge-generic",
                    "challenge-rating", 42, true),
                new(Guid.Parse("14000000-0000-4000-8000-0000000000b4"),
                    EncounterActivationMode.PopulationMaintained, 30, null, EncounterSpawnDistribution.Weighted),
                new Dictionary<string, string>()),
            CompositionRequiredAtRuntime = true,
        };
    }

    private static EncounterWorldProfile WithScenario(EncounterWorldProfile encounter)
    {
        var groupId = encounter.Composition!.Groups[0].Id;
        var secondWaveId = Guid.Parse("14000000-0000-4000-8000-0000000000d9");
        return encounter with
        {
            Scenario = new(Guid.Parse("14000000-0000-4000-8000-0000000000d0"), 1, "test:r084",
                [new(Guid.Parse("14000000-0000-4000-8000-0000000000d1"), "anchor:test",
                    new(new(2, 0, 4), QuaternionValue.Identity, Vector3Value.One), 8, true,
                    EncounterWorldProfile.VisibilityMode.BehindCover, true, 10)],
                EncounterSpawnDistribution.Weighted,
                new(Guid.Parse("14000000-0000-4000-8000-0000000000d2"), true, true, true, true, true,
                    EncounterNetworkAuthority.Server, ["party"]),
                new(Guid.Parse("14000000-0000-4000-8000-0000000000d3"), "1.0.0",
                    new(Guid.Parse("14000000-0000-4000-8000-0000000000d4"), "EncounterEntered", "scripts/entered"),
                    new(Guid.Parse("14000000-0000-4000-8000-0000000000d5"), "EncounterExited", "scripts/exited"),
                    new(Guid.Parse("14000000-0000-4000-8000-0000000000d6"), "EncounterExhausted", "scripts/exhausted")),
                [new(Guid.Parse("14000000-0000-4000-8000-0000000000d7"), 1, "Opening", ["area.occupied"],
                    [new(Guid.Parse("14000000-0000-4000-8000-0000000000d8"), 1, [groupId], 0,
                            EncounterWaveCompletionPolicy.AllDefeated, null, null, null, "audio:warning", secondWaveId),
                        new(secondWaveId, 2, [groupId], 2, EncounterWaveCompletionPolicy.TimerElapsed, null,
                            null, null, "audio:advance", null)], [])], new Dictionary<string, string>()),
            ScenarioRequiredAtRuntime = true,
        };
    }

    private static EncounterWorldProfile WithStatistics(EncounterWorldProfile encounter) => encounter with
    {
        Statistics = new(Guid.Parse("14000000-0000-4000-8000-0000000000e1"), 1, "test:r085", 64, 840,
            "duration:actors-linear", 5, "reward:cost-linear", .5, [.5, .9, .99], true,
            new Dictionary<string, string>()),
        StatisticsRequiredAtRuntime = true,
    };
}
