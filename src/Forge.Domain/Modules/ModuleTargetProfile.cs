namespace Forge.Domain.Modules;

public sealed record ModuleTargetProfile
{
    public const int CurrentSchemaVersion = 1;

    public ModuleTargetProfileId Id { get; }

    public int SchemaVersion { get; }

    public ModuleTargetKind Target { get; }

    public bool Enabled { get; }

    public bool IncludeEditorData { get; }

    public bool IncludeDebugSymbols { get; }

    public bool OptimizeContent { get; }

    public bool RequireApprovedLicenses { get; }

    public string OutputName { get; }

    public string SecretsProfile { get; }

    public string Provenance { get; }

    public ModuleTargetProfile(
        ModuleTargetProfileId id,
        ModuleTargetKind target,
        bool enabled,
        bool includeEditorData,
        bool includeDebugSymbols,
        bool optimizeContent,
        bool requireApprovedLicenses,
        string outputName,
        string secretsProfile,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputName);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretsProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Module target profile schema '{schemaVersion}' is not supported.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Target = target;
        Enabled = enabled;
        IncludeEditorData = includeEditorData;
        IncludeDebugSymbols = includeDebugSymbols;
        OptimizeContent = optimizeContent;
        RequireApprovedLicenses = requireApprovedLicenses;
        OutputName = outputName.Trim();
        SecretsProfile = secretsProfile.Trim();
        Provenance = provenance.Trim();
    }
}
