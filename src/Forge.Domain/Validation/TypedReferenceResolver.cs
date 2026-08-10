namespace Forge.Domain.Validation;

public sealed record ValidationResource(
    Guid Id, string Type, string Package, string Version, bool Accessible, string Path);

public sealed record ValidationReference(
    Guid SourceId, Guid TargetId, string ExpectedType, string RequiredPackage,
    string MinimumVersion, string Path);

public static class TypedReferenceResolver
{
    public const string MissingCode = "qa.reference.missing";
    public const string WrongTypeCode = "qa.reference.wrong_type";
    public const string PackageInaccessibleCode = "qa.reference.package_inaccessible";
    public const string VersionMismatchCode = "qa.reference.version_mismatch";

    public static IReadOnlyList<DomainDiagnostic> Validate(
        IReadOnlyList<ValidationResource> resources,
        IReadOnlyList<ValidationReference> references)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(references);
        var diagnostics = new List<DomainDiagnostic>();
        var byId = resources.GroupBy(resource => resource.Id).ToDictionary(group => group.Key, group => group.First());
        foreach (var reference in references.OrderBy(item => item.Path, StringComparer.Ordinal))
        {
            if (!byId.TryGetValue(reference.TargetId, out var target))
            {
                diagnostics.Add(new(MissingCode, $"Reference target '{reference.TargetId}' does not exist.", reference.Path));
                continue;
            }
            if (!string.Equals(target.Type, reference.ExpectedType, StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(new(WrongTypeCode,
                    $"Reference expects '{reference.ExpectedType}' but target is '{target.Type}'.", reference.Path));
            if (!target.Accessible || (!string.IsNullOrWhiteSpace(reference.RequiredPackage) &&
                !string.Equals(target.Package, reference.RequiredPackage, StringComparison.OrdinalIgnoreCase)))
                diagnostics.Add(new(PackageInaccessibleCode, "Reference target package is inaccessible.", reference.Path));
            if (!VersionAtLeast(target.Version, reference.MinimumVersion))
                diagnostics.Add(new(VersionMismatchCode,
                    $"Reference requires version {reference.MinimumVersion} but target is {target.Version}.", reference.Path));
        }
        return diagnostics;
    }

    private static bool VersionAtLeast(string actual, string minimum) =>
        Version.TryParse(actual.Split('-', 2)[0], out var actualVersion) &&
        Version.TryParse(minimum.Split('-', 2)[0], out var minimumVersion) && actualVersion >= minimumVersion;
}
