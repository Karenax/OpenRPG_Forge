namespace Forge.Domain.Entities;

public sealed record EntityPropertyOverrides
{
    public const int CurrentSchemaVersion = 1;

    public static EntityPropertyOverrides Empty { get; } = new(CurrentSchemaVersion, []);

    public int SchemaVersion { get; }

    public IReadOnlyList<EntityPropertyOverride> Properties { get; }

    public EntityPropertyOverrides(
        int schemaVersion,
        IReadOnlyList<EntityPropertyOverride>? properties = null)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                $"Unsupported entity override schema; expected version {CurrentSchemaVersion}.");
        }

        var materialized = (properties ?? [])
            .OrderBy(property => property.Key, StringComparer.Ordinal)
            .ToArray();
        if (materialized.Select(property => property.Key).Distinct(StringComparer.Ordinal).Count() !=
            materialized.Length)
        {
            throw new ArgumentException("Entity override keys must be unique.", nameof(properties));
        }

        SchemaVersion = schemaVersion;
        Properties = materialized;
    }

    public bool ContentEquals(EntityPropertyOverrides other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return SchemaVersion == other.SchemaVersion && Properties.SequenceEqual(other.Properties);
    }
}
