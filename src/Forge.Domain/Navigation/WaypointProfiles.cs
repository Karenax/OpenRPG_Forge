using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public enum MarkerVisibilityScope { Player, Party, World }
public enum MarkerVisibilityPolicy { Always, HiddenUntilRevealed, WhileTracked }
public enum PatrolTraversalPolicy { Loop, PingPong, Once, Random, Weighted, Scripted }

public sealed record WaypointEditorAppearance(string Icon, string Color, string Layer, bool Visible, double ScreenSize);

public sealed record MapMarker(
    string Text,
    string Icon,
    int Floor,
    string Category,
    double Precision,
    MarkerVisibilityPolicy VisibilityPolicy,
    MarkerVisibilityScope VisibilityScope,
    bool Tracked,
    bool Revealed,
    string RevealEvent,
    string RevealAnimation,
    bool Persistent);

public sealed record PatrolPoint(
    Guid Id,
    Vector3Value Position,
    double WaitSeconds,
    double FacingDegrees,
    double Speed,
    string? Action,
    string? BranchCondition,
    double Weight = 1);

public sealed record WaypointProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    Vector3Value Position,
    WaypointEditorAppearance Appearance,
    MapMarker? Marker);

public sealed record PatrolRoute(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    PatrolTraversalPolicy Traversal,
    IReadOnlyList<PatrolPoint> Points);

public sealed record PatrolCursor(int Index, int Direction, bool Complete);
