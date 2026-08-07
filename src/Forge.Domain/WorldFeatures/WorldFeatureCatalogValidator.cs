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
        }

        ValidateScripts(catalog.Scripts, diagnostics);
        return diagnostics;
    }

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
