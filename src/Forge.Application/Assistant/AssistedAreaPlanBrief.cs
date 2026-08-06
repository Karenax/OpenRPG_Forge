namespace Forge.Application.Assistant;

public sealed record AssistedAreaPlanBrief(
    Guid PlanId,
    Guid AreaId,
    string Title,
    Forge.Domain.Assistant.AssistedAreaArchetype Archetype,
    int Seed,
    float WidthMeters,
    float DepthMeters,
    int NodeCount,
    IReadOnlyList<Guid> KitBlueprintIds,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Objectives,
    IReadOnlyList<string> LoreSourceIds,
    string Provenance);
