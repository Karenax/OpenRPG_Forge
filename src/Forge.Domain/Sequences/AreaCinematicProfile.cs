using Forge.Domain.Areas;

namespace Forge.Domain.Sequences;

public sealed record AreaCinematicProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<CameraAnchorDefinition> CameraAnchors { get; }
    public string Provenance { get; }

    public AreaCinematicProfile(Guid id, int schemaVersion, IReadOnlyList<CameraAnchorDefinition> cameraAnchors,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(cameraAnchors);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = cameraAnchors.OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            values.Select(item => item.Id).Distinct().Count() != values.Length ||
            values.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Area cinematic profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        CameraAnchors = values;
        Provenance = provenance.Trim();
    }

    public static AreaCinematicProfile CreateDefault(AreaId areaId) =>
        new(areaId.Value, CurrentSchemaVersion, [], "migration-default");
}
