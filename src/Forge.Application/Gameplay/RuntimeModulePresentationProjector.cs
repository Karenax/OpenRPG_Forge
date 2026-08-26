using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Items;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.Application.Gameplay;

public static class RuntimeModulePresentationProjector
{
    public static RuntimeModulePresentation Project(
        ProjectDefinition project,
        AreaId areaId,
        RuntimeModulePresentationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(areaId);
        context ??= new RuntimeModulePresentationContext();

        var area = project.Areas.SingleOrDefault(candidate => candidate.Id == areaId)
            ?? throw new KeyNotFoundException($"Area '{areaId}' is not part of the project.");
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var playerEntity = ResolvePlayerEntity(area, blueprints, context.PlayerEntityId);
        var playerBlueprint = playerEntity is not null && blueprints.TryGetValue(playerEntity.BlueprintId, out var value)
            ? value
            : project.Blueprints.FirstOrDefault(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn);
        var locale = project.Module.Metadata.DefaultLocale;
        var hero = ProjectActor(playerBlueprint, playerEntity, locale, "Héros", context.RuntimePlayerResources);
        var party = ProjectParty(project, area, blueprints, context.PartyBlueprintIds, locale);
        var inventory = ProjectInventory(project, playerBlueprint, context, locale);
        var progression = ProjectProgression(playerBlueprint, context);
        var journal = ProjectJournal(project.Quests, context, locale);
        var description = ProjectDescription(project);
        var map = ProjectMap(project, area, context, journal.ActiveObjective, description);

        return new RuntimeModulePresentation(
            project.Module.DisplayName,
            area.DisplayName,
            description,
            hero with { Level = progression.Level },
            party,
            inventory,
            progression,
            journal,
            map);
    }

    private static EntityInstance? ResolvePlayerEntity(
        AreaDefinition area,
        IReadOnlyDictionary<BlueprintId, BlueprintDefinition> blueprints,
        EntityId? requestedId)
    {
        if (requestedId is not null)
        {
            return area.Entities.SingleOrDefault(entity => entity.Id == requestedId)
                ?? throw new KeyNotFoundException($"Player entity '{requestedId}' is not part of area '{area.Id}'.");
        }

        return area.Entities.FirstOrDefault(entity =>
            blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
            blueprint.Kind == BlueprintKind.PlayerSpawn);
    }

    private static RuntimeModulePresentation.Actor ProjectActor(
        BlueprintDefinition? blueprint,
        EntityInstance? entity,
        string locale,
        string neutralName,
        IReadOnlyList<RuntimeActorResourceSnapshot>? runtimeResources = null)
    {
        var overrideName = entity?.Overrides.Properties.SingleOrDefault(property =>
            string.Equals(property.Key, "runtime.displayName", StringComparison.Ordinal))?.Value;
        var overridePortrait = entity?.Overrides.Properties.SingleOrDefault(property =>
            string.Equals(property.Key, "runtime.portrait", StringComparison.Ordinal))?.Value;
        var overridePortraitAlt = entity?.Overrides.Properties.SingleOrDefault(property =>
            string.Equals(property.Key, "runtime.portraitAlt", StringComparison.Ordinal))?.Value;
        var authoredName = ResolveLocalizedName(blueprint?.CreatureFoundation?.Identity.Names, locale);
        var displayName = FirstNonEmpty(overrideName, authoredName, blueprint?.DisplayName, neutralName)!;
        var portrait = blueprint?.CreaturePresentation?.Portraits
            .OrderBy(item => string.Equals(item.Expression, "neutral", StringComparison.Ordinal) ? 0 : 1)
            .ThenByDescending(item => item.Width * item.Height)
            .FirstOrDefault();
        var role = blueprint?.CreatureProgression?.Tracks.FirstOrDefault()?.DisplayName;
        var resources = runtimeResources is null
            ? blueprint?.CreatureFoundation?.Resources
                .Select(resource => new RuntimeModulePresentation.Resource(
                    resource.ResourceId, resource.Current, resource.Maximum))
                .ToArray() ?? []
            : runtimeResources
                .OrderBy(resource => resource.Id, StringComparer.Ordinal)
                .Select(resource => new RuntimeModulePresentation.Resource(
                    resource.Id, resource.Current, resource.Maximum))
                .ToArray();

        return new RuntimeModulePresentation.Actor(
            entity?.Id.ToString() ?? blueprint?.Id.ToString() ?? string.Empty,
            displayName,
            FirstNonEmpty(role),
            FirstNonEmpty(overridePortrait, portrait?.AssetPath),
            FirstNonEmpty(overridePortraitAlt, portrait?.AltText) ?? $"Portrait neutre de {displayName}",
            blueprint?.CreatureProgression?.Tracks.FirstOrDefault()?.Level,
            resources);
    }

