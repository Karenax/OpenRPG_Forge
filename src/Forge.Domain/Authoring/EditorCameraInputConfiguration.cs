namespace Forge.Domain.Authoring;

public enum EditorCameraPilotMode
{
    Orbit,
    Fly,
}

public sealed record EditorCameraInputConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public EditorCameraPilotMode PilotMode { get; }
    public IReadOnlyDictionary<string, string> Bindings { get; }
    public bool ShareBookmarksWithProject { get; }

    public EditorCameraInputConfiguration(
        Guid id,
        int schemaVersion,
        string provenance,
        EditorCameraPilotMode pilotMode,
        IReadOnlyDictionary<string, string> bindings,
        bool shareBookmarksWithProject = false)
    {
        if (id == Guid.Empty) throw new ArgumentException("Camera input UUID must be non-empty.", nameof(id));
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(bindings);
        var required = new[] { "forward", "back", "left", "right", "up", "down", "focus" };
        var normalized = bindings.ToDictionary(
            pair => pair.Key.Trim().ToLowerInvariant(),
            pair => pair.Value.Trim().ToUpperInvariant(),
            StringComparer.Ordinal);
        if (required.Any(key => !normalized.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) ||
            normalized.Values.Distinct(StringComparer.Ordinal).Count() != normalized.Count)
            throw new ArgumentException("Camera bindings must define unique keys for forward, back, left, right, up, down and focus.", nameof(bindings));
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        PilotMode = pilotMode;
        Bindings = normalized;
        ShareBookmarksWithProject = shareBookmarksWithProject;
    }

    public static EditorCameraInputConfiguration CreateDefault(Guid profileId) => new(
        StableId(profileId), CurrentSchemaVersion, "openrpg-forge-default", EditorCameraPilotMode.Fly,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["forward"] = "W",
            ["back"] = "S",
            ["left"] = "A",
            ["right"] = "D",
            ["up"] = "E",
            ["down"] = "Q",
            ["focus"] = "F",
        });

    private static Guid StableId(Guid source) => new(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes($"{source:D}:camera-input"))[..16]);
}
