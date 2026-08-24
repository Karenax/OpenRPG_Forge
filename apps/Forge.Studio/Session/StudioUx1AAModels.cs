using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx1AAPlaceableOption(
    BlueprintId BlueprintId,
    string DisplayName,
    bool Ready,
    string Availability);

internal sealed record StudioUx1AAChildOption(
    BlueprintId BlueprintId,
    string DisplayName);

internal sealed record StudioUx1AACatalog(
    IReadOnlyList<StudioUx1AAPlaceableOption> Placeables,
    IReadOnlyList<StudioUx1AAChildOption> ChildPlaceables);

internal sealed record StudioUx1AAEntryOption(Guid Id, string DisplayName);

internal sealed record StudioUx1AATransformDraft(
    Vector3Value Position,
    Vector3Value RotationDegrees,
    Vector3Value Scale,
    Vector3Value Pivot,
    double SnapIncrement,
    Guid? ParentId,
    bool AlignToSurface,
    string TransformAnimationId);

internal sealed record StudioUx1AASocketDraft(
    Guid Id,
    bool Existing,
    string OriginalSocketId,
    string SocketId,
    string Kind,
    StudioUx1AATransformDraft Transform,
    string AllowedTagsText,
    bool SnapEnabled,
    int MaximumAttachments);

internal sealed record StudioUx1AAAttachmentDraft(
    Guid Id,
    bool Existing,
    string SocketId,
    BlueprintId? ChildBlueprintId,
    string TagsText,
    StudioUx1AATransformDraft Transform,
    string PrefabId);

internal sealed record StudioUx1AAVerbDraft(
    Guid Id,
    string VerbId,
    string LabelKey,
    int BasePriority,
    string RequiredContextText,
    string ForbiddenContextText,
    string EventBinding);

internal sealed record StudioUx1AAValidationDraft(
    string AnimationIdsText,
    string LockIdsText,
    string TrapIdsText,
    string InventoryIdsText,
    string PhysicsProfileIdsText,
    int DrawCallBudget,
    int ScriptBudget,
    int MinimumDrawCallBudget,
    int MinimumScriptBudget);

internal sealed record StudioPlaceableAssemblyValidationDraft(
    BlueprintId BlueprintId,
    Guid ProfileId,
    IReadOnlyList<StudioUx1AAEntryOption> SocketChoices,
    IReadOnlyList<StudioUx1AAEntryOption> AttachmentChoices,
    StudioUx1AASocketDraft Socket,
    StudioUx1AAAttachmentDraft Attachment,
    IReadOnlyList<StudioUx1AAVerbDraft> Verbs,
    string PreviewContextText,
    StudioUx1AAValidationDraft Validation,
    PlaceableWorldProfile OriginalProfile);

internal sealed record StudioUx1AAPathPreview(
    bool Succeeded,
    string Summary,
    string DiagnosticCode);

internal sealed record StudioUx1AAPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx1AAPathPreview Assembly,
    StudioUx1AAPathPreview MultiVerb,
    StudioUx1AAPathPreview Validation);

internal sealed record StudioUx1AAActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1AASmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool PrerequisitesRefused,
    bool AssemblyPreview,
    bool MultiVerbPreview,
    bool ValidationPreview,
    bool DryRun,
    bool CommandParity,
    bool InvalidSocketBlocked,
    bool InvalidAttachmentBlocked,
    bool InvalidVerbBlocked,
    bool InvalidValidationBlocked,
    bool StalePreviewBlocked,
    bool DamageAuthorityPreserved,
    bool NeighborCapabilitiesPreserved,
    bool NeighborPlaceablePreserved,
    bool UndoRedo,
    bool Persistence,
    bool HumanDiagnostics);

internal sealed record StudioUx1AAEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> SelectedEntityIds,
    EntityId? SelectedEntityId);

internal sealed class StudioUx1AAEvidenceWorkspace : IDisposable
{
    private const string DirectoryPrefix = "openrpg-forge-studio-ux1aa-evidence-";
    private bool _disposed;

    internal StudioSessionController Session { get; }
    internal string RootPath { get; }
    internal bool ResourcesReleased { get; private set; }

    internal StudioUx1AAEvidenceWorkspace(StudioSessionController session, string rootPath)
    {
        Session = session;
        RootPath = rootPath;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        var released = true;
        try
        {
            Session.CloseUx1AA();
            Session.Close();
        }
        catch (Exception exception)
        {
            released = false;
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AA_EVIDENCE_SESSION_CLOSE_FAILED type={exception.GetType().Name}");
        }
        try
        {
            var root = Path.GetFullPath(RootPath);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            var directoryName = Path.GetFileName(root);
            if (root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(directoryName) &&
                directoryName.StartsWith(DirectoryPrefix, StringComparison.Ordinal) &&
                Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            released &= !Directory.Exists(root);
        }
        catch (Exception exception)
        {
            released = false;
            Godot.GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AA_EVIDENCE_CLEANUP_FAILED type={exception.GetType().Name}");
        }
        ResourcesReleased = released;
    }

    internal static string CreateRootPath() => Path.Combine(
        Path.GetTempPath(),
        DirectoryPrefix + Guid.NewGuid().ToString("N"));
}
