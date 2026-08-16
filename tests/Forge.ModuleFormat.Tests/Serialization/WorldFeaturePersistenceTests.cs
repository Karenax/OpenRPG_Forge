using System.Text.Json.Nodes;
using Forge.Domain.Blueprints;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class WorldFeaturePersistenceTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(), "openrpg-forge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Aggregate_and_workspace_round_trips_preserve_all_world_feature_families()
    {
        var project = CompleteProject();
        var aggregate = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(aggregate);
        var created = ProjectSession.Create(_temporaryRoot, project);
        var reopened = ProjectSession.Open(_temporaryRoot);

        Assert.Contains("\"formatVersion\": \"0.6.36\"", aggregate, StringComparison.Ordinal);
        Assert.Equal(aggregate, ProjectJsonSerializer.Serialize(restored));
        Assert.True(created.Succeeded);
        Assert.True(reopened.Succeeded);
        Assert.Contains("\"formatVersion\": \"1.28.0\"", File.ReadAllText(reopened.Session!.ProjectFilePath),
            StringComparison.Ordinal);
        var catalog = reopened.Session.Project.Module.WorldFeatures;
        Assert.Single(catalog.Placeables);
        Assert.True(catalog.Placeables[0].FoundationRequiredAtRuntime);
        Assert.Equal("world-placeable", Assert.IsType<PlaceableWorldProfile.FoundationPolicy>(catalog.Placeables[0].Foundation).Slug);
        Assert.Equal(new Vector3Value(1.25f, 0, -2.5f), catalog.Placeables[0].Foundation!.Transform.Position);
        Assert.Equal("InteractionExecuted", catalog.Placeables[0].Foundation!.OnUsed!.EventBinding);
        Assert.Equal("lock:test", catalog.Placeables[0].Foundation!.Validation!.LockIds.Single());
        Assert.True(catalog.Placeables[0].OperationsRequiredAtRuntime);
        var placeableOperations = Assert.IsType<PlaceableOperationsProfile>(catalog.Placeables[0].Operations);
        Assert.Equal("inventory:test", placeableOperations.Container!.InventoryId);
        Assert.True(placeableOperations.Container.Replicated);
        Assert.Equal("closed", placeableOperations.StateMachine!.InitialStateId);
        Assert.Equal(6, placeableOperations.StateMachine.States.Count);
        Assert.Equal(100, placeableOperations.Damageable!.MaximumHealth);
        Assert.Equal("PlaceableProtectionOverride", placeableOperations.Protection!.AuditBinding);
        Assert.Equal(PlaceableAuthoredLockState.Locked, placeableOperations.Lock!.InitialState);
        Assert.Equal("mechanics", placeableOperations.Lock.SkillId);
        Assert.Equal(12, placeableOperations.Lock.Difficulty);
        Assert.True(placeableOperations.Lock.Hackable && placeableOperations.Lock.Relockable &&
                    placeableOperations.Lock.CanJam);
        Assert.True(catalog.Placeables[0].FactionOwnershipRequiredAtRuntime);
        var factionOwnership = Assert.IsType<PlaceableFactionOwnershipProfile>(
            catalog.Placeables[0].FactionOwnership);
        Assert.Equal(Guid.Parse("15200000-0000-4000-8000-000000000060"),
            factionOwnership.PrimaryFactionId);
        Assert.True(factionOwnership.InheritHostility && factionOwnership.UnauthorizedActionsAreCrime &&
                    factionOwnership.MembersOnlyAccess && factionOwnership.BlockFriendlyTargeting);
        Assert.True(catalog.Placeables[0].ConversationRequiredAtRuntime);
        var conversation = Assert.IsType<PlaceableConversationProfile>(catalog.Placeables[0].Conversation);
        Assert.Equal(2, conversation.Bindings.Count);
        Assert.Equal(project.Dialogues[0].Id.Value, conversation.Bindings[0].DialogueId);
        Assert.Equal(PlaceableConversationTrigger.Interaction, conversation.Bindings[0].Trigger);
        Assert.Equal(PlaceableConversationTrigger.Item, conversation.Bindings[1].Trigger);
        Assert.Equal(project.Blueprints.Single(blueprint => blueprint.Kind == BlueprintKind.Item).Id,
            conversation.Bindings[1].ItemBlueprintId);
        Assert.True(catalog.Placeables[0].SecurityEventsRequiredAtRuntime);
        Assert.True(catalog.Placeables[0].TrapRequiredAtRuntime);
        Assert.True(catalog.Placeables[0].LootRequiredAtRuntime);
        Assert.True(catalog.Placeables[0].ContainerEventsRequiredAtRuntime);
        Assert.True(catalog.Placeables[0].LockEventsRequiredAtRuntime);
        Assert.True(catalog.Placeables[0].CredentialRequiredAtRuntime);
        var credential = Assert.IsType<PlaceableWorldProfile.CredentialPolicy>(catalog.Placeables[0].Credential);
        Assert.Equal("key", credential.CredentialId);
        Assert.Equal(PlaceableWorldProfile.CredentialConsumption.Consume, credential.Consumption);
        Assert.Equal(1, credential.Charges);
        Assert.True(credential.AuditUse);
        var placeableSecurityEvents = Assert.IsType<PlaceableSecurityEventsProfile>(
            catalog.Placeables[0].SecurityEvents);
        var trap = Assert.IsType<PlaceableTrapProfile>(placeableSecurityEvents.Trap);
        var loot = Assert.IsType<PlaceableLootSpawnerProfile>(placeableSecurityEvents.Loot);
        var containerEvents = Assert.IsType<PlaceableContainerStateEventsProfile>(
            placeableSecurityEvents.ContainerEvents);
        var lockEvents = Assert.IsType<PlaceableLockStateEventsProfile>(placeableSecurityEvents.LockEvents);
        var contentsChanged = Assert.IsType<PlaceableContentsChangedEventProfile>(
            placeableSecurityEvents.ContentsChanged);
        Assert.Equal("damage:ember", trap.PayloadId);
        Assert.Equal(30, trap.ResetDelaySeconds);
        Assert.Equal(PlaceableAuthoredLootSpawnTiming.Open, loot.Timing);
        Assert.Equal(Guid.Parse("15000000-0000-4000-8000-000000000008"),
            loot.LootTable.Entries.Single().ItemDefinitionId);
        Assert.Equal("ContainerOpened", containerEvents.OpenedBinding);
        Assert.Equal("ContainerClosed", containerEvents.ClosedBinding);
        Assert.Equal("LockStateChanging", lockEvents.ChangingBinding);
        Assert.True(lockEvents.ControlledVeto);
        Assert.Equal("ContainerContentsChanged", contentsChanged.EventBinding);
        Assert.Single(catalog.Doors);
        Assert.Equal(9, catalog.Doors[0].Foundation!.States.Count);
        Assert.Equal(DoorWorldProfile.DestinationKind.Waypoint,
            catalog.Doors[0].Foundation!.Portal!.DestinationKind);
        Assert.Equal("nav-obstacle:test-door", catalog.Doors[0].Foundation!.Navigation!.ObstacleId);
        Assert.True(catalog.Doors[0].Foundation!.Navigation!.VisualizeInEditor);
        Assert.True(catalog.Doors[0].AccessEventsRequiredAtRuntime);
        Assert.Equal("key:ember", catalog.Doors[0].AccessEvents!.Lock.CredentialIds.Single());
        Assert.Equal("DoorOpenRejected", catalog.Doors[0].AccessEvents!.Rejection.EventBinding);
        Assert.Equal("damage:ember", catalog.Doors[0].AccessEvents!.Trap.PayloadId);
        Assert.Equal("PortalUseRequested", catalog.Doors[0].AccessEvents!.PortalUse.RequestedBinding);
        Assert.Equal("DoorClosed", catalog.Doors[0].AccessEvents!.StateEvents.ClosedBinding);
        Assert.True(catalog.Doors[0].AccessEvents!.LockStateEventsRequiredAtRuntime);
        Assert.Equal("DoorUnlocked", catalog.Doors[0].AccessEvents!.LockStateEvents!.UnlockedBinding);
        Assert.Single(catalog.Triggers);
        Assert.True(catalog.Triggers[0].FoundationRequiredAtRuntime);
        Assert.Equal(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D, catalog.Triggers[0].Foundation!.Shape.Kind);
        Assert.Equal("VolumeEntered", catalog.Triggers[0].Foundation!.OnEnter.EventBinding);
        Assert.Equal("anchor:exit", catalog.Triggers[0].Foundation!.Portal!.SpawnAnchorId);
        Assert.True(catalog.Triggers[0].Foundation!.ValidationRequiredAtRuntime);
        Assert.Equal("scripts/enter", catalog.Triggers[0].Foundation!.Validation!.KnownScriptIds[0]);
        Assert.Single(catalog.Encounters);
        Assert.Equal(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
            catalog.Encounters[0].Foundation!.Volume.Shape);
        Assert.Single(catalog.Encounters[0].Foundation!.Candidates);
        Assert.Single(catalog.VehicleRoutes);
        Assert.Single(catalog.Merchants);
        Assert.True(catalog.Merchants[0].TransactionsRequiredAtRuntime);
        Assert.Equal("standard", Assert.IsType<MerchantWorldProfile.TransactionPolicy>(catalog.Merchants[0].Transactions)
            .Stock.Single().VariantId);
        Assert.True(catalog.Merchants[0].FoundationRequiredAtRuntime);
        var merchantFoundation = Assert.IsType<MerchantFoundationProfile>(catalog.Merchants[0].Foundation);
        Assert.Equal("world-store", merchantFoundation.Slug);
        Assert.Equal("merchant-world-nominal", merchantFoundation.Wizard.TestFixtureId);
        Assert.Equal(MerchantSupplyPolicy.Infinite, merchantFoundation.Stock.Single().Supply);
        Assert.Equal(2, merchantFoundation.Pricing.BuyCurve.Count);
        Assert.Equal("scripts/merchant_rule", merchantFoundation.Restrictions.CustomRuleBinding);
        Assert.True(catalog.Merchants[0].ExperienceRequiredAtRuntime);
        var merchantExperience = Assert.IsType<MerchantStoreExperienceProfile>(catalog.Merchants[0].Experience);
        Assert.Equal("1.0.0", merchantExperience.Sessions.ForgeScriptApiVersion);
        Assert.Equal("StoreSessionOpened", merchantExperience.Sessions.OpenEventBinding);
        Assert.Equal("StoreSessionClosed", merchantExperience.Sessions.CloseEventBinding);
        Assert.True(merchantExperience.Sessions.IncludeTransactionSummary);
        Assert.True(merchantExperience.Ui.KeyboardNavigation);
        Assert.True(merchantExperience.Ui.GamepadNavigation);
        Assert.True(merchantExperience.Ui.ScreenReaderLabels);
        Assert.Equal(["name", "compare", "price", "quantity"], merchantExperience.Ui.Columns);
        Assert.Single(catalog.Companions);
        var companionValidation = Assert.IsType<CompanionWorldProfile.ValidationPolicy>(catalog.Companions[0].Validation);
        Assert.Equal("test:r037", companionValidation.Provenance);
        Assert.Equal(["move", "follow", "hold", "rally", "guard", "interact", "attack", "defend",
            "focus-fire", "protect", "disengage", "use-ability"], companionValidation.CommandIds);
        Assert.True(catalog.Companions[0].ValidationRequiredAtRuntime);
        var companionFoundation = Assert.IsType<CompanionWorldProfile.FoundationProfile>(
            catalog.Companions[0].Foundation);
        Assert.True(catalog.Companions[0].FoundationRequiredAtRuntime);
        Assert.Equal("not-recruited", companionFoundation.PreRecruitment.ContextId);
        Assert.Equal("recruited-by-speaker", companionFoundation.OwnerDialogue.ContextId);
        Assert.True(companionFoundation.Membership.Atomic);
        Assert.Equal(3, companionFoundation.Party.CompanionSlots);
        Assert.Equal(8, companionFoundation.Party.ServerActorLimit);
        Assert.Equal(2, companionFoundation.Party.RoleLimits["Support"]);
        var companionControl = Assert.IsType<CompanionWorldProfile.ControlProfile>(catalog.Companions[0].Control);
        Assert.True(catalog.Companions[0].ControlRequiredAtRuntime);
        Assert.Equal("individual", companionControl.Ownership.Kind);
        Assert.Equal(6, companionControl.Movement.CommandKinds.Count);
        Assert.Equal(6, companionControl.Tactical.CommandKinds.Count);
        Assert.Equal("Support", companionControl.Ai.Role);
        Assert.Equal("owner-or-gm", companionControl.Ai.AuthorizationLevel);
        Assert.Equal("full", companionControl.Equipment.Permission);
        Assert.Single(companionControl.Equipment.LockedItemIds);
        Assert.Single(companionControl.Equipment.SuggestedLoadout);
        var companionExperience = Assert.IsType<CompanionWorldProfile.ExperienceProfile>(
            catalog.Companions[0].Experience);
        Assert.True(catalog.Companions[0].ExperienceRequiredAtRuntime);
        Assert.Equal("recoverable-corpse", companionExperience.Death.Mode);
        Assert.Equal([10d, 30d, 60d],
            [companionExperience.Cohesion.NavigateThreshold, companionExperience.Cohesion.PortalThreshold,
             companionExperience.Cohesion.TeleportThreshold]);
        Assert.True(companionExperience.Hud.ScreenReaderLabels);
        Assert.Equal(["health", "focus"], companionExperience.Hud.VisibleResourceIds);
        Assert.Single(catalog.Scripts.Camera.Shots);
        Assert.Single(catalog.Scripts.TagRouting.Routes);
        Assert.Single(catalog.Scripts.ModuleFlags);
        Assert.Single(catalog.Scripts.AiSettings);
    }

    [Fact]
    public void Previous_formats_without_catalog_migrate_to_empty_optional_fallback()
    {
        var aggregate = JsonNode.Parse(ProjectJsonSerializer.Serialize(CompleteProject()))!.AsObject();
        aggregate["formatVersion"] = ProjectJsonSerializer.PreWorldFeaturesFormatVersion;
        aggregate["project"]!["module"]!.AsObject().Remove("worldFeatures");
        var restored = ProjectJsonSerializer.Deserialize(aggregate.ToJsonString());
        Assert.Empty(restored.Module.WorldFeatures.Placeables);
        Assert.Empty(restored.Module.WorldFeatures.Merchants);
        Assert.Empty(restored.Module.WorldFeatures.Scripts.ModuleFlags);

        var created = ProjectSession.Create(_temporaryRoot, CompleteProject());
        var path = created.Session!.ProjectFilePath;
        var workspace = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        workspace["formatVersion"] = "1.25.0";
        workspace["project"]!["module"]!.AsObject().Remove("worldFeatures");
        File.WriteAllText(path, workspace.ToJsonString());
        var reopened = ProjectSession.Open(_temporaryRoot);
        Assert.True(reopened.Succeeded);
        Assert.Empty(reopened.Session!.Project.Module.WorldFeatures.Companions);
        Assert.True(reopened.Session.Save(reopened.Session.Project).Succeeded);
        Assert.Contains("\"formatVersion\": \"1.28.0\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void Placeable_event_extension_fields_default_when_absent_from_existing_json()
    {
        var document = JsonNode.Parse(ProjectJsonSerializer.Serialize(CompleteProject()))!.AsObject();
        var placeable = document["project"]!["module"]!["worldFeatures"]!["placeables"]!
            .AsArray()[0]!.AsObject();
        placeable.Remove("contentsChangedRequiredAtRuntime");
        placeable.Remove("damageEvents");
        placeable.Remove("damageEventsRequiredAtRuntime");

        var restored = ProjectJsonSerializer.Deserialize(document.ToJsonString());
        var profile = restored.Module.WorldFeatures.Placeables.Single();

        Assert.False(profile.ContentsChangedRequiredAtRuntime);
        Assert.Null(profile.DamageEvents);
        Assert.False(profile.DamageEventsRequiredAtRuntime);
    }

    [Fact]
    public void Placeable_direct_damage_events_round_trip_without_composition_profile()
    {
        var source = CompleteProject();
        var original = source.Module.WorldFeatures.Placeables.Single();
        var directDamage = original.CompositionEvents!.DamageEvents with
        {
            DamagedBinding = "DirectDamageApplied",
            DestroyedBinding = "DirectEntityDestroyed",
        };
        var direct = original with
        {
            CompositionEvents = null,
            CompositionEventsRequiredAtRuntime = false,
            DamageEvents = directDamage,
            DamageEventsRequiredAtRuntime = true,
        };

        var restored = ProjectJsonSerializer.Deserialize(
            ProjectJsonSerializer.Serialize(WithPlaceable(source, direct)));
        var profile = restored.Module.WorldFeatures.Placeables.Single();
        var canonical = PlaceableDamageEventsResolver.Resolve(profile);

        Assert.Equal(directDamage, profile.DamageEvents);
        Assert.Null(profile.CompositionEvents);
        Assert.True(profile.DamageEventsRequiredAtRuntime);
        Assert.Equal(PlaceableDamageEventsAuthority.Direct, canonical.Authority);
    }

    [Fact]
    public void Placeable_composition_damage_events_remain_the_historical_canonical_fallback()
    {
        var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(CompleteProject()));
        var profile = restored.Module.WorldFeatures.Placeables.Single();
        var canonical = PlaceableDamageEventsResolver.Resolve(profile);

        Assert.Null(profile.DamageEvents);
        Assert.NotNull(profile.CompositionEvents?.DamageEvents);
        Assert.True(canonical.Available);
        Assert.Equal(profile.CompositionEvents!.DamageEvents, canonical.Profile);
        Assert.Equal(PlaceableDamageEventsAuthority.Composition, canonical.Authority);
    }

    [Fact]
    public void Placeable_matching_dual_damage_events_round_trip_as_one_canonical_authority()
    {
        var source = CompleteProject();
        var original = source.Module.WorldFeatures.Placeables.Single();
        var dual = original with
        {
            DamageEvents = original.CompositionEvents!.DamageEvents,
            DamageEventsRequiredAtRuntime = true,
        };

        var restored = ProjectJsonSerializer.Deserialize(
            ProjectJsonSerializer.Serialize(WithPlaceable(source, dual)));
        var profile = restored.Module.WorldFeatures.Placeables.Single();
        var canonical = PlaceableDamageEventsResolver.Resolve(profile);

        Assert.Equal(profile.DamageEvents, profile.CompositionEvents!.DamageEvents);
        Assert.True(canonical.Available);
        Assert.Equal(PlaceableDamageEventsAuthority.DualCanonical, canonical.Authority);
        Assert.Null(canonical.DiagnosticCode);
    }

    [Fact]
    public void R074Companion_foundation_validation_is_localized_and_optional_required_semantics_are_explicit()
    {
        var project = CompleteProject();
        var companion = project.Module.WorldFeatures.Companions.Single();
        var foundation = companion.Foundation!;
        var invalid = companion with
        {
            Foundation = foundation with
            {
                Recruitable = foundation.Recruitable with { Enabled = false, Cost = -1 },
                PreRecruitment = foundation.PreRecruitment with { DialogueId = Guid.Empty, ContextId = "wrong" },
                OwnerDialogue = foundation.OwnerDialogue with { DialogueId = Guid.Empty, CommandIds = [] },
                Membership = foundation.Membership with { Atomic = false, TransferInventory = false },
                Party = foundation.Party with
                {
                    CompanionSlots = 0,
                    ServerActorLimit = 0,
                    RoleLimits = new Dictionary<string, int>(),
                },
            },
        };
        var invalidCatalog = new WorldFeatureCatalog(project.Module.WorldFeatures.Id, 1, "test:r074",
            project.Module.WorldFeatures.Placeables, project.Module.WorldFeatures.Doors,
            project.Module.WorldFeatures.Triggers, project.Module.WorldFeatures.Encounters,
            project.Module.WorldFeatures.VehicleRoutes, project.Module.WorldFeatures.Merchants, [invalid],
            project.Module.WorldFeatures.Scripts);
        var invalidProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(invalidCatalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var diagnostics = WorldFeatureCatalogValidator.Validate(invalidProject);

        Assert.Contains(diagnostics, item => item.Code == "world.companion.recruitable_invalid" &&
                                             item.Path.EndsWith("/foundation/recruitable", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "world.companion.pre_recruitment_dialogue_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.companion.owner_dialogue_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.companion.membership_transaction_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.companion.party_policy_invalid");

        var optional = companion with { Foundation = null, FoundationRequiredAtRuntime = false };
        var required = companion with { Foundation = null, FoundationRequiredAtRuntime = true };
        Assert.DoesNotContain(WorldFeatureCatalogValidator.Validate(WithCompanion(project, optional)),
            item => item.Code == "world.companion.foundation_required");
        Assert.Contains(WorldFeatureCatalogValidator.Validate(WithCompanion(project, required)),
            item => item.Code == "world.companion.foundation_required");
    }

    [Fact]
    public void R075Companion_control_validation_is_localized_and_optional_required_semantics_are_explicit()
    {
        var project = CompleteProject();
        var companion = project.Module.WorldFeatures.Companions.Single();
        var control = companion.Control!;
        var invalid = companion with
        {
            Control = control with
            {
                Ownership = control.Ownership with { Kind = "invalid", ControllerIds = [], Revision = -1 },
                Movement = control.Movement with { CommandKinds = ["move"], MaximumPriority = -1 },
                Tactical = control.Tactical with { CommandKinds = ["attack"], Stances = [], AbilityIds = [] },
                Ai = control.Ai with { Autonomy = 2, PlanId = Guid.Empty, SemanticDiff = "none" },
                Equipment = control.Equipment with
                {
                    Permission = "invalid",
                    SuggestedLoadout = new Dictionary<string, Guid>(),
                    AllowCompare = false,
                },
            },
        };
        var diagnostics = WorldFeatureCatalogValidator.Validate(WithCompanion(project, invalid));

        Assert.Contains(diagnostics, item => item.Code == "world.companion.ownership_invalid" &&
                                             item.Path.EndsWith("/control/ownership", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "world.companion.movement_commands_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.companion.tactical_commands_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.companion.ai_control_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.companion.equipment_control_invalid");

        var optional = companion with { Control = null, ControlRequiredAtRuntime = false };
        var required = companion with { Control = null, ControlRequiredAtRuntime = true };
        Assert.DoesNotContain(WorldFeatureCatalogValidator.Validate(WithCompanion(project, optional)),
            item => item.Code == "world.companion.control_required");
        Assert.Contains(WorldFeatureCatalogValidator.Validate(WithCompanion(project, required)),
            item => item.Code == "world.companion.control_required");
    }

    [Fact]
    public void R076Companion_experience_validation_is_localized_and_optional_required_semantics_are_explicit()
    {
        var project = CompleteProject();
        var companion = project.Module.WorldFeatures.Companions.Single();
        var experience = companion.Experience!;
        var invalid = companion with
        {
            Experience = experience with
            {
                Death = experience.Death with { Mode = "resurrection", ResurrectionResourceId = null },
                Cohesion = experience.Cohesion with
                {
                    NavigateThreshold = 30,
                    PortalThreshold = 20,
                    TeleportThreshold = 10,
                    SafeAnchorTags = [],
                },
                Hud = experience.Hud with
                {
                    Keyboard = false,
                    Gamepad = false,
                    ScreenReaderLabels = false,
                    VisibleResourceIds = [],
                },
            },
        };
        var diagnostics = WorldFeatureCatalogValidator.Validate(WithCompanion(project, invalid));

        Assert.Contains(diagnostics, item => item.Code == "world.companion.death_experience_invalid" &&
                                             item.Path.EndsWith("/experience/death", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "world.companion.cohesion_experience_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.companion.party_hud_inaccessible");

        var optional = companion with { Experience = null, ExperienceRequiredAtRuntime = false };
        var required = companion with { Experience = null, ExperienceRequiredAtRuntime = true };
        Assert.DoesNotContain(WorldFeatureCatalogValidator.Validate(WithCompanion(project, optional)),
            item => item.Code == "world.companion.experience_required");
        Assert.Contains(WorldFeatureCatalogValidator.Validate(WithCompanion(project, required)),
            item => item.Code == "world.companion.experience_required");
    }

    [Fact]
    public void Aggregate_round_trip_preserves_trap_only_security_profile_and_requirement()
    {
        var source = CompleteProject();
        var catalog = source.Module.WorldFeatures;
        var original = catalog.Placeables.Single();
        var trapOnly = original with
        {
            SecurityEvents = original.SecurityEvents! with
            {
                Loot = null,
                ContainerEvents = null,
                LockEvents = null,
                ContentsChanged = null,
            },
            SecurityEventsRequiredAtRuntime = false,
            TrapRequiredAtRuntime = true,
            LootRequiredAtRuntime = false,
            ContainerEventsRequiredAtRuntime = false,
            LockEventsRequiredAtRuntime = false,
        };
        var partialCatalog = new WorldFeatureCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            catalog.Provenance,
            [trapOnly],
            catalog.Doors,
            catalog.Triggers,
            catalog.Encounters,
            catalog.VehicleRoutes,
            catalog.Merchants,
            catalog.Companions,
            catalog.Scripts);
        var project = new ProjectDefinition(
            source.Id,
            source.Title,
            source.Module.WithWorldFeatures(partialCatalog),
            source.Blueprints,
            source.Areas,
            source.Dialogues,
            source.Quests);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var profile = restored.Module.WorldFeatures.Placeables.Single();

        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.True(profile.TrapRequiredAtRuntime);
        Assert.False(profile.SecurityEventsRequiredAtRuntime);
        Assert.NotNull(profile.SecurityEvents?.Trap);
        Assert.Null(profile.SecurityEvents?.Loot);
        Assert.Null(profile.SecurityEvents?.ContainerEvents);
        Assert.Null(profile.SecurityEvents?.LockEvents);
        Assert.Null(profile.SecurityEvents?.ContentsChanged);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot)) Directory.Delete(_temporaryRoot, true);
    }

    private static ProjectDefinition WithCompanion(ProjectDefinition project, CompanionWorldProfile companion)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, source.Triggers, source.Encounters, source.VehicleRoutes,
            source.Merchants, [companion], source.Scripts);
        return new(project.Id, project.Title, project.Module.WithWorldFeatures(catalog), project.Blueprints,
            project.Areas, project.Dialogues, project.Quests);
    }

    private static ProjectDefinition WithPlaceable(ProjectDefinition project, PlaceableWorldProfile placeable)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            [placeable], source.Doors, source.Triggers, source.Encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        return new(project.Id, project.Title, project.Module.WithWorldFeatures(catalog), project.Blueprints,
            project.Areas, project.Dialogues, project.Quests);
    }

    private static ProjectDefinition CompleteProject()
    {
        var source = TestProjectFactory.CreateValid();
        var placeable = new BlueprintDefinition(BlueprintId.From(Guid.Parse("15000000-0000-4000-8000-000000000001")),
            "world-placeable", "World placeable", BlueprintKind.Placeable);
        var door = new BlueprintDefinition(BlueprintId.From(Guid.Parse("15000000-0000-4000-8000-000000000002")),
            "world-door", "World door", BlueprintKind.Door);
        var trigger = new BlueprintDefinition(BlueprintId.From(Guid.Parse("15000000-0000-4000-8000-000000000003")),
            "world-trigger", "World trigger", BlueprintKind.Trigger);
        var item = new BlueprintDefinition(BlueprintId.From(Guid.Parse("15000000-0000-4000-8000-000000000008")),
            "world-item", "World item", BlueprintKind.Item);
        var creature = source.Blueprints.Single();
        var area = source.Areas.Single();
        var dialogue = source.Dialogues.Single();
        var quest = source.Quests.Single();
        var routeId = Guid.Parse("15000000-0000-4000-8000-000000000004");
        var routePointA = Guid.Parse("15000000-0000-4000-8000-000000000005");
        var routePointB = Guid.Parse("15000000-0000-4000-8000-000000000006");
        var scripts = new ScriptAutomationProfile(source.Module.Id.Value, 1, "test:g13-aa-ab",
            new([new(Guid.Parse("15000000-0000-4000-8000-000000000007"), "rig", "shot", 2, .25, true, true)],
                "restore", true, true),
            new(true, true, [new("door", "open", "scripts/door_open", true)]),
            [new("rest.enabled", ScriptAutomationProfile.SettingKind.Boolean, "true",
                ScriptAutomationProfile.FlagScope.Module, "Safe rest switch.", 1)],
            [new("combat.style", ScriptAutomationProfile.SettingKind.Choice, "balanced", ["balanced", "bold"],
                "Balanced combat style.", true)]);
        var catalog = new WorldFeatureCatalog(source.Module.Id.Value, 1, "test:g13-n-ab",
            [new(Guid.Parse("15100000-0000-4000-8000-000000000001"), 1, "test:g13-n-o", placeable.Id,
                "ash", dialogue.Id.Value,
                new("key", PlaceableWorldProfile.CredentialConsumption.Consume, 1, null, true),
                new([], new(false, 0, 0, true, "placeable")),
                new(PlaceableWorldProfile.BodyMode.Rigid, 4, [], true, 2, true, "server"),
                new(Guid.Parse("15200000-0000-4000-8000-000000000040"), 1, "test:r046", "world-placeable",
                    [new("fr-FR", "Placeable", "Examiner")], ["world", "test"], "Test author notes",
                    new("assets/placeable.glb", ["mesh:placeable"], [], "assets/icon.png", "assets/portrait.png", []),
                    new("box", new(1, 1, 1), true, true, "world"),
                    new([new(Guid.Parse("15200000-0000-4000-8000-000000000041"), "placeable.examine",
                            "placeable-used", "true")], 2.5, 120, true, "placeable.examine", 1, "server", ["true"]),
                    null, new(true, true, true, true, true,
                        new Dictionary<string, string> { ["onUsed"] = "scripts/placeable_used" }),
                    new(new(1.25f, 0, -2.5f), new(0, 45, 0), Vector3Value.One, new(0, .5f, 0), .25,
                        null, true, "animation:rise"),
                    new Dictionary<string, string> { ["variant"] = "lit" },
                    new(Guid.Parse("15200000-0000-4000-8000-000000000042"), 1, "test:r047", "1.0.0",
                        "InteractionExecuted", "scripts/before", "scripts/after"),
                    new(["animation:rise"], ["lock:test"], ["trap:test"], ["inventory:test"],
                        ["physics:test"], 4, 4), true), true,
                new(Guid.Parse("15200000-0000-4000-8000-000000000043"), 1, "test:r093",
                    new(Guid.Parse("15200000-0000-4000-8000-000000000044"), 24, 160, "party",
                        ["container.open", "container.withdraw"], true, "loot:test", "ui:chest", true,
                        "inventory:test"),
                    new(Guid.Parse("15200000-0000-4000-8000-000000000045"), "closed",
                        [
                            new(Guid.Parse("15200000-0000-4000-8000-000000000046"), "idle", "animation:idle", "visual:idle", true),
                            new(Guid.Parse("15200000-0000-4000-8000-000000000047"), "open", "animation:open", "visual:open", true),
                            new(Guid.Parse("15200000-0000-4000-8000-000000000048"), "closed", "animation:closed", "visual:closed", true),
                            new(Guid.Parse("15200000-0000-4000-8000-000000000049"), "broken", "animation:broken", "visual:broken", false),
                            new(Guid.Parse("15200000-0000-4000-8000-00000000004a"), "active", "animation:active", "visual:active", true),
                            new(Guid.Parse("15200000-0000-4000-8000-00000000004b"), "custom", "animation:custom", "visual:custom", true),
                        ],
                        [new(Guid.Parse("15200000-0000-4000-8000-00000000004c"), "closed", "open", "open", 1, "true")]),
                    new(Guid.Parse("15200000-0000-4000-8000-00000000004d"), 100, 100, 10,
                        new Dictionary<string, double> { ["force"] = .5 }, [.75, .5, .25], "vfx:shatter",
                        "debris:test", true, 20),
                    new(Guid.Parse("15200000-0000-4000-8000-00000000004e"), ["destroy", "move"],
                        ["placeable.plot.override"], "Protected", true, "PlaceableProtectionOverride"),
                    new(Guid.Parse("15200000-0000-4000-8000-00000000004f"), PlaceableAuthoredLockState.Locked,
                        ["key:test"], "mechanics", 12, "sha256:test", true, true, true, ["lock.admin"])), true,
                new(Guid.Parse("15200000-0000-4000-8000-000000000050"), 1, "test:r094",
                    new(Guid.Parse("15200000-0000-4000-8000-000000000051"), "container:open",
                        "damage:ember", 10, 12, 30, "ash", "vfx:ember", "ash", true, true),
                    new(Guid.Parse("15200000-0000-4000-8000-000000000052"),
                        Guid.Parse("15200000-0000-4000-8000-000000000053"),
                        PlaceableAuthoredLootSpawnTiming.Open, 42, true, [], 0,
                        new(Guid.Parse("15200000-0000-4000-8000-000000000053"), 1, "test:r094",
                            [new(item.Id.Value, "relic", 1, 1, 2, 1, "true", true)])),
                    new(Guid.Parse("15200000-0000-4000-8000-000000000054"), "1.0.0",
                        "ContainerOpened", "ContainerClosed", true, true, true, true),
                    new(Guid.Parse("15200000-0000-4000-8000-000000000055"), "1.0.0",
                        "LockStateChanging", "LockStateChanged", true, true, true, true),
                    new(Guid.Parse("15200000-0000-4000-8000-000000000056"), "1.0.0",
                        "ContainerContentsChanged", true, true, true, true)), true,
                FactionOwnership: new(
                    Guid.Parse("15200000-0000-4000-8000-000000000061"), 1, "test:ux1-n",
                    Guid.Parse("15200000-0000-4000-8000-000000000060"),
                    [Guid.Parse("15200000-0000-4000-8000-000000000060")],
                    true, true, true, true),
                FactionOwnershipRequiredAtRuntime: true,
                Conversation: new(
                    Guid.Parse("15200000-0000-4000-8000-000000000062"), 1, "test:ux1-o",
                    [
                        new(Guid.Parse("15200000-0000-4000-8000-000000000063"), dialogue.Id.Value,
                            PlaceableConversationTrigger.Interaction, "examine", null,
                            dialogue.EntryPoints[0].Key, 20, true),
                        new(Guid.Parse("15200000-0000-4000-8000-000000000064"), dialogue.Id.Value,
                            PlaceableConversationTrigger.Item, string.Empty, item.Id,
                            dialogue.EntryPoints[0].Key, 10, false),
                    ]),
                ConversationRequiredAtRuntime: true,
                CredentialRequiredAtRuntime: true,
                TrapRequiredAtRuntime: true,
                LootRequiredAtRuntime: true,
                ContainerEventsRequiredAtRuntime: true,
                LockEventsRequiredAtRuntime: true)],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000002"), 1, "test:g13-p-q", door.Id,
                new(10, 1, false, "damaged", "breached", true, "debris", "repair"),
                new(string.Empty, "loading.title", "loading.tip", true, 0), [], dialogue.Id.Value, "ash",
                new(DoorWorldProfile.AssemblyKind.Double,
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000001"), "left", -1, 0, false)],
                    "open", false, "close"),
                new(DoorWorldProfile.PlacementMode.FreePlacement, null, null, "closed",
                    [
                        new("open", DoorWorldProfile.StateKind.Open, "animation:open", false, true),
                        new("closed", DoorWorldProfile.StateKind.Closed, "animation:closed", true, true),
                        new("opening", DoorWorldProfile.StateKind.Opening, "animation:opening", false, false),
                        new("closing", DoorWorldProfile.StateKind.Closing, "animation:closing", true, false),
                        new("locked", DoorWorldProfile.StateKind.Locked, "animation:locked", true, true),
                        new("jammed", DoorWorldProfile.StateKind.Jammed, "animation:jammed", true, true),
                        new("broken", DoorWorldProfile.StateKind.Broken, "animation:broken", false, false),
                        new("disabled", DoorWorldProfile.StateKind.Disabled, "animation:disabled", true, false),
                        new("custom", DoorWorldProfile.StateKind.Custom, "animation:custom", true, true),
                    ],
                    [new("closed", "opening", "interact", 1, null)],
                    new(2.5, "both", 1, 2, 1, "server", 2, "door.unavailable"),
                    new(DoorWorldProfile.DestinationKind.Waypoint,
                        Guid.Parse("15200000-0000-4000-8000-000000000010"), "anchor:exit", 180, true,
                        ["portal.travel"]),
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000011"),
                        Guid.Parse("15200000-0000-4000-8000-000000000012"), false, "Retour")],
                    new Dictionary<string, string>(),
                    new("nav-obstacle:test-door", "nav-link:test-door", ["closed", "locked", "jammed", "disabled", "custom"],
                        ["open", "broken"], .75, .25, true)),
                new(Guid.Parse("15300000-0000-4000-8000-000000000001"), 1, "test:r081",
                    "assets/doors/test.glb", TransformValue.Identity,
                    new(Guid.Parse("15300000-0000-4000-8000-000000000002"), DoorAccessLockState.Locked,
                        ["key:ember"], "mechanics", 12, null, true, true, true, true, ["door.unlock"]),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000003"), "1.0.0", "DoorOpenRejected",
                        [new("fr-FR", "Verrouillée."), new("en-US", "Locked.")], "audio:locked",
                        "animation:rattle", "scripts/rejected"),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000004"), "door:open", "damage:ember",
                        10, 12, 30, "ash-watch", "vfx:ember", "ash-watch", true, false, ["front"], ["interact"]),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000005"), "1.0.0",
                        "PortalUseRequested", "PortalUseCompleted"),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000006"), "1.0.0",
                        "DoorOpened", "DoorClosed"), new Dictionary<string, string>(),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000007"), 1, "test:r082", "1.0.0",
                        "DoorLocked", "DoorUnlocked"), true), true)],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000003"), 1, "test:g13-r-s", trigger.Id, area.Id,
                null, new("inspect", "cursor", "outline", "prompt", "script", true, "use"), null, "trigger",
                new("key", false, "denied"), new(true, "danger", false, true, "allowed", "denied"),
                new("tracking", 1, "trace", 0, 0, null, quest.Id.Value),
                new([creature.Id], 1, true, 0, "server", dialogue.Id.Value), new(true, true, true, true, true),
                new(Guid.Parse("15200000-0000-4000-8000-000000000050"), 1, "test:r048", "world-trigger",
                    new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                        [new(0, 0, 0), new(4, 0, 0), new(4, 0, 4), new(0, 0, 4)], [],
                        new(4, 3, 4), 3, .25, true), new(["creature"], ["player"], [], ["characters"]),
                    ["area.ready"], 5, TriggerWorldProfile.FirePolicy.OncePerEntity,
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000051"), "transition", "area:next",
                        new Dictionary<string, string>())],
                    new(TriggerWorldProfile.DestinationKind.Waypoint,
                        Guid.Parse("15200000-0000-4000-8000-000000000052"), "anchor:exit", new(0, 180, 0),
                        true, ["portal.travel"]), new(new(1.25f, 0, -2.5f), Vector3Value.Zero, Vector3Value.One, null),
                    new Dictionary<string, string> { ["variant"] = "exit" },
                    new(Guid.Parse("15200000-0000-4000-8000-000000000053"), 1, "test:r048", "1.0.0",
                        "VolumeEntered", null, "scripts/enter"),
                    new(Guid.Parse("15200000-0000-4000-8000-000000000054"), 1, "test:r048", "1.0.0",
                        "VolumeExited", null, "scripts/exit"),
                    new([Guid.Parse("15200000-0000-4000-8000-000000000052")], ["area:next"],
                        ["scripts/enter", "scripts/exit", "script"], ["characters"]), true), true)],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000004"), 1, "test:g13-t-u", area.Id,
                new(1, 1, "true", true, true, 1), "ash", new(true, 1, 1, "encounter"),
                new(1, "ash", routeId, 1, 0, "false"),
                new(EncounterWorldProfile.VisibilityMode.Hidden, 0, false, string.Empty), new(1, 1, 1, true, true),
                new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                        [new(0, 0, 0), new(4, 0, 0), new(4, 0, 4), new(0, 0, 4)], Vector3Value.Zero,
                        4, 4, ["player"], ["party"], true, 12, "scripts/encounter", true),
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000020"), creature.Id,
                        EncounterWorldProfile.CandidateRole.Controller, 3, 8, ["true"], ["ember"], 1)],
                    12, ["anchor:test"], ["reward:test"], 4))],
            [new(routeId, 1, "test:g13-v", area.Id,
                [new(routePointA, Vector3Value.Zero, 0, 2), new(routePointB, new(5, 0, 0), 0, 2)],
                [new("lane", 0, 5, "forward")], [new(Guid.Parse("15200000-0000-4000-8000-000000000002"), routePointB, 1, "stop")],
                5, false, "server")],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000005"), 1, "test:g13-w-x", creature.Id,
                [new("gold", "currency.gold", 1, true, false)], [new("gold", 100, 0, 0, 10, 200)],
                new(50, 100, new Dictionary<string, decimal>(), -1, true),
                new(true, [], false, false, 0, "crime"), new(24, "day", routeId, true, true),
                new("bargain", 1, .1, .1, 0, true), null,
                [new(Guid.Parse("15200000-0000-4000-8000-000000000003"), "repair", "repair", "gold", 1, "repair", "true")],
                new(true, 30, 100, true, true, true),
                new(Guid.Parse("15200000-0000-4000-8000-000000000030"), 1, "test:r045",
                    "interaction:trade", "choice:trade",
                    [new(Guid.Parse("15200000-0000-4000-8000-000000000031"), item.Id, "standard", 12, 5,
                        "gold", ["quest:open"])], ["fr-FR", "en-US"], ["quest:open"],
                    new Dictionary<string, string> { ["onOpen"] = "scripts/merchant_open" }, 100, 1000, true), true,
                new(Guid.Parse("15400000-0000-4000-8000-000000000001"), 1, "test:r091", "world-store",
                    "merchant.world.name", ["fr-FR", "en-US"],
                    [new(Guid.Parse("15400000-0000-4000-8000-000000000002"), creature.Id,
                        dialogue.Id.Value, "choice:trade", "interaction:trade", "npc")],
                    new(Guid.Parse("15400000-0000-4000-8000-000000000003"), 1, "test:r091", true, true,
                        true, true, true, "merchant-world-nominal"),
                    [new(Guid.Parse("15400000-0000-4000-8000-000000000004"), item.Id, "standard",
                        MerchantSupplyPolicy.Infinite, 12, "supplier:world", 24, 1, ["quest:closed"], 5, "gold")],
                    new(Guid.Parse("15400000-0000-4000-8000-000000000005"), 1, "test:r091", 1.2m, .6m,
                        [new(0, 1), new(100, 1.1m)], [new(0, 1), new(100, .9m)],
                        new Dictionary<string, decimal> { ["gold"] = 1 }, .1m, .2m, .2m, .1m, 1, 500),
                    new(Guid.Parse("15400000-0000-4000-8000-000000000006"), 1, "test:r091", ["relic"],
                        ["merchant-approved"], ["stolen"], 1, 5, ["author:forge"], .5, null,
                        "scripts/merchant_rule")), true,
                new(Guid.Parse("15400000-0000-4000-8000-000000000007"), 1, "test:r092",
                    new(Guid.Parse("15400000-0000-4000-8000-000000000008"), 1, "test:r092", "1.0.0",
                        "StoreSessionOpened", "StoreSessionClosed", true, true, true, true),
                    new(Guid.Parse("15400000-0000-4000-8000-000000000009"), 1, "test:r092", true, true,
                        true, true, true, true, true, true,
                        ["name", "compare", "price", "quantity"])), true)],
            [new(Guid.Parse("15100000-0000-4000-8000-000000000006"), 1, "test:g13-y-z", creature.Id,
                new(dialogue.Id.Value, "full", "limited", "greeting", "full", "deny"),
                new("line", 0, 1, true, true, true),
                new(CompanionWorldProfile.ProgressionMode.Automatic, 1, 20, true, true, [5]),
                new(0, 0, 0, 0, [], []),
                new([creature.Id], 1, 0, [], true, dialogue.Id.Value),
                new(quest.Id.Value, "true", "departed", 0, "none"),
                new(true, true, 1, "sight", "traps", false),
                new(["heal"], [], .5, "ally", true),
                new(true, true, true, true, "owner"), new(true, true, "owner", true, 60),
                new(Guid.Parse("15300000-0000-4000-8000-000000000001"), 1, "test:r037",
                    new(true, true, "scripts/recruit", "gold", 5, 0),
                    new("party", "server", true),
                    ["move", "follow", "hold", "rally", "guard", "interact", "attack", "defend",
                        "focus-fire", "protect", "disengage", "use-ability"],
                    new("companion.inventory", "companion.standard", ["main-hand", "armor"]),
                    new("companion.recoverable", "recoverable-corpse", 30),
                    [new("recruit", dialogue.Id.Value, "dialogue.recruit"),
                     new("owner", dialogue.Id.Value, "dialogue.owner")],
                    new(false, [area.Id], true, true, "party.safe", 15)), true,
                new(Guid.Parse("15300000-0000-4000-8000-000000000010"), 1, "test:r074",
                    new(Guid.Parse("15300000-0000-4000-8000-000000000011"), 1, "test:r074", true,
                        ["quest:open"], 5, "gold", 0, ["relation:increase"], true, true,
                        "scripts/recruit"),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000012"), 1, "test:r074",
                        dialogue.Id.Value, "not-recruited", ["quest:open"], "offer.join", "offer.refuse",
                        ["relation:increase"]),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000013"), 1, "test:r074",
                        dialogue.Id.Value, "recruited-by-speaker",
                        ["move", "follow", "hold", "rally", "guard", "interact", "attack", "defend",
                            "focus-fire", "protect", "disengage", "use-ability"], true, true, true,
                        "companion.depart"),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000014"), 1, "test:r074", true, true,
                        true, true, CompanionWorldProfile.CompanionPersistence.Campaign,
                        "companion.inventory", "party.safe", true),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000015"), 1, "test:r074", 3, 1, 1, 1,
                        8, new Dictionary<string, int> { ["Support"] = 2 })), true,
                new(Guid.Parse("15300000-0000-4000-8000-000000000020"), 1, "test:r075",
                    new(Guid.Parse("15300000-0000-4000-8000-000000000021"), 1, "test:r075", "individual",
                        ["player:owner"], true, ["party", "shared", "game-master"], 1),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000022"), 1, "test:r075",
                        ["move", "follow", "hold", "rally", "guard", "interact"], "line", "party-waypoints",
                        0, 100),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000023"), 1, "test:r075",
                        ["attack", "defend", "focus-fire", "protect", "disengage", "use-ability"],
                        ["passive", "defensive", "balanced", "aggressive"], ["heal"], 0, 100),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000024"), 1, "test:r075", .6, .4, .5,
                        false, "Support", 3, ["heal"],
                        Guid.Parse("15300000-0000-4000-8000-000000000025"),
                        Guid.Parse("15300000-0000-4000-8000-000000000026"),
                        "replace:/companion/control/ai|role:Support", "owner-or-gm"),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000027"), 1, "test:r075", "full",
                        [Guid.Parse("15300000-0000-4000-8000-000000000028")],
                        new Dictionary<string, Guid>
                        {
                            ["main-hand"] = Guid.Parse("15300000-0000-4000-8000-000000000029"),
                        }, true, "player:owner")), true,
                new(Guid.Parse("15300000-0000-4000-8000-000000000030"), 1, "test:r076",
                    new(Guid.Parse("15300000-0000-4000-8000-000000000031"), 1, "test:r076",
                        "recoverable-corpse", 30, "resource:ember", "creature:heir", "memorial:elyra", true),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000032"), 1, "test:r076",
                        10, 30, 60, true, true, ["party.safe"]),
                    new(Guid.Parse("15300000-0000-4000-8000-000000000033"), 1, "test:r076",
                        true, true, true, true, true, true, true, true, true, true,
                        ["health", "focus"])), true)], scripts);
        var factions = new FactionCatalog(
            [new(Guid.Parse("15200000-0000-4000-8000-000000000060"), 1, "test:ux1-n",
                "watchers", "Veilleurs", false, [], ["guard"])],
            [], [], []);
        return new(source.Id, source.Title, source.Module.WithFactions(factions).WithWorldFeatures(catalog),
            [.. source.Blueprints, placeable, door, trigger, item], source.Areas, source.Dialogues, source.Quests);
    }
}
