using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Authoring;

public sealed record CreatorSearchProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; init; }
    public int SchemaVersion { get; init; }
    public string Provenance { get; init; }
    public int ResultLimit { get; init; }
    public bool IncludeScripts { get; init; }
    public bool IncludeAssets { get; init; }
    public bool IncludeDiagnostics { get; init; }

    public CreatorSearchProfile(Guid id, int schemaVersion, string? provenance, int resultLimit,
        bool includeScripts, bool includeAssets, bool includeDiagnostics)
    {
        if (id == Guid.Empty) throw new ArgumentException("Search profile UUID is required.", nameof(id));
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (string.IsNullOrWhiteSpace(provenance)) throw new ArgumentException("Search profile provenance is required.", nameof(provenance));
        if (resultLimit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(resultLimit));
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance.Trim();
        ResultLimit = resultLimit;
        IncludeScripts = includeScripts;
        IncludeAssets = includeAssets;
        IncludeDiagnostics = includeDiagnostics;
    }

    public static CreatorSearchProfile CreateDefault(Guid projectId) => new(
        Stable(projectId), CurrentSchemaVersion, "migration-default", 500, true, true, true);

    private static Guid Stable(Guid projectId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{projectId:D}:creator-search"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}
