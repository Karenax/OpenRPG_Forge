using Forge.Domain.Blueprints;
using Forge.Domain.Content;
using Forge.Domain.Projects;
using Forge.Application.Projects;
using Forge.ModuleFormat.Content;

namespace Forge.Studio.Session;

internal sealed record StudioUx3HContentDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    ContentWorkspaceConfigurationId Id,
    string BatchGlob,
    IReadOnlyList<string> BatchExtensions,
    ContentBatchCollisionPolicy BatchCollisionPolicy,
    string BatchProvenance,
    string BatchLicenseSpdx,
    ContentMergePolicy MergePolicy,
    string Provenance,
    ContentPackageCatalog Packages,
    IReadOnlyList<DataTableDefinition> DataTables,
    bool Approved);

internal sealed record StudioUx3HDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx3HContentPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool SourcesResolved,
    bool TablesValid,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3HDiagnostic> Diagnostics,
    IReadOnlyList<string> SourcePlan,
    IReadOnlyList<string> TablePlan);

internal sealed record StudioUx3HBatchPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string SourceRoot,
    string DestinationRelativePath,
    long TotalBytes,
    IReadOnlyList<string> Entries,
    IReadOnlyList<string> Diagnostics);

internal sealed record StudioUx3HBlueprintChoice(BlueprintId Id, string Label, string Kind, string SourceLabel);

internal sealed record StudioUx3HExportPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string OutputRelativePath,
    string ContentSha256,
    long Bytes,
    int BlueprintCount,
    int DependencyCount,
    int PreviewCount,
    IReadOnlyList<string> Entries);

internal sealed record StudioUx3HActionResult(bool Succeeded, string Message);

internal sealed record StudioUx3HApprovalSnapshot(
    StudioUx3HContentDraft? Draft,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? BatchFingerprint,
    string? ExportFingerprint);

internal sealed record StudioUx3HEvidencePlan(
    ProjectDefinition Project,
    string SourceWorkspaceRoot,
    string BatchSourceRelativePath,
    string BatchDestinationRelativePath,
    string SourceFileRelativePath,
    BlueprintId BlueprintId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx3HSmokeResult(
    bool Succeeded,
    bool ExplicitSelections,
    bool BatchConfiguration,
    bool BatchDryRun,
    bool BatchApplyUndoRedo,
    bool BlueprintExport,
    bool BlueprintExportUndoRedo,
    bool TextSources,
    bool DataTables,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborPreserved,
    bool ProjectSourcePreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
