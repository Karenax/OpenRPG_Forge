namespace Forge.ModuleFormat.Packages;

public sealed record ModulePackageExportResult(
    string PackagePath,
    ModuleManifest Manifest,
    int FileCount,
    long ArchiveBytes);
