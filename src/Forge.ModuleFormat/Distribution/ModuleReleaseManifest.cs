using Forge.Domain.Modules;

namespace Forge.ModuleFormat.Distribution;

public sealed record ModuleReleaseManifest(
    string FormatVersion,
    ModuleReleaseConfigurationId ReleaseId,
    ModuleReleaseChannel Channel,
    ModuleTargetKind Target,
    ModuleDistributionPlatform Platform,
    string PackageFile,
    string PackageSha256,
    long PackageBytes,
    ModuleSigningMode SigningMode,
    string? SignatureFile,
    string? SignatureSha256,
    string? DeltaFile,
    string? DeltaSha256,
    bool ResumeSupported,
    bool RollbackSupported,
    WindowsPackageMode? WindowsMode,
    bool PrerequisitesDetection,
    IReadOnlyList<string> Prerequisites,
    bool InstallLog,
    bool Uninstaller,
    string Provenance);
