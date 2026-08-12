using Forge.Application.Commands;

namespace Forge.Mcp.Projects;

public sealed record EditorProductivityResource(
    int SchemaVersion,
    long Revision,
    EditorProductivityProfileItem ActiveProfile,
    IReadOnlyList<EditorProductivityProfileItem> Profiles,
    IReadOnlyList<EditorHelpTopicItem> HelpTopics,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record EditorProductivityProfileItem(
    string Id,
    string Name,
    string Role,
    IReadOnlyList<string> VisibleKinds,
    IReadOnlyList<string> VisibleLayerIds,
    IReadOnlyList<string> VisibleVolumeKinds,
    bool IsolateSelection,
    bool GhostHidden,
    bool BrushMode,
    bool ContinuousStroke,
    string Distribution,
    string Alignment,
    double Spacing,
    bool SplitView,
    string SplitAxis,
    string? ActiveAreaId,
    string? ComparisonAreaId,
    bool ContextualHelp,
    bool SourcedAssistant,
    IReadOnlyList<string> Panels,
    IReadOnlyList<string> Permissions);

public sealed record EditorHelpTopicItem(string Key, string Title, string Description,
    IReadOnlyList<string> Dependencies, IReadOnlyList<string> Examples, IReadOnlyList<string> Citations);

public sealed record EditorProductivityPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string Role,
    IReadOnlyList<string> VisibleLayerIds,
    IReadOnlyList<string> VisibleVolumeKinds,
    bool BrushMode,
    bool ContinuousStroke,
    string Distribution,
    string Alignment,
    double Spacing,
    int Columns,
    int Seed,
    double SurfaceOffset,
    bool SplitView,
    string SplitAxis,
    double SplitRatio,
    string? ActiveAreaId,
    string? ComparisonAreaId,
    bool ContextualHelp,
    bool SourcedAssistant,
    string ActiveHelpTopic);

public sealed record EditorProductivityAuditEntry(string PlanId, string Operation, long Revision,
    DateTimeOffset Timestamp, bool Succeeded);

public sealed record EditorProductivityPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    EditorProductivityProfileItem? Preview,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<EditorProductivityAuditEntry> Audit);
