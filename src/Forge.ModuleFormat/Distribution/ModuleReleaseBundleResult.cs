namespace Forge.ModuleFormat.Distribution;

public sealed record ModuleReleaseBundleResult(
    string BundlePath,
    ModuleReleaseManifest Manifest,
    int FileCount,
    long BundleBytes);