    private static IReadOnlyList<RuntimeModulePresentation.Actor> ProjectParty(
        ProjectDefinition project,
        AreaDefinition area,
        IReadOnlyDictionary<BlueprintId, BlueprintDefinition> blueprints,
        IReadOnlyList<BlueprintId>? partyBlueprintIds,
        string locale)
    {
        if (partyBlueprintIds is null || partyBlueprintIds.Count == 0) return [];

        return partyBlueprintIds
            .Distinct()
            .Select(blueprintId =>
            {
                if (!blueprints.TryGetValue(blueprintId, out var blueprint))
                    throw new KeyNotFoundException($"Party blueprint '{blueprintId}' is not part of project '{project.Id}'.");
                var entity = area.Entities.FirstOrDefault(candidate => candidate.BlueprintId == blueprintId);
                return ProjectActor(blueprint, entity, locale, "Compagnon");
            })
            .ToArray();
    }

    private static RuntimeModulePresentation.InventoryView ProjectInventory(
        ProjectDefinition project,
        BlueprintDefinition? playerBlueprint,
        RuntimeModulePresentationContext context,
        string locale)
    {
        var blueprintById = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var rulesInventory = playerBlueprint?.CreatureFoundation?.RulesComposition?.Inventory;
        var items = new List<RuntimeModulePresentation.InventoryItem>();
        var authoredUnitWeights = new Dictionary<Guid, double>();
        var capacity = 0;
        var maximumWeight = 0d;
        if (rulesInventory is not null)
        {
            foreach (var container in rulesInventory.Containers)
            {
                capacity += container.SlotCount;
                maximumWeight += container.MaximumWeight;
                foreach (var entry in container.Items)
                {
                    blueprintById.TryGetValue(entry.ItemBlueprintId, out var itemBlueprint);
                    items.Add(ProjectInventoryItem(entry.Id, entry.ItemBlueprintId.ToString(), itemBlueprint,
                        entry.Quantity, container.ContainerId, locale));
                    authoredUnitWeights[entry.Id] = entry.UnitWeight;
                }
            }
        }

        var runtimeStacks = (context.RuntimeInventory ?? [])
            .GroupBy(stack => stack.Slug, StringComparer.Ordinal)
            .Select(group => new RuntimeInventoryStack(
                group.Key,
                group.Aggregate(0, (quantity, stack) => checked(quantity + stack.Quantity))))
            .OrderBy(stack => stack.Slug, StringComparer.Ordinal);
        foreach (var runtimeStack in runtimeStacks)
        {
            var itemBlueprint = project.Blueprints.FirstOrDefault(blueprint =>
                blueprint.Kind == BlueprintKind.Item &&
                string.Equals(blueprint.Slug, runtimeStack.Slug, StringComparison.Ordinal));
            var authoredIndex = itemBlueprint is null
                ? -1
                : items.FindIndex(item =>
                    string.Equals(item.BlueprintId, itemBlueprint.Id.ToString(), StringComparison.Ordinal));
            if (authoredIndex >= 0)
            {
                var authored = items[authoredIndex];
                items[authoredIndex] = authored with { Quantity = authored.Quantity + runtimeStack.Quantity };
                continue;
            }
            items.Add(ProjectInventoryItem(
                itemBlueprint?.Id.Value ?? StableRuntimeInventoryId(runtimeStack.Slug),
                itemBlueprint?.Id.ToString() ?? string.Empty,
                itemBlueprint,
                runtimeStack.Quantity,
                "runtime",
                locale,
                fallbackName: runtimeStack.Slug));
        }

        var entriesById = items.ToDictionary(item => item.InstanceId);
        var equipment = rulesInventory?.EquipmentSlots
            .Where(pair => entriesById.ContainsKey(pair.Value))
            .Select(pair => new RuntimeModulePresentation.EquipmentItem(pair.Key, entriesById[pair.Value]))
            .ToArray() ?? [];
        var currencies = (context.CurrencyBalances ?? new Dictionary<string, decimal>())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new RuntimeModulePresentation.Currency(pair.Key, pair.Key, pair.Value))
            .ToArray();
        var blueprintByStringId = project.Blueprints.ToDictionary(
            blueprint => blueprint.Id.ToString(),
            StringComparer.Ordinal);
        var weight = items.Sum(item => ResolveInventoryUnitWeight(
            item, blueprintByStringId, authoredUnitWeights) * item.Quantity);

