using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
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
        var quests = project.Quests.Select(item => item.Id.Value).ToHashSet();

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
            if (profile.Credential is { } credential && (string.IsNullOrWhiteSpace(credential.CredentialId) || credential.Charges < 0 ||
                credential.Consumption == PlaceableWorldProfile.CredentialConsumption.Charge && credential.Charges < 1))
                Add(diagnostics, "world.placeable.credential_invalid", "Credential identity and charge policy are invalid.", $"{path}/credential");
            ValidateEvents(profile.Reactive.Events, path, diagnostics);
            if (profile.Reactive.Scheduler.Enabled && (profile.Reactive.Scheduler.IntervalSeconds <= 0 ||
                profile.Reactive.Scheduler.BudgetPerMinute < 1 || string.IsNullOrWhiteSpace(profile.Reactive.Scheduler.CustomNamespace)))
                Add(diagnostics, "world.placeable.scheduler_invalid", "Enabled scheduler requires cadence, budget and namespace.", $"{path}/reactive/scheduler");
            if (profile.Physics is { } physics && (physics.Mass <= 0 || physics.SleepAfterSeconds < 0 ||
                string.IsNullOrWhiteSpace(physics.Authority)))
                Add(diagnostics, "world.placeable.physics_invalid", "Dynamic physics requires mass, sleep policy and authority.", $"{path}/physics");
            ValidatePlaceableFoundation(profile, path, diagnostics);
        }

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
        }

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
            if (profile.Clue?.QuestId is { } questId && !quests.Contains(questId))
                Add(diagnostics, "world.reference.quest_missing", "Trigger clue quest reference is missing.", $"{path}/clue/questId");
            if (profile.Narrative is { } narrative && !dialogues.Contains(narrative.DialogueId))
                Add(diagnostics, "world.reference.dialogue_missing", "Narrative trigger dialogue reference is missing.", $"{path}/narrative/dialogueId");
            ValidateTriggerFoundation(profile, path, diagnostics);
        }

        foreach (var profile in catalog.Encounters)
        {
            var path = $"module/worldFeatures/encounters/{profile.Id}";
            Identity(profile.Id, profile.SchemaVersion, profile.Provenance, path, diagnostics);
            if (!areas.Contains(profile.AreaId))
                Add(diagnostics, "world.reference.area_missing", "Encounter area reference is missing.", $"{path}/areaId");
            if (profile.Respawn is { } respawn && (respawn.DelaySeconds < 0 || respawn.MaximumCycles < 0 || respawn.AntiFarmMultiplier is < 0 or > 1))
                Add(diagnostics, "world.encounter.respawn_invalid", "Respawn delay, cycles and anti-farm multiplier are invalid.", $"{path}/respawn");
            if (profile.Scheduler is { } scheduler && scheduler.HeartbeatEnabled &&
                (scheduler.IntervalSeconds <= 0 || scheduler.BudgetPerMinute < 1 || string.IsNullOrWhiteSpace(scheduler.CustomEventNamespace)))
                Add(diagnostics, "world.encounter.scheduler_invalid", "Encounter heartbeat requires cadence, budget and namespace.", $"{path}/scheduler");
            if (profile.Reinforcements is { } reinforcement && (reinforcement.RouteId == Guid.Empty ||
                reinforcement.MaximumActors < 1 || reinforcement.DelaySeconds < 0))
                Add(diagnostics, "world.encounter.reinforcement_invalid", "Reinforcements require route, cap and delay.", $"{path}/reinforcements");
            if (profile.Visibility.MinimumCameraDistance < 0)
                Add(diagnostics, "world.encounter.visibility_invalid", "Visibility distance cannot be negative.", $"{path}/visibility");
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
            if (!geometryValid || volume.ThreatRadius < 0 ||
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
                if (candidate.Weight <= 0 || candidate.Cost <= 0 || candidate.MaximumAlive <= 0 ||
                    candidate.Conditions.Any(string.IsNullOrWhiteSpace) || candidate.Variants.Any(string.IsNullOrWhiteSpace))
                    Add(diagnostics, "world.encounter.candidate_invalid",
                        "Candidate weight, role cost, conditions, variants and alive limit are invalid.", candidatePath);
            }
            if (foundation.SpawnBudget <= 0 ||
                foundation.Candidates.Count > 0 && foundation.Candidates.All(item => item.Cost > foundation.SpawnBudget))
                Add(diagnostics, "world.encounter.budget_invalid",
                    "Spawn budget must be positive and admit at least one candidate.", $"{foundationPath}/spawnBudget");
            if (foundation.SpawnAnchorIds.Count == 0 ||
                foundation.SpawnAnchorIds.Any(string.IsNullOrWhiteSpace) ||
                foundation.SpawnAnchorIds.Distinct(StringComparer.Ordinal).Count() != foundation.SpawnAnchorIds.Count)
                Add(diagnostics, "world.encounter.anchors_invalid",
                    "Encounter requires unique reachable spawn anchors.", $"{foundationPath}/spawnAnchorIds");
            if (foundation.RewardIds.Any(string.IsNullOrWhiteSpace))
                Add(diagnostics, "world.encounter.rewards_invalid",
                    "Encounter reward references cannot be empty.", $"{foundationPath}/rewardIds");
            if (foundation.NetworkActorCap <= 0 ||
                foundation.Candidates.Any(item => item.MaximumAlive > foundation.NetworkActorCap))
                Add(diagnostics, "world.encounter.network_cap_invalid",
                    "Candidate limits must fit the authoritative network actor cap.", $"{foundationPath}/networkActorCap");
        }

        foreach (var route in catalog.VehicleRoutes)
        {
            var path = $"module/worldFeatures/vehicleRoutes/{route.Id}";
            Identity(route.Id, route.SchemaVersion, route.Provenance, path, diagnostics);
            if (!areas.Contains(route.AreaId))
                Add(diagnostics, "world.reference.area_missing", "Vehicle route area reference is missing.", $"{path}/areaId");
            if (route.Points.Count < 2 || route.Lanes.Count == 0 || route.DefaultSpeedLimit <= 0 ||
                route.Points.Any(item => item.Width <= 0) || route.Lanes.Any(item => item.SpeedLimit <= 0))
                Add(diagnostics, "world.vehicle_route.invalid", "Vehicle route requires points, lanes, width and speed limits.", path);
            var pointIds = route.Points.Select(item => item.Id).ToHashSet();
            if (route.Stops.Any(item => !pointIds.Contains(item.RoutePointId) || item.WaitSeconds < 0))
                Add(diagnostics, "world.vehicle_route.stop_invalid", "Vehicle stops must reference route points and non-negative waits.", $"{path}/stops");
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
        if (transform.Scale.X is <= .01f or > 100 || transform.Scale.Y is <= .01f or > 100 ||
            transform.Scale.Z is <= .01f or > 100 || !double.IsFinite(transform.SnapIncrement) ||
            transform.SnapIncrement is < 0 or > 10000 || transform.ParentId == profile.Id ||
            new[] { transform.Position.X, transform.Position.Y, transform.Position.Z,
                transform.Pivot.X, transform.Pivot.Y, transform.Pivot.Z }.Any(value => Math.Abs(value) > 1_000_000))
            Add(diagnostics, "world.placeable.transform_invalid",
                "Transform scale, pivot, position, snap and parent must remain finite and bounded.", $"{foundationPath}/transform");
        if (foundation.Components.ScriptBindings.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            Add(diagnostics, "world.placeable.scripts_invalid",
                "Placeable script event and route bindings are required.", $"{foundationPath}/components/scriptBindings");
        ValidatePlaceableRuntimeContracts(foundation, foundationPath, diagnostics);
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
            return;
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

    private static void ValidateCompanion(
        ProjectDefinition project,
        CompanionWorldProfile profile,
        IReadOnlySet<Guid> dialogues,
        IReadOnlySet<AreaId> areas,
        string path,
        List<DomainDiagnostic> diagnostics)
    {
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
}
