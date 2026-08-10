using Forge.Domain.Validation;

namespace Forge.Domain.Build;

public sealed record DisposableBuildCachePolicy(
    bool ExplicitDirectory,
    bool ContentAddressed,
    bool Invalidatable,
    bool RebuildableFromSource,
    bool NeverCanonical,
    string IndexFileName,
    int MaximumEntries);

public sealed record DocumentNormalizationPolicy(
    bool CanonicalJson,
    bool Utf8WithoutBom,
    bool LineFeedEndings,
    bool StablePropertyOrder,
    bool StableNumericFormatting,
    bool AtomicWrites,
    int MaximumDocumentBytes);

public sealed record WorkspaceMigrationPolicy(
    bool VersionedSteps,
    bool RequireDryRun,
    bool BackupBeforeApply,
    bool RequireIdempotence,
    bool DowngradeWhenSupported,
    string BackupDirectory,
    int MaximumBackups);

public sealed record ReverseReferencePolicy(
    bool IndexedGraph,
    bool IncludeForwardReferences,
    bool IncludeReverseReferences,
    bool RebuildableFromSource,
    bool EditorAccess,
    bool CliAccess,
    bool McpAccess,
    int MaximumNodes);

public sealed record BuildSemanticDiffPolicy(
    bool Areas,
    bool Graphs,
    bool Entities,
    bool Packages,
    bool Scripts,
    bool TextOutput,
    bool JsonOutput,
    bool StableOrdering,
    int MaximumEntries);

public sealed record WorkspaceBuildCollaborationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    DisposableBuildCachePolicy Cache,
    DocumentNormalizationPolicy Normalization,
    WorkspaceMigrationPolicy Migrations,
    ReverseReferencePolicy References,
    BuildSemanticDiffPolicy SemanticDiff)
{
    public const int CurrentSchemaVersion = 1;
    public static readonly Guid DefaultId = Guid.Parse("b0230000-0000-4000-8000-000000000001");

    public static WorkspaceBuildCollaborationProfile CreateDefault() => new(
        DefaultId,
        CurrentSchemaVersion,
        "openrpg-forge:r023-build-collaboration",
        new(true, true, true, true, true, "cache-index.json", 100_000),
        new(true, true, true, true, true, true, 16 * 1024 * 1024),
        new(true, true, true, true, true, ".forge/migration-backups", 20),
        new(true, true, true, true, true, true, true, 100_000),
        new(true, true, true, true, true, true, true, true, 100_000));
}

public static class WorkspaceBuildCollaborationProfileValidator
{
    public const string IdentityInvalidCode = "build.collaboration.identity_invalid";
    public const string SchemaUnsupportedCode = "build.collaboration.schema_unsupported";
    public const string CacheInvalidCode = "build.collaboration.cache_invalid";
    public const string NormalizationInvalidCode = "build.collaboration.normalization_invalid";
    public const string MigrationInvalidCode = "build.collaboration.migration_invalid";
    public const string ReferencesInvalidCode = "build.collaboration.references_invalid";
    public const string SemanticDiffInvalidCode = "build.collaboration.semantic_diff_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(WorkspaceBuildCollaborationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var root = $"build/collaboration/{profile.Id:D}";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Collaboration profile UUID and provenance are required.", root));
        if (profile.SchemaVersion != WorkspaceBuildCollaborationProfile.CurrentSchemaVersion)
            diagnostics.Add(new(SchemaUnsupportedCode, "Collaboration profile schema must be 1.", $"{root}/schemaVersion"));
        var cache = profile.Cache;
        if (!cache.ExplicitDirectory || !cache.ContentAddressed || !cache.Invalidatable ||
            !cache.RebuildableFromSource || !cache.NeverCanonical || !IsFileName(cache.IndexFileName) ||
            cache.MaximumEntries is < 1 or > 1_000_000)
            diagnostics.Add(new(CacheInvalidCode,
                "Cache must be explicit, content-addressed, disposable, rebuildable and bounded.", $"{root}/cache"));
        var normalization = profile.Normalization;
        if (!normalization.CanonicalJson || !normalization.Utf8WithoutBom || !normalization.LineFeedEndings ||
            !normalization.StablePropertyOrder || !normalization.StableNumericFormatting ||
            !normalization.AtomicWrites || normalization.MaximumDocumentBytes is < 1_024 or > 64 * 1024 * 1024)
            diagnostics.Add(new(NormalizationInvalidCode,
                "Normalization requires canonical JSON, UTF-8/LF, stable ordering/numbers and atomic writes.",
                $"{root}/normalization"));
        var migrations = profile.Migrations;
        if (!migrations.VersionedSteps || !migrations.RequireDryRun || !migrations.BackupBeforeApply ||
            !migrations.RequireIdempotence || !migrations.DowngradeWhenSupported ||
            !IsSafeRelativePath(migrations.BackupDirectory) || migrations.MaximumBackups is < 1 or > 100)
            diagnostics.Add(new(MigrationInvalidCode,
                "Migrations require versioned dry-runs, backups, idempotence and explicit downgrade support.",
                $"{root}/migrations"));
        var references = profile.References;
        if (!references.IndexedGraph || !references.IncludeForwardReferences ||
            !references.IncludeReverseReferences || !references.RebuildableFromSource ||
            !references.EditorAccess || !references.CliAccess || !references.McpAccess ||
            references.MaximumNodes is < 10 or > 1_000_000)
            diagnostics.Add(new(ReferencesInvalidCode,
                "References require a bounded rebuildable forward/reverse graph exposed to editor, CLI and MCP.",
                $"{root}/references"));
        var diff = profile.SemanticDiff;
        if (!diff.Areas || !diff.Graphs || !diff.Entities || !diff.Packages || !diff.Scripts ||
            !diff.TextOutput || !diff.JsonOutput || !diff.StableOrdering ||
            diff.MaximumEntries is < 1 or > 1_000_000)
            diagnostics.Add(new(SemanticDiffInvalidCode,
                "Semantic diff requires area, graph, entity, package and script changes in stable text and JSON.",
                $"{root}/semanticDiff"));
        return diagnostics;
    }

    private static bool IsFileName(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !value.Contains('/') && !value.Contains('\\');

    private static bool IsSafeRelativePath(string value) =>
        !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) &&
        value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");
}