        return new RuntimeModulePresentation.InventoryView(
            items.OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.InstanceId).ToArray(),
            equipment,
            currencies,
            items.Count,
            capacity,
            weight,
            maximumWeight);
    }

    private static RuntimeModulePresentation.InventoryItem ProjectInventoryItem(
        Guid instanceId,
        string blueprintId,
        BlueprintDefinition? blueprint,
        int quantity,
        string containerId,
        string locale,
        string? fallbackName = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        var text = ResolveItemText(blueprint?.ItemFoundation, locale);
        return new RuntimeModulePresentation.InventoryItem(
            instanceId,
            blueprintId,
            FirstNonEmpty(text?.Name, blueprint?.DisplayName, fallbackName, "Objet")!,
            FirstNonEmpty(text?.TypeLabel, blueprint?.ItemFoundation?.Type.Slug, "Objet")!,
            FirstNonEmpty(text?.LongDescription, text?.ShortDescription, "Aucune description renseignée.")!,
            quantity,
            blueprint?.ItemFoundation?.BaseValue ?? 0,
            containerId);
    }

    private static double ResolveInventoryUnitWeight(
        RuntimeModulePresentation.InventoryItem item,
        IReadOnlyDictionary<string, BlueprintDefinition> blueprintByStringId,
        IReadOnlyDictionary<Guid, double> authoredUnitWeights)
    {
        if (blueprintByStringId.TryGetValue(item.BlueprintId, out var blueprint) &&
            blueprint.ItemFoundation?.Creation?.Inventory is { } governedInventory)
        {
            return governedInventory.Mass;
        }

        if (authoredUnitWeights.TryGetValue(item.InstanceId, out var authoredUnitWeight))
        {
            return authoredUnitWeight;
        }

        return 0;
    }

    private static Guid StableRuntimeInventoryId(string slug)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-runtime-inventory:{slug}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static ItemFoundationText? ResolveItemText(ItemFoundationProfile? profile, string locale) =>
        profile?.Texts.FirstOrDefault(item => string.Equals(item.Locale, locale, StringComparison.OrdinalIgnoreCase))
        ?? profile?.Texts.FirstOrDefault();

    private static RuntimeModulePresentation.ProgressionView ProjectProgression(
        BlueprintDefinition? playerBlueprint,
        RuntimeModulePresentationContext context)
    {
        var profile = playerBlueprint?.CreatureProgression;
        var tracks = context.RuntimeProgressionTracks is null
            ? profile?.Tracks.Select(track => new RuntimeModulePresentation.ProgressionTrack(
                track.TrackId, track.DisplayName, track.Level, track.MaximumLevel, track.Experience)).ToArray() ?? []
            : context.RuntimeProgressionTracks
                .OrderBy(track => track.Id, StringComparer.Ordinal)
                .Select(track => new RuntimeModulePresentation.ProgressionTrack(
                    track.Id, track.DisplayName, track.Level, track.MaximumLevel, track.Experience))
                .ToArray();
        var attributes = playerBlueprint?.CreatureFoundation?.RulesComposition?.Attributes.BaseValues
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new RuntimeModulePresentation.NamedValue(pair.Key, pair.Key, pair.Value, null))
            .ToArray() ?? [];
        var skills = profile?.Skills.Select(skill => new RuntimeModulePresentation.NamedValue(
            skill.SkillId, skill.DisplayName, skill.Rank, skill.MaximumRank)).ToArray() ?? [];
        var talents = profile?.Talents.Select(talent => new RuntimeModulePresentation.NamedValue(
            talent.TalentId, talent.DisplayName, talent.Rank, talent.MaximumRank)).ToArray() ?? [];

        var liveTracksProvided = context.RuntimeProgressionTracks is not null;
        return new RuntimeModulePresentation.ProgressionView(
            liveTracksProvided ? tracks.FirstOrDefault()?.Level : context.RuntimeLevel ?? tracks.FirstOrDefault()?.Level,
            liveTracksProvided
                ? tracks.FirstOrDefault()?.Experience
                : context.RuntimeExperience ?? tracks.FirstOrDefault()?.Experience,
            tracks,
            attributes,
            skills,
            talents);
    }

    private static RuntimeModulePresentation.JournalView ProjectJournal(
        IReadOnlyList<QuestDefinition> quests,
        RuntimeModulePresentationContext context,
        string locale)
    {
        var states = (context.QuestStates ?? [])
            .GroupBy(snapshot => snapshot.QuestId)
            .ToDictionary(
                group => group.Key,
                group => group.Count() == 1
                    ? group.Single()
                    : throw new InvalidOperationException($"Quest '{group.Key}' has more than one runtime state."));
        var knownQuestIds = quests.Select(quest => quest.Id).ToHashSet();
        var unknownQuest = states.Keys.FirstOrDefault(questId => !knownQuestIds.Contains(questId));
        if (unknownQuest is not null)
            throw new InvalidOperationException($"Runtime quest '{unknownQuest}' is not defined by the project.");

        var entries = quests
            .Where(quest => states.TryGetValue(quest.Id, out var snapshot) && snapshot.IsAccepted)
            .OrderByDescending(quest => quest.Priority)
            .ThenBy(quest => quest.Id.ToString(), StringComparer.Ordinal)
            .Select(quest => ProjectQuest(quest, states.GetValueOrDefault(quest.Id), context.TrackedQuestId, locale))
            .ToArray();
        var active = entries.SingleOrDefault(entry => entry.IsActive);
        return new RuntimeModulePresentation.JournalView(
            entries,
            active,
            active?.Objectives.FirstOrDefault() ?? active?.Summary);
    }

    private static RuntimeModulePresentation.QuestEntry ProjectQuest(
        QuestDefinition quest,
        RuntimeQuestStateSnapshot? runtimeState,
        QuestId? trackedQuestId,
        string locale)
    {
        var stateId = runtimeState?.StateId ?? quest.InitialStateId;
        var state = quest.States.SingleOrDefault(candidate => candidate.Id == stateId)
            ?? throw new InvalidOperationException(
                $"Runtime state '{stateId}' is not defined by quest '{quest.Id}'.");
        return new RuntimeModulePresentation.QuestEntry(
            quest.Id.ToString(),
            ResolveLocalized(quest.Title, locale),
            ResolveLocalized(quest.Description, locale),
            quest.Category,
            state.DisplayName,
            ResolveLocalized(state.Journal.Summary, locale),
            ResolveLocalized(state.Journal.Details, locale),
            state.Journal.Objectives.Select(objective => ResolveLocalized(objective, locale)).ToArray(),
            runtimeState?.IsAccepted == true,
            trackedQuestId == quest.Id,
            state.TerminalKind);
    }

    private static RuntimeModulePresentation.MapView ProjectMap(
        ProjectDefinition project,
        AreaDefinition area,
        RuntimeModulePresentationContext context,
        string? objective,
        string? description)
    {
        var weatherProfile = area.Environment.Weather;
        var weatherId = context.WeatherStateId ?? weatherProfile.InitialStateId;
        var weather = weatherProfile.States.SingleOrDefault(state => state.Id == weatherId)?.Name;
        var map = area.Authoring.Map;
        var grid = map.Enabled
            ? new AreaMapExplorationGrid(area.Dimensions, map.FloorCount, map.ExplorationCellSize)
            : null;
        var totalCells = grid?.TotalCells ?? 0;
        double? explorationPercent = null;
        var revealedCells = 0;
        var activeFloor = context.Exploration?.ActiveFloor ?? 0;
        IReadOnlyList<Forge.Domain.Validation.DomainDiagnostic> explorationDiagnostics = [];
        if (context.Exploration is not null)
        {
            var resolution = AreaExperienceAuthoringScriptService.Normalize(area, context.Exploration);
            activeFloor = resolution.State.ActiveFloor;
            if (activeFloor < 0 || activeFloor >= map.FloorCount)
                throw new InvalidOperationException($"Map floor '{activeFloor}' is outside the active area policy.");
            revealedCells = resolution.State.RevealedCells.Count;
            explorationPercent = totalCells == 0
                ? 0
                : Math.Clamp(revealedCells * 100d / totalCells, 0, 100);
            explorationDiagnostics = resolution.Diagnostics;
        }

        var markers = new List<RuntimeModulePresentation.MapMarker>();
        foreach (var pin in map.Pins.Where(pin => pin.InitiallyVisible ||
                     context.Exploration?.RevealedPins.Contains(pin.Id) == true).Where(pin =>
                     pin.Floor == activeFloor))
        {
            markers.Add(new(pin.Id.ToString("D"), pin.Label, "pin", pin.X, pin.Z, pin.Floor, false));
        }

        foreach (var waypoint in area.Navigation.Waypoints.Waypoints.Where(waypoint =>
                     waypoint.Appearance.Visible && waypoint.Marker is not null &&
                     (waypoint.Marker.VisibilityPolicy == MarkerVisibilityPolicy.Always ||
                      waypoint.Marker.Revealed || waypoint.Marker.Tracked) &&
                     (waypoint.Marker.Floor < 0 || waypoint.Marker.Floor == activeFloor)))
        {
            markers.Add(new(waypoint.Id.ToString("D"), waypoint.Marker!.Text, "waypoint",
                waypoint.Position.X, waypoint.Position.Z, waypoint.Marker.Floor, waypoint.Marker.Tracked));
        }

        if (activeFloor == 0)
        {
            foreach (var portal in area.Portals)
            {
                var source = area.Entities.SingleOrDefault(entity => entity.Id == portal.SourceEntityId);
                if (source is not null)
                {
                    markers.Add(new(portal.Id.ToString(), portal.DisplayName, "portal",
                        source.Transform.Position.X, source.Transform.Position.Z, 0, false));
                }
            }
        }

        var portals = project.Areas.SelectMany(sourceArea => sourceArea.Portals.Select(portal =>
            new RuntimeModulePresentation.MapPortal(
                portal.Id.ToString(), portal.DisplayName, sourceArea.Id.ToString(), portal.DestinationAreaId.ToString())))
            .OrderBy(portal => portal.SourceAreaId, StringComparer.Ordinal)
            .ThenBy(portal => portal.Id, StringComparer.Ordinal)
            .ToArray();
        var patrolRoutes = area.Navigation.Waypoints.PatrolRoutes
            .Select(route => new
            {
                Route = route,
                Floor = ResolveRouteFloor(route, area.Navigation.Waypoints.Waypoints),
            })
            .Where(item => item.Floor < 0 || item.Floor == activeFloor)
            .Select(item => new RuntimeModulePresentation.MapPatrolRoute(
                item.Route.Id.ToString("D"),
                item.Floor,
                item.Route.Points
                    .Select(point => new RuntimeModulePresentation.MapPoint(point.Position.X, point.Position.Z))
                    .ToArray()))
            .ToArray();

        return new RuntimeModulePresentation.MapView(
            area.Id.ToString(),
            area.Slug,
            area.DisplayName,
            description,
            project.Areas.Count,
            portals.Length,
            patrolRoutes.Length,
            map.Pins.Count,
            area.Navigation.Waypoints.Waypoints.Count,
            FirstNonEmpty(weather),
            FirstNonEmpty(objective),
            explorationPercent,
            revealedCells,
            totalCells,
            activeFloor,
            map.FloorCount,
            area.Dimensions.Width,
            area.Dimensions.Depth,
            project.Areas.OrderBy(candidate => candidate.Id.ToString(), StringComparer.Ordinal)
                .Select(candidate => new RuntimeModulePresentation.MapArea(
                    candidate.Id.ToString(), candidate.DisplayName, candidate.Id == area.Id)).ToArray(),
            portals,
            markers.OrderBy(marker => marker.Kind, StringComparer.Ordinal).ThenBy(marker => marker.Label,
                StringComparer.Ordinal).ToArray(),
            patrolRoutes,
            explorationDiagnostics);
    }

    private static int ResolveRouteFloor(
        PatrolRoute route,
        IReadOnlyList<WaypointProfile> waypoints)
    {
        var pointIds = route.Points.Select(point => point.Id).ToHashSet();
        var floors = waypoints
            .Where(waypoint => pointIds.Contains(waypoint.Id) && waypoint.Marker is not null)
            .Select(waypoint => waypoint.Marker!.Floor)
            .Distinct()
            .ToArray();
        return floors.Length == 1 ? floors[0] : -1;
    }

    private static string? ProjectDescription(ProjectDefinition project)
    {
        var localization = project.Module.Metadata.GetDefaultLocalization();
        return FirstNonEmpty(localization.Description, localization.Summary, project.Module.Description);
    }

    private static string ResolveLocalized(LocalizedText value, string locale) =>
        value.Variants.FirstOrDefault(variant =>
            string.Equals(variant.Locale, locale, StringComparison.OrdinalIgnoreCase))?.Text
        ?? value.Fallback;

    private static string? ResolveLocalizedName(IReadOnlyDictionary<string, string>? names, string locale)
    {
        if (names is null || names.Count == 0) return null;
        return names.TryGetValue(locale, out var localized) && !string.IsNullOrWhiteSpace(localized)
            ? localized
            : names.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
