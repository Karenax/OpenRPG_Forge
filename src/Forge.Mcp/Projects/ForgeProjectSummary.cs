namespace Forge.Mcp.Projects;

public sealed record ForgeProjectSummary(
    string ProjectId,
    string Title,
    string ModuleId,
    string ModuleSlug,
    string ModuleTitle,
    string EntryAreaId,
    long Revision,
    int BlueprintCount,
    int AreaCount,
    int EntityCount,
    int DialogueCount,
    int QuestCount);
