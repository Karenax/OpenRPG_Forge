namespace Forge.Domain.Build;

public sealed record BuildDependencyNode
{
    public BuildResourceId Id { get; }

    public string Path { get; }

    public BuildResourceKind Kind { get; }

    public string ContentSha256 { get; }

    public string LicenseSpdx { get; }

    public string Provenance { get; }

    public IReadOnlyList<BuildResourceId> Dependencies { get; }

    public BuildDependencyNode(
        BuildResourceId id,
        string path,
        BuildResourceKind kind,
        string contentSha256,
        string licenseSpdx,
        string provenance,
        IReadOnlyList<BuildResourceId>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseSpdx);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!IsSha256(contentSha256))
        {
            throw new ArgumentException("Build resource hashes must be lowercase SHA-256 values.", nameof(contentSha256));
        }

        var normalizedPath = path.Replace('\\', '/').Trim();
        var materializedDependencies = (dependencies ?? [])
            .Distinct()
            .OrderBy(dependency => dependency.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (materializedDependencies.Contains(id))
        {
            throw new ArgumentException("A build resource cannot depend on itself.", nameof(dependencies));
        }

        Id = id;
        Path = normalizedPath;
        Kind = kind;
        ContentSha256 = contentSha256;
        LicenseSpdx = licenseSpdx.Trim();
        Provenance = provenance.Trim();
        Dependencies = materializedDependencies;
    }

    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
