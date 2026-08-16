using Forge.Application.Creator;
using Forge.Domain.Authoring;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private Button _viewportToolsButton = null!;
    private StudioViewportToolsOverlay _viewportToolsOverlay = null!;

    private void InitializeUx0COverlays()
    {
        _viewportToolsOverlay = new StudioViewportToolsOverlay(_session, WorldViewport);
        _viewportToolsOverlay.StateChanged += ApplyViewportToolsState;
        _viewportToolsOverlay.StatusChanged += SetStatus;
        AddChild(_viewportToolsOverlay);
    }

    private void OpenViewportTools()
    {
        _globalSearchOverlay.Close();
        _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close();
        _viewportToolsOverlay.Open();
    }

    private void ApplyViewportToolsState()
    {
        WorldViewport.SetViewportMode(_session.WorkspaceProfile.Shell.ViewportMode);
        WorldViewport.RefreshManipulationProfile();
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        NavigationChanged?.Invoke();
    }

    internal bool PrepareUx0CEvidence(string mode)
    {
        if (!WorldViewport.PrepareEditingEvidence(_session, showContextMenu: false))
        {
            return false;
        }
        return _viewportToolsOverlay.PrepareEvidence(mode);
    }

    internal bool RunUx0CSmoke()
    {
        var originalWorkspace = _session.WorkspaceProfile;
        var originalCamera = _session.CameraProfile;
        var originalProject = _session.Project;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var entities = _session.ActiveArea!.Entities.Take(3).ToArray();
        if (entities.Length < 3)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX0C_FAILED reason=entities_missing");
            return false;
        }

        var modesValid = true;
        var navigationOverlay = false;
        var collisionOverlay = false;
        foreach (var mode in Enum.GetValues<EditorViewportMode>())
        {
            var applied = _session.ConfigureViewportMode(mode, persist: false);
            WorldViewport.SetViewportMode(mode);
            modesValid &= applied.Succeeded && WorldViewport.ViewportMode == mode;
            navigationOverlay |= mode == EditorViewportMode.Navigation &&
                                 WorldViewport.DiagnosticOverlayName == "NavigationOverlay";
            collisionOverlay |= mode == EditorViewportMode.Collisions &&
                                WorldViewport.DiagnosticOverlayName == "CollisionsOverlay";
        }

        var directSource = _session.WorkspaceProfile.Shell.DirectManipulation;
        var selection = new EditorSelectionConfiguration(
            directSource.Selection.Id,
            1,
            "studio:ux0-c-smoke",
            multiSelectionEnabled: true,
            focusOnSelection: true,
            frameSelection: true,
            lockSelection: false,
            editCommonPropertiesOnly: true);
        var transform = new EditorTransformToolConfiguration(
            directSource.Transform.Id,
            1,
            "studio:ux0-c-smoke",
            EditorTransformTool.Scale,
            EditorCoordinateSpace.Local,
            EditorPivotMode.SelectionCenter,
            translationSnap: .25f,
            rotationSnapDegrees: 15,
            scaleSnap: .05f,
            numericInputEnabled: true,
            alignmentEnabled: true);
        var direct = new EditorDirectManipulationProfile(
            directSource.Id,
            1,
            "studio:ux0-c-smoke",
            selection,
            transform);
        var directPreview = _session.PreviewDirectManipulation(direct);
        var directApply = _session.ConfigureDirectManipulation(direct, persist: false);
        _session.ConfigureViewportMode(EditorViewportMode.Editing, persist: false);
        WorldViewport.SetViewportMode(EditorViewportMode.Editing);
        WorldViewport.RefreshManipulationProfile();

        var ids = entities.Select(entity => entity.Id).ToArray();
        var multiSelected = _session.SelectEntities(ids, ids[1]);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        var selectionVisible = WorldViewport.SelectedEntityCount == 3;

        var lockedSelection = new EditorSelectionConfiguration(
            selection.Id,
            1,
            "studio:ux0-c-smoke-lock",
            true,
            true,
            true,
            lockSelection: true,
            editCommonPropertiesOnly: true);
        var lockedDirect = new EditorDirectManipulationProfile(
            direct.Id,
            1,
            "studio:ux0-c-smoke-lock",
            lockedSelection,
            transform);
        var lockApply = _session.ConfigureDirectManipulation(lockedDirect, persist: false);
        var workspaceUndo = _session.UndoWorkspaceProfile(persist: false);
        var lockUndone = !_session.WorkspaceProfile.Shell.DirectManipulation.Selection.LockSelection;
        var workspaceRedo = _session.RedoWorkspaceProfile(persist: false);
        var lockRedone = _session.WorkspaceProfile.Shell.DirectManipulation.Selection.LockSelection;
        WorldViewport.RefreshManipulationProfile();

        var originals = entities.ToDictionary(entity => entity.Id, entity => entity.Transform);
        var translationPreview = _session.PreviewSelectedManipulation(
            EditorTransformTool.Translate,
            new Vector3Value(.25f, 0, 0),
            configuration: transform);
        var projectUnchangedByPreview = ReferenceEquals(_session.Project, originalProject);
        var translation = _session.ManipulateSelectedEntities(
            EditorTransformTool.Translate,
            new Vector3Value(.25f, 0, 0),
            configuration: transform);
        var translated = translation.Succeeded && entities.All(source =>
            _session.ActiveArea!.Entities.Single(entity => entity.Id == source.Id).Transform != originals[source.Id]);
        var projectUndo = translation.Succeeded && _session.Undo().Succeeded;
        var translationUndone = projectUndo && entities.All(source =>
            _session.ActiveArea!.Entities.Single(entity => entity.Id == source.Id).Transform == originals[source.Id]);
        var projectRedo = projectUndo && _session.Redo().Succeeded;
        var translationRedone = projectRedo && entities.All(source =>
            _session.ActiveArea!.Entities.Single(entity => entity.Id == source.Id).Transform != originals[source.Id]);

        var rotation = _session.ManipulateSelectedEntities(
            EditorTransformTool.Rotate,
            new Vector3Value(0, 15, 0),
            configuration: transform);
        var rotated = rotation.Succeeded && entities.All(source =>
            _session.ActiveArea!.Entities.Single(entity => entity.Id == source.Id).Transform.Rotation !=
            originals[source.Id].Rotation);
        var scale = _session.ManipulateSelectedEntities(
            EditorTransformTool.Scale,
            new Vector3Value(1.05f, 1.05f, 1.05f),
            configuration: transform);
        var scaled = scale.Succeeded && entities.All(source =>
            _session.ActiveArea!.Entities.Single(entity => entity.Id == source.Id).Transform.Scale !=
            originals[source.Id].Scale);
        var alignment = _session.ManipulateSelectedEntities(
            EditorTransformTool.Translate,
            Vector3Value.Zero,
            EditorTransformAlignment.MinimumX,
            transform);
        var aligned = alignment.Succeeded && _session.ActiveArea!.Entities
            .Where(entity => ids.Contains(entity.Id))
            .Select(entity => entity.Transform.Position.X)
            .Distinct()
            .Count() == 1;
        var invalidScaleBlocked = !_session.PreviewSelectedManipulation(
            EditorTransformTool.Scale,
            Vector3Value.Zero,
            configuration: transform).Succeeded;

        while (_session.CanUndo && !ReferenceEquals(_session.Project, originalProject))
        {
            _session.Undo();
        }
        var projectRestored = ReferenceEquals(_session.Project, originalProject) && !_session.IsDirty;

        var cameraViews = 0;
        foreach (var view in Enum.GetValues<EditorCameraView>())
        {
            var result = _session.NavigateCamera(EditorCameraOperation.SetView, view: view);
            if (result.Succeeded)
            {
                cameraViews++;
                WorldViewport.ApplyCameraProfile(_session.CameraProfile);
            }
        }
        var orthographic = WorldViewport.CameraProjection == EditorCameraProjection.Orthographic;
        var input = new EditorCameraInputConfiguration(
            originalCamera.Input.Id,
            EditorCameraInputConfiguration.CurrentSchemaVersion,
            "studio:ux0-c-smoke",
            EditorCameraPilotMode.Fly,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["forward"] = "I",
                ["back"] = "K",
                ["left"] = "J",
                ["right"] = "L",
                ["up"] = "U",
                ["down"] = "O",
                ["focus"] = "F",
            });
        var speed = _session.NavigateCamera(EditorCameraOperation.SetSpeed, x: 2);
        var sensitivity = _session.NavigateCamera(EditorCameraOperation.SetSensitivity, x: .5f);
        var configuredInput = _session.NavigateCamera(EditorCameraOperation.SetInput, input: input);
        var beforePilot = _session.CameraProfile.Pose;
        var pilot = _session.NavigateCamera(EditorCameraOperation.Pilot, x: 2, y: 1, z: 3);
        var pilotedPose = _session.CameraProfile.Pose;
        var cameraUndo = pilot.Succeeded && _session.UndoCamera().Succeeded &&
                         _session.CameraProfile.Pose == beforePilot;
        var cameraRedo = cameraUndo && _session.RedoCamera().Succeeded &&
                         _session.CameraProfile.Pose == pilotedPose;
        var bookmarkId = EditorCameraBookmarkId.From(
            Guid.Parse("ca200000-0000-4000-8000-000000000001"));
        var bookmark = _session.NavigateCamera(
            EditorCameraOperation.SaveBookmark,
            bookmarkId: bookmarkId,
            bookmarkName: "Vue UX0-C");
        var moveAfterBookmark = _session.NavigateCamera(EditorCameraOperation.Pan, x: 20);
        var recall = _session.NavigateCamera(
            EditorCameraOperation.RecallBookmark,
            bookmarkId: bookmarkId);
        var bookmarkRecalled = bookmark.Succeeded && moveAfterBookmark.Succeeded && recall.Succeeded &&
                               _session.CameraProfile.Pose == pilotedPose;
        var invalidCameraBlocked = !_session.NavigateCamera(
            EditorCameraOperation.SetSpeed,
            x: 0,
            dryRun: true).Succeeded;

        var roundTrip = VerifyUx0CRoundTrip(_session.WorkspaceProfile, _session.CameraProfile);
        while (_session.CanUndoCamera && !ReferenceEquals(_session.CameraProfile, originalCamera))
        {
            _session.UndoCamera();
        }
        var cameraRestored = ReferenceEquals(_session.CameraProfile, originalCamera);

        _session.ApplyWorkspaceProfile(originalWorkspace);
        _session.SelectEntities(originalSelection, originalActive);
        WorldViewport.Rebuild(_session, frameArea: false);
        WorldViewport.ApplyCameraProfile(_session.CameraProfile);
        WorldViewport.SetViewportMode(_session.WorkspaceProfile.Shell.ViewportMode);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        RefreshInspector();
        UpdateCommandState();

        var valid = modesValid && navigationOverlay && collisionOverlay &&
                    directPreview.Succeeded && directApply.Succeeded &&
                    multiSelected && selectionVisible && WorldViewport.ScaleGizmoHandleCount == 4 &&
                    lockApply.Succeeded && workspaceUndo.Succeeded && lockUndone &&
                    workspaceRedo.Succeeded && lockRedone &&
                    translationPreview.Succeeded && projectUnchangedByPreview && translated &&
                    projectUndo && translationUndone && projectRedo && translationRedone &&
                    rotated && scaled && aligned && invalidScaleBlocked && projectRestored &&
                    cameraViews == 6 && orthographic && speed.Succeeded && sensitivity.Succeeded &&
                    configuredInput.Succeeded && cameraUndo && cameraRedo && bookmarkRecalled &&
                    invalidCameraBlocked && roundTrip && cameraRestored &&
                    !_session.WorkspaceProfileHasUnsavedChanges;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0C_{(valid ? "PASS" : "FAILED")} " +
            $"modes=6 navigation_overlay={navigationOverlay.ToString().ToLowerInvariant()} " +
            $"collision_overlay={collisionOverlay.ToString().ToLowerInvariant()} camera_views={cameraViews} " +
            $"camera_undo={(cameraUndo && cameraRedo).ToString().ToLowerInvariant()} " +
            $"camera_roundtrip={roundTrip.ToString().ToLowerInvariant()} selection={ids.Length} " +
            $"lock={lockRedone.ToString().ToLowerInvariant()} translate={translated.ToString().ToLowerInvariant()} " +
            $"rotate={rotated.ToString().ToLowerInvariant()} scale={scaled.ToString().ToLowerInvariant()} " +
            $"alignment={aligned.ToString().ToLowerInvariant()} " +
            $"project_undo={(projectUndo && projectRedo).ToString().ToLowerInvariant()} " +
            $"workspace_undo={(workspaceUndo.Succeeded && workspaceRedo.Succeeded).ToString().ToLowerInvariant()} " +
            $"invalid_blocked={(invalidScaleBlocked && invalidCameraBlocked).ToString().ToLowerInvariant()} " +
            "windows=1 persisted=false");
        return valid;
    }

    private static bool VerifyUx0CRoundTrip(
        EditorWorkspaceProfile workspace,
        EditorCameraProfile camera)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux0c-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            EditorWorkspaceProfileStore.Save(root, workspace);
            var cameraSave = EditorCameraProfileStore.Save(root, camera);
            var restoredWorkspace = EditorWorkspaceProfileStore.TryLoad(root);
            var restoredCamera = EditorCameraProfileStore.LoadOrCreate(
                root,
                _sessionProjectId(camera));
            return cameraSave.Succeeded && restoredWorkspace is not null &&
                   restoredWorkspace.Shell.ViewportMode == workspace.Shell.ViewportMode &&
                   restoredWorkspace.Shell.DirectManipulation.Transform.Tool ==
                   workspace.Shell.DirectManipulation.Transform.Tool &&
                   restoredCamera.Diagnostics.Count == 0 &&
                   restoredCamera.Profile.Pose == camera.Pose &&
                   restoredCamera.Profile.Input.Bindings.SequenceEqual(camera.Input.Bindings) &&
                   restoredCamera.Profile.Bookmarks.Count == camera.Bookmarks.Count;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        static Forge.Domain.Projects.ProjectId _sessionProjectId(EditorCameraProfile profile) =>
            Forge.Domain.Projects.ProjectId.From(profile.Id.Value);
    }
}
