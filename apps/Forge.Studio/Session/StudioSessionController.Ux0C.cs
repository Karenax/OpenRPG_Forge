using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Entities;
using Forge.Application.Time;
using Forge.Domain.Authoring;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly ManipulateEntitiesHandler _manipulateEntities = new(new SystemClock());
    private readonly NavigateEditorCameraHandler _navigateCamera = new(new SystemClock());
    private readonly Dictionary<Forge.Domain.Areas.AreaId, IReadOnlyList<EntityId>> _areaSelectionSets = [];
    private EditorCameraCommandHistory? _cameraHistory;
    private EditorCameraProfile? _persistedCameraProfile;

    internal IReadOnlyList<EntityId> SelectedEntityIds
    {
        get
        {
            if (ActiveAreaId is not null && _areaSelectionSets.TryGetValue(ActiveAreaId, out var selected))
            {
                return selected;
            }

            return SelectedEntityId is null ? [] : [SelectedEntityId];
        }
    }

    internal EditorCameraProfile CameraProfile => _cameraHistory?.State.Profile ??
        throw new InvalidOperationException("No Studio camera profile is active.");

    internal bool CanUndoCamera => _cameraHistory?.CanUndo == true;

    internal bool CanRedoCamera => _cameraHistory?.CanRedo == true;

    internal bool CameraProfileHasUnsavedChanges =>
        _persistedCameraProfile is not null &&
        !string.Equals(
            CameraProfileFingerprint(_persistedCameraProfile),
            CameraProfileFingerprint(CameraProfile),
            StringComparison.Ordinal);

    internal bool SelectEntities(IReadOnlyList<EntityId> entityIds, EntityId? activeEntityId = null)
    {
        ArgumentNullException.ThrowIfNull(entityIds);
        var area = ActiveArea;
        if (area is null || ActiveAreaId is null)
        {
            return entityIds.Count == 0;
        }

        var distinct = entityIds.Distinct().ToArray();
        if (distinct.Length != entityIds.Count ||
            distinct.Any(id => area.Entities.All(entity => entity.Id != id)))
        {
            return false;
        }

        if (!WorkspaceProfile.Shell.DirectManipulation.Selection.MultiSelectionEnabled && distinct.Length > 1)
        {
            return false;
        }

        var active = activeEntityId is not null && distinct.Contains(activeEntityId)
            ? activeEntityId
            : distinct.LastOrDefault();
        _areaSelectionSets[ActiveAreaId] = distinct;
        _areaSelections[ActiveAreaId] = active;
        SelectedEntityId = active;
        return true;
    }

    internal StudioActionResult ConfigureViewportMode(EditorViewportMode mode, bool persist)
    {
        var current = WorkspaceProfile;
        var shell = new EditorShellConfiguration(
            current.Shell.Id,
            EditorShellConfiguration.CurrentSchemaVersion,
            "studio:ux0-c-viewport-mode",
            current.Shell.Layout,
            current.Shell.ExplorerGrouping,
            mode,
            current.Shell.PaletteFilter,
            current.Shell.DirectManipulation);
        var candidate = EditorWorkspaceProfilePlanner.ConfigureShell(current, shell);
        return ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-c-viewport-mode", persist);
    }

    internal EditorWorkspaceProfile BuildDirectManipulationCandidate(EditorDirectManipulationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var current = WorkspaceProfile;
        var shell = new EditorShellConfiguration(
            current.Shell.Id,
            EditorShellConfiguration.CurrentSchemaVersion,
            "studio:ux0-c-direct-manipulation",
            current.Shell.Layout,
            current.Shell.ExplorerGrouping,
            current.Shell.ViewportMode,
            current.Shell.PaletteFilter,
            profile);
        return EditorWorkspaceProfilePlanner.ConfigureShell(current, shell);
    }

    internal StudioActionResult PreviewDirectManipulation(EditorDirectManipulationProfile profile) =>
        PreviewWorkspaceProfile(BuildDirectManipulationCandidate(profile));

    internal StudioActionResult ConfigureDirectManipulation(
        EditorDirectManipulationProfile profile,
        bool persist) =>
        ApplyWorkspaceProfileCandidate(
            BuildDirectManipulationCandidate(profile),
            "studio:ux0-c-direct-manipulation",
            persist);

    internal StudioActionResult PreviewSelectedManipulation(
        EditorTransformTool tool,
        Vector3Value value,
        EditorTransformAlignment alignment = EditorTransformAlignment.None,
        EditorTransformToolConfiguration? configuration = null)
    {
        if (History is null || ActiveAreaId is null)
        {
            return new StudioActionResult(false, "Aucun module ou aucune zone active.");
        }

        var command = BuildManipulationCommand(
            CommandId.From(Guid.NewGuid()),
            tool,
            value,
            alignment,
            dryRun: true,
            configuration: configuration);
        var result = _manipulateEntities.Handle(History.State, command);
        return result.Succeeded
            ? new StudioActionResult(
                true,
                $"Aperçu valide · {SelectedEntityIds.Count} objet(s) · aucune donnée modifiée")
            : Failure(result);
    }

    internal StudioActionResult ManipulateSelectedEntities(
        EditorTransformTool tool,
        Vector3Value value,
        EditorTransformAlignment alignment = EditorTransformAlignment.None,
        EditorTransformToolConfiguration? configuration = null)
    {
        var commandId = CommandId.From(Guid.NewGuid());
        return ExecuteMutation(
            dryRun => _manipulateEntities.Handle(
                History!.State,
                BuildManipulationCommand(commandId, tool, value, alignment, dryRun, configuration)),
            alignment == EditorTransformAlignment.None
                ? $"{SelectedEntityIds.Count} objet(s) transformé(s) · Ctrl+Z pour annuler"
                : $"{SelectedEntityIds.Count} objet(s) aligné(s) · Ctrl+Z pour annuler");
    }

    internal StudioActionResult NavigateCamera(
        EditorCameraOperation operation,
        float x = 0,
        float y = 0,
        float z = 0,
        Vector3Value? target = null,
        float radius = 1,
        EditorCameraView view = EditorCameraView.Perspective,
        EditorCameraBookmarkId? bookmarkId = null,
        string? bookmarkName = null,
        EditorCameraInputConfiguration? input = null,
        EditorCameraPose? pose = null,
        bool dryRun = false)
    {
        if (_cameraHistory is null)
        {
            return new StudioActionResult(false, "Aucun profil de caméra n’est ouvert.");
        }

        var command = new NavigateEditorCameraCommand(
            CommandId.From(Guid.NewGuid()),
            _cameraHistory.State.Revision,
            operation,
            x,
            y,
            target,
            radius,
            view,
            bookmarkId,
            bookmarkName,
            dryRun,
            z,
            input,
            pose);
        var result = _navigateCamera.Handle(_cameraHistory.State, command);
        if (!result.Succeeded)
        {
            return new StudioActionResult(
                false,
                result.Diagnostics.Count == 0
                    ? "Commande caméra refusée."
                    : string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        if (!dryRun)
        {
            _cameraHistory.Record(result);
        }

        return new StudioActionResult(
            true,
            dryRun
                ? $"Aperçu caméra valide · {CameraOperationLabel(operation)}"
                : $"Caméra mise à jour · {CameraOperationLabel(operation)} · enregistrer le profil pour la conserver");
    }

    internal StudioActionResult SetCameraPose(EditorCameraPose pose, bool dryRun = false) =>
        NavigateCamera(EditorCameraOperation.SetPose, pose: pose, dryRun: dryRun);

    internal StudioActionResult UndoCamera()
    {
        if (_cameraHistory?.Undo() != true)
        {
            return new StudioActionResult(false, "Aucun mouvement de caméra à annuler.");
        }

        return new StudioActionResult(true, "Mouvement de caméra annulé");
    }

    internal StudioActionResult RedoCamera()
    {
        if (_cameraHistory?.Redo() != true)
        {
            return new StudioActionResult(false, "Aucun mouvement de caméra à rétablir.");
        }

        return new StudioActionResult(true, "Mouvement de caméra rétabli");
    }

    internal StudioActionResult SaveCameraProfile()
    {
        if (_cameraHistory is null || Session is null)
        {
            return new StudioActionResult(false, "Aucun profil de caméra n’est ouvert.");
        }

        var result = EditorCameraProfileStore.Save(Session.RootDirectory, _cameraHistory.State.Profile);
        if (!result.Succeeded)
        {
            return new StudioActionResult(
                false,
                string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        _persistedCameraProfile = _cameraHistory.State.Profile;
        return new StudioActionResult(true, "Profil de caméra enregistré atomiquement dans .forge");
    }

    private ManipulateEntitiesCommand BuildManipulationCommand(
        CommandId commandId,
        EditorTransformTool tool,
        Vector3Value value,
        EditorTransformAlignment alignment,
        bool dryRun,
        EditorTransformToolConfiguration? configuration = null)
    {
        var transform = configuration ?? WorkspaceProfile.Shell.DirectManipulation.Transform;
        var selectedIds = SelectedEntityId is null
            ? SelectedEntityIds.ToArray()
            : new[] { SelectedEntityId! }
                .Concat(SelectedEntityIds.Where(id => id != SelectedEntityId))
                .ToArray();
        return new ManipulateEntitiesCommand(
            commandId,
            ManipulateEntitiesHandler.CurrentSchemaVersion,
            "studio:ux0-c-direct-manipulation",
            History!.State.Revision,
            ActiveAreaId!,
            selectedIds,
            tool,
            value,
            transform.CoordinateSpace,
            transform.PivotMode,
            alignment,
            transform.TranslationSnap,
            transform.RotationSnapDegrees,
            transform.ScaleSnap,
            dryRun);
    }

    private void LoadUx0CPreferences(string root)
    {
        var camera = EditorCameraProfileStore.LoadOrCreate(root, Project!.Id);
        _cameraHistory = new EditorCameraCommandHistory(camera.Profile);
        _persistedCameraProfile = camera.Profile;
        if (camera.Diagnostics.Count > 0)
        {
            AppendWorkspacePreferenceWarning(
                $"Profil de caméra ignoré : {camera.Diagnostics[0].Message}");
        }
    }

    private void CloseUx0C()
    {
        _areaSelectionSets.Clear();
        _cameraHistory = null;
        _persistedCameraProfile = null;
    }

    private void AppendWorkspacePreferenceWarning(string warning)
    {
        WorkspacePreferenceWarning = string.IsNullOrWhiteSpace(WorkspacePreferenceWarning)
            ? warning
            : WorkspacePreferenceWarning + " · " + warning;
    }

    private static string CameraProfileFingerprint(EditorCameraProfile profile)
    {
        static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string Pose(EditorCameraPose pose) => string.Join(',',
            Number(pose.Pivot.X), Number(pose.Pivot.Y), Number(pose.Pivot.Z),
            Number(pose.YawDegrees), Number(pose.PitchDegrees), Number(pose.Distance),
            pose.Projection, Number(pose.FieldOfView), Number(pose.OrthographicSize));

        return string.Join('|',
            profile.Id.Value,
            profile.SchemaVersion,
            Number(profile.NavigationSpeed),
            Number(profile.MouseSensitivity),
            Pose(profile.Pose),
            profile.Provenance,
            profile.Input.PilotMode,
            profile.Input.ShareBookmarksWithProject,
            string.Join(';', profile.Input.Bindings.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}")),
            string.Join(';', profile.Bookmarks.Select(bookmark => $"{bookmark.Id.Value}:{bookmark.Name}:{Pose(bookmark.Pose)}")));
    }

    private static string CameraOperationLabel(EditorCameraOperation operation) => operation switch
    {
        EditorCameraOperation.Orbit => "orbite",
        EditorCameraOperation.Pan => "déplacement latéral",
        EditorCameraOperation.Zoom => "zoom adaptatif",
        EditorCameraOperation.Focus => "sélection cadrée",
        EditorCameraOperation.SetView => "vue orthographique",
        EditorCameraOperation.SetSpeed => "vitesse",
        EditorCameraOperation.SetSensitivity => "sensibilité",
        EditorCameraOperation.SaveBookmark => "point de vue mémorisé",
        EditorCameraOperation.RecallBookmark => "point de vue rappelé",
        EditorCameraOperation.Pilot => "pilotage",
        EditorCameraOperation.SetInput => "touches configurées",
        EditorCameraOperation.SetPose => "position restaurée",
        _ => "navigation",
    };
}
