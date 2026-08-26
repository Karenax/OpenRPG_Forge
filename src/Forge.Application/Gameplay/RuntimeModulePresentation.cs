using Forge.Domain.Quests;
using Forge.Domain.Validation;

namespace Forge.Application.Gameplay;

public sealed record RuntimeModulePresentation(
    string ModuleTitle,
    string AreaTitle,
    string? Description,
    RuntimeModulePresentation.Actor Hero,
    IReadOnlyList<RuntimeModulePresentation.Actor> Party,
    RuntimeModulePresentation.InventoryView Inventory,
    RuntimeModulePresentation.ProgressionView Progression,
    RuntimeModulePresentation.JournalView Journal,
    RuntimeModulePresentation.MapView Map)
{
    public sealed record Actor(
        string Id,
        string DisplayName,
        string? Role,
        string? PortraitPath,
        string PortraitAltText,
        int? Level,
        IReadOnlyList<Resource> Resources);

    public sealed record Resource(string Id, double Current, double Maximum);

    public sealed record InventoryView(
        IReadOnlyList<InventoryItem> Items,
        IReadOnlyList<EquipmentItem> Equipment,
        IReadOnlyList<Currency> Currencies,
        int OccupiedSlots,
        int Capacity,
        double Weight,
        double MaximumWeight);

    public sealed record InventoryItem(
        Guid InstanceId,
        string BlueprintId,
        string Name,
        string Type,
        string Description,
        int Quantity,
        decimal UnitValue,
        string ContainerId);

    public sealed record EquipmentItem(string Slot, InventoryItem Item);

    public sealed record Currency(string Id, string DisplayName, decimal Amount);

    public sealed record ProgressionView(
        int? Level,
        double? Experience,
        IReadOnlyList<ProgressionTrack> Tracks,
        IReadOnlyList<NamedValue> Attributes,
        IReadOnlyList<NamedValue> Skills,
        IReadOnlyList<NamedValue> Talents);

    public sealed record ProgressionTrack(
        string Id,
        string DisplayName,
        int Level,
        int MaximumLevel,
        double Experience);

    public sealed record NamedValue(string Id, string DisplayName, double Value, double? Maximum);

    public sealed record JournalView(
        IReadOnlyList<QuestEntry> Entries,
        QuestEntry? ActiveEntry,
        string? ActiveObjective)
    {
        public int TotalCount => Entries.Count;

        public int AcceptedCount => Entries.Count(entry => entry.IsAccepted);

        public int ActiveCount => Entries.Count(entry => entry.IsAccepted && !entry.IsTerminal);

        public int TerminalCount => Entries.Count(entry => entry.IsAccepted && entry.IsTerminal);

        public int CompletedCount => Entries.Count(entry =>
            entry.IsAccepted && entry.TerminalKind == QuestTerminalKind.Completed);
    }

    public sealed record QuestEntry(
        string Id,
        string Title,
        string Description,
        string Category,
        string State,
        string Summary,
        string Details,
        IReadOnlyList<string> Objectives,
        bool IsAccepted,
        bool IsTracked,
        QuestTerminalKind TerminalKind)
    {
        public bool IsTerminal => TerminalKind != QuestTerminalKind.None;

        public bool IsActive => IsAccepted && IsTracked && !IsTerminal;
    }

    public sealed record MapView(
        string CurrentAreaId,
        string CurrentAreaSlug,
        string CurrentAreaName,
        string? Description,
        int AreaCount,
        int PortalCount,
        int PatrolRouteCount,
        int PinCount,
        int WaypointCount,
        string? Weather,
        string? Objective,
        double? ExplorationPercent,
        int RevealedCells,
        int TotalCells,
        int ActiveFloor,
        int FloorCount,
        float Width,
        float Depth,
        IReadOnlyList<MapArea> Areas,
        IReadOnlyList<MapPortal> Portals,
        IReadOnlyList<MapMarker> Markers,
        IReadOnlyList<MapPatrolRoute> PatrolRoutes,
        IReadOnlyList<DomainDiagnostic> ExplorationDiagnostics);

    public sealed record MapArea(string Id, string DisplayName, bool IsCurrent);

    public sealed record MapPortal(
        string Id,
        string DisplayName,
        string SourceAreaId,
        string DestinationAreaId);

    public sealed record MapMarker(
        string Id,
        string Label,
        string Kind,
        float X,
        float Z,
        int Floor,
        bool Tracked);

    public sealed record MapPatrolRoute(
        string Id,
        int Floor,
        IReadOnlyList<MapPoint> Points);

    public sealed record MapPoint(float X, float Z);
}
