using Forge.Application.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Quests;

namespace Forge.Application.Gameplay;

public sealed record RuntimeModulePresentationContext(
    EntityId? PlayerEntityId = null,
    IReadOnlyList<BlueprintId>? PartyBlueprintIds = null,
    IReadOnlyCollection<RuntimeInventoryStack>? RuntimeInventory = null,
    IReadOnlyList<RuntimeActorResourceSnapshot>? RuntimePlayerResources = null,
    IReadOnlyList<RuntimeProgressionTrackSnapshot>? RuntimeProgressionTracks = null,
    IReadOnlyDictionary<string, decimal>? CurrencyBalances = null,
    Guid? WeatherStateId = null,
    IReadOnlyList<RuntimeQuestStateSnapshot>? QuestStates = null,
    QuestId? TrackedQuestId = null,
    AreaMapExplorationState? Exploration = null,
    int? RuntimeLevel = null,
    double? RuntimeExperience = null);
