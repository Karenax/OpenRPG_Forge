namespace Forge.ModuleFormat.Content;

public sealed record BlueprintExportManifest(
    int SchemaVersion,
    string PackageKind,
    IReadOnlyList<string> BlueprintIds,
    IReadOnlyList<string> TransitiveDependencies,
    IReadOnlyList<string> Licenses,
    IReadOnlyList<string> Migrations,
    IReadOnlyList<string> Previews);
