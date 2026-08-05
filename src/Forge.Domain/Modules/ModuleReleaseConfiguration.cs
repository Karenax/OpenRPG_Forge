using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Modules;

public sealed record ModuleReleaseConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public ModuleReleaseConfigurationId Id { get; }

    public int SchemaVersion { get; }

    public ModuleReleaseChannel Channel { get; }

    public IReadOnlyList<ModuleDistributionPlatform> Platforms { get; }

    public ModuleSigningConfiguration Signing { get; }

    public ModuleDeltaConfiguration Delta { get; }

    public WindowsPackagingConfiguration WindowsPackaging { get; }

    public string Provenance { get; }

    public ModuleReleaseConfiguration(
        ModuleReleaseConfigurationId id,
        ModuleReleaseChannel channel,
        IReadOnlyList<ModuleDistributionPlatform> platforms,
        ModuleSigningConfiguration signing,
        ModuleDeltaConfiguration delta,
        WindowsPackagingConfiguration windowsPackaging,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(platforms);
        ArgumentNullException.ThrowIfNull(signing);
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(windowsPackaging);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Module release schema '{schemaVersion}' is not supported.");
        }

        var normalizedPlatforms = platforms.Distinct().Order().ToArray();
        if (normalizedPlatforms.Length == 0 || normalizedPlatforms.Length != platforms.Count)
        {
            throw new ArgumentException("A release requires at least one unique platform.", nameof(platforms));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Channel = channel;
        Platforms = normalizedPlatforms;
        Signing = signing;
        Delta = delta;
        WindowsPackaging = windowsPackaging;
        Provenance = provenance.Trim();
    }

    public static ModuleReleaseConfiguration CreateDefault(ModuleDistributionConfigurationId distributionId)
    {
        ArgumentNullException.ThrowIfNull(distributionId);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{distributionId}:release"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new ModuleReleaseConfiguration(
            ModuleReleaseConfigurationId.From(new Guid(guidBytes)),
            ModuleReleaseChannel.Development,
            [ModuleDistributionPlatform.WindowsX64],
            new ModuleSigningConfiguration(
                ModuleSigningMode.Optional,
                "local-signing",
                [],
                [],
                allowUnsignedDevelopment: true),
            new ModuleDeltaConfiguration(
                enabled: true,
                chunkSizeBytes: 1024 * 1024,
                allowResume: true,
                deduplicateByHash: true,
                allowRollback: true,
                retainedVersions: 2),
            new WindowsPackagingConfiguration(
                WindowsPackageMode.PortableAndInstaller,
                detectPrerequisites: true,
                writeInstallLog: true,
                includeUninstaller: true,
                ["windows-x64", "vulkan-or-d3d12"]),
            "migration-default");
    }
}
