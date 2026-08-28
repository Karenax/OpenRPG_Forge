using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public static class WorldFeatureCatalogValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var catalog = project.Module.WorldFeatures;
        var diagnostics = new List<DomainDiagnostic>();
        var blueprints = project.Blueprints.ToDictionary(item => item.Id);
        var areas = project.Areas.Select(item => item.Id).ToHashSet();
        var dialogues = project.Dialogues.Select(item => item.Id.Value).ToHashSet();
        var dialogueEntryPoints = project.Dialogues.ToDictionary(
            item => item.Id.Value,
            item => (IReadOnlySet<string>)item.EntryPoints
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal));
        var itemBlueprintIds = project.Blueprints
            .Where(item => item.Kind == BlueprintKind.Item)
            .Select(item => item.Id)
            .ToHashSet();
        var quests = project.Quests.Select(item => item.Id.Value).ToHashSet();
        var encounters = catalog.Encounters.Select(item => item.Id).ToHashSet();
        var factionIds = project.Module.Factions.Factions.Select(item => item.Id).ToHashSet();
        var spawnAnchorsByArea = NavigationDestinationCatalog.Build(project).Entries
            .Where(item => item.Kind == NavigationDestinationKind.SpawnAnchor &&
                           item.AreaId is not null &&
                           !string.IsNullOrWhiteSpace(item.ArrivalAnchorKey))
            .GroupBy(item => item.AreaId!)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlySet<string>)group.Select(item => item.ArrivalAnchorKey!)
                    .ToHashSet(StringComparer.Ordinal));

        if (catalog.Id != project.Module.Id.Value)
            Add(diagnostics, "world.catalog.id_mismatch", "The world-feature catalog ID must match the module ID.", "module/worldFeatures/id");
        if (catalog.SchemaVersion != WorldFeatureCatalog.CurrentSchemaVersion)
            Add(diagnostics, "world.catalog.schema_unsupported", "The world-feature catalog schema is unsupported.", "module/worldFeatures/schemaVersion");

        ValidateTargets(catalog.Placeables, item => item.Id, item => item.BlueprintId, BlueprintKind.Placeable,
            "placeables", blueprints, diagnostics);
        ValidateTargets(catalog.Doors, item => item.Id, item => item.BlueprintId, BlueprintKind.Door,
            "doors", blueprints, diagnostics);
        ValidateTargets(catalog.Triggers, item => item.Id, item => item.BlueprintId, BlueprintKind.Trigger,
            "triggers", blueprints, diagnostics);
        ValidateCreatureTargets(catalog.Merchants, item => item.Id, item => item.BlueprintId,
            "merchants", blueprints, diagnostics);
        ValidateCreatureTargets(catalog.Companions, item => item.Id, item => item.BlueprintId,
            "companions", blueprints, diagnostics);

        foreach (var profile in catalog.Placeables)
        {
            var path = $"module/worldFeatures/placeables/{profile.Id}";
            Identity(profile.Id, profile.SchemaVersion, profile.Provenance, path, diagnostics);
            if (profile.DialogueId is { } dialogueId && !dialogues.Contains(dialogueId))
                Add(diagnostics, "world.reference.dialogue_missing", "Placeable dialogue reference is missing.", $"{path}/dialogueId");
            if (profile.Credential is null)
            {
                if (profile.CredentialRequiredAtRuntime)
                    Add(diagnostics, "world.placeable.credential_required_missing",
                        "Required placeable credential policy is missing.", $"{path}/credential");
            }
            else
            {
                var credential = profile.Credential;
                if (string.IsNullOrWhiteSpace(credential.CredentialId) || credential.Charges < 0 ||
                    credential.Consumption == PlaceableWorldProfile.CredentialConsumption.Charge &&
                    credential.Charges < 1)
                    Add(diagnostics, "world.placeable.credential_invalid",
                        "Credential identity and charge policy are invalid.", $"{path}/credential");
                if (credential.Consumption == PlaceableWorldProfile.CredentialConsumption.VirtualPermission &&
                    string.IsNullOrWhiteSpace(credential.VirtualPermission))
                    Add(diagnostics, "world.placeable.credential_virtual_permission_missing",
                        "Virtual credential policy requires a permission.", $"{path}/credential/virtualPermission");
                if (credential.CredentialId.StartsWith("item:", StringComparison.OrdinalIgnoreCase) &&
                    (!Guid.TryParse(credential.CredentialId[5..], out var credentialItemId) ||
                     !itemBlueprintIds.Contains(BlueprintId.From(credentialItemId))))
                    Add(diagnostics, "world.placeable.credential_item_missing",
                        "Placeable credential item reference is missing.", $"{path}/credential/credentialId");
            }
            ValidateEvents(profile.Reactive.Events, path, diagnostics);
            if (profile.Reactive.Scheduler.Enabled && (profile.Reactive.Scheduler.IntervalSeconds <= 0 ||
                profile.Reactive.Scheduler.BudgetPerMinute < 1 || string.IsNullOrWhiteSpace(profile.Reactive.Scheduler.CustomNamespace)))
                Add(diagnostics, "world.placeable.scheduler_invalid", "Enabled scheduler requires cadence, budget and namespace.", $"{path}/reactive/scheduler");
            if (profile.Physics is { } physics && (physics.Mass <= 0 || physics.SleepAfterSeconds < 0 ||
                string.IsNullOrWhiteSpace(physics.Authority)))
                Add(diagnostics, "world.placeable.physics_invalid", "Dynamic physics requires mass, sleep policy and authority.", $"{path}/physics");
            ValidatePlaceableFoundation(profile, path, diagnostics);
            ValidatePlaceableOperations(profile, path, itemBlueprintIds, diagnostics);
            ValidatePlaceableSecurityEvents(profile, path,
                blueprints.Keys.Select(item => item.Value).ToHashSet(), diagnostics);
            ValidatePlaceableDamageEvents(profile, path, diagnostics);
            ValidatePlaceableCompositionEvents(profile, path,
                blueprints.Keys.Select(item => item.Value).ToHashSet(), diagnostics);
            ValidatePlaceableFactionOwnership(profile, path, factionIds, diagnostics);
            ValidatePlaceableConversation(profile, path, dialogueEntryPoints, itemBlueprintIds, diagnostics);
        }
        foreach (var duplicate in catalog.Placeables
                     .Where(item => item.FactionOwnership is not null)
                     .GroupBy(item => item.FactionOwnership!.Id)
                     .Where(group => group.Key == Guid.Empty || group.Count() > 1))
        {
            Add(diagnostics, "world.placeable.faction_ownership_uuid_duplicate",
                "Faction ownership UUID is reused by multiple placeable profiles.",
                $"module/worldFeatures/placeables/{duplicate.First().Id}/factionOwnership/{duplicate.Key}");
        }
        foreach (var duplicate in catalog.Placeables
                     .Where(item => item.Conversation is not null)
                     .GroupBy(item => item.Conversation!.Id)
                     .Where(group => group.Key == Guid.Empty || group.Count() > 1))
        {
            Add(diagnostics, "world.placeable.conversation_uuid_duplicate",
                "Conversation profile UUID is reused by multiple placeable profiles.",
                $"module/worldFeatures/placeables/{duplicate.First().Id}/conversation/{duplicate.Key}");
        }
        foreach (var duplicate in catalog.Placeables
                     .Where(item => item.Conversation is not null)
                     .SelectMany(item => item.Conversation!.Bindings.Select(binding => (item, binding)))
                     .GroupBy(value => value.binding.Id)
                     .Where(group => group.Key == Guid.Empty || group.Count() > 1))
        {
            Add(diagnostics, "world.placeable.conversation_binding_uuid_duplicate",
                "Dialogue binding UUID is reused by multiple placeable profiles.",
                $"module/worldFeatures/placeables/{duplicate.First().item.Id}/conversation/bindings/{duplicate.Key}");
        }
        ValidatePlaceableTransformHierarchy(catalog.Placeables, diagnostics);

        foreach (var profile in catalog.Doors)
        {
            var path = $"module/worldFeatures/doors/{profile.Id}";
            Identity(profile.Id, profile.SchemaVersion, profile.Provenance, path, diagnostics);
            if (profile.Damage is { } damage && (damage.MaximumHealth <= 0 || damage.Hardness < 0 ||
                string.IsNullOrWhiteSpace(damage.BreachedVisual)))
                Add(diagnostics, "world.door.damage_invalid", "Door damage policy requires health, hardness and breach visual.", $"{path}/damage");
            if (profile.Loading is { } loading && (!loading.UseDestinationFallback && string.IsNullOrWhiteSpace(loading.ImageAssetId) ||
                loading.MinimumDisplaySeconds < 0))
                Add(diagnostics, "world.door.loading_invalid", "Loading presentation requires a governed image or fallback.", $"{path}/loading");
            ValidateEvents(profile.Events, path, diagnostics);
            if (profile.DialogueId is { } dialogueId && !dialogues.Contains(dialogueId))
                Add(diagnostics, "world.reference.dialogue_missing", "Door dialogue reference is missing.", $"{path}/dialogueId");
            if (profile.Assembly is { } assembly && (assembly.Panels.Count == 0 ||
                assembly.Panels.Select(item => item.Id).Distinct().Count() != assembly.Panels.Count ||
                string.IsNullOrWhiteSpace(assembly.Sequence)))
                Add(diagnostics, "world.door.assembly_invalid", "Door assembly requires unique panels and a sequence.", $"{path}/assembly");
            if (profile.Foundation is { } foundation)
            {
                var foundationPath = $"{path}/foundation";
                if (foundation.Placement == DoorWorldProfile.PlacementMode.ArchitectureSocket &&
                    (string.IsNullOrWhiteSpace(foundation.ArchitectureSocketId) || foundation.Adapter is null ||
                     string.IsNullOrWhiteSpace(foundation.Adapter.KitId) ||
                     string.IsNullOrWhiteSpace(foundation.Adapter.SocketKind)))
                    Add(diagnostics, "world.door.socket_adapter_missing",
                        "Architecture-socket doors require a socket and compatible kit adapter.",
                        $"{foundationPath}/placement");
                var stateIds = foundation.States.Select(item => item.Id).ToArray();
                if (foundation.States.Count < 3 || stateIds.Distinct(StringComparer.Ordinal).Count() != stateIds.Length ||
                    !stateIds.Contains(foundation.InitialStateId, StringComparer.Ordinal) ||
                    foundation.States.Any(item => string.IsNullOrWhiteSpace(item.Id) ||
                        string.IsNullOrWhiteSpace(item.AnimationId)))
                    Add(diagnostics, "world.door.state_machine_invalid",
                        "Door states require unique IDs, animations and a valid initial state.",
                        $"{foundationPath}/states");
                if (foundation.Transitions.Count == 0 || foundation.Transitions.Any(item =>
                        !stateIds.Contains(item.FromStateId, StringComparer.Ordinal) ||
                        !stateIds.Contains(item.ToStateId, StringComparer.Ordinal) ||
                        string.IsNullOrWhiteSpace(item.Trigger) || item.DurationSeconds < 0))
                    Add(diagnostics, "world.door.transition_invalid",
                        "Door transitions must connect known states with non-negative duration.",
                        $"{foundationPath}/transitions");
                var interaction = foundation.Interaction;
                if (interaction.Range <= 0 || interaction.QueueCapacity < 1 ||
                    new[] { interaction.ClearanceWidth, interaction.ClearanceHeight, interaction.ClearanceDepth }
                        .Any(value => value <= 0 || !double.IsFinite(value)) ||
                    string.IsNullOrWhiteSpace(interaction.Authority))
                    Add(diagnostics, "world.door.interaction_invalid",
                        "Door interaction requires range, clearance, queue and authority.",
                        $"{foundationPath}/interaction");
                if (foundation.Portal is { } portal && (portal.DestinationId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(portal.SpawnAnchorId) ||
                    portal.Permissions.Any(string.IsNullOrWhiteSpace)))
                    Add(diagnostics, "world.door.portal_invalid",
                        "Door portal requires a typed destination, spawn anchor and valid permissions.",
                        $"{foundationPath}/portal");
                if (foundation.Links.Select(item => item.Id).Distinct().Count() != foundation.Links.Count ||
                    foundation.Links.Any(item => item.Id == Guid.Empty || item.TargetId == Guid.Empty ||
                        !item.OneWay && string.IsNullOrWhiteSpace(item.ReturnNavigationLabel)))
                    Add(diagnostics, "world.door.links_invalid",
                        "Door links require UUIDs, targets and return navigation for bidirectional pairs.",
                        $"{foundationPath}/links");
                if (foundation.Links.Any(item => item.TargetId == profile.Id) ||
                    foundation.Portal is { DestinationKind: DoorWorldProfile.DestinationKind.Door } doorPortal &&
                    doorPortal.DestinationId == profile.Id)
                    Add(diagnostics, "world.door.transition_loop",
                        "Door transitions cannot target the same door.", $"{foundationPath}/links");
                var navigation = foundation.Navigation;
                if (navigation is null || string.IsNullOrWhiteSpace(navigation.ObstacleId) ||
                    string.IsNullOrWhiteSpace(navigation.LinkId) ||
                    navigation.OpeningPassableAt is < 0 or > 1 || navigation.ClosingBlockedAt is < 0 or > 1)
                    Add(diagnostics, "world.door.navigation_invalid",
                        "Dynamic navigation requires obstacle/link IDs and normalized animation thresholds.",
                        $"{foundationPath}/navigation");
                if (navigation is not null)
                {
                    var blocking = navigation.BlockingStateIds.ToHashSet(StringComparer.Ordinal);
                    var traversable = navigation.TraversableStateIds.ToHashSet(StringComparer.Ordinal);
                    if (blocking.Overlaps(traversable) ||
                        blocking.Concat(traversable).Any(item => !stateIds.Contains(item, StringComparer.Ordinal)) ||
                        foundation.States.Any(item => blocking.Contains(item.Id) && !item.CollisionEnabled ||
                                                      traversable.Contains(item.Id) && item.CollisionEnabled))
                        Add(diagnostics, "world.door.navigation_state_mismatch",
                            "Navigation state sets must be disjoint, reference known states and match collision.",
                             $"{foundationPath}/navigation/states");
                }
            }
            if (profile.AccessEvents is { } accessEvents)
                diagnostics.AddRange(DoorAccessEventProfileValidator.Validate(accessEvents, $"{path}/accessEvents"));
            else if (profile.AccessEventsRequiredAtRuntime)
                Add(diagnostics, "world.door.access_events_required",
                    "Door access/events are required by this runtime profile.", $"{path}/accessEvents");
            ValidateDoorRuntimeValidation(profile, path, diagnostics);
        }

        var doorAccessComponentIds = catalog.Doors.Where(item => item.AccessEvents is not null)
            .SelectMany(item => new (Guid DoorId, Guid? ComponentId)[]
            {
                (DoorId: item.Id, ComponentId: item.AccessEvents!.Id),
                (DoorId: item.Id, ComponentId: item.AccessEvents.Lock.Id),
                (DoorId: item.Id, ComponentId: item.AccessEvents.Rejection.Id),
                (DoorId: item.Id, ComponentId: item.AccessEvents.Trap.Id),
                (DoorId: item.Id, ComponentId: item.AccessEvents.PortalUse.Id),
                (DoorId: item.Id, ComponentId: item.AccessEvents.StateEvents.Id),
                (DoorId: item.Id, ComponentId: item.AccessEvents.LockStateEvents?.Id),
            })
            .Where(item => item.ComponentId.HasValue)
            .Select(item => (item.DoorId, ComponentId: item.ComponentId!.Value))
            .GroupBy(item => item.ComponentId)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in doorAccessComponentIds)
            Add(diagnostics, "world.door.access_events_uuid_duplicate",
                "Door access/event component UUID is reused by multiple door profiles.",
                $"module/worldFeatures/doors/{duplicate.First().DoorId}/accessEvents/{duplicate.Key}");

        foreach (var profile in catalog.Triggers)
        {
            var path = $"module/worldFeatures/triggers/{profile.Id}";
            Identity(profile.Id, profile.SchemaVersion, profile.Provenance, path, diagnostics);
            if (!areas.Contains(profile.AreaId))
                Add(diagnostics, "world.reference.area_missing", "Trigger area reference is missing.", $"{path}/areaId");
            if (profile.Discovery is { } discovery && (discovery.Senses.Count == 0 || discovery.Difficulty < 0 ||
                discovery.RevealTargetId == Guid.Empty || string.IsNullOrWhiteSpace(discovery.FeedbackId)))
                Add(diagnostics, "world.trigger.discovery_invalid", "Discovery requires senses, target and feedback.", $"{path}/discovery");
            if (profile.Interaction is { } interaction && (string.IsNullOrWhiteSpace(interaction.Verb) ||
                string.IsNullOrWhiteSpace(interaction.ScriptId) || string.IsNullOrWhiteSpace(interaction.Permission)))
                Add(diagnostics, "world.trigger.interaction_invalid", "Interaction requires verb, script and permission.", $"{path}/interaction");
            if (profile.Scheduler is { } scheduler && (scheduler.IntervalSeconds <= 0 || scheduler.BudgetPerMinute < 1))
                Add(diagnostics, "world.trigger.scheduler_invalid", "Trigger scheduler requires positive cadence and budget.", $"{path}/scheduler");
            if (profile.Access is { } access && string.IsNullOrWhiteSpace(access.CredentialId))
                Add(diagnostics, "world.trigger.access_invalid", "Trigger access requires a credential.", $"{path}/access");
            if (profile.Rest is { } rest)
            {
                if (string.IsNullOrWhiteSpace(rest.DangerQuery) ||
                    string.IsNullOrWhiteSpace(rest.AllowedMessageKey) ||
                    string.IsNullOrWhiteSpace(rest.DeniedMessageKey) ||
                    !SafeIdentifier(rest.DangerQuery) || !SafeIdentifier(rest.AllowedMessageKey) ||
                    !SafeIdentifier(rest.DeniedMessageKey))
                    Add(diagnostics, "world.trigger.rest_invalid",
                        "Rest trigger policy requires a safe danger query and localized allowed/denied message keys.",
                        $"{path}/rest");
            }
            if (profile.Clue is { } clue)
            {
                if (!SafeIdentifier(clue.SkillId) || clue.Difficulty < 0 || !SafeIdentifier(clue.TraceId) ||
                    !double.IsFinite(clue.DirectionDegrees) || clue.DirectionDegrees is < 0 or >= 360 ||
                    !double.IsFinite(clue.AgeHours) || clue.AgeHours < 0 ||
                    clue.FalseClueId is { } falseClue &&
                    (!SafeIdentifier(falseClue) || string.Equals(falseClue, clue.TraceId, StringComparison.Ordinal)))
                    Add(diagnostics, "world.trigger.clue_invalid",
                        "Clue trigger policy requires a skill, difficulty, trace, normalized direction, age and distinct optional false clue.",
                        $"{path}/clue");
            }
            if (profile.Clue?.QuestId is { } questId && !quests.Contains(questId))
                Add(diagnostics, "world.reference.quest_missing", "Trigger clue quest reference is missing.", $"{path}/clue/questId");
            if (profile.Narrative is { } narrative)
            {
                if (narrative.Participants.Distinct().Count() != narrative.Participants.Count ||
                    narrative.Priority is < 0 or > 1_000 || !double.IsFinite(narrative.CooldownSeconds) ||
                    narrative.CooldownSeconds < 0 || !SafeIdentifier(narrative.MultiplayerPolicy))
                    Add(diagnostics, "world.trigger.narrative_invalid",
                        "Narrative trigger policy requires unique participants, bounded priority/cooldown and a multiplayer policy.",
                        $"{path}/narrative");
                foreach (var participant in narrative.Participants.Where(participant =>
                             !blueprints.TryGetValue(participant, out var blueprint) ||
                             blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)))
                    Add(diagnostics, "world.trigger.narrative_participant_missing",
                        "Narrative trigger participant must resolve to a creature or player blueprint.",
                        $"{path}/narrative/participants/{participant}");
                if (!dialogues.Contains(narrative.DialogueId))
                    Add(diagnostics, "world.reference.dialogue_missing",
                        "Narrative trigger dialogue reference is missing.", $"{path}/narrative/dialogueId");
            }
            ValidateTriggerFoundation(profile, path, diagnostics);
            ValidateTriggerAdvanced(profile, path, factionIds, diagnostics);
        }

        foreach (var profile in catalog.Encounters)
        {
            var path = $"module/worldFeatures/encounters/{profile.Id}";
            Identity(profile.Id, profile.SchemaVersion, profile.Provenance, path, diagnostics);
            if (!areas.Contains(profile.AreaId))
                Add(diagnostics, "world.reference.area_missing", "Encounter area reference is missing.", $"{path}/areaId");
            if (profile.Respawn is { } respawn && (!double.IsFinite(respawn.DelaySeconds) ||
                respawn.DelaySeconds < 0 || respawn.MaximumCycles < 0 ||
                !double.IsFinite(respawn.AntiFarmMultiplier) || respawn.AntiFarmMultiplier is < 0 or > 1))
                Add(diagnostics, "world.encounter.respawn_invalid", "Respawn delay, cycles and anti-farm multiplier are invalid.", $"{path}/respawn");
            if (profile.Scheduler is { } scheduler && scheduler.HeartbeatEnabled &&
                (!double.IsFinite(scheduler.IntervalSeconds) || scheduler.IntervalSeconds <= 0 ||
                 scheduler.BudgetPerMinute < 1 || string.IsNullOrWhiteSpace(scheduler.CustomEventNamespace)))
                Add(diagnostics, "world.encounter.scheduler_invalid", "Encounter heartbeat requires cadence, budget and namespace.", $"{path}/scheduler");
            if (profile.Reinforcements is { } reinforcement && (reinforcement.RouteId == Guid.Empty ||
                reinforcement.MaximumActors < 1 || !double.IsFinite(reinforcement.DelaySeconds) ||
                reinforcement.DelaySeconds < 0))
                Add(diagnostics, "world.encounter.reinforcement_invalid", "Reinforcements require route, cap and delay.", $"{path}/reinforcements");
            if (!Enum.IsDefined(profile.Visibility.Mode) ||
                !double.IsFinite(profile.Visibility.MinimumCameraDistance) ||
                profile.Visibility.MinimumCameraDistance < 0)
                Add(diagnostics, "world.encounter.visibility_invalid", "Visibility distance cannot be negative.", $"{path}/visibility");
            if (profile.Composition is { } composition)
                diagnostics.AddRange(EncounterCompositionProfileValidator.Validate(composition, profile.Foundation,
                    $"{path}/composition"));
            else if (profile.CompositionRequiredAtRuntime)
                Add(diagnostics, "world.encounter.composition_required",
                    "Encounter composition is required by this runtime profile.", $"{path}/composition");
            if (profile.Scenario is { } scenario)
                diagnostics.AddRange(EncounterScenarioProfileValidator.Validate(scenario, profile.Foundation,
                    profile.Composition, $"{path}/scenario", encounters, quests, profile.Id));
            else if (profile.ScenarioRequiredAtRuntime)
                Add(diagnostics, "world.encounter.scenario_required",
                    "Encounter scenario is required by this runtime profile.", $"{path}/scenario");
            if (profile.Statistics is { } statistics)
                diagnostics.AddRange(EncounterStatisticsProfileValidator.Validate(statistics,
                    $"{path}/statistics"));
            else if (profile.StatisticsRequiredAtRuntime)
                Add(diagnostics, "world.encounter.statistics_required",
                    "Encounter statistics are required by this runtime profile.", $"{path}/statistics");
            if (profile.Foundation is not { } foundation)
            {
                Add(diagnostics, "world.encounter.foundation_missing",
                    "Encounter activation volume and spawn pool are required.", $"{path}/foundation");
                continue;
            }
            var foundationPath = $"{path}/foundation";
            var volume = foundation.Volume;
            var geometryValid = volume.Shape switch
            {
                EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D => volume.Points.Count >= 3 && volume.Height > 0,
                EncounterWorldProfile.VolumeShape.Box3D => volume.Points.Count >= 2 && volume.Height > 0,
                EncounterWorldProfile.VolumeShape.Sphere3D => volume.Radius > 0,
                EncounterWorldProfile.VolumeShape.Capsule3D => volume.Radius > 0 && volume.Height >= volume.Radius * 2,
                _ => false,
            };
            if (!geometryValid || volume.Points.Any(point => !Finite(point)) || !Finite(volume.Center) ||
                !double.IsFinite(volume.Height) || !double.IsFinite(volume.Radius) ||
                !double.IsFinite(volume.ThreatRadius) || volume.ThreatRadius < 0 ||
                volume.RequiredEntityTags.Any(string.IsNullOrWhiteSpace) ||
                volume.AllowedFactions.Any(string.IsNullOrWhiteSpace) ||
                volume.ActivationScriptId is { } activationScript && string.IsNullOrWhiteSpace(activationScript))
                Add(diagnostics, "world.encounter.volume_invalid",
                    "Activation volume requires valid 2D/3D geometry, filters, threat radius and script binding.",
                    $"{foundationPath}/volume");
            var candidateIds = foundation.Candidates.Select(item => item.Id).ToArray();
            if (foundation.Candidates.Count == 0 || candidateIds.Any(item => item == Guid.Empty) ||
                candidateIds.Distinct().Count() != candidateIds.Length)
                Add(diagnostics, "world.encounter.pool_invalid",
                    "Encounter spawn pool requires unique candidate IDs.", $"{foundationPath}/candidates");
            foreach (var candidate in foundation.Candidates)
            {
                var candidatePath = $"{foundationPath}/candidates/{candidate.Id}";
                if (!blueprints.TryGetValue(candidate.BlueprintId, out var candidateBlueprint) ||
                    candidateBlueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
                    Add(diagnostics, "world.encounter.candidate_missing",
                        "Encounter candidate must reference a creature blueprint.", $"{candidatePath}/blueprintId");
                if (!Enum.IsDefined(candidate.Role) || !double.IsFinite(candidate.Weight) ||
                    !double.IsFinite(candidate.Cost) || candidate.Weight <= 0 || candidate.Cost <= 0 ||
                    candidate.MaximumAlive <= 0 ||
                    candidate.Conditions.Any(string.IsNullOrWhiteSpace) || candidate.Variants.Any(string.IsNullOrWhiteSpace))
                    Add(diagnostics, "world.encounter.candidate_invalid",
                        "Candidate weight, role cost, conditions, variants and alive limit are invalid.", candidatePath);
            }
            if (!double.IsFinite(foundation.SpawnBudget) || foundation.SpawnBudget <= 0 ||
                foundation.Candidates.Count > 0 && foundation.Candidates.All(item => item.Cost > foundation.SpawnBudget))
                Add(diagnostics, "world.encounter.budget_invalid",
                    "Spawn budget must be positive and admit at least one candidate.", $"{foundationPath}/spawnBudget");
            if (foundation.SpawnAnchorIds.Count == 0 ||
                foundation.SpawnAnchorIds.Any(string.IsNullOrWhiteSpace) ||
                foundation.SpawnAnchorIds.Distinct(StringComparer.Ordinal).Count() != foundation.SpawnAnchorIds.Count)
                Add(diagnostics, "world.encounter.anchors_invalid",
                    "Encounter requires unique reachable spawn anchors.", $"{foundationPath}/spawnAnchorIds");
            else
            {
                spawnAnchorsByArea.TryGetValue(profile.AreaId, out var availableSpawnAnchors);
                var missingSpawnAnchors = foundation.SpawnAnchorIds
                    .Where(anchorId => availableSpawnAnchors is null || !availableSpawnAnchors.Contains(anchorId))
                    .ToArray();
                if (missingSpawnAnchors.Length > 0)
                    Add(diagnostics, "world.encounter.anchor_reference_missing",
                        $"Encounter spawn anchors do not resolve in area '{profile.AreaId}': " +
                        string.Join(", ", missingSpawnAnchors), $"{foundationPath}/spawnAnchorIds");
            }
            if (foundation.RewardIds.Any(string.IsNullOrWhiteSpace))
                Add(diagnostics, "world.encounter.rewards_invalid",
                    "Encounter reward references cannot be empty.", $"{foundationPath}/rewardIds");
            if (foundation.NetworkActorCap <= 0 ||
                foundation.Candidates.Any(item => item.MaximumAlive > foundation.NetworkActorCap))
                Add(diagnostics, "world.encounter.network_cap_invalid",
                    "Candidate limits must fit the authoritative network actor cap.", $"{foundationPath}/networkActorCap");
        }

        var encounterCompositionIds = catalog.Encounters.Where(item => item.Composition is not null)
            .SelectMany(item => new[]
            {
                (EncounterId: item.Id, ComponentId: item.Composition!.Id),
                (EncounterId: item.Id, ComponentId: item.Composition.Difficulty.Id),
                (EncounterId: item.Id, ComponentId: item.Composition.Selection.Id),
                (EncounterId: item.Id, ComponentId: item.Composition.Activation.Id),
            }.Concat(item.Composition.Groups.Select(group => (EncounterId: item.Id, ComponentId: group.Id))))
            .GroupBy(item => item.ComponentId)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in encounterCompositionIds)
            Add(diagnostics, "world.encounter.composition_uuid_duplicate",
                "Encounter composition component UUID is reused by multiple encounter profiles.",
                $"module/worldFeatures/encounters/{duplicate.First().EncounterId}/composition/{duplicate.Key}");

        var encounterScenarioIds = catalog.Encounters.Where(item => item.Scenario is not null)
            .SelectMany(item => new[]
            {
                (EncounterId: item.Id, ComponentId: item.Scenario!.Id),
                (EncounterId: item.Id, ComponentId: item.Scenario.Activator.Id),
                (EncounterId: item.Id, ComponentId: item.Scenario.Lifecycle.Id),
                (EncounterId: item.Id, ComponentId: item.Scenario.Lifecycle.Entered.Id),
                (EncounterId: item.Id, ComponentId: item.Scenario.Lifecycle.Exited.Id),
                (EncounterId: item.Id, ComponentId: item.Scenario.Lifecycle.Exhausted.Id),
            }.Concat(item.Scenario.SpawnAnchors.Select(anchor =>
                    (EncounterId: item.Id, ComponentId: anchor.Id)))
                .Concat(item.Scenario.Phases.Select(phase =>
                    (EncounterId: item.Id, ComponentId: phase.Id)))
                .Concat(item.Scenario.Phases.SelectMany(phase => phase.Waves).Select(wave =>
                    (EncounterId: item.Id, ComponentId: wave.Id))))
            .GroupBy(item => item.ComponentId)
            .Where(group => group.Count() > 1);
        foreach (var duplicate in encounterScenarioIds)
            Add(diagnostics, "world.encounter.scenario_uuid_duplicate",
                "Encounter scenario component UUID is reused by multiple encounter profiles.",
                $"module/worldFeatures/encounters/{duplicate.First().EncounterId}/scenario/{duplicate.Key}");

        var encounterStatisticsIds = catalog.Encounters.Where(item => item.Statistics is not null)
            .GroupBy(item => item.Statistics!.Id)
            .Where(group => group.Key == Guid.Empty || group.Count() > 1);
        foreach (var duplicate in encounterStatisticsIds)
            Add(diagnostics, "world.encounter.statistics_uuid_duplicate",
                "Encounter statistics UUID is reused by multiple encounter profiles.",
                $"module/worldFeatures/encounters/{duplicate.First().Id}/statistics/{duplicate.Key}");

        foreach (var route in catalog.VehicleRoutes)
        {
            var path = $"module/worldFeatures/vehicleRoutes/{route.Id}";
            Identity(route.Id, route.SchemaVersion, route.Provenance, path, diagnostics);
            if (!areas.Contains(route.AreaId))
                Add(diagnostics, "world.reference.area_missing", "Vehicle route area reference is missing.", $"{path}/areaId");
            var pointIds = route.Points.Select(item => item.Id).ToHashSet();
            if (route.Points.Count < 2 || pointIds.Count != route.Points.Count ||
                route.Points.Any(item => item.Id == Guid.Empty || !double.IsFinite(item.TangentDegrees) ||
                    !double.IsFinite(item.Width) || item.Width <= 0))
                Add(diagnostics, "world.vehicle_route.points_invalid",
                    "Vehicle route points require unique UUIDs, finite tangents and positive finite widths.",
                    $"{path}/points");

            var laneIds = route.Lanes.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            if (route.Lanes.Count == 0 || laneIds.Count != route.Lanes.Count ||
                route.Lanes.Any(item => !SafeIdentifier(item.Id) || !double.IsFinite(item.LateralOffset) ||
                    !double.IsFinite(item.SpeedLimit) || item.SpeedLimit <= 0 || !SafeIdentifier(item.Direction)))
                Add(diagnostics, "world.vehicle_route.lanes_invalid",
                    "Vehicle lanes require unique identifiers, finite offsets, positive finite speeds and a direction.",
                    $"{path}/lanes");

            if (!double.IsFinite(route.DefaultSpeedLimit) || route.DefaultSpeedLimit <= 0)
                Add(diagnostics, "world.vehicle_route.speed_invalid",
                    "Vehicle route default speed must be positive and finite.", $"{path}/defaultSpeedLimit");

            if (route.NetworkSyncMode is not ("server-snapshots" or "bounded-prediction" or "local"))
                Add(diagnostics, "world.vehicle_route.network_sync_invalid",
                    "Vehicle route network sync must use server snapshots, bounded prediction or local mode.",
                    $"{path}/networkSyncMode");

            var stopIds = route.Stops.Select(item => item.Id).ToHashSet();
            if (stopIds.Count != route.Stops.Count || route.Stops.Any(item => item.Id == Guid.Empty ||
                    !pointIds.Contains(item.RoutePointId) || !double.IsFinite(item.WaitSeconds) ||
                    item.WaitSeconds < 0 || !SafeIdentifier(item.ServiceId)))
                Add(diagnostics, "world.vehicle_route.stop_invalid",
                    "Vehicle stops require unique UUIDs, known route points, finite waits and a service identifier.",
                    $"{path}/stops");
        }

        foreach (var profile in catalog.Merchants)
        {
            var path = $"module/worldFeatures/merchants/{profile.Id}";
            Identity(profile.Id, profile.SchemaVersion, profile.Provenance, path, diagnostics);
            var currencyIds = profile.Currencies.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            if (profile.Currencies.Count == 0 || currencyIds.Count != profile.Currencies.Count ||
                profile.Currencies.Any(item => string.IsNullOrWhiteSpace(item.Id) || item.ExchangeRate <= 0))
                Add(diagnostics, "world.merchant.currency_invalid", "Currencies require unique IDs and positive exchange rates.", $"{path}/currencies");
            if (profile.Treasury.Any(item => !currencyIds.Contains(item.CurrencyId) || item.Balance < 0 || item.MaximumBalance < item.Balance))
                Add(diagnostics, "world.merchant.treasury_invalid", "Treasury accounts require known currencies and valid balances.", $"{path}/treasury");
            if (profile.Purchase.MaximumPerItem < 0 || profile.Purchase.MaximumPerTransaction < profile.Purchase.MaximumPerItem)
                Add(diagnostics, "world.merchant.purchase_invalid", "Purchase caps are inconsistent.", $"{path}/purchase");
            if (profile.Negotiation.MaximumDiscount is < 0 or > 1 || profile.Negotiation.MaximumMarkup is < 0 or > 1)
                Add(diagnostics, "world.merchant.negotiation_invalid", "Negotiation bounds must be normalized.", $"{path}/negotiation");
            if (profile.Services.Any(item => item.Id == Guid.Empty || !currencyIds.Contains(item.CurrencyId) || item.Price < 0))
                Add(diagnostics, "world.merchant.service_invalid", "Services require IDs, known currencies and valid prices.", $"{path}/services");
            ValidateMerchantTransactions(profile, blueprints, currencyIds, path, diagnostics);
            ValidateMerchantFoundation(profile, blueprints, dialogues, currencyIds, path, diagnostics);
            ValidateMerchantExperience(profile, path, diagnostics);
        }

        foreach (var profile in catalog.Companions)
        {
            var path = $"module/worldFeatures/companions/{profile.Id}";
            Identity(profile.Id, profile.SchemaVersion, profile.Provenance, path, diagnostics);
            if (!dialogues.Contains(profile.Dialogue.DialogueId) || !dialogues.Contains(profile.Banter.DialogueId))
                Add(diagnostics, "world.reference.dialogue_missing", "Companion dialogue reference is missing.", $"{path}/dialogue");
            if (profile.PersonalQuest is { } quest && !quests.Contains(quest.QuestId))
                Add(diagnostics, "world.reference.quest_missing", "Companion quest reference is missing.", $"{path}/personalQuest/questId");
            if (profile.Formation.Slot < 0 || profile.Formation.Spacing <= 0)
                Add(diagnostics, "world.companion.formation_invalid", "Formation requires a non-negative slot and positive spacing.", $"{path}/formation");
            if (profile.Progression.CurrentLevel < 1 || profile.Progression.MaximumLevel < profile.Progression.CurrentLevel)
                Add(diagnostics, "world.companion.progression_invalid", "Companion progression levels are invalid.", $"{path}/progression");
            if (new[] { profile.Relationship.Affinity, profile.Relationship.Trust, profile.Relationship.Fear, profile.Relationship.Loyalty }
                .Any(value => value is < -1 or > 1))
                Add(diagnostics, "world.companion.relationship_invalid", "Relationship axes must be between -1 and 1.", $"{path}/relationship");
            ValidateCompanion(project, profile, dialogues, areas, path, diagnostics);
        }

        ValidateScripts(catalog.Scripts, diagnostics);
        return diagnostics;
    }

    private static void ValidateDoorRuntimeValidation(DoorWorldProfile profile, string path,
        List<DomainDiagnostic> diagnostics)
    {
        var validationPath = $"{path}/validation";
        if (profile.Validation is null)
        {
            if (profile.ValidationRequiredAtRuntime)
                Add(diagnostics, "world.door.validation_required_missing",
                    "Required door validation context is missing.", validationPath);
            return;
        }

        if (profile.Foundation is null)
            Add(diagnostics, "world.door.validation_foundation_missing",
                "Door validation context requires an authored foundation.", $"{path}/foundation");

        var validation = profile.Validation;
        var stringLists = new IReadOnlyList<string>[]
        {
            validation.KnownAnimationIds,
            validation.KnownCollisionProfileIds,
            validation.KnownCredentialIds,
            validation.KnownScriptIds,
        };
        if (validation.KnownDestinationIds.Any(id => id == Guid.Empty) ||
            validation.KnownDestinationIds.Distinct().Count() != validation.KnownDestinationIds.Count ||
            stringLists.Any(values => values.Any(string.IsNullOrWhiteSpace) ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Count))
            Add(diagnostics, "world.door.validation_context_invalid",
                "Door validation references must be non-empty and unique.", validationPath);
    }

    private static void ValidatePlaceableFoundation(PlaceableWorldProfile profile, string path,
        List<DomainDiagnostic> diagnostics)
    {
        var foundationPath = $"{path}/foundation";
        if (profile.Foundation is null)
        {
            if (profile.FoundationRequiredAtRuntime)
                Add(diagnostics, "world.placeable.foundation_required_missing",
                    "Required placeable foundation is missing.", foundationPath);
            return;
        }

        var foundation = profile.Foundation;
        Identity(foundation.Id, foundation.SchemaVersion, foundation.Provenance, foundationPath, diagnostics);
        if (string.IsNullOrWhiteSpace(foundation.Slug) || foundation.Texts.Count == 0 ||
            foundation.Texts.Any(item => string.IsNullOrWhiteSpace(item.Locale) || string.IsNullOrWhiteSpace(item.Name)) ||
            foundation.Texts.Select(item => item.Locale).Distinct(StringComparer.OrdinalIgnoreCase).Count() != foundation.Texts.Count)
            Add(diagnostics, "world.placeable.identity_invalid",
                "Placeable slug and unique localized names are required.", $"{foundationPath}/identity");
        if (string.IsNullOrWhiteSpace(foundation.Visual.AssetId) || foundation.Visual.MeshIds.Count == 0 ||
            foundation.Visual.MeshIds.Any(string.IsNullOrWhiteSpace) || string.IsNullOrWhiteSpace(foundation.Visual.IconAssetId) ||
            string.IsNullOrWhiteSpace(foundation.Visual.PortraitAssetId))
            Add(diagnostics, "world.placeable.visual_invalid",
                "Placeable asset, mesh, icon and portrait are required.", $"{foundationPath}/visual");
        if (foundation.Tags.Any(string.IsNullOrWhiteSpace) ||
            foundation.Tags.Distinct(StringComparer.Ordinal).Count() != foundation.Tags.Count ||
            foundation.Overrides.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            Add(diagnostics, "world.placeable.metadata_invalid",
                "Placeable tags and explicit overrides must be non-empty and unique.", $"{foundationPath}/metadata");

        var collision = foundation.Collision;
        if (collision.Enabled && (string.IsNullOrWhiteSpace(collision.Shape) || collision.Size.X <= 0 ||
            collision.Size.Y <= 0 || collision.Size.Z <= 0 || string.IsNullOrWhiteSpace(collision.PhysicsLayer)) ||
            collision.NavigationObstacle && !collision.Enabled)
            Add(diagnostics, "world.placeable.collision_invalid",
                "Enabled collision requires positive geometry and navigation obstacles require collision.", $"{foundationPath}/collision");
        if (foundation.Interaction is null && foundation.StaticProp is null ||
            foundation.Interaction is not null && foundation.StaticProp is not null)
            Add(diagnostics, "world.placeable.role_invalid",
                "Placeable must be exactly one of interactable or static decoration.", $"{foundationPath}/role");
        if (foundation.StaticProp is { } prop &&
            (!prop.Batched && !prop.Instanced || prop.MergeEligible && prop.CollisionEnabled))
            Add(diagnostics, "world.placeable.static_invalid",
                "Static decoration requires batching or instancing and merged props cannot keep per-instance collision.", $"{foundationPath}/staticProp");
        if (foundation.Interaction is { } interaction)
        {
            var verbIds = interaction.Verbs.Select(item => item.Id).ToArray();
            if (interaction.Verbs.Count == 0 || verbIds.Any(id => id == Guid.Empty) || verbIds.Distinct().Count() != verbIds.Length ||
                interaction.Verbs.Any(item => string.IsNullOrWhiteSpace(item.LabelKey) || string.IsNullOrWhiteSpace(item.EventName)) ||
                !double.IsFinite(interaction.MaximumDistance) || interaction.MaximumDistance <= 0 ||
                !double.IsFinite(interaction.MaximumAngleDegrees) || interaction.MaximumAngleDegrees is <= 0 or > 360 ||
                !double.IsFinite(interaction.DurationSeconds) || interaction.DurationSeconds < 0 ||
                string.IsNullOrWhiteSpace(interaction.PromptKey) || string.IsNullOrWhiteSpace(interaction.Authority))
                Add(diagnostics, "world.placeable.interaction_invalid",
                    "Interaction requires unique verbs, range, angle, prompt, duration and authority.", $"{foundationPath}/interaction");
        }

        var transform = foundation.Transform;
        var transformValues = new[]
        {
            transform.Position.X, transform.Position.Y, transform.Position.Z,
            transform.RotationDegrees.X, transform.RotationDegrees.Y, transform.RotationDegrees.Z,
            transform.Scale.X, transform.Scale.Y, transform.Scale.Z,
            transform.Pivot.X, transform.Pivot.Y, transform.Pivot.Z,
        };
        if (transformValues.Any(value => !float.IsFinite(value) || Math.Abs(value) > 1_000_000) ||
            transform.Scale.X is <= .01f or > 100 || transform.Scale.Y is <= .01f or > 100 ||
            transform.Scale.Z is <= .01f or > 100 || !double.IsFinite(transform.SnapIncrement) ||
            transform.SnapIncrement is < 0 or > 10000 || transform.ParentId == profile.Id ||
            transform.TransformAnimationId is { Length: 0 } or { Length: > 256 })
            Add(diagnostics, "world.placeable.transform_invalid",
                "Transform scale, pivot, position, snap and parent must remain finite and bounded.", $"{foundationPath}/transform");
        if (foundation.Components.ScriptBindings.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            Add(diagnostics, "world.placeable.scripts_invalid",
                "Placeable script event and route bindings are required.", $"{foundationPath}/components/scriptBindings");
        ValidatePlaceableRuntimeContracts(foundation, foundationPath, diagnostics);
    }

    private static void ValidatePlaceableTransformHierarchy(
        IReadOnlyList<PlaceableWorldProfile> placeables,
        List<DomainDiagnostic> diagnostics)
    {
        var profiles = placeables.ToDictionary(profile => profile.Id);
        foreach (var profile in placeables.Where(profile => profile.Foundation?.Transform.ParentId is not null))
        {
            var path = $"module/worldFeatures/placeables/{profile.Id}/foundation/transform/parentId";
            var parentId = profile.Foundation!.Transform.ParentId!.Value;
            if (!profiles.TryGetValue(parentId, out var parent) || parent.Foundation is null)
            {
                Add(diagnostics, "world.placeable.parent_missing",
                    "Placeable transform parent must resolve to another canonical placeable foundation.", path);
                continue;
            }

            var visited = new HashSet<Guid>();
            PlaceableWorldProfile? current = profile;
            while (current?.Foundation?.Transform.ParentId is { } nextId)
            {
                if (!visited.Add(current.Id) || nextId == profile.Id)
                {
                    Add(diagnostics, "world.placeable.parent_cycle",
                        "Placeable transform hierarchy must remain acyclic.", path);
                    break;
                }
                current = profiles.GetValueOrDefault(nextId);
                if (current is null)
                {
                    break;
                }
            }
        }
    }

    private static void ValidatePlaceableOperations(
        PlaceableWorldProfile profile,
        string path,
        IReadOnlySet<BlueprintId> itemBlueprintIds,
        List<DomainDiagnostic> diagnostics)
    {
        var operationsPath = $"{path}/operations";
        if (profile.Operations is null)
        {
            if (profile.OperationsRequiredAtRuntime)
                Add(diagnostics, "world.placeable.operations_required_missing",
                    "Required placeable operations profile is missing.", operationsPath);
            return;
        }

        var operations = profile.Operations;
        Identity(operations.Id, operations.SchemaVersion, operations.Provenance, operationsPath, diagnostics);
        if (profile.Foundation is null)
            Add(diagnostics, "world.placeable.operations_foundation_missing",
                "Placeable operations require the canonical foundation.", operationsPath);

        var ids = new List<Guid> { profile.Id, operations.Id };
        if (profile.Foundation is { } foundation)
        {
            ids.Add(foundation.Id);
            if (foundation.Components.HasContainer && operations.Container is null)
                Add(diagnostics, "world.placeable.container_required_missing",
                    "Container component requires a canonical container profile.", $"{operationsPath}/container");
            if (foundation.Components.HasHealth && operations.Damageable is null)
                Add(diagnostics, "world.placeable.damageable_required_missing",
                    "Health component requires a canonical damageable profile.", $"{operationsPath}/damageable");
            if (foundation.Components.HasLock && operations.Lock is null)
                Add(diagnostics, "world.placeable.lock_required_missing",
                    "Lock component requires a canonical lock profile.", $"{operationsPath}/lock");
        }

        if (operations.Container is { } container)
        {
            ids.Add(container.Id);
            if (container.Id == Guid.Empty || container.CapacitySlots < 1 ||
                !double.IsFinite(container.CapacityMass) || container.CapacityMass < 0 ||
                string.IsNullOrWhiteSpace(container.OwnershipPolicy) ||
                container.AccessPermissions.Any(string.IsNullOrWhiteSpace) ||
                container.AccessPermissions.Distinct(StringComparer.Ordinal).Count() != container.AccessPermissions.Count ||
                string.IsNullOrWhiteSpace(container.LootPolicyId) || string.IsNullOrWhiteSpace(container.UiProfileId) ||
                string.IsNullOrWhiteSpace(container.InventoryId))
                Add(diagnostics, "world.placeable.container_invalid",
                    "Container requires capacity, ownership, access, persistence, loot, UI, replication and inventory.",
                    $"{operationsPath}/container");
        }

        if (operations.StateMachine is { } machine)
        {
            ids.Add(machine.Id);
            ids.AddRange(machine.States.Select(item => item.Id));
            ids.AddRange(machine.Transitions.Select(item => item.Id));
            var stateIds = machine.States.Select(item => item.StateId).ToArray();
            var requiredStates = new[] { "idle", "open", "closed", "broken", "active", "custom" };
            if (machine.Id == Guid.Empty || string.IsNullOrWhiteSpace(machine.InitialStateId) ||
                !stateIds.Contains(machine.InitialStateId, StringComparer.Ordinal) ||
                requiredStates.Except(stateIds, StringComparer.Ordinal).Any() ||
                stateIds.Any(string.IsNullOrWhiteSpace) ||
                stateIds.Distinct(StringComparer.Ordinal).Count() != stateIds.Length ||
                machine.States.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.AnimationId) ||
                    string.IsNullOrWhiteSpace(item.VisualOverrideId)) ||
                machine.Transitions.Any(item => item.Id == Guid.Empty ||
                    !stateIds.Contains(item.FromStateId, StringComparer.Ordinal) ||
                    !stateIds.Contains(item.ToStateId, StringComparer.Ordinal) ||
                    string.IsNullOrWhiteSpace(item.Trigger) || !double.IsFinite(item.DurationSeconds) ||
                    item.DurationSeconds < 0 || item.Condition is { Length: 0 }))
                Add(diagnostics, "world.placeable.state_machine_invalid",
                    "Visual state machine requires idle, open, closed, broken, active and custom states with valid transitions.",
                    $"{operationsPath}/stateMachine");
        }
        else
            Add(diagnostics, "world.placeable.state_machine_required_missing",
                "Placeable operations require a visual state machine.", $"{operationsPath}/stateMachine");

        if (operations.Damageable is { } damageable)
        {
            ids.Add(damageable.Id);
            if (damageable.Id == Guid.Empty || !double.IsFinite(damageable.MaximumHealth) ||
                damageable.MaximumHealth <= 0 || !double.IsFinite(damageable.CurrentHealth) ||
                damageable.CurrentHealth is < 0 || damageable.CurrentHealth > damageable.MaximumHealth ||
                !double.IsFinite(damageable.Armor) || damageable.Armor < 0 ||
                damageable.Resistances.Count == 0 || damageable.Resistances.Any(item =>
                    string.IsNullOrWhiteSpace(item.Key) || !double.IsFinite(item.Value) || item.Value is < 0 or > 1) ||
                damageable.VisualStageThresholds.Count == 0 || damageable.VisualStageThresholds.Any(item =>
                    !double.IsFinite(item) || item is < 0 or > 1) ||
                damageable.VisualStageThresholds.Distinct().Count() != damageable.VisualStageThresholds.Count ||
                string.IsNullOrWhiteSpace(damageable.DestructionEffectId) ||
                string.IsNullOrWhiteSpace(damageable.DebrisDefinitionId) ||
                !double.IsFinite(damageable.RepairCost) || damageable.RepairCost < 0)
                Add(diagnostics, "world.placeable.damageable_invalid",
                    "Damageable profile requires bounded health, armor, resistances, stages, destruction, debris and repair.",
                    $"{operationsPath}/damageable");
        }

        if (operations.Protection is { } protection)
        {
            ids.Add(protection.Id);
            if (protection.Id == Guid.Empty || protection.ProtectedActions.Count == 0 ||
                protection.ProtectedActions.Any(string.IsNullOrWhiteSpace) ||
                protection.ProtectedActions.Distinct(StringComparer.Ordinal).Count() != protection.ProtectedActions.Count ||
                protection.RequiredPermissions.Count == 0 ||
                protection.RequiredPermissions.Any(string.IsNullOrWhiteSpace) ||
                protection.RequiredPermissions.Distinct(StringComparer.Ordinal).Count() != protection.RequiredPermissions.Count ||
                string.IsNullOrWhiteSpace(protection.Feedback) || string.IsNullOrWhiteSpace(protection.AuditBinding))
                Add(diagnostics, "world.placeable.protection_invalid",
                    "Protection requires actions, permissions, feedback and an audited GM override binding.",
                    $"{operationsPath}/protection");
        }
        else
            Add(diagnostics, "world.placeable.protection_required_missing",
                "Placeable operations require an explicit protection policy.", $"{operationsPath}/protection");

        if (operations.Lock is { } lockProfile)
        {
            ids.Add(lockProfile.Id);
            var itemCredentialsValid = lockProfile.CredentialIds
                .Where(value => value.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
                .All(value => Guid.TryParse(value[5..], out var itemId) &&
                              itemBlueprintIds.Contains(BlueprintId.From(itemId)));
            var mechanisms = lockProfile.CredentialIds.Count + lockProfile.RequiredPermissions.Count +
                (string.IsNullOrWhiteSpace(lockProfile.SkillId) ? 0 : 1) +
                (string.IsNullOrWhiteSpace(lockProfile.CodeHash) ? 0 : 1) + (lockProfile.Hackable ? 1 : 0);
            if (lockProfile.Id == Guid.Empty || mechanisms == 0 ||
                lockProfile.CredentialIds.Any(string.IsNullOrWhiteSpace) ||
                lockProfile.CredentialIds.Distinct(StringComparer.Ordinal).Count() != lockProfile.CredentialIds.Count ||
                lockProfile.RequiredPermissions.Any(string.IsNullOrWhiteSpace) ||
                lockProfile.RequiredPermissions.Distinct(StringComparer.Ordinal).Count() != lockProfile.RequiredPermissions.Count ||
                lockProfile.SkillId is { Length: 0 } || lockProfile.CodeHash is { Length: 0 } ||
                !double.IsFinite(lockProfile.Difficulty) || lockProfile.Difficulty < 0 ||
                lockProfile.Hackable && string.IsNullOrWhiteSpace(lockProfile.SkillId) ||
                !itemCredentialsValid)
                Add(diagnostics, "world.placeable.lock_invalid",
                    "Lock requires a state and valid credential, skill, code, hacking, relock, jam or permission policy.",
                    $"{operationsPath}/lock");
        }

        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            Add(diagnostics, "world.placeable.operations_identity_conflict",
                "Placeable operation identities must be non-empty and unique.", operationsPath);
    }

    private static void ValidatePlaceableFactionOwnership(
        PlaceableWorldProfile profile,
        string path,
        IReadOnlySet<Guid> factionIds,
        List<DomainDiagnostic> diagnostics)
    {
        var ownershipPath = $"{path}/factionOwnership";
        if (profile.FactionOwnership is null)
        {
            if (profile.FactionOwnershipRequiredAtRuntime)
            {
                Add(diagnostics, "world.placeable.faction_ownership_required_missing",
                    "Required placeable faction ownership profile is missing.", ownershipPath);
            }
            return;
        }

        diagnostics.AddRange(PlaceableFactionOwnershipProfileValidator.Validate(
            profile.FactionOwnership,
            factionIds,
            ownershipPath));

        var occupiedIds = new Guid?[]
        {
            profile.Id,
            profile.Foundation?.Id,
            profile.Operations?.Id,
            profile.SecurityEvents?.Id,
            profile.CompositionEvents?.Id,
            profile.Conversation?.Id,
        };
        if (occupiedIds.Any(id => id == profile.FactionOwnership.Id))
        {
            Add(diagnostics, "world.placeable.faction_ownership_identity_conflict",
                "Faction ownership identity must be distinct from the placeable and its other profiles.",
                ownershipPath);
        }
    }

    private static void ValidatePlaceableConversation(
        PlaceableWorldProfile profile,
        string path,
        IReadOnlyDictionary<Guid, IReadOnlySet<string>> dialogueEntryPoints,
        IReadOnlySet<BlueprintId> itemBlueprintIds,
        List<DomainDiagnostic> diagnostics)
    {
        var conversationPath = $"{path}/conversation";
        if (profile.Conversation is null)
        {
            if (profile.ConversationRequiredAtRuntime)
            {
                Add(diagnostics, "world.placeable.conversation_required_missing",
                    "Required placeable conversation profile is missing.", conversationPath);
            }
            return;
        }

        diagnostics.AddRange(PlaceableConversationProfileValidator.Validate(
            profile.Conversation,
            dialogueEntryPoints,
            itemBlueprintIds,
            conversationPath));

        var occupiedIds = new Guid?[]
        {
            profile.Id,
            profile.Foundation?.Id,
            profile.Operations?.Id,
            profile.SecurityEvents?.Id,
            profile.CompositionEvents?.Id,
            profile.FactionOwnership?.Id,
        };
        if (occupiedIds.Any(id => id == profile.Conversation.Id) ||
            profile.Conversation.Bindings.Any(binding => occupiedIds.Contains(binding.Id) ||
                                                         binding.Id == profile.Conversation.Id))
        {
            Add(diagnostics, "world.placeable.conversation_identity_conflict",
                "Conversation and binding identities must be distinct from the placeable and its other profiles.",
                conversationPath);
        }
    }

    private static void ValidatePlaceableSecurityEvents(PlaceableWorldProfile profile, string path,
        IReadOnlySet<Guid> blueprintIds, List<DomainDiagnostic> diagnostics)
    {
        var eventsPath = $"{path}/securityEvents";
        if (profile.SecurityEvents is null)
        {
            if (profile.SecurityEventsRequiredAtRuntime)
                Add(diagnostics, "world.placeable.security_events_required_missing",
                    "Required placeable security and event profile is missing.", eventsPath);
            if (profile.TrapRequiredAtRuntime)
                Add(diagnostics, "world.placeable.trap_required_missing",
                    "Required placeable trap profile is missing.", $"{eventsPath}/trap");
            if (profile.LootRequiredAtRuntime)
                Add(diagnostics, "world.placeable.loot_required_missing",
                    "Required placeable initial treasure profile is missing.", $"{eventsPath}/loot");
            if (profile.ContainerEventsRequiredAtRuntime)
                Add(diagnostics, "world.placeable.container_events_required_missing",
                    "Required placeable open and close event profile is missing.",
                    $"{eventsPath}/containerEvents");
            if (profile.LockEventsRequiredAtRuntime)
                Add(diagnostics, "world.placeable.lock_events_required_missing",
                    "Required placeable lock and unlock event profile is missing.",
                    $"{eventsPath}/lockEvents");
            if (profile.ContentsChangedRequiredAtRuntime)
                Add(diagnostics, "world.placeable.contents_changed_required_missing",
                    "Required placeable contents changed event profile is missing.",
                    $"{eventsPath}/contentsChanged");
            return;
        }

        var events = profile.SecurityEvents;
        Identity(events.Id, events.SchemaVersion, events.Provenance, eventsPath, diagnostics);
        if (profile.Foundation is null)
            Add(diagnostics, "world.placeable.security_events_foundation_missing",
                "Placeable security and events require the canonical foundation.", eventsPath);

        var ids = new List<Guid> { profile.Id, events.Id };
        if (profile.Foundation is not null) ids.Add(profile.Foundation.Id);
        if (profile.Operations is not null) ids.Add(profile.Operations.Id);

        if (events.Trap is { } trap)
        {
            ids.Add(trap.Id);
            if (profile.Foundation is { Components.HasTrap: false })
                Add(diagnostics, "world.placeable.trap_component_missing",
                    "Authored trap data requires the placeable trap component.", $"{eventsPath}/trap");
            if (trap.Id == Guid.Empty || string.IsNullOrWhiteSpace(trap.TriggerId) ||
                string.IsNullOrWhiteSpace(trap.PayloadId) || !double.IsFinite(trap.DetectionDifficulty) ||
                trap.DetectionDifficulty < 0 || !double.IsFinite(trap.DisarmDifficulty) ||
                trap.DisarmDifficulty < 0 || !double.IsFinite(trap.ResetDelaySeconds) ||
                trap.ResetDelaySeconds < 0 || string.IsNullOrWhiteSpace(trap.OwnerId) ||
                string.IsNullOrWhiteSpace(trap.VisualEffectId) || string.IsNullOrWhiteSpace(trap.FriendlyPolicy))
                Add(diagnostics, "world.placeable.trap_invalid",
                    "Trap requires trigger, payload, detection, disarm, reset, ownership, VFX and friendly policy.",
                    $"{eventsPath}/trap");
        }
        else if (profile.TrapRequiredAtRuntime || profile.Foundation?.Components.HasTrap == true)
        {
            Add(diagnostics, "world.placeable.trap_required_missing",
                "Required placeable trap profile is missing.", $"{eventsPath}/trap");
        }

        if (events.Loot is { } loot)
        {
            var lootTable = loot.LootTable;
            ids.Add(loot.Id);
            ids.Add(lootTable.Id);
            if (loot.Id == Guid.Empty || loot.LootTableId == Guid.Empty || loot.LootTableId != lootTable.Id ||
                lootTable.Id == Guid.Empty || lootTable.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(lootTable.Provenance) || loot.Generation < 0 ||
                lootTable.Entries.Count == 0 || lootTable.Entries.Any(item => item.ItemDefinitionId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(item.Group) || !double.IsFinite(item.Weight) || item.Weight <= 0 ||
                    item.MinimumQuantity < 0 || item.MaximumQuantity < item.MinimumQuantity || item.MinimumQuality < 0) ||
                loot.SavedResults.Any(item => item.ItemDefinitionId == Guid.Empty || item.Quantity < 1 ||
                    item.Quality < 0 || string.IsNullOrWhiteSpace(item.Group)))
                Add(diagnostics, "world.placeable.loot_spawner_invalid",
                    "Loot spawner requires a valid table, timing, seed, generation and persisted results.",
                    $"{eventsPath}/loot");
            foreach (var entry in lootTable.Entries.Where(item => !blueprintIds.Contains(item.ItemDefinitionId)))
                Add(diagnostics, "world.placeable.loot_item_reference_missing",
                    "Placeable loot entry references a missing item blueprint.",
                    $"{eventsPath}/loot/lootTable/entries/{entry.ItemDefinitionId}");
        }
        else if (profile.LootRequiredAtRuntime)
        {
            Add(diagnostics, "world.placeable.loot_required_missing",
                "Required placeable initial treasure profile is missing.", $"{eventsPath}/loot");
        }

        if (events.ContainerEvents is { } containerEvents)
        {
            ids.Add(containerEvents.Id);
            if (containerEvents.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(containerEvents.ForgeScriptApiVersion) ||
                string.IsNullOrWhiteSpace(containerEvents.OpenedBinding) ||
                string.IsNullOrWhiteSpace(containerEvents.ClosedBinding) || !containerEvents.IncludeActor ||
                !containerEvents.IncludeResult || !containerEvents.IncludePreviousState ||
                !containerEvents.IncludeAuthority)
                Add(diagnostics, "world.placeable.container_events_invalid",
                    "Container open and close events require bindings, actor, result, previous state and authority.",
                    $"{eventsPath}/containerEvents");
        }
        else if (profile.ContainerEventsRequiredAtRuntime)
        {
            Add(diagnostics, "world.placeable.container_events_required_missing",
                "Required placeable open and close event profile is missing.",
                $"{eventsPath}/containerEvents");
        }

        if (events.LockEvents is { } lockEvents)
        {
            ids.Add(lockEvents.Id);
            if (lockEvents.Id == Guid.Empty || profile.Operations?.Lock is null ||
                string.IsNullOrWhiteSpace(lockEvents.ForgeScriptApiVersion) ||
                string.IsNullOrWhiteSpace(lockEvents.ChangingBinding) ||
                string.IsNullOrWhiteSpace(lockEvents.ChangedBinding) || !lockEvents.IncludeMethod ||
                !lockEvents.IncludeCredential || !lockEvents.IncludeActor || !lockEvents.ControlledVeto)
                Add(diagnostics, "world.placeable.lock_events_invalid",
                    "Lock events require before/after bindings, method, credential, actor and controlled veto.",
                    $"{eventsPath}/lockEvents");
        }
        else if (profile.LockEventsRequiredAtRuntime)
        {
            Add(diagnostics, "world.placeable.lock_events_required_missing",
                "Required placeable lock and unlock event profile is missing.",
                $"{eventsPath}/lockEvents");
        }

        if (events.ContentsChanged is { } disturbed)
        {
            ids.Add(disturbed.Id);
            if (disturbed.Id == Guid.Empty || string.IsNullOrWhiteSpace(disturbed.ForgeScriptApiVersion) ||
                string.IsNullOrWhiteSpace(disturbed.EventBinding) || !disturbed.IncludeTransactionalDelta ||
                !disturbed.IncludeCause || !disturbed.IncludeRevision || !disturbed.IncludeActor)
                Add(diagnostics, "world.placeable.contents_changed_invalid",
                    "Container contents event requires transactional delta, cause, revisions and actor.",
                    $"{eventsPath}/contentsChanged");
        }
        else if (profile.ContentsChangedRequiredAtRuntime)
        {
            Add(diagnostics, "world.placeable.contents_changed_required_missing",
                "Required placeable contents changed event profile is missing.",
                $"{eventsPath}/contentsChanged");
        }

        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            Add(diagnostics, "world.placeable.security_events_identity_conflict",
                "Placeable security and event identities must be non-empty and unique.", eventsPath);
    }

    private static void ValidatePlaceableCompositionEvents(PlaceableWorldProfile profile, string path,
        IReadOnlySet<Guid> blueprintIds, List<DomainDiagnostic> diagnostics)
    {
        var compositionPath = $"{path}/compositionEvents";
        if (profile.CompositionEvents is null)
        {
            if (profile.CompositionEventsRequiredAtRuntime)
                Add(diagnostics, "world.placeable.composition_events_required_missing",
                    "Required placeable damage, socket and multi-verb profile is missing.", compositionPath);
            return;
        }

        var composition = profile.CompositionEvents;
        Identity(composition.Id, composition.SchemaVersion, composition.Provenance, compositionPath,
            diagnostics);
        if (profile.Foundation is null)
            Add(diagnostics, "world.placeable.composition_events_foundation_missing",
                "Placeable composition events require the canonical foundation.", compositionPath);
        else if (!profile.Foundation.Collision.Enabled)
            Add(diagnostics, "world.placeable.composition_events_collision_missing",
                "Placeable sockets require canonical collision geometry.", $"{compositionPath}/sockets");
        if (profile.Operations?.Damageable is null)
            Add(diagnostics, "world.placeable.damage_events_damageable_missing",
                "Placeable damage and destruction events require the canonical damageable profile.",
                $"{compositionPath}/damageEvents");

        var ids = new List<Guid>
        {
            profile.Id,
            composition.Id,
            composition.DamageEvents.Id,
            composition.MultiVerb.Id,
        };
        ids.AddRange(composition.Sockets.Select(item => item.Id));
        ids.AddRange(composition.Attachments.Select(item => item.Id));
        ids.AddRange(composition.MultiVerb.Verbs.Select(item => item.Id));

        var damage = composition.DamageEvents;
        if (damage.Id == Guid.Empty || string.IsNullOrWhiteSpace(damage.ForgeScriptApiVersion) ||
            string.IsNullOrWhiteSpace(damage.DamagedBinding) ||
            string.IsNullOrWhiteSpace(damage.DestroyedBinding) || !damage.IncludeSource ||
            !damage.IncludeAmount || !damage.IncludeDamageType || !damage.IncludePreviousHealth ||
            !damage.IncludeCurrentHealth || !damage.IncludeContext)
            Add(diagnostics, "world.placeable.damage_events_invalid",
                "Damage events require uniform source, amount, type, health and context payloads.",
                $"{compositionPath}/damageEvents");

        var socketIds = composition.Sockets.Select(item => item.SocketId).ToArray();
        if (composition.Sockets.Count == 0 || socketIds.Any(string.IsNullOrWhiteSpace) ||
            socketIds.Distinct(StringComparer.Ordinal).Count() != socketIds.Length ||
            composition.Sockets.Any(socket => socket.Id == Guid.Empty || string.IsNullOrWhiteSpace(socket.Kind) ||
                socket.AllowedAttachmentTags.Count == 0 ||
                socket.AllowedAttachmentTags.Any(string.IsNullOrWhiteSpace) ||
                socket.AllowedAttachmentTags.Distinct(StringComparer.Ordinal).Count() !=
                socket.AllowedAttachmentTags.Count || socket.MaximumAttachments < 1 ||
                InvalidCompositionTransform(socket.LocalTransform)))
            Add(diagnostics, "world.placeable.attachment_sockets_invalid",
                "Attachment sockets require unique identity, kind, bounded transform, tags and capacity.",
                $"{compositionPath}/sockets");

        foreach (var attachment in composition.Attachments)
        {
            var socket = composition.Sockets.FirstOrDefault(item => item.SocketId == attachment.SocketId);
            if (attachment.Id == Guid.Empty || socket is null ||
                !blueprintIds.Contains(attachment.ChildBlueprintId.Value) || attachment.Tags.Count == 0 ||
                attachment.Tags.Any(string.IsNullOrWhiteSpace) ||
                attachment.Tags.Distinct(StringComparer.Ordinal).Count() != attachment.Tags.Count ||
                InvalidCompositionTransform(attachment.Transform) ||
                socket is not null && !socket.AllowedAttachmentTags.Intersect(attachment.Tags,
                    StringComparer.Ordinal).Any())
                Add(diagnostics, "world.placeable.attachment_invalid",
                    "Attachment requires a compatible socket, resolved blueprint, tags and bounded transform.",
                    $"{compositionPath}/attachments/{attachment.Id}");
        }

        foreach (var socket in composition.Sockets.Where(socket =>
                     composition.Attachments.Count(item => item.SocketId == socket.SocketId) >
                     socket.MaximumAttachments))
            Add(diagnostics, "world.placeable.attachment_socket_capacity_exceeded",
                "Authored attachments exceed socket capacity.",
                $"{compositionPath}/sockets/{socket.Id}/maximumAttachments");

        var verbs = composition.MultiVerb.Verbs;
        var verbIds = verbs.Select(item => item.VerbId).ToArray();
        var requiredStandardVerbs = new[] { "inspect", "use", "open", "talk", "take", "push", "climb" };
        var hasCustomVerb = verbs.Any(item => !requiredStandardVerbs.Contains(item.VerbId,
            StringComparer.OrdinalIgnoreCase));
        if (composition.MultiVerb.Id == Guid.Empty || verbs.Count < 8 ||
            verbIds.Any(string.IsNullOrWhiteSpace) ||
            verbIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != verbIds.Length ||
            requiredStandardVerbs.Any(required => !verbIds.Contains(required,
                StringComparer.OrdinalIgnoreCase)) || !hasCustomVerb ||
            verbs.Any(verb => verb.Id == Guid.Empty || string.IsNullOrWhiteSpace(verb.LabelKey) ||
                string.IsNullOrWhiteSpace(verb.EventBinding) || verb.RequiredContext.Any(string.IsNullOrWhiteSpace) ||
                verb.ForbiddenContext.Any(string.IsNullOrWhiteSpace) ||
                verb.RequiredContext.Intersect(verb.ForbiddenContext, StringComparer.Ordinal).Any()))
            Add(diagnostics, "world.placeable.multi_verb_invalid",
                "Multi-verb interaction requires Inspect, Use, Open, Talk, Take, Push, Climb and one custom contextual verb.",
                $"{compositionPath}/multiVerb");

        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            Add(diagnostics, "world.placeable.composition_events_identity_conflict",
                "Placeable composition identities must be non-empty and unique.", compositionPath);
    }

    private static void ValidatePlaceableDamageEvents(PlaceableWorldProfile profile, string path,
        List<DomainDiagnostic> diagnostics)
    {
        var damagePath = $"{path}/damageEvents";
        var canonical = PlaceableDamageEventsResolver.Resolve(profile);
        if (canonical.Authority == PlaceableDamageEventsAuthority.Divergent)
        {
            Add(diagnostics, PlaceableDamageEventsResolver.CatalogDivergenceDiagnosticCode,
                "Direct and composition damage event authorities must have the same identity and values.",
                damagePath);
            return;
        }

        if (!canonical.Available || canonical.Profile is null)
        {
            if (profile.DamageEventsRequiredAtRuntime)
                Add(diagnostics, "world.placeable.damage_events_required_missing",
                    "Required placeable damage and destruction event profile is missing.", damagePath);
            return;
        }

        if (canonical.Authority == PlaceableDamageEventsAuthority.Composition)
            return;

        var damage = canonical.Profile;
        if (profile.Operations?.Damageable is null)
            Add(diagnostics, "world.placeable.damage_events_damageable_missing",
                "Placeable damage and destruction events require the canonical damageable profile.", damagePath);
        if (damage.Id == Guid.Empty || string.IsNullOrWhiteSpace(damage.ForgeScriptApiVersion) ||
            string.IsNullOrWhiteSpace(damage.DamagedBinding) ||
            string.IsNullOrWhiteSpace(damage.DestroyedBinding) || !damage.IncludeSource ||
            !damage.IncludeAmount || !damage.IncludeDamageType || !damage.IncludePreviousHealth ||
            !damage.IncludeCurrentHealth || !damage.IncludeContext)
            Add(diagnostics, "world.placeable.damage_events_invalid",
                "Damage events require uniform source, amount, type, health and context payloads.", damagePath);

        var occupiedIds = new Guid?[]
        {
            profile.Id,
            profile.Foundation?.Id,
            profile.Operations?.Id,
            profile.SecurityEvents?.Id,
            profile.CompositionEvents?.Id,
            profile.FactionOwnership?.Id,
            profile.Conversation?.Id,
        };
        if (occupiedIds.Contains(damage.Id))
            Add(diagnostics, "world.placeable.damage_events_identity_conflict",
                "Damage event identity must be distinct from the placeable and its other profiles.", damagePath);
    }

    private static bool InvalidCompositionTransform(PlaceableWorldProfile.TransformPolicy transform)
        => transform.Scale.X is <= .01f or > 100 || transform.Scale.Y is <= .01f or > 100 ||
           transform.Scale.Z is <= .01f or > 100 || !double.IsFinite(transform.SnapIncrement) ||
           transform.SnapIncrement is < 0 or > 10000 ||
           new[]
           {
               transform.Position.X, transform.Position.Y, transform.Position.Z,
               transform.Pivot.X, transform.Pivot.Y, transform.Pivot.Z,
           }.Any(value => Math.Abs(value) > 1_000_000);

    private static void ValidateTriggerAdvanced(
        TriggerWorldProfile profile,
        string path,
        IReadOnlySet<Guid> factionIds,
        List<DomainDiagnostic> diagnostics)
    {
        var advancedPath = $"{path}/advanced";
        if (profile.Advanced is null)
        {
            if (profile.AdvancedRequiredAtRuntime)
                Add(diagnostics, "world.trigger.advanced_required",
                    "Required trigger trap, query, gameplay-volume and occupancy-debug profile is missing.",
                    advancedPath);
            return;
        }

        var advanced = profile.Advanced;
        Identity(advanced.Id, advanced.SchemaVersion, advanced.Provenance, advancedPath, diagnostics);
        if (profile.Foundation is null)
            Add(diagnostics, "world.trigger.advanced_foundation_missing",
                "Advanced trigger capabilities require canonical trigger geometry and transform.", advancedPath);
        if (string.IsNullOrWhiteSpace(advanced.ForgeScriptApiVersion))
            Add(diagnostics, "world.trigger.advanced_api_invalid",
                "Advanced trigger capabilities require a ForgeScript API version.", $"{advancedPath}/forgeScriptApiVersion");

        var trap = advanced.Trap;
        var trapBindings = new[] { trap.DetectedBinding, trap.DisarmedBinding, trap.TriggeredBinding,
            trap.ResetBinding };
        if (trap.Id == Guid.Empty || string.IsNullOrWhiteSpace(trap.TriggerId) ||
            string.IsNullOrWhiteSpace(trap.PayloadId) || !double.IsFinite(trap.DetectionDifficulty) ||
            trap.DetectionDifficulty < 0 || !double.IsFinite(trap.DisarmDifficulty) ||
            trap.DisarmDifficulty < 0 || !double.IsFinite(trap.ResetDelaySeconds) ||
            trap.ResetDelaySeconds < 0 || string.IsNullOrWhiteSpace(trap.OwnerId) ||
            string.IsNullOrWhiteSpace(trap.VisualEffectId) || string.IsNullOrWhiteSpace(trap.FriendlyPolicy) ||
            !trap.VisualizationColor.StartsWith('#') || trap.VisualizationColor.Length is not (4 or 7 or 9) ||
            trapBindings.Any(string.IsNullOrWhiteSpace) ||
            trapBindings.Distinct(StringComparer.Ordinal).Count() != trapBindings.Length)
            Add(diagnostics, "world.trigger.trap_invalid",
                "Trap requires detection, disarm, payload, reset, ownership, GM visualization and unique bindings.",
                $"{advancedPath}/trap");

        var query = advanced.TargetFilter;
        var queryLists = new IReadOnlyList<string>[] { query.RequiredTags, query.ExcludedTags,
            query.RequiredComponents };
        if (query.Id == Guid.Empty || queryLists.Any(values => values.Any(string.IsNullOrWhiteSpace) ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Count) ||
            query.RequiredTags.Intersect(query.ExcludedTags, StringComparer.Ordinal).Any() ||
            query.FactionIds.Any(id => id == Guid.Empty) || query.FactionIds.Distinct().Count() != query.FactionIds.Count ||
            query.OwnerId is { Length: 0 } || query.TeamId is { Length: 0 } ||
            query.SandboxPredicate is { } predicate &&
            (predicate.Split("==", StringSplitOptions.TrimEntries).Length != 2 ||
             predicate.Split("==", StringSplitOptions.TrimEntries).Any(string.IsNullOrWhiteSpace)))
            Add(diagnostics, "world.trigger.target_filter_invalid",
                "Typed target query tags, factions, components, owner, team and sandbox predicate must be valid.",
                $"{advancedPath}/targetFilter");
        foreach (var factionId in query.FactionIds
                     .Where(id => id != Guid.Empty && !factionIds.Contains(id))
                     .Distinct())
            Add(diagnostics, "world.trigger.faction_reference_missing",
                "Trigger target filter faction reference is missing.",
                $"{advancedPath}/targetFilter/factionIds/{factionId}");

        var volumes = advanced.GameplayVolumes;
        var volumeKinds = Enum.GetValues<TriggerGameplayVolumeKind>();
        if (volumes.Count != volumeKinds.Length || volumes.Any(item => item.Id == Guid.Empty || !item.Enabled ||
                item.NumericParameters.Count + item.TextParameters.Count == 0 ||
                item.NumericParameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Key) ||
                    !double.IsFinite(parameter.Value)) ||
                item.TextParameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Key) ||
                    string.IsNullOrWhiteSpace(parameter.Value)) ||
                !string.Equals(item.EnterBinding, $"{item.Kind}VolumeEntered", StringComparison.Ordinal) ||
                !string.Equals(item.ExitBinding, $"{item.Kind}VolumeExited", StringComparison.Ordinal)) ||
            volumes.Select(item => item.Id).Distinct().Count() != volumes.Count ||
            volumes.Select(item => item.Kind).Distinct().Count() != volumeKinds.Length ||
            volumeKinds.Any(kind => volumes.All(item => item.Kind != kind)))
            Add(diagnostics, "world.trigger.gameplay_volumes_invalid",
                "Gameplay volume library must define all ten typed, enabled and uniquely bound volume kinds.",
                $"{advancedPath}/gameplayVolumes");

        var debug = advanced.OccupancyDebug;
        if (debug.Id == Guid.Empty || !debug.Enabled || !debug.ShowOccupants || !debug.ShowLastFires ||
            !debug.ShowCooldowns || !debug.ShowFilters || !debug.ShowEventTrace ||
            debug.MaximumTraceEntries is < 1 or > 1000)
            Add(diagnostics, "world.trigger.occupancy_debug_invalid",
                "Occupancy debug must expose occupants, fires, cooldowns, filters and a bounded event trace.",
                $"{advancedPath}/occupancyDebug");

        var componentIds = new[] { advanced.Id, trap.Id, query.Id, debug.Id }
            .Concat(volumes.Select(item => item.Id)).ToArray();
        if (componentIds.Any(id => id == Guid.Empty) || componentIds.Distinct().Count() != componentIds.Length ||
            profile.Foundation is { } foundation && componentIds.Contains(foundation.Id))
            Add(diagnostics, "world.trigger.advanced_uuid_duplicate",
                "Advanced trigger component UUIDs must be non-empty and unique.", advancedPath);
    }

    private static void ValidateTriggerFoundation(TriggerWorldProfile profile, string path,
        List<DomainDiagnostic> diagnostics)
    {
        var foundationPath = $"{path}/foundation";
        if (profile.Foundation is null)
        {
            if (profile.FoundationRequiredAtRuntime)
                Add(diagnostics, "world.trigger.foundation_required_missing",
                    "Required trigger foundation is missing.", foundationPath);
            return;
        }

        var foundation = profile.Foundation;
        Identity(foundation.Id, foundation.SchemaVersion, foundation.Provenance, foundationPath, diagnostics);
        if (string.IsNullOrWhiteSpace(foundation.Slug) ||
            foundation.Overrides.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            Add(diagnostics, "world.trigger.identity_invalid",
                "Trigger slug and explicit overrides are required.", $"{foundationPath}/identity");
        var shape = foundation.Shape;
        var shapeValid = shape.Kind switch
        {
            TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D => shape.Vertices.Count >= 3 && shape.Height > 0 &&
                shape.Holes.All(hole => hole.Count >= 3),
            TriggerWorldProfile.ShapeKind.Box3D => shape.Size.X > 0 && shape.Size.Y > 0 && shape.Size.Z > 0,
            TriggerWorldProfile.ShapeKind.Sphere3D => shape.Size.X > 0,
            TriggerWorldProfile.ShapeKind.Capsule3D => shape.Size.X > 0 && shape.Size.Y > 0,
            TriggerWorldProfile.ShapeKind.ConvexHull3D => shape.Vertices.Count >= 4,
            _ => false,
        };
        if (!shapeValid || !double.IsFinite(shape.Height) || !double.IsFinite(shape.SnapIncrement) ||
            shape.SnapIncrement is < 0 or > 10000)
            Add(diagnostics, "world.trigger.geometry_invalid",
                "Trigger geometry, holes, height and snap policy are invalid.", $"{foundationPath}/shape");
        var filters = foundation.Filter.EntityKinds.Concat(foundation.Filter.RequiredTags)
            .Concat(foundation.Filter.ExcludedTags).Concat(foundation.Filter.Layers).ToArray();
        if (filters.Any(string.IsNullOrWhiteSpace) || foundation.Conditions.Any(string.IsNullOrWhiteSpace) ||
            !double.IsFinite(foundation.CooldownSeconds) || foundation.CooldownSeconds < 0)
            Add(diagnostics, "world.trigger.policy_invalid",
                "Trigger filters, conditions and cooldown must be valid.", $"{foundationPath}/policy");
        var actionIds = foundation.Actions.Select(item => item.Id).ToArray();
        if (foundation.Actions.Count == 0 || actionIds.Any(id => id == Guid.Empty) ||
            actionIds.Distinct().Count() != actionIds.Length || foundation.Actions.Any(item =>
                string.IsNullOrWhiteSpace(item.Kind) || string.IsNullOrWhiteSpace(item.TargetId) ||
                item.Parameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Key))))
            Add(diagnostics, "world.trigger.actions_invalid",
                "Trigger actions require unique identities, kinds and targets.", $"{foundationPath}/actions");
        if (foundation.Portal is { } portal && (portal.DestinationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(portal.SpawnAnchorId) || portal.Permissions.Any(string.IsNullOrWhiteSpace)))
            Add(diagnostics, "world.trigger.portal_invalid",
                "Portal triggers require a typed destination, spawn anchor and permissions.", $"{foundationPath}/portal");
        var transform = foundation.Transform;
        var transformValues = new[] { transform.Position.X, transform.Position.Y, transform.Position.Z,
            transform.RotationDegrees.X, transform.RotationDegrees.Y, transform.RotationDegrees.Z,
            transform.Scale.X, transform.Scale.Y, transform.Scale.Z };
        if (transformValues.Any(value => !float.IsFinite(value) || Math.Abs(value) > 1_000_000) ||
            transform.Scale.X is <= .01f or > 100 || transform.Scale.Y is <= .01f or > 100 ||
            transform.Scale.Z is <= .01f or > 100 || transform.ParentId == profile.Id)
            Add(diagnostics, "world.trigger.transform_invalid",
                "Trigger transform must remain finite, bounded and acyclic.", $"{foundationPath}/transform");
        ValidateTriggerEvent(foundation.OnEnter, "VolumeEntered", $"{foundationPath}/onEnter", diagnostics);
        ValidateTriggerEvent(foundation.OnExit, "VolumeExited", $"{foundationPath}/onExit", diagnostics);
        ValidateTriggerRuntimeContracts(profile, foundation, foundationPath, diagnostics);
    }

    private static void ValidateTriggerRuntimeContracts(TriggerWorldProfile profile,
        TriggerWorldProfile.FoundationPolicy foundation, string foundationPath,
        List<DomainDiagnostic> diagnostics)
    {
        if (foundation.Validation is null)
        {
            if (foundation.ValidationRequiredAtRuntime)
                Add(diagnostics, "world.trigger.validation_required_missing",
                    "Required trigger validation context is missing.", $"{foundationPath}/validation");
            return;
        }

        var validation = foundation.Validation;
        if (foundation.Shape.Kind == TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D &&
            (GeometryPolygonValidator.Validate(foundation.Shape.Vertices, $"{foundationPath}/shape/vertices").Count > 0 ||
             foundation.Shape.Holes.Any(hole => GeometryPolygonValidator.Validate(hole,
                 $"{foundationPath}/shape/holes").Count > 0)))
            Add(diagnostics, "world.trigger.geometry_self_intersection",
                "Trigger polygons and holes cannot self-intersect.", $"{foundationPath}/shape");
        if (foundation.Filter.RequiredTags.Intersect(foundation.Filter.ExcludedTags, StringComparer.Ordinal).Any())
            Add(diagnostics, "world.trigger.filter_contradiction",
                "A required trigger tag cannot also be excluded.", $"{foundationPath}/filter");
        if (foundation.Actions.Any(action => !validation.KnownTargetIds.Contains(action.TargetId, StringComparer.Ordinal)))
            Add(diagnostics, "world.trigger.action_target_missing",
                "Trigger action targets must resolve in the validation context.", $"{foundationPath}/actions");
        if (foundation.Portal is { } portal && !validation.KnownPortalDestinationIds.Contains(portal.DestinationId))
            Add(diagnostics, "world.trigger.portal_destination_missing",
                "Trigger portal destination must resolve in the validation context.", $"{foundationPath}/portal/destinationId");
        var scripts = foundation.Actions.SelectMany(action => action.Parameters
                .Where(parameter => string.Equals(parameter.Key, "script", StringComparison.Ordinal))
                .Select(parameter => parameter.Value))
            .Concat(new[] { foundation.OnEnter.BeforeScriptId, foundation.OnEnter.AfterScriptId,
                foundation.OnExit.BeforeScriptId, foundation.OnExit.AfterScriptId, profile.Interaction?.ScriptId }
                .OfType<string>())
            .ToArray();
        if (scripts.Any(script => !validation.KnownScriptIds.Contains(script, StringComparer.Ordinal)))
            Add(diagnostics, "world.trigger.script_reference_missing",
                "Trigger scripts must resolve in the validation context.", $"{foundationPath}/scripts");
        if (foundation.Filter.Layers.Count > 0 &&
            !foundation.Filter.Layers.Intersect(validation.AccessibleLayers, StringComparer.Ordinal).Any())
            Add(diagnostics, "world.trigger.inaccessible",
                "Trigger layers must be reachable from at least one accessible layer.", $"{foundationPath}/filter/layers");
        var lists = new IReadOnlyList<string>[] { validation.KnownTargetIds, validation.KnownScriptIds,
            validation.AccessibleLayers };
        if (lists.Any(values => values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Count) ||
            validation.KnownPortalDestinationIds.Any(id => id == Guid.Empty) ||
            validation.KnownPortalDestinationIds.Distinct().Count() != validation.KnownPortalDestinationIds.Count)
            Add(diagnostics, "world.trigger.validation_context_invalid",
                "Trigger validation references must be non-empty and unique.", $"{foundationPath}/validation");
    }

    private static void ValidateTriggerEvent(TriggerWorldProfile.EventPolicy policy, string expectedBinding,
        string path, List<DomainDiagnostic> diagnostics)
    {
        Identity(policy.Id, policy.SchemaVersion, policy.Provenance, path, diagnostics);
        if (string.IsNullOrWhiteSpace(policy.ForgeScriptApiVersion) ||
            !string.Equals(policy.EventBinding, expectedBinding, StringComparison.Ordinal) ||
            policy.BeforeScriptId is { Length: 0 } || policy.AfterScriptId is { Length: 0 })
            Add(diagnostics, "world.trigger.event_invalid",
                $"Trigger event must bind {expectedBinding} with a ForgeScript API version.", path);
    }

    private static void ValidatePlaceableRuntimeContracts(PlaceableWorldProfile.FoundationPolicy foundation,
        string foundationPath, List<DomainDiagnostic> diagnostics)
    {
        if (foundation.OnUsed is { } onUsed)
        {
            Identity(onUsed.Id, onUsed.SchemaVersion, onUsed.Provenance, $"{foundationPath}/onUsed", diagnostics);
            if (foundation.Interaction is null || string.IsNullOrWhiteSpace(onUsed.ForgeScriptApiVersion) ||
                string.IsNullOrWhiteSpace(onUsed.EventBinding) || onUsed.BeforeScriptId is { Length: 0 } ||
                onUsed.AfterScriptId is { Length: 0 })
                Add(diagnostics, "world.placeable.on_used_invalid",
                    "OnUsed requires an interactable placeable, API version and event binding.", $"{foundationPath}/onUsed");
        }
        else if (foundation.OnUsedRequiredAtRuntime)
            Add(diagnostics, "world.placeable.on_used_required_missing",
                "Required OnUsed policy is missing.", $"{foundationPath}/onUsed");

        if (foundation.Validation is not { } validation)
        {
            if (foundation.Transform.TransformAnimationId is not null)
                Add(diagnostics, "world.placeable.animation_reference_missing",
                    "Transform animation must resolve in the runtime validation policy.",
                    $"{foundationPath}/validation/animationIds");
            return;
        }
        var uniqueNonEmpty = (IReadOnlyList<string> values) => values.All(value => !string.IsNullOrWhiteSpace(value)) &&
            values.Distinct(StringComparer.Ordinal).Count() == values.Count;
        if (!uniqueNonEmpty(validation.AnimationIds) ||
            foundation.Transform.TransformAnimationId is { } animation && !validation.AnimationIds.Contains(animation, StringComparer.Ordinal))
            Add(diagnostics, "world.placeable.animation_invalid",
                "Transform animations must resolve in the validation policy.", $"{foundationPath}/validation/animationIds");
        if (!uniqueNonEmpty(validation.LockIds) || foundation.Components.HasLock && validation.LockIds.Count == 0)
            Add(diagnostics, "world.placeable.lock_reference_missing",
                "Lock components require unique lock references.", $"{foundationPath}/validation/lockIds");
        if (!uniqueNonEmpty(validation.TrapIds) || foundation.Components.HasTrap && validation.TrapIds.Count == 0)
            Add(diagnostics, "world.placeable.trap_reference_missing",
                "Trap components require unique trap references.", $"{foundationPath}/validation/trapIds");
        if (!uniqueNonEmpty(validation.InventoryIds) || foundation.Components.HasContainer && validation.InventoryIds.Count == 0)
            Add(diagnostics, "world.placeable.inventory_reference_missing",
                "Container components require unique inventory references.", $"{foundationPath}/validation/inventoryIds");
        if (!uniqueNonEmpty(validation.PhysicsProfileIds) || foundation.Components.HasPhysics && validation.PhysicsProfileIds.Count == 0)
            Add(diagnostics, "world.placeable.physics_profile_missing",
                "Physics components require a physics profile.", $"{foundationPath}/validation/physicsProfileIds");
        if (validation.DrawCallBudget < foundation.Visual.MeshIds.Count || validation.DrawCallBudget < 1 ||
            validation.ScriptBudget < foundation.Components.ScriptBindings.Count || validation.ScriptBudget < 0)
            Add(diagnostics, "world.placeable.budget_exceeded",
                "Draw-call and script budgets must cover canonical placeable resources.", $"{foundationPath}/validation/budgets");
    }

    private static void ValidateMerchantTransactions(
        MerchantWorldProfile profile,
        IReadOnlyDictionary<BlueprintId, BlueprintDefinition> blueprints,
        IReadOnlySet<string> currencyIds,
        string path,
        List<DomainDiagnostic> diagnostics)
    {
        var transactionPath = $"{path}/transactions";
        if (profile.Transactions is null)
        {
            if (profile.TransactionsRequiredAtRuntime)
                Add(diagnostics, "world.merchant.transactions_required_missing",
                    "Required merchant transaction policy is missing.", transactionPath);
            return;
        }

        var policy = profile.Transactions;
        Identity(policy.Id, policy.SchemaVersion, policy.Provenance, transactionPath, diagnostics);
        if (string.IsNullOrWhiteSpace(policy.VendorInteractionId) || string.IsNullOrWhiteSpace(policy.DialogueChoiceId))
            Add(diagnostics, "world.merchant.vendor_binding_invalid",
                "Merchant transactions require interaction and dialogue bindings.", $"{transactionPath}/vendorBinding");
        if (!policy.ServerAuthoritative)
            Add(diagnostics, "world.merchant.authority_invalid",
                "Merchant transactions must be server authoritative.", $"{transactionPath}/serverAuthoritative");
        if (policy.BuyerCapacity <= 0 || policy.SellerCapacity <= 0)
            Add(diagnostics, "world.merchant.capacity_invalid",
                "Merchant transaction capacities must be positive.", $"{transactionPath}/capacity");
        if (policy.Locales.Count == 0 || policy.Locales.Any(string.IsNullOrWhiteSpace) ||
            policy.Locales.Distinct(StringComparer.OrdinalIgnoreCase).Count() != policy.Locales.Count)
            Add(diagnostics, "world.merchant.localization_invalid",
                "Merchant transaction locales must be present and unique.", $"{transactionPath}/locales");
        if (policy.RestrictionIds.Any(string.IsNullOrWhiteSpace) ||
            policy.RestrictionIds.Distinct(StringComparer.Ordinal).Count() != policy.RestrictionIds.Count)
            Add(diagnostics, "world.merchant.restrictions_invalid",
                "Merchant restrictions must be non-empty and unique.", $"{transactionPath}/restrictionIds");
        if (policy.Scripts.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            Add(diagnostics, "world.merchant.scripts_invalid",
                "Merchant script event keys and routes are required.", $"{transactionPath}/scripts");

        var stockIds = policy.Stock.Select(item => item.Id).ToArray();
        if (policy.Stock.Count == 0 || stockIds.Any(id => id == Guid.Empty) || stockIds.Distinct().Count() != stockIds.Length)
            Add(diagnostics, "world.merchant.stock_identity_invalid",
                "Merchant stock entries require unique stable identities.", $"{transactionPath}/stock");
        foreach (var stock in policy.Stock)
        {
            var stockPath = $"{transactionPath}/stock/{stock.Id}";
            if (!blueprints.TryGetValue(stock.ItemBlueprintId, out var blueprint) || blueprint.Kind != BlueprintKind.Item)
                Add(diagnostics, "world.merchant.stock_item_invalid",
                    "Merchant stock must reference an item blueprint.", $"{stockPath}/itemBlueprintId");
            if (string.IsNullOrWhiteSpace(stock.VariantId) || stock.Quantity < 0 || stock.UnitPrice < 0 ||
                !currencyIds.Contains(stock.CurrencyId))
                Add(diagnostics, "world.merchant.stock_values_invalid",
                    "Merchant stock requires variant, quantity, price and known currency.", stockPath);
            if (stock.RestrictionIds.Any(id => !policy.RestrictionIds.Contains(id, StringComparer.Ordinal)))
                Add(diagnostics, "world.merchant.stock_restriction_invalid",
                    "Stock restrictions must resolve in the merchant policy.", $"{stockPath}/restrictionIds");
        }
    }

    private static void ValidateMerchantFoundation(
        MerchantWorldProfile profile,
        IReadOnlyDictionary<BlueprintId, BlueprintDefinition> blueprints,
        IReadOnlySet<Guid> dialogues,
        IReadOnlySet<string> currencyIds,
        string path,
        List<DomainDiagnostic> diagnostics)
    {
        var foundationPath = $"{path}/foundation";
        if (profile.Foundation is null)
        {
            if (profile.FoundationRequiredAtRuntime)
                Add(diagnostics, "world.merchant.foundation_required_missing",
                    "Required merchant foundation profile is missing.", foundationPath);
            return;
        }

        var foundation = profile.Foundation;
        Identity(foundation.Id, foundation.SchemaVersion, foundation.Provenance, foundationPath, diagnostics);
        if (string.IsNullOrWhiteSpace(foundation.Slug) || string.IsNullOrWhiteSpace(foundation.DisplayNameKey) ||
            foundation.Locales.Count == 0 || foundation.Locales.Any(string.IsNullOrWhiteSpace) ||
            foundation.Locales.Distinct(StringComparer.OrdinalIgnoreCase).Count() != foundation.Locales.Count)
            Add(diagnostics, "world.merchant.foundation_identity_invalid",
                "Store slug, display-name key and unique locales are required.", foundationPath);

        var vendorIds = foundation.Vendors.Select(item => item.Id).ToArray();
        if (foundation.Vendors.Count == 0 || vendorIds.Any(id => id == Guid.Empty) ||
            vendorIds.Distinct().Count() != vendorIds.Length)
            Add(diagnostics, "world.merchant.vendor_identity_invalid",
                "At least one uniquely identified vendor binding is required.", $"{foundationPath}/vendors");
        foreach (var vendor in foundation.Vendors)
        {
            var vendorPath = $"{foundationPath}/vendors/{vendor.Id}";
            if (!blueprints.TryGetValue(vendor.VendorBlueprintId, out var blueprint) ||
                blueprint.Kind != BlueprintKind.Creature)
                Add(diagnostics, "world.merchant.vendor_reference_missing",
                    "Vendor binding must reference a creature blueprint.", $"{vendorPath}/vendorBlueprintId");
            if (!dialogues.Contains(vendor.DialogueId))
                Add(diagnostics, "world.merchant.dialogue_reference_missing",
                    "Vendor binding must reference an existing dialogue.", $"{vendorPath}/dialogueId");
            if (string.IsNullOrWhiteSpace(vendor.DialogueChoiceId) ||
                string.IsNullOrWhiteSpace(vendor.InteractionId) || string.IsNullOrWhiteSpace(vendor.Channel))
                Add(diagnostics, "world.merchant.vendor_binding_invalid",
                    "Vendor dialogue choice, interaction and channel are required.", vendorPath);
        }

        Identity(foundation.Wizard.Id, foundation.Wizard.SchemaVersion, foundation.Wizard.Provenance,
            $"{foundationPath}/wizard", diagnostics);
        if (!foundation.Wizard.CreateStoreDefinition || !foundation.Wizard.CreateVendorBinding ||
            !foundation.Wizard.CreateDialogueChoice || !foundation.Wizard.CreateInteraction ||
            !foundation.Wizard.CreateTests || string.IsNullOrWhiteSpace(foundation.Wizard.TestFixtureId))
            Add(diagnostics, "world.merchant.wizard_incomplete",
                "Store wizard must generate the definition, vendor, dialogue choice, interaction and tests.",
                $"{foundationPath}/wizard");

        var stockIds = foundation.Stock.Select(item => item.Id).ToArray();
        if (stockIds.Any(id => id == Guid.Empty) || stockIds.Distinct().Count() != stockIds.Length)
            Add(diagnostics, "world.merchant.foundation_stock_identity_invalid",
                "Store stock entries require unique stable identities.", $"{foundationPath}/stock");
        foreach (var entry in foundation.Stock)
        {
            var stockPath = $"{foundationPath}/stock/{entry.Id}";
            if (!blueprints.TryGetValue(entry.ItemBlueprintId, out var blueprint) ||
                blueprint.Kind != BlueprintKind.Item)
                Add(diagnostics, "world.merchant.foundation_stock_item_missing",
                    "Store stock entry must reference an item blueprint.", $"{stockPath}/itemBlueprintId");
            if (string.IsNullOrWhiteSpace(entry.VariantId) || entry.Quantity < 0 || entry.RestockWorldHours < 0 ||
                entry.DisplayOrder < 0 || entry.BaseValue < 0 || !currencyIds.Contains(entry.CurrencyId) ||
                entry.HiddenConditions.Any(string.IsNullOrWhiteSpace) ||
                entry.HiddenConditions.Distinct(StringComparer.Ordinal).Count() != entry.HiddenConditions.Count)
                Add(diagnostics, "world.merchant.foundation_stock_values_invalid",
                    "Stock supply, quantity, restock, order, price, currency and hidden conditions are invalid.", stockPath);
            if (entry.Supply == MerchantSupplyPolicy.Finite && entry.Quantity < 1)
                Add(diagnostics, "world.merchant.finite_stock_empty",
                    "Finite stock requires a positive quantity.", $"{stockPath}/quantity");
        }

        var pricing = foundation.Pricing;
        var pricingPath = $"{foundationPath}/pricing";
        Identity(pricing.Id, pricing.SchemaVersion, pricing.Provenance, pricingPath, diagnostics);
        if (pricing.BuyMultiplier < 0 || pricing.SellMultiplier < 0 || pricing.MinimumPrice < 0 ||
            pricing.MaximumPrice < pricing.MinimumPrice ||
            pricing.CurrencyRates.Count == 0 || pricing.CurrencyRates.Any(item =>
                !currencyIds.Contains(item.Key) || item.Value <= 0) ||
            new[] { pricing.ReputationFactor, pricing.ScarcityFactor, pricing.ConditionFactor,
                pricing.NegotiationFactor }.Any(value => value < 0) ||
            !ValidCurve(pricing.BuyCurve) || !ValidCurve(pricing.SellCurve))
            Add(diagnostics, "world.merchant.foundation_pricing_invalid",
                "Pricing curves, currencies, factors and bounds are invalid.", pricingPath);

        var query = foundation.Restrictions;
        var queryPath = $"{foundationPath}/restrictions";
        Identity(query.Id, query.SchemaVersion, query.Provenance, queryPath, diagnostics);
        var filterValues = query.WhitelistTypes.Concat(query.WhitelistTags).Concat(query.BlacklistTags)
            .Concat(query.Provenances).ToArray();
        if (query.MinimumQuality < 0 || query.MaximumQuality < query.MinimumQuality ||
            query.MinimumCondition is < 0 or > 1 || filterValues.Any(string.IsNullOrWhiteSpace) ||
            query.WhitelistTags.Intersect(query.BlacklistTags, StringComparer.OrdinalIgnoreCase).Any() ||
            query.CustomRuleBinding is { Length: 0 })
            Add(diagnostics, "world.merchant.foundation_query_invalid",
                "Whitelist, blacklist, quality, provenance, condition, owner and custom rule are invalid.", queryPath);
    }

    private static bool ValidCurve(IReadOnlyList<MerchantPriceCurvePoint> points)
    {
        if (points.Count == 0 || points.Any(item => item.BaseValue < 0 || item.Multiplier <= 0)) return false;
        var ordered = points.OrderBy(item => item.BaseValue).ToArray();
        return ordered.Select(item => item.BaseValue).Distinct().Count() == ordered.Length &&
               ordered.SequenceEqual(points);
    }

    private static void ValidateMerchantExperience(
        MerchantWorldProfile profile,
        string path,
        List<DomainDiagnostic> diagnostics)
    {
        var experiencePath = $"{path}/experience";
        if (profile.Experience is null)
        {
            if (profile.ExperienceRequiredAtRuntime)
                Add(diagnostics, "world.merchant.experience_required_missing",
                    "Required merchant store experience profile is missing.", experiencePath);
            return;
        }

        var experience = profile.Experience;
        Identity(experience.Id, experience.SchemaVersion, experience.Provenance, experiencePath, diagnostics);
        var sessions = experience.Sessions;
        var sessionsPath = $"{experiencePath}/sessions";
        Identity(sessions.Id, sessions.SchemaVersion, sessions.Provenance, sessionsPath, diagnostics);
        if (!Version.TryParse(sessions.ForgeScriptApiVersion, out _) ||
            string.IsNullOrWhiteSpace(sessions.OpenEventBinding) ||
            string.IsNullOrWhiteSpace(sessions.CloseEventBinding) ||
            sessions.OpenEventBinding == sessions.CloseEventBinding ||
            !sessions.IncludeCustomer || !sessions.IncludeVendor || !sessions.IncludePriceContext ||
            !sessions.IncludeTransactionSummary)
            Add(diagnostics, "world.merchant.session_events_invalid",
                "Store session events require an API version, distinct bindings, customer, vendor, price context and transaction summary.",
                sessionsPath);

        var ui = experience.Ui;
        var uiPath = $"{experiencePath}/ui";
        Identity(ui.Id, ui.SchemaVersion, ui.Provenance, uiPath, diagnostics);
        if (!ui.KeyboardNavigation || !ui.GamepadNavigation || !ui.Compare || !ui.Filters || !ui.Cart ||
            !ui.Confirmation || !ui.ScreenReaderLabels || !ui.HighContrast || ui.Columns.Count < 3 ||
            ui.Columns.Any(string.IsNullOrWhiteSpace) ||
            ui.Columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ui.Columns.Count)
            Add(diagnostics, "world.merchant.store_ui_invalid",
                "Store UI requires keyboard/gamepad navigation, compare, filters, cart, confirmation, accessibility and unique columns.",
                uiPath);

        if (new[] { experience.Id, sessions.Id, ui.Id }.Distinct().Count() != 3)
            Add(diagnostics, "world.merchant.experience_identity_conflict",
                "Store experience, session and UI identities must be distinct.", experiencePath);
    }

    private static void ValidateCompanion(
        ProjectDefinition project,
        CompanionWorldProfile profile,
        IReadOnlySet<Guid> dialogues,
        IReadOnlySet<AreaId> areas,
        string path,
        List<DomainDiagnostic> diagnostics)
    {
        ValidateCompanionFoundation(profile, dialogues, path, diagnostics);
        ValidateCompanionControl(profile, path, diagnostics);
        ValidateCompanionExperience(profile, path, diagnostics);
        if (profile.Validation is null)
        {
            if (profile.ValidationRequiredAtRuntime)
                Add(diagnostics, "world.companion.validation_required",
                    "Required companion validation policy is missing.", $"{path}/validation");
            return;
        }

        var policy = profile.Validation;
        var validationPath = $"{path}/validation";
        Identity(policy.Id, policy.SchemaVersion, policy.Provenance, validationPath, diagnostics);

        var recruitment = policy.Recruitment;
        if ((!recruitment.AllowDialogue && !recruitment.AllowScript) ||
            recruitment.AllowScript && !SafeIdentifier(recruitment.ScriptId) ||
            recruitment.Cost < 0 || recruitment.MinimumReputation is < -1 or > 1 ||
            recruitment.Cost > 0 && !SafeIdentifier(recruitment.CurrencyId))
            Add(diagnostics, "world.companion.recruitment_invalid",
                "Recruitment requires a dialogue or script path, valid currency/cost and normalized reputation.",
                $"{validationPath}/recruitment");

        var ownershipKinds = new HashSet<string>(["individual", "party", "shared", "ai-only", "game-master"],
            StringComparer.Ordinal);
        if (!ownershipKinds.Contains(policy.Ownership.Kind) || string.IsNullOrWhiteSpace(policy.Ownership.Authority))
            Add(diagnostics, "world.companion.ownership_invalid",
                "Ownership kind and authority are required.", $"{validationPath}/ownership");

        if (policy.CommandIds.Count == 0 ||
            policy.CommandIds.Distinct(StringComparer.Ordinal).Count() != policy.CommandIds.Count ||
            policy.CommandIds.Any(command => !SafeIdentifier(command)))
            Add(diagnostics, "world.companion.commands_invalid",
                "Companion commands must be non-empty, unique and use safe bindings.", $"{validationPath}/commandIds");

        if (!SafeIdentifier(policy.Equipment.InventoryId) || !SafeIdentifier(policy.Equipment.EquipmentPolicyId) ||
            policy.Equipment.Slots.Count == 0 ||
            policy.Equipment.Slots.Distinct(StringComparer.Ordinal).Count() != policy.Equipment.Slots.Count ||
            policy.Equipment.Slots.Any(slot => !SafeIdentifier(slot)))
            Add(diagnostics, "world.companion.equipment_invalid",
                "Inventory, equipment policy and unique slots must be compatible bindings.",
                $"{validationPath}/equipment");

        var deathModes = new HashSet<string>(
            ["downed", "permanent-death", "recoverable-corpse", "resurrection", "replacement", "memorial"],
            StringComparer.Ordinal);
        if (!SafeIdentifier(policy.Death.PolicyId) || !deathModes.Contains(policy.Death.Mode) ||
            policy.Death.RecoverySeconds < 0)
            Add(diagnostics, "world.companion.death_policy_invalid",
                "Death policy requires a known mode, policy binding and non-negative recovery time.",
                $"{validationPath}/death");

        if (policy.DialogueContexts.Count < 2 ||
            policy.DialogueContexts.Select(context => context.ContextId).Distinct(StringComparer.Ordinal).Count() !=
            policy.DialogueContexts.Count ||
            policy.DialogueContexts.Any(context => !SafeIdentifier(context.ContextId) ||
                !SafeIdentifier(context.Binding) || !dialogues.Contains(context.DialogueId)))
            Add(diagnostics, "world.companion.dialogue_contexts_invalid",
                "Recruit and owner dialogue contexts must be unique and reference compatible dialogues.",
                $"{validationPath}/dialogueContexts");

        var navigation = policy.CrossZoneNavigation;
        if (navigation.TimeoutSeconds <= 0 ||
            navigation.AllowedAreaIds.Distinct().Count() != navigation.AllowedAreaIds.Count ||
            navigation.AllowedAreaIds.Any(areaId => !areas.Contains(areaId)) ||
            navigation.SafeFallback && string.IsNullOrWhiteSpace(navigation.FallbackAnchorTag) ||
            navigation.Enabled && (navigation.AllowedAreaIds.Count < 2 ||
                !navigation.FollowPortals && !navigation.SafeFallback))
            Add(diagnostics, "world.companion.cross_zone_navigation_invalid",
                "Cross-zone navigation requires valid areas, portal/fallback policy, anchor and timeout.",
                $"{validationPath}/crossZoneNavigation");
        else if (navigation.Enabled && !AreasAreConnected(project, navigation.AllowedAreaIds))
            Add(diagnostics, "world.companion.cross_zone_unreachable",
                "Allowed companion areas are not connected by the authored portal graph.",
                $"{validationPath}/crossZoneNavigation/allowedAreaIds");
    }

    private static void ValidateCompanionFoundation(
        CompanionWorldProfile profile,
        IReadOnlySet<Guid> dialogues,
        string path,
        List<DomainDiagnostic> diagnostics)
    {
        var foundation = profile.Foundation;
        var foundationPath = $"{path}/foundation";
        if (foundation is null)
        {
            if (profile.FoundationRequiredAtRuntime)
                Add(diagnostics, "world.companion.foundation_required",
                    "Required companion foundation profile is missing.", foundationPath);
            return;
        }

        Identity(foundation.Id, foundation.SchemaVersion, foundation.Provenance, foundationPath, diagnostics);
        var identities = new[]
        {
            (foundation.Recruitable.Id, foundation.Recruitable.SchemaVersion, foundation.Recruitable.Provenance,
                $"{foundationPath}/recruitable"),
            (foundation.PreRecruitment.Id, foundation.PreRecruitment.SchemaVersion,
                foundation.PreRecruitment.Provenance, $"{foundationPath}/preRecruitment"),
            (foundation.OwnerDialogue.Id, foundation.OwnerDialogue.SchemaVersion,
                foundation.OwnerDialogue.Provenance, $"{foundationPath}/ownerDialogue"),
            (foundation.Membership.Id, foundation.Membership.SchemaVersion, foundation.Membership.Provenance,
                $"{foundationPath}/membership"),
            (foundation.Party.Id, foundation.Party.SchemaVersion, foundation.Party.Provenance,
                $"{foundationPath}/party"),
        };
        foreach (var identity in identities)
            Identity(identity.Id, identity.SchemaVersion, identity.Provenance, identity.Item4, diagnostics);
        if (identities.Select(item => item.Id).Append(foundation.Id).Distinct().Count() != identities.Length + 1)
            Add(diagnostics, "world.companion.foundation_identity_duplicate",
                "Companion foundation identities must be unique.", foundationPath);

        var recruitable = foundation.Recruitable;
        if (!recruitable.Enabled || (!recruitable.AllowDialogue && !recruitable.AllowScript) ||
            recruitable.AllowScript && !SafeIdentifier(recruitable.ScriptId) || recruitable.Cost < 0 ||
            recruitable.MinimumReputation is < -1 or > 1 ||
            recruitable.Cost > 0 && !SafeIdentifier(recruitable.CurrencyId) ||
            recruitable.Conditions.Distinct(StringComparer.Ordinal).Count() != recruitable.Conditions.Count ||
            recruitable.Conditions.Any(item => !SafeIdentifier(item)) ||
            recruitable.ConsequenceIds.Distinct(StringComparer.Ordinal).Count() != recruitable.ConsequenceIds.Count ||
            recruitable.ConsequenceIds.Any(item => !SafeIdentifier(item)))
            Add(diagnostics, "world.companion.recruitable_invalid",
                "Recruitable creature requires governed paths, cost, reputation, conditions and consequences.",
                $"{foundationPath}/recruitable");

        var preRecruitment = foundation.PreRecruitment;
        if (!dialogues.Contains(preRecruitment.DialogueId) ||
            !string.Equals(preRecruitment.ContextId, "not-recruited", StringComparison.Ordinal) ||
            !SafeIdentifier(preRecruitment.OfferNodeId) || !SafeIdentifier(preRecruitment.RefusalNodeId) ||
            preRecruitment.Conditions.Distinct(StringComparer.Ordinal).Count() != preRecruitment.Conditions.Count ||
            preRecruitment.Conditions.Any(item => !SafeIdentifier(item)) ||
            preRecruitment.ConsequenceIds.Distinct(StringComparer.Ordinal).Count() !=
            preRecruitment.ConsequenceIds.Count ||
            preRecruitment.ConsequenceIds.Any(item => !SafeIdentifier(item)))
            Add(diagnostics, "world.companion.pre_recruitment_dialogue_invalid",
                "Pre-recruitment dialogue must resolve its context, offer, refusal, conditions and consequences.",
                $"{foundationPath}/preRecruitment");

        var ownerDialogue = foundation.OwnerDialogue;
        var validatedCommands = profile.Validation?.CommandIds;
        if (!dialogues.Contains(ownerDialogue.DialogueId) ||
            !string.Equals(ownerDialogue.ContextId, "recruited-by-speaker", StringComparison.Ordinal) ||
            ownerDialogue.CommandIds.Count == 0 ||
            ownerDialogue.CommandIds.Distinct(StringComparer.Ordinal).Count() != ownerDialogue.CommandIds.Count ||
            ownerDialogue.CommandIds.Any(item => !SafeIdentifier(item) ||
                validatedCommands is not null && !validatedCommands.Contains(item, StringComparer.Ordinal)) ||
            !ownerDialogue.ExposeRelationship || !ownerDialogue.ExposeEquipment ||
            !ownerDialogue.ExposeProgression || !SafeIdentifier(ownerDialogue.DepartureActionId))
            Add(diagnostics, "world.companion.owner_dialogue_invalid",
                "Owner dialogue must expose governed commands, relationship, equipment, progression and departure.",
                $"{foundationPath}/ownerDialogue");

        var membership = foundation.Membership;
        if (!membership.Atomic || !membership.EnforcePartyCapacity || !membership.TransferOwnership ||
            !membership.TransferInventory || !membership.PreserveDismissPosition ||
            !SafeIdentifier(membership.InventoryId) || !SafeIdentifier(membership.DismissAnchorTag))
            Add(diagnostics, "world.companion.membership_transaction_invalid",
                "Recruit and dismiss transactions must atomically preserve capacity, ownership, inventory, position and persistence.",
                $"{foundationPath}/membership");

        var party = foundation.Party;
        if (party.CompanionSlots < 1 || party.SummonSlots < 0 || party.PetSlots < 0 ||
            party.TemporaryAllySlots < 0 || party.ServerActorLimit < party.CompanionSlots ||
            party.RoleLimits.Count == 0 || party.RoleLimits.Any(item => !SafeIdentifier(item.Key) ||
                item.Value < 0 || item.Value > party.CompanionSlots))
            Add(diagnostics, "world.companion.party_policy_invalid",
                "Party policy requires bounded companion, summon, pet, ally, role and server slots.",
                $"{foundationPath}/party");
    }

    private static void ValidateCompanionControl(
        CompanionWorldProfile profile,
        string path,
        List<DomainDiagnostic> diagnostics)
    {
        var control = profile.Control;
        var controlPath = $"{path}/control";
        if (control is null)
        {
            if (profile.ControlRequiredAtRuntime)
                Add(diagnostics, "world.companion.control_required",
                    "Required companion control profile is missing.", controlPath);
            return;
        }

        Identity(control.Id, control.SchemaVersion, control.Provenance, controlPath, diagnostics);
        var identities = new[]
        {
            (control.Ownership.Id, control.Ownership.SchemaVersion, control.Ownership.Provenance,
                $"{controlPath}/ownership"),
            (control.Movement.Id, control.Movement.SchemaVersion, control.Movement.Provenance,
                $"{controlPath}/movement"),
            (control.Tactical.Id, control.Tactical.SchemaVersion, control.Tactical.Provenance,
                $"{controlPath}/tactical"),
            (control.Ai.Id, control.Ai.SchemaVersion, control.Ai.Provenance, $"{controlPath}/ai"),
            (control.Equipment.Id, control.Equipment.SchemaVersion, control.Equipment.Provenance,
                $"{controlPath}/equipment"),
        };
        foreach (var identity in identities)
            Identity(identity.Id, identity.SchemaVersion, identity.Provenance, identity.Item4, diagnostics);
        if (identities.Select(item => item.Id).Append(control.Id).Distinct().Count() != identities.Length + 1)
            Add(diagnostics, "world.companion.control_identity_duplicate",
                "Companion control identities must be unique.", controlPath);

        var ownership = control.Ownership;
        var ownershipKinds = new HashSet<string>(["individual", "party", "shared", "ai-only", "game-master"],
            StringComparer.Ordinal);
        if (!ownershipKinds.Contains(ownership.Kind) || ownership.ControllerIds.Count == 0 ||
            ownership.ControllerIds.Distinct(StringComparer.Ordinal).Count() != ownership.ControllerIds.Count ||
            ownership.ControllerIds.Any(item => !SafeIdentifier(item)) || ownership.Revision < 0 ||
            ownership.TransferAllowed && (ownership.AllowedTargetKinds.Count == 0 ||
                ownership.AllowedTargetKinds.Distinct(StringComparer.Ordinal).Count() !=
                ownership.AllowedTargetKinds.Count || ownership.AllowedTargetKinds.Any(item =>
                    !ownershipKinds.Contains(item) || string.Equals(item, ownership.Kind, StringComparison.Ordinal))))
            Add(diagnostics, "world.companion.ownership_invalid",
                "Ownership requires a supported kind, controllers, revision and bounded transfer targets.",
                $"{controlPath}/ownership");

        var movement = control.Movement;
        var movementKinds = new HashSet<string>(["move", "follow", "hold", "rally", "guard", "interact"],
            StringComparer.Ordinal);
        if (!SetEquals(movement.CommandKinds, movementKinds) || !SafeIdentifier(movement.DefaultFormationId) ||
            !SafeIdentifier(movement.WaypointQuery) || movement.MinimumPriority < 0 ||
            movement.MaximumPriority < movement.MinimumPriority)
            Add(diagnostics, "world.companion.movement_commands_invalid",
                "Movement commands require Move, Follow, Hold, Rally, Guard and Interact with bounded priorities.",
                $"{controlPath}/movement");

        var tactical = control.Tactical;
        var tacticalKinds = new HashSet<string>(
            ["attack", "defend", "focus-fire", "protect", "disengage", "use-ability"], StringComparer.Ordinal);
        var stances = new HashSet<string>(["passive", "defensive", "balanced", "aggressive"],
            StringComparer.Ordinal);
        var validationCommands = profile.Validation?.CommandIds;
        if (!SetEquals(tactical.CommandKinds, tacticalKinds) || !SetEquals(tactical.Stances, stances) ||
            tactical.AbilityIds.Count == 0 || tactical.AbilityIds.Distinct(StringComparer.Ordinal).Count() !=
            tactical.AbilityIds.Count || tactical.AbilityIds.Any(item => !SafeIdentifier(item)) ||
            validationCommands is not null && tactical.CommandKinds.Any(command =>
                !validationCommands.Contains(command.Replace("-", string.Empty, StringComparison.Ordinal),
                    StringComparer.Ordinal) && !validationCommands.Contains(command, StringComparer.Ordinal)) ||
            tactical.MinimumPriority < 0 || tactical.MaximumPriority < tactical.MinimumPriority)
            Add(diagnostics, "world.companion.tactical_commands_invalid",
                "Tactical commands require targets, stances, abilities and bounded priorities.",
                $"{controlPath}/tactical");

        var ai = control.Ai;
        if (ai.Autonomy is < 0 or > 1 || ai.RiskTolerance is < 0 or > 1 || ai.ResourceUsage is < 0 or > 1 ||
            ai.FollowDistance < 0 || !SafeIdentifier(ai.Role) || ai.AllowedAbilityTags.Count == 0 ||
            ai.AllowedAbilityTags.Distinct(StringComparer.Ordinal).Count() != ai.AllowedAbilityTags.Count ||
            ai.AllowedAbilityTags.Any(item => !SafeIdentifier(item)) || ai.PlanId == Guid.Empty ||
            ai.TransactionId == Guid.Empty || ai.PlanId == ai.TransactionId ||
            !ai.SemanticDiff.Contains("companion/control/ai", StringComparison.Ordinal) ||
            !SafeIdentifier(ai.AuthorizationLevel))
            Add(diagnostics, "world.companion.ai_control_invalid",
                "Companion AI requires bounded autonomy, risk, resources, role, abilities and governed plan metadata.",
                $"{controlPath}/ai");

        var equipment = control.Equipment;
        var permissions = new HashSet<string>(["none", "view", "suggest", "equip", "full"],
            StringComparer.Ordinal);
        if (!permissions.Contains(equipment.Permission) || !equipment.AllowCompare ||
            !SafeIdentifier(equipment.InventoryOwnerId) || equipment.LockedItemIds.Any(item => item == Guid.Empty) ||
            equipment.LockedItemIds.Distinct().Count() != equipment.LockedItemIds.Count ||
            equipment.SuggestedLoadout.Count == 0 || equipment.SuggestedLoadout.Any(item =>
                !SafeIdentifier(item.Key) || item.Value == Guid.Empty))
            Add(diagnostics, "world.companion.equipment_control_invalid",
                "Companion equipment requires permissions, locks, suggested loadout, comparison and ownership.",
                $"{controlPath}/equipment");
    }

    private static bool SetEquals(IReadOnlyList<string> values, IReadOnlySet<string> expected) =>
        values.Count == expected.Count && values.Distinct(StringComparer.Ordinal).Count() == values.Count &&
        values.All(expected.Contains);

    private static void ValidateCompanionExperience(
        CompanionWorldProfile profile,
        string path,
        List<DomainDiagnostic> diagnostics)
    {
        var experience = profile.Experience;
        var experiencePath = $"{path}/experience";
        if (experience is null)
        {
            if (profile.ExperienceRequiredAtRuntime)
                Add(diagnostics, "world.companion.experience_required",
                    "Required companion experience profile is missing.", experiencePath);
            return;
        }

        Identity(experience.Id, experience.SchemaVersion, experience.Provenance, experiencePath, diagnostics);
        var identities = new[]
        {
            (experience.Death.Id, experience.Death.SchemaVersion, experience.Death.Provenance,
                $"{experiencePath}/death"),
            (experience.Cohesion.Id, experience.Cohesion.SchemaVersion, experience.Cohesion.Provenance,
                $"{experiencePath}/cohesion"),
            (experience.Hud.Id, experience.Hud.SchemaVersion, experience.Hud.Provenance,
                $"{experiencePath}/hud"),
        };
        foreach (var identity in identities)
            Identity(identity.Id, identity.SchemaVersion, identity.Provenance, identity.Item4, diagnostics);
        if (identities.Select(item => item.Id).Append(experience.Id).Distinct().Count() != identities.Length + 1)
            Add(diagnostics, "world.companion.experience_identity_duplicate",
                "Companion experience identities must be unique.", experiencePath);

        var death = experience.Death;
        var deathModes = new HashSet<string>(
            ["downed", "permanent-death", "recoverable-corpse", "resurrection", "replacement", "memorial"],
            StringComparer.Ordinal);
        if (!deathModes.Contains(death.Mode) || death.DownedDurationSeconds < 0 ||
            death.Mode == "downed" && death.DownedDurationSeconds <= 0 ||
            death.Mode == "resurrection" && !SafeIdentifier(death.ResurrectionResourceId ?? string.Empty) ||
            death.Mode == "replacement" && !SafeIdentifier(death.ReplacementCreatureId ?? string.Empty) ||
            death.Mode == "memorial" && !SafeIdentifier(death.MemorialId ?? string.Empty))
            Add(diagnostics, "world.companion.death_experience_invalid",
                "Death experience requires a complete downed, death, corpse, resurrection, replacement or memorial policy.",
                $"{experiencePath}/death");

        var cohesion = experience.Cohesion;
        if (cohesion.NavigateThreshold < 0 || cohesion.PortalThreshold < cohesion.NavigateThreshold ||
            cohesion.TeleportThreshold < cohesion.PortalThreshold ||
            cohesion.AllowSafeTeleport && (cohesion.SafeAnchorTags.Count == 0 ||
                cohesion.SafeAnchorTags.Distinct(StringComparer.Ordinal).Count() != cohesion.SafeAnchorTags.Count ||
                cohesion.SafeAnchorTags.Any(item => !SafeIdentifier(item))))
            Add(diagnostics, "world.companion.cohesion_experience_invalid",
                "Cohesion requires ordered distance thresholds, portal policy and safe teleport anchors.",
                $"{experiencePath}/cohesion");

        var hud = experience.Hud;
        if ((!hud.Keyboard && !hud.Gamepad) || !hud.ScreenReaderLabels || !hud.Portraits || !hud.Health ||
            !hud.Statuses || !hud.QuickCommands || hud.VisibleResourceIds.Count == 0 ||
            hud.VisibleResourceIds.Distinct(StringComparer.Ordinal).Count() != hud.VisibleResourceIds.Count ||
            hud.VisibleResourceIds.Any(item => !SafeIdentifier(item)))
            Add(diagnostics, "world.companion.party_hud_inaccessible",
                "Party HUD requires portraits, health, statuses, commands, accessible labels, navigation and resources.",
                $"{experiencePath}/hud");
    }

    private static bool AreasAreConnected(ProjectDefinition project, IReadOnlyList<AreaId> allowed)
    {
        if (allowed.Count < 2) return false;
        var allowedSet = allowed.ToHashSet();
        var adjacency = allowed.ToDictionary(area => area, _ => new HashSet<AreaId>());
        foreach (var area in project.Areas.Where(area => allowedSet.Contains(area.Id)))
            foreach (var portal in area.Portals.Where(portal => allowedSet.Contains(portal.DestinationAreaId)))
            {
                adjacency[area.Id].Add(portal.DestinationAreaId);
                if (portal.Bidirectional) adjacency[portal.DestinationAreaId].Add(area.Id);
            }

        var visited = new HashSet<AreaId>();
        var pending = new Queue<AreaId>();
        pending.Enqueue(allowed[0]);
        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current)) continue;
            foreach (var neighbor in adjacency[current]) pending.Enqueue(neighbor);
        }
        return visited.SetEquals(allowedSet);
    }

    private static bool SafeIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("..", StringComparison.Ordinal) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or ':' or '/');

    private static void ValidateScripts(ScriptAutomationProfile profile, List<DomainDiagnostic> diagnostics)
    {
        const string path = "module/worldFeatures/scripts";
        Identity(profile.Id, profile.SchemaVersion, profile.Provenance, path, diagnostics);
        if (profile.Camera.Shots.Any(item => item.Id == Guid.Empty || item.DurationSeconds <= 0 || item.BlendSeconds < 0) ||
            string.IsNullOrWhiteSpace(profile.Camera.AbortRecovery))
            Add(diagnostics, "world.script.camera_invalid", "Camera sequences require bounded shots and abort recovery.", $"{path}/camera");
        if (profile.TagRouting.Routes.Any(item => string.IsNullOrWhiteSpace(item.BlueprintTag) ||
            string.IsNullOrWhiteSpace(item.Event) || string.IsNullOrWhiteSpace(item.ScriptId)))
            Add(diagnostics, "world.script.tag_route_invalid", "Tag routes require tag, event and script.", $"{path}/tagRouting");
        if (profile.ModuleFlags.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != profile.ModuleFlags.Count ||
            profile.ModuleFlags.Any(item => string.IsNullOrWhiteSpace(item.Documentation) || item.IntroducedSchemaVersion < 1))
            Add(diagnostics, "world.script.flag_invalid", "Module flags require unique names, documentation and migration version.", $"{path}/moduleFlags");
        if (profile.AiSettings.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != profile.AiSettings.Count ||
            profile.AiSettings.Any(item => string.IsNullOrWhiteSpace(item.Preview)))
            Add(diagnostics, "world.script.ai_setting_invalid", "AI settings require unique names and previews.", $"{path}/aiSettings");
    }

    private static void ValidateEvents(IEnumerable<PlaceableWorldProfile.EventBinding> events, string path,
        List<DomainDiagnostic> diagnostics)
    {
        if (events.Any(item => string.IsNullOrWhiteSpace(item.Event) || string.IsNullOrWhiteSpace(item.ScriptId)))
            Add(diagnostics, "world.event.binding_invalid", "Event bindings require event and script IDs.", $"{path}/events");
    }

    private static void ValidateEvents(IEnumerable<DoorWorldProfile.EventBinding> events, string path,
        List<DomainDiagnostic> diagnostics)
    {
        if (events.Any(item => string.IsNullOrWhiteSpace(item.Event) || string.IsNullOrWhiteSpace(item.ScriptId)))
            Add(diagnostics, "world.event.binding_invalid", "Event bindings require event and script IDs.", $"{path}/events");
    }

    private static void ValidateTargets<T>(IEnumerable<T> profiles, Func<T, Guid> id, Func<T, BlueprintId> target,
        BlueprintKind kind, string segment, IReadOnlyDictionary<BlueprintId, BlueprintDefinition> blueprints,
        List<DomainDiagnostic> diagnostics)
    {
        foreach (var profile in profiles)
        {
            var targetId = target(profile);
            if (!blueprints.TryGetValue(targetId, out var blueprint) || blueprint.Kind != kind)
                Add(diagnostics, "world.reference.blueprint_missing", $"World profile requires a {kind} blueprint.",
                    $"module/worldFeatures/{segment}/{id(profile)}/blueprintId");
        }
    }

    private static void ValidateCreatureTargets<T>(IEnumerable<T> profiles, Func<T, Guid> id,
        Func<T, BlueprintId> target, string segment, IReadOnlyDictionary<BlueprintId, BlueprintDefinition> blueprints,
        List<DomainDiagnostic> diagnostics)
    {
        foreach (var profile in profiles)
        {
            if (!blueprints.TryGetValue(target(profile), out var blueprint) ||
                blueprint.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
                Add(diagnostics, "world.reference.blueprint_missing", "World profile requires a creature blueprint.",
                    $"module/worldFeatures/{segment}/{id(profile)}/blueprintId");
        }
    }

    private static void Identity(Guid id, int schemaVersion, string provenance, string path,
        List<DomainDiagnostic> diagnostics)
    {
        if (id == Guid.Empty || schemaVersion != 1 || string.IsNullOrWhiteSpace(provenance))
            Add(diagnostics, "world.profile.identity_invalid", "Profile ID, schema and provenance are required.", path);
    }

    private static void Add(List<DomainDiagnostic> diagnostics, string code, string message, string path) =>
        diagnostics.Add(new DomainDiagnostic(code, message, path));

    private static bool Finite(Forge.Domain.Spatial.Vector3Value value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
