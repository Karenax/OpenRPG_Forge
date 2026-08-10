namespace Forge.Domain.Authoring;

public enum EditorTransformTool
{
    Translate,
    Rotate,
    Scale,
}

public enum EditorCoordinateSpace
{
    World,
    Local,
}

public enum EditorPivotMode
{
    Individual,
    SelectionCenter,
    ActiveObject,
}

public enum EditorTransformAlignment
{
    None,
    MinimumX,
    MaximumX,
    CenterX,
    MinimumY,
    MaximumY,
    CenterY,
    MinimumZ,
    MaximumZ,
    CenterZ,
    Ground,
}

public sealed record EditorSelectionConfiguration
{
    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool MultiSelectionEnabled { get; }
    public bool FocusOnSelection { get; }
    public bool FrameSelection { get; }
    public bool LockSelection { get; }
    public bool EditCommonPropertiesOnly { get; }

    public EditorSelectionConfiguration(
        Guid id,
        int schemaVersion,
        string provenance,
        bool multiSelectionEnabled = true,
        bool focusOnSelection = true,
        bool frameSelection = true,
        bool lockSelection = false,
        bool editCommonPropertiesOnly = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("Selection configuration UUID must be non-empty.", nameof(id));
        if (schemaVersion != 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        MultiSelectionEnabled = multiSelectionEnabled;
        FocusOnSelection = focusOnSelection;
        FrameSelection = frameSelection;
        LockSelection = lockSelection;
        EditCommonPropertiesOnly = editCommonPropertiesOnly;
    }
}

public sealed record EditorTransformToolConfiguration
{
    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public EditorTransformTool Tool { get; }
    public EditorCoordinateSpace CoordinateSpace { get; }
    public EditorPivotMode PivotMode { get; }
    public float TranslationSnap { get; }
    public float RotationSnapDegrees { get; }
    public float ScaleSnap { get; }
    public bool NumericInputEnabled { get; }
    public bool AlignmentEnabled { get; }

    public EditorTransformToolConfiguration(
        Guid id,
        int schemaVersion,
        string provenance,
        EditorTransformTool tool = EditorTransformTool.Translate,
        EditorCoordinateSpace coordinateSpace = EditorCoordinateSpace.World,
        EditorPivotMode pivotMode = EditorPivotMode.SelectionCenter,
        float translationSnap = 1,
        float rotationSnapDegrees = 15,
        float scaleSnap = .05f,
        bool numericInputEnabled = true,
        bool alignmentEnabled = true)
    {
        if (id == Guid.Empty) throw new ArgumentException("Transform tool UUID must be non-empty.", nameof(id));
        if (schemaVersion != 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!float.IsFinite(translationSnap) || translationSnap is <= 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(translationSnap));
        if (!float.IsFinite(rotationSnapDegrees) || rotationSnapDegrees is <= 0 or > 180)
            throw new ArgumentOutOfRangeException(nameof(rotationSnapDegrees));
        if (!float.IsFinite(scaleSnap) || scaleSnap is <= 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(scaleSnap));
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Tool = tool;
        CoordinateSpace = coordinateSpace;
        PivotMode = pivotMode;
        TranslationSnap = translationSnap;
        RotationSnapDegrees = rotationSnapDegrees;
        ScaleSnap = scaleSnap;
        NumericInputEnabled = numericInputEnabled;
        AlignmentEnabled = alignmentEnabled;
    }
}

public sealed record EditorDirectManipulationProfile
{
    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public EditorSelectionConfiguration Selection { get; }
    public EditorTransformToolConfiguration Transform { get; }

    public EditorDirectManipulationProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        EditorSelectionConfiguration selection,
        EditorTransformToolConfiguration transform)
    {
        if (id == Guid.Empty) throw new ArgumentException("Direct manipulation UUID must be non-empty.", nameof(id));
        if (schemaVersion != 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(transform);
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        Selection = selection;
        Transform = transform;
    }

    public static EditorDirectManipulationProfile CreateDefault(Guid profileId) => new(
        StableId(profileId, "direct-manipulation"), 1, "openrpg-forge-default",
        new EditorSelectionConfiguration(StableId(profileId, "selection"), 1, "openrpg-forge-default"),
        new EditorTransformToolConfiguration(StableId(profileId, "transform"), 1, "openrpg-forge-default"));

    private static Guid StableId(Guid source, string suffix) =>
        new(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{source:D}:{suffix}"))[..16]);
}
