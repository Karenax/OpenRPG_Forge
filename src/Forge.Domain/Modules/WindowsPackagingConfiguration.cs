namespace Forge.Domain.Modules;

public sealed record WindowsPackagingConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }

    public WindowsPackageMode Mode { get; }

    public bool DetectPrerequisites { get; }

    public bool WriteInstallLog { get; }

    public bool IncludeUninstaller { get; }

    public IReadOnlyList<string> Prerequisites { get; }

    public WindowsPackagingConfiguration(
        WindowsPackageMode mode,
        bool detectPrerequisites,
        bool writeInstallLog,
        bool includeUninstaller,
        IReadOnlyList<string>? prerequisites,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Windows packaging schema '{schemaVersion}' is not supported.");
        }

        var normalized = (prerequisites ?? [])
            .Select(value => value?.Trim() ?? string.Empty)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Any(string.IsNullOrWhiteSpace) ||
            normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
        {
            throw new ArgumentException("Windows prerequisites must be non-empty and unique.", nameof(prerequisites));
        }

        SchemaVersion = schemaVersion;
        Mode = mode;
        DetectPrerequisites = detectPrerequisites;
        WriteInstallLog = writeInstallLog;
        IncludeUninstaller = includeUninstaller;
        Prerequisites = normalized;
    }
}
