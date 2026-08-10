using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class WorldFeatureRuntimeResolverTests
{
    private static readonly BlueprintId PlaceableId = BlueprintId.From(Guid.Parse("11000000-0000-4000-8000-000000000001"));
    private static readonly BlueprintId DoorId = BlueprintId.From(Guid.Parse("11000000-0000-4000-8000-000000000002"));
    private static readonly BlueprintId TriggerId = BlueprintId.From(Guid.Parse("11000000-0000-4000-8000-000000000003"));
    private static readonly BlueprintId CreatureId = BlueprintId.From(Guid.Parse("11000000-0000-4000-8000-000000000004"));
    private static readonly AreaId AreaId = AreaId.From(Guid.Parse("11000000-0000-4000-8000-000000000005"));

    [Fact]
    public void G13N_PlaceableSocialAccess_ConsumesGovernedCredential()
    {
        var profile = Placeable() with
        {
            FactionId = "ash-watch",
            DialogueId = Guid.Parse("11000000-0000-4000-8000-000000000006"),
            Credential = new("key:ember", PlaceableWorldProfile.CredentialConsumption.Charge, 3, null, true),
        };
        var result = WorldFeatureRuntimeResolver.ResolvePlaceableAccess(profile, new HashSet<string> { "key:ember" },
            new HashSet<string>(), 3);
        Assert.True(result.Allowed);
        Assert.True(result.Consume);
        Assert.Equal(2, result.RemainingCharges);
        Assert.Contains("audited", result.Audit);
        Assert.Equal("ash-watch", profile.FactionId);
        Assert.NotNull(profile.DialogueId);
    }

    [Fact]
    public void G13O_PlaceableReactivePhysics_BudgetsEventsAndKeepsAuthority()
    {
        var profile = Placeable();
        Assert.True(WorldFeatureRuntimeResolver.ResolvePlaceableEvent(profile, "physical-attack-resolved", 0, 0).Dispatched);
        Assert.True(WorldFeatureRuntimeResolver.ResolvePlaceableEvent(profile, "ability-impacted", 0, 0).Dispatched);
        Assert.False(WorldFeatureRuntimeResolver.ResolvePlaceableEvent(profile, "heartbeat", 12, 10).Dispatched);
        Assert.Equal(PlaceableWorldProfile.BodyMode.Rigid, profile.Physics!.Mode);
        Assert.True(profile.Physics.Replicated);
        Assert.Equal("server", profile.Physics.Authority);
    }

    [Fact]
    public void R046_PlaceableFoundationResolvesSnappedTransformOverlayAndOptionalRequiredFallback()
    {
        var profile = Placeable() with { Foundation = PlaceableFoundation(), FoundationRequiredAtRuntime = true };
        var resolved = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile);
        Assert.True(resolved.Available);
        Assert.Equal(new Vector3Value(1.25f, 2.5f, -.25f), resolved.SnappedTransform!.Position);
        Assert.Contains("pivot=0,0.5,0", resolved.Overlay);
        Assert.Contains("snap=0.25", resolved.Overlay);

        var optional = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with
        { Foundation = null, FoundationRequiredAtRuntime = false });
        Assert.False(optional.Available); Assert.Empty(optional.DiagnosticCode);
        var required = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(profile with { Foundation = null });
        Assert.False(required.Available);
        Assert.Equal("WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING", required.DiagnosticCode);
    }

    [Fact]
    public void R047_OnUsedProducesTypedInteractionWithActorTargetResultAndGovernedScripts()
    {
        var foundation = PlaceableFoundation() with
        {
            OnUsed = new(Guid.Parse("12000000-0000-4000-8000-000000000012"), 1, "test:r047", "1.0.0",
                "InteractionExecuted", "scripts/before", "scripts/after"),
            OnUsedRequiredAtRuntime = true,
        };
        var profile = Placeable() with { Foundation = foundation, FoundationRequiredAtRuntime = true };
        var eventId = Guid.Parse("12000000-0000-4000-8000-000000000013");
        var executed = WorldFeatureRuntimeResolver.ExecutePlaceableUse(profile, eventId,
            foundation.Interaction!.Verbs[0].Id, "player:ilyne", new(1, 2, 3), "success", DateTimeOffset.UnixEpoch);

        Assert.Equal(eventId, executed.EventId);
        Assert.Equal("player:ilyne", executed.ActorId);
        Assert.Equal(new Vector3Value(1, 2, 3), executed.TargetPoint);
        Assert.Equal("success", executed.Result);
        Assert.Equal("InteractionExecuted", executed.EventBinding);
        Assert.Equal("1.0.0", executed.ForgeScriptApiVersion);
        Assert.Equal("scripts/before", executed.BeforeScriptId);
        Assert.Equal("scripts/after", executed.AfterScriptId);
        var error = Assert.Throws<InvalidOperationException>(() => WorldFeatureRuntimeResolver.ExecutePlaceableUse(
            profile with { Foundation = foundation with { OnUsed = null } }, eventId,
            foundation.Interaction.Verbs[0].Id, "player", Vector3Value.Zero, "success", DateTimeOffset.UnixEpoch));
        Assert.Equal("WORLD_PLACEABLE_ON_USED_REQUIRED_MISSING", error.Message);
    }

    [Fact]
    public void G13P_DoorDamageLoading_ProducesBreachAndNavigationUpdate()
    {
        var profile = Door();
        var result = WorldFeatureRuntimeResolver.ApplyDoorDamage(profile, 30, 40);
        Assert.True(result.Breached);
        Assert.True(result.NavigationChanged);
        Assert.Equal("door.breached", result.Visual);
        Assert.Equal("assets/loading/crypt.png", profile.Loading!.ImageAssetId);
        Assert.True(WorldFeatureRuntimeResolver.ResolveDoorEvent(profile, "physical-attack-resolved", 0, 0).Dispatched);
        Assert.Contains(profile.Events, item => item.Event == "damage-applied");
        Assert.Contains(profile.Events, item => item.Event == "entity-destroyed");
    }

    [Fact]
    public void G13Q_DoorReactiveComplex_ResolvesBindingsAndAssembly()
    {
        var profile = Door();
        Assert.True(WorldFeatureRuntimeResolver.ResolveDoorEvent(profile, "ability-impacted", 0, 0).Dispatched);
        Assert.False(WorldFeatureRuntimeResolver.ResolveDoorEvent(profile, "heartbeat", 60, 5).Dispatched);
        Assert.Contains(profile.Events, item => item.Event == "custom:ash:seal");
        Assert.NotNull(profile.DialogueId);
        Assert.Equal("ash-watch", profile.FactionId);
        Assert.Equal(DoorWorldProfile.AssemblyKind.Double, profile.Assembly!.Kind);
        Assert.Equal(2, profile.Assembly.Panels.Count);
    }

    [Fact]
    public void R040_DoorFoundationExecutesStateInteractionPortalAndBidirectionalLink()
    {
        var profile = Door();
        var accepted = WorldFeatureRuntimeResolver.InteractWithDoor(profile, "closed", "front",
            new HashSet<string> { "portal.travel" }, 0, new HashSet<string>());
        var denied = WorldFeatureRuntimeResolver.InteractWithDoor(profile, "closed", "front",
            new HashSet<string>(), 0, new HashSet<string>());

        Assert.True(accepted.Accepted);
        Assert.Equal("opening", accepted.CurrentState);
        Assert.False(accepted.CollisionEnabled);
        Assert.Equal(DoorWorldProfile.DestinationKind.Waypoint, accepted.Portal!.DestinationKind);
        Assert.Equal("permission-denied", denied.Reason);
        Assert.Equal(9, profile.Foundation!.States.Count);
        Assert.False(Assert.Single(profile.Foundation.Links).OneWay);
    }

    [Fact]
    public void R041_DoorNavigationSynchronizesObstacleLinkAnimationAndVisualization()
    {
        var profile = Door();
        var closed = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, "closed", 1);
        var openingEarly = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, "opening", .5);
        var openingLate = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, "opening", .8);
        var closingEarly = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, "closing", .2);
        var closingLate = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(profile, "closing", .3);

        Assert.True(closed.ObstacleEnabled);
        Assert.False(closed.LinkEnabled);
        Assert.True(openingEarly.ObstacleEnabled);
        Assert.True(openingLate.LinkEnabled);
        Assert.True(closingEarly.LinkEnabled);
        Assert.True(closingLate.ObstacleEnabled);
        Assert.Contains("blocked", closed.Visualization);
        Assert.Equal("DoorNavigationChanged", openingLate.EventName);
    }

    [Fact]
    public void G13R_TriggerDiscoveryAccess_RequiresDiscoveryCredentialAndPermission()
    {
        var profile = Trigger();
        var hidden = WorldFeatureRuntimeResolver.EvaluateTrigger(profile, 5, new HashSet<string>(),
            new HashSet<string>(), false, 1, 0, false);
        Assert.False(hidden.Fired);
        Assert.False(hidden.Revealed);
        var denied = WorldFeatureRuntimeResolver.EvaluateTrigger(profile, 20, new HashSet<string>(),
            new HashSet<string> { "trigger.use" }, false, 1, 0, false);
        Assert.Equal("credential-missing", denied.Reason);
        var allowed = WorldFeatureRuntimeResolver.EvaluateTrigger(profile, 20,
            new HashSet<string> { "key:ember" }, new HashSet<string> { "trigger.use" }, false, 1, 0, false);
        Assert.True(allowed.Fired);
        Assert.True(allowed.RestAllowed);
        Assert.Equal("cursor.secret", profile.Interaction!.Icon);
    }

    [Fact]
    public void G13S_TriggerAutomationNarrativeDirector_ProducesAuditedEffects()
    {
        var profile = Trigger();
        var result = WorldFeatureRuntimeResolver.EvaluateTrigger(profile, 20,
            new HashSet<string> { "key:ember" }, new HashSet<string> { "trigger.use" }, false, 2, 0, true);
        Assert.True(result.Fired);
        Assert.Equal("director", result.Reason);
        Assert.Contains(result.Effects, item => item.StartsWith("clue:", StringComparison.Ordinal));
        Assert.Contains(result.Effects, item => item.StartsWith("dialogue:", StringComparison.Ordinal));
        Assert.Contains("director:audit", result.Effects);
        Assert.Equal("trigger.ash", profile.CustomEventNamespace);
        Assert.True(profile.Scheduler!.AggregateOccupants);
    }

    [Fact]
    public void R048_TriggerFoundationSnapsGeometryAndEmitsTypedEnterExitEventsWithPortal()
    {
        var foundation = TriggerFoundation();
        var profile = Trigger() with { Foundation = foundation, FoundationRequiredAtRuntime = true };
        var resolved = WorldFeatureRuntimeResolver.ResolveTriggerFoundation(profile);
        Assert.True(resolved.Available);
        Assert.Equal(new Vector3Value(2.25f, 0, .25f), resolved.SnappedShape!.Vertices[1]);
        Assert.Equal(new Vector3Value(1.25f, 0, -2.5f), resolved.SnappedTransform!.Position);
        Assert.Contains("holes=1", resolved.Overlay);

        var state = new WorldFeatureRuntimeResolver.TriggerRuntimeState(
            new Dictionary<string, DateTimeOffset>(), new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(), false);
        var entered = WorldFeatureRuntimeResolver.EnterTrigger(profile, state, "player:ilyne", "creature",
            new HashSet<string> { "player" }, new HashSet<string> { "characters" }, new(1, 0, 2),
            new(0, 0, 1), new Dictionary<string, string> { ["crypt.open"] = "true" }, DateTimeOffset.UnixEpoch);
        Assert.True(entered.Fired);
        Assert.Equal("VolumeEntered", entered.Event!.EventBinding);
        Assert.Equal(new Vector3Value(1, 0, 2), entered.Event.Point);
        Assert.Contains("characters", entered.Event.Layers);
        Assert.Equal("start-encounter", entered.Actions.Single().Kind);
        Assert.Equal(TriggerWorldProfile.DestinationKind.Waypoint, foundation.Portal!.Destination);

        var exited = WorldFeatureRuntimeResolver.ExitTrigger(profile, entered.State, "player:ilyne",
            DateTimeOffset.UnixEpoch.AddSeconds(12), "walked-out");
        Assert.Equal(TimeSpan.FromSeconds(12), exited.Event!.DurationInside);
        Assert.Equal("walked-out", exited.Event.Reason);
        Assert.Equal("VolumeExited", exited.Event.EventBinding);

        var optional = WorldFeatureRuntimeResolver.ResolveTriggerFoundation(profile with
        { Foundation = null, FoundationRequiredAtRuntime = false });
        Assert.False(optional.Available); Assert.Empty(optional.DiagnosticCode);
        var required = WorldFeatureRuntimeResolver.ResolveTriggerFoundation(profile with { Foundation = null });
        Assert.Equal("WORLD_TRIGGER_FOUNDATION_REQUIRED_MISSING", required.DiagnosticCode);
    }

    [Fact]
    public void R049_TriggerValidationContextResolvesOrReportsOptionalAndRequiredFallback()
    {
        var foundation = TriggerFoundation() with
        {
            Validation = new([Guid.Parse("12000000-0000-4000-8000-000000000022")],
                ["encounter:crypt"], ["scripts/before-enter", "scripts/after-enter", "scripts/after-exit",
                    "scripts/trigger_clicked"], ["characters"]),
            ValidationRequiredAtRuntime = true,
        };
        var profile = Trigger() with { Foundation = foundation, FoundationRequiredAtRuntime = true };
        var resolved = WorldFeatureRuntimeResolver.ResolveTriggerValidation(profile);
        Assert.True(resolved.Available);
        Assert.Contains("targets=1", resolved.Summary);
        Assert.Contains("scripts=4", resolved.Summary);

        var optional = WorldFeatureRuntimeResolver.ResolveTriggerValidation(profile with
        { Foundation = foundation with { Validation = null, ValidationRequiredAtRuntime = false } });
        Assert.False(optional.Available); Assert.Empty(optional.DiagnosticCode);
        var required = WorldFeatureRuntimeResolver.ResolveTriggerValidation(profile with
        { Foundation = foundation with { Validation = null } });
        Assert.Equal("WORLD_TRIGGER_VALIDATION_REQUIRED_MISSING", required.DiagnosticCode);
    }

    [Fact]
    public void G13T_EncounterLifecycleFaction_RespawnsWithinBoundedCycle()
    {
        var profile = Encounter();
        var result = WorldFeatureRuntimeResolver.EvaluateEncounter(profile, 1, 0, false, false, 31);
        Assert.True(result.Spawn);
        Assert.Equal(2, result.NextCycle);
        Assert.Equal("ash-hostile", profile.SpawnFactionId);
        Assert.True(profile.Scheduler!.HeartbeatEnabled);
        Assert.Equal("encounter.ash", profile.Scheduler.CustomEventNamespace);
    }

    [Fact]
    public void G13U_EncounterTactical_ReinforcesOutOfViewAndCleansAbandonedActors()
    {
        var profile = Encounter();
        var visible = WorldFeatureRuntimeResolver.EvaluateEncounter(profile, 0, 3, true, false, 31);
        Assert.False(visible.Reinforce);
        Assert.Equal("visible", visible.Reason);
        var hidden = WorldFeatureRuntimeResolver.EvaluateEncounter(profile, 0, 3, false, false, 31);
        Assert.True(hidden.Reinforce);
        var cleanup = WorldFeatureRuntimeResolver.EvaluateEncounter(profile, 0, 0, false, true, 121);
        Assert.True(cleanup.Cleanup);
    }

    [Fact]
    public void R042_EncounterActivationFiltersAndSelectsBoundedCandidatePool()
    {
        var profile = Encounter();
        var accepted = WorldFeatureRuntimeResolver.ActivateEncounter(profile, new HashSet<string> { "player" },
            "party", true, 10, .5, new HashSet<string> { "area.occupied" }, new Dictionary<Guid, int>());
        var denied = WorldFeatureRuntimeResolver.ActivateEncounter(profile, new HashSet<string> { "player" },
            "party", false, 10, .5, new HashSet<string> { "area.occupied" }, new Dictionary<Guid, int>());
        var exhausted = WorldFeatureRuntimeResolver.ActivateEncounter(profile, new HashSet<string> { "player" },
            "party", true, 10, .5, new HashSet<string> { "area.occupied" },
            new Dictionary<Guid, int> { [profile.Foundation!.Candidates[0].Id] = 4 });

        Assert.True(accepted.Activated);
        Assert.Equal(CreatureId, accepted.BlueprintId);
        Assert.Equal("scripts/encounter_activate", accepted.ActivationScriptId);
        Assert.Equal("line-of-sight", denied.Reason);
        Assert.Equal("pool-exhausted", exhausted.Reason);
    }

    [Fact]
    public void G13V_VehicleRoutes_InterpolatesSplineLaneAndStopPolicy()
    {
        var route = VehicleRoute();
        var sample = WorldFeatureRuntimeResolver.SampleVehicleRoute(route, 1);
        Assert.Equal(20, sample.Position.X);
        Assert.Equal(8, sample.SpeedLimit);
        Assert.True(sample.AtStop);
        Assert.Equal(5, sample.WaitSeconds);
        Assert.False(route.AllowOvertaking);
        Assert.Equal("server-snapshots", route.NetworkSyncMode);
    }

    [Fact]
    public void G13W_MerchantCore_EnforcesTreasuryCapsStolenGoodsAndRestock()
    {
        var profile = Merchant();
        var accepted = WorldFeatureRuntimeResolver.QuoteMerchant(profile, "crown", 80, false, "crypt", .5, 0, 0, 0);
        Assert.True(accepted.Accepted);
        Assert.Equal(920, accepted.TreasuryAfter);
        var stolen = WorldFeatureRuntimeResolver.QuoteMerchant(profile, "crown", 80, true, "crypt", .5, 0, 0, 0);
        Assert.False(stolen.Accepted);
        Assert.StartsWith("crime:", stolen.Audit);
        Assert.True(profile.Restock.Persistent);
        Assert.NotNull(profile.Restock.SupplierRouteId);
    }

    [Fact]
    public void G13X_MerchantAdvanced_UsesCurrenciesNegotiationMarketServicesAndLedger()
    {
        var profile = Merchant();
        var quote = WorldFeatureRuntimeResolver.QuoteMerchant(profile, "token", 25, false, "outside", .8, 10, .1, .4);
        Assert.True(quote.Accepted);
        Assert.Equal("token", quote.CurrencyId);
        Assert.StartsWith("ledger:", quote.Audit);
        Assert.NotNull(profile.Market);
        Assert.Contains(profile.Services, item => item.Kind == "repair");
        Assert.True(profile.Negotiation.ServerAuthoritative);
    }

    [Fact]
    public void G13Y_CompanionSocialFormationProgression_ExposesAuthoredPolicies()
    {
        var profile = Companion();
        var result = WorldFeatureRuntimeResolver.EvaluateCompanion(profile, "party", .8, false, 0, false, false);
        Assert.True(result.DialogueAllowed);
        Assert.Equal("wedge:1", result.Formation);
        Assert.Equal(CompanionWorldProfile.ProgressionMode.PlayerDirected, profile.Progression.Mode);
        Assert.Equal(.75, profile.Relationship.Loyalty);
        Assert.Equal(2, profile.Banter.Priority);
    }

    [Fact]
    public void G13Z_CompanionGameplayControl_HandlesTrapsAbilitiesPossessionAndRegroup()
    {
        var profile = Companion();
        var result = WorldFeatureRuntimeResolver.EvaluateCompanion(profile, "owner", .2, true, 2, true, true);
        Assert.True(result.HandleTrap);
        Assert.True(result.UseAbility);
        Assert.True(result.DirectControl);
        Assert.True(result.Regroup);
        Assert.NotNull(profile.PersonalQuest);
        Assert.True(profile.PartySplit.SynchronizeQuests);
    }

    [Fact]
    public void G13AA_ScriptCameraTag_RoutesExplicitEventAndRestoresControl()
    {
        var profile = Scripts();
        var result = WorldFeatureRuntimeResolver.ResolveScript(profile, "door.ash", "opened", cameraSequence: true);
        Assert.Equal("scripts/door_ash_opened", result.ScriptId);
        Assert.True(result.CameraControlLocked);
        Assert.Equal("restore-camera-and-control", result.AbortRecovery);
        Assert.True(profile.TagRouting.ExplicitOnly);
        Assert.True(profile.Camera.AccessibilitySkipEnabled);
    }

    [Fact]
    public void G13AB_ScriptSwitches_UseTypedNamedDefaultsAndBoundedAiValues()
    {
        var profile = Scripts();
        var result = WorldFeatureRuntimeResolver.ResolveScript(profile, "none", "none",
            new Dictionary<string, string> { ["rest.enabled"] = "false" },
            new Dictionary<string, string> { ["combat.aggression"] = "invalid" });
        Assert.Equal("false", result.Flags["rest.enabled"]);
        Assert.Equal("balanced", result.AiSettings["combat.aggression"]);
        Assert.Equal(ScriptAutomationProfile.SettingKind.Boolean, profile.ModuleFlags[0].Kind);
        Assert.Equal(ScriptAutomationProfile.SettingKind.Choice, profile.AiSettings[0].Kind);
    }

    private static PlaceableWorldProfile Placeable() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000001"), 1, "test:g13-n-o", PlaceableId,
        null, null, null,
        new([
            new("physical-attack-resolved", "scripts/placeable_attack", false, true),
            new("ability-impacted", "scripts/placeable_ability", false, true),
            new("heartbeat", "scripts/placeable_heartbeat", false, true),
            new("custom:ash:pulse", "scripts/placeable_custom", false, true),
        ], new(true, 5, 12, true, "placeable.ash")),
        new(PlaceableWorldProfile.BodyMode.Rigid, 12, ["lock-y"], true, 4, true, "server"));

    private static PlaceableWorldProfile.FoundationPolicy PlaceableFoundation() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000010"), 1, "test:r046", "test-placeable",
        [new("fr-FR", "Placeable", "Examiner")], ["test"], "notes",
        new("assets/placeable.glb", ["mesh:placeable"], [], "assets/icon.png", "assets/portrait.png", []),
        new("box", new(1, 1, 1), true, true, "world"),
        new([new(Guid.Parse("12000000-0000-4000-8000-000000000011"), "placeable.examine",
                "placeable-used", "true")], 2.5, 120, true, "placeable.examine", 1, "server", ["true"]),
        null, new(true, false, false, false, true,
            new Dictionary<string, string> { ["onUsed"] = "scripts/placeable_used" }),
        new(new(1.24f, 2.49f, -.24f), new(0, 45, 0), Vector3Value.One, new(0, .5f, 0), .25,
            null, true, "animation:rise"), new Dictionary<string, string> { ["variant"] = "lit" });

    private static DoorWorldProfile Door() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000002"), 1, "test:g13-p-q", DoorId,
        new(30, 4, false, "door.damaged", "door.breached", true, "assets/debris/door.glb", "service.repair-door"),
        new("assets/loading/crypt.png", "loading.crypt.title", "loading.crypt.tip", true, .5),
        [
            new("damage-applied", "scripts/door_damaged", false, true),
            new("entity-destroyed", "scripts/door_destroyed", false, true),
            new("physical-attack-resolved", "scripts/door_attack", false, true),
            new("ability-impacted", "scripts/door_ability", false, true),
            new("heartbeat", "scripts/door_heartbeat", false, true),
            new("custom:ash:seal", "scripts/door_custom", false, true),
        ],
        Guid.Parse("11000000-0000-4000-8000-000000000006"), "ash-watch",
        new(DoorWorldProfile.AssemblyKind.Double,
        [new(Guid.Parse("13000000-0000-4000-8000-000000000001"), "left", -1, 0, false),
         new(Guid.Parse("13000000-0000-4000-8000-000000000002"), "right", 1, .1, true)],
        "open-together", true, "close-safe"),
        new(DoorWorldProfile.PlacementMode.ArchitectureSocket, "socket:crypt-arch",
            new("kit.crypt", "arched", new Dictionary<string, string> { ["door"] = "material.ash" }, 0, 0, 0),
            "closed",
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
            [new("closed", "opening", "interact", 1, null),
             new("opening", "open", "animation-complete", 0, null),
             new("open", "closing", "interact", 1, null),
             new("closing", "closed", "animation-complete", 0, null)],
            new(2.5, "both", 1, 2, 1, "server", 2, "door.unavailable"),
            new(DoorWorldProfile.DestinationKind.Waypoint,
                Guid.Parse("13000000-0000-4000-8000-000000000040"), "anchor:crypt-exit", 180, true,
                ["portal.travel"]),
            [new(Guid.Parse("13000000-0000-4000-8000-000000000041"),
                Guid.Parse("13000000-0000-4000-8000-000000000042"), false, "Retour vers la crypte")],
            new Dictionary<string, string> { ["interaction.prompt"] = "door.open" },
            new("nav-obstacle:crypt-door", "nav-link:crypt-door",
                ["closed", "locked", "jammed", "disabled", "custom"], ["open", "broken"], .75, .25, true)));

    private static TriggerWorldProfile Trigger() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000003"), 1, "test:g13-r-s", TriggerId, AreaId,
        new(["sight", "search"], 10, Guid.Parse("13000000-0000-4000-8000-000000000003"),
            "feedback.secret", true, TriggerWorldProfile.DiscoveryScope.Party),
        new("inspect", "cursor.secret", "outline.gold", "trigger.secret.prompt", "scripts/trigger_clicked", true, "trigger.use"),
        new(2, 30, true, true), "trigger.ash", new("key:ember", false, "trigger.key.denied"),
        new(true, "danger.nearby", true, true, "rest.allowed", "rest.denied"),
        new("tracking", 8, "ash-tracks", 45, 2, "false-trail", Guid.Parse("11000000-0000-4000-8000-000000000007")),
        new([CreatureId], 4, true, 60, "party-vote", Guid.Parse("11000000-0000-4000-8000-000000000006")),
        new(true, true, true, true, true));

    private static TriggerWorldProfile.FoundationPolicy TriggerFoundation() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000020"), 1, "test:r048", "crypt-threshold",
        new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
            [new(0, 0, 0), new(2.24f, 0, .24f), new(2, 0, 2), new(0, 0, 2)],
            [[new(.5f, 0, .5f), new(1, 0, .5f), new(.75f, 0, 1)]], new(2, 3, 2), 3, .25, true),
        new(["creature"], ["player"], ["undead"], ["characters"]), ["crypt.open"], 5,
        TriggerWorldProfile.FirePolicy.OncePerEntity,
        [new(Guid.Parse("12000000-0000-4000-8000-000000000021"), "start-encounter", "encounter:crypt",
            new Dictionary<string, string>())],
        new(TriggerWorldProfile.DestinationKind.Waypoint,
            Guid.Parse("12000000-0000-4000-8000-000000000022"), "anchor:exit", new(0, 180, 0), true,
            ["portal.travel"]), new(new(1.24f, 0, -2.49f), Vector3Value.Zero, Vector3Value.One, null),
        new Dictionary<string, string> { ["editor.overlay"] = "volume" },
        new(Guid.Parse("12000000-0000-4000-8000-000000000023"), 1, "test:r048", "1.0.0",
            "VolumeEntered", "scripts/before-enter", "scripts/after-enter"),
        new(Guid.Parse("12000000-0000-4000-8000-000000000024"), 1, "test:r048", "1.0.0",
            "VolumeExited", null, "scripts/after-exit"));

    private static EncounterWorldProfile Encounter() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000004"), 1, "test:g13-t-u", AreaId,
        new(30, 3, "area.occupied", true, true, .5), "ash-hostile",
        new(true, 5, 12, "encounter.ash"),
        new(2, "ash-hostile", Guid.Parse("12000000-0000-4000-8000-000000000005"), 4, 3, "boss.dead"),
        new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx.spawn.ash"),
        new(30, 30, 120, true, true),
        new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)], Vector3Value.Zero,
                4, 8, ["player"], ["party"], true, 18, "scripts/encounter_activate", true),
            [new(Guid.Parse("13000000-0000-4000-8000-000000000050"), CreatureId,
                EncounterWorldProfile.CandidateRole.Controller, 3, 8, ["area.occupied"], ["ember"], 4)],
            36, ["anchor:crypt-north"], ["reward:ember-shard"], 12));

    private static VehicleRouteProfile VehicleRoute()
    {
        var start = Guid.Parse("13000000-0000-4000-8000-000000000010");
        var end = Guid.Parse("13000000-0000-4000-8000-000000000011");
        return new(Guid.Parse("12000000-0000-4000-8000-000000000005"), 1, "test:g13-v", AreaId,
            [new(start, new(0, 0, 0), 0, 3), new(end, new(20, 0, 0), 0, 3)],
            [new("outbound", 0, 8, "forward")], [new(Guid.Parse("13000000-0000-4000-8000-000000000012"), end, 5, "caravan-stop")],
            10, false, "server-snapshots");
    }

    private static MerchantWorldProfile Merchant() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000006"), 1, "test:g13-w-x", CreatureId,
        [new("crown", "currency.crown", 1, true, false), new("token", "currency.token", 2, true, true)],
        [new("crown", 1000, 100, 0, 100, 2000), new("token", 500, 0, 0, 50, 1000)],
        new(500, 800, new Dictionary<string, decimal> { ["weapon"] = 300 }, -.5, true),
        new(false, ["crypt"], true, true, .25, "crime.stolen-goods"),
        new(24, "market.new-day", Guid.Parse("12000000-0000-4000-8000-000000000005"), true, true),
        new("bargain", 12, .2, .2, 60, true),
        new("ashlands", .25, .5, new Dictionary<string, double> { ["festival"] = -.1 }, true),
        [new(Guid.Parse("13000000-0000-4000-8000-000000000020"), "repair", "service.repair", "crown", 25, "repair.item", "item.damaged")],
        new(true, 90, 500, true, true, true));

    private static CompanionWorldProfile Companion() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000007"), 1, "test:g13-y-z", CreatureId,
        new(Guid.Parse("11000000-0000-4000-8000-000000000006"), "full", "limited", "greeting", "full", "deny"),
        new("wedge", 1, 1.5, true, true, true),
        new(CompanionWorldProfile.ProgressionMode.PlayerDirected, 4, 20, true, true, [5, 10, 15]),
        new(.5, .6, 0, .75, ["friend"], ["crypt-rescue"]),
        new([CreatureId], 2, 90, ["crypt"], true,
            Guid.Parse("11000000-0000-4000-8000-000000000006")),
        new(Guid.Parse("11000000-0000-4000-8000-000000000007"), "loyalty>=0.5", "departed", .5, "camp-reaction"),
        new(true, true, 4, "sight", "disable-device", true),
        new(["heal", "shield"], ["ultimate"], .3, "lowest-health", true),
        new(true, true, true, true, "party.owner-or-gm"),
        new(true, true, "owner-instance", true, 120));

    private static ScriptAutomationProfile Scripts() => new(
        Guid.Parse("12000000-0000-4000-8000-000000000008"), 1, "test:g13-aa-ab",
        new([new(Guid.Parse("13000000-0000-4000-8000-000000000030"), "rig.dialogue", "shot.door", 3, .5, true, true)],
            "restore-camera-and-control", true, true),
        new(true, true, [new("door.ash", "opened", "scripts/door_ash_opened", true)]),
        [new("rest.enabled", ScriptAutomationProfile.SettingKind.Boolean, "true", ScriptAutomationProfile.FlagScope.Module,
            "Allows safe-rest policies.", 1)],
        [new("combat.aggression", ScriptAutomationProfile.SettingKind.Choice, "balanced", ["passive", "balanced", "aggressive"],
            "Balanced target and risk selection.", true)]);
}
