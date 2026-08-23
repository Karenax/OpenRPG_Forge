using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioGameplayProductionWorkbenchUx6E _gameplayProductionWorkbenchUx6E = null!;
    private Button _gameplayProductionButtonUx6E = null!;

    private void BuildUx6EActions(HBoxContainer headingRow)
    {
        _gameplayProductionButtonUx6E = StudioStyle.Button("Gameplay étendu", compact: true);
        _gameplayProductionButtonUx6E.TooltipText =
            "Apesanteur, véhicules, montures et succès, sous une commande canonique.";
        _gameplayProductionButtonUx6E.Pressed += OpenGameplayProductionUx6E;
        headingRow.AddChild(_gameplayProductionButtonUx6E);
    }

    private void InitializeUx6EOverlay()
    {
        _gameplayProductionWorkbenchUx6E = new StudioGameplayProductionWorkbenchUx6E(_session);
        _gameplayProductionWorkbenchUx6E.StatusChanged += SetStatus;
        _gameplayProductionWorkbenchUx6E.GameplayProductionChanged += RefreshAfterUx6EMutation;
        AddChild(_gameplayProductionWorkbenchUx6E);
    }

    private void OpenGameplayProductionUx6E()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer son gameplay étendu.", error: true);
            return;
        }
        if (PageId is not ("runtime" or "abilities")) SwitchPage("runtime", notify: false);
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        CloseUx6WOverlay();
        CloseUx6XOverlay();
        CloseUx6YOverlay();
        CloseUx6ZOverlay();
        _gameplayProductionWorkbenchUx6E.Open();
        SetStatus("UX6-E · quatre profils, dry-run canonique et décisions déterministes.");
    }

    private void RefreshAfterUx6EMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6EActionState();
    }

    private void UpdateUx6EActionState()
    {
        if (_gameplayProductionButtonUx6E is not null)
            _gameplayProductionButtonUx6E.Visible = PageId is "runtime" or "abilities" &&
                                                     _session.Project is not null;
    }

    private void CloseUx6EOverlay()
    {
        if (_gameplayProductionWorkbenchUx6E is not null) _gameplayProductionWorkbenchUx6E.Close();
    }

    internal bool PrepareUx6EEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseUx6BOverlay();
        CloseUx6COverlay();
        CloseUx6DOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        CloseUx6ZOverlay();
        _gameplayProductionWorkbenchUx6E.Open();
        return _gameplayProductionWorkbenchUx6E.PrepareEvidence(mode);
    }

    internal bool RunUx6ESmoke()
    {
        var originalPage = PageId;
        var originalOpen = _gameplayProductionWorkbenchUx6E.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureGameplayProductionApprovalUx6E();

        var uiTransaction = _gameplayProductionWorkbenchUx6E.RunUiTransactionEvidence();
        var prepared = PrepareUx6EEvidence("ux6-e-gameplay-production");
        var uiReady = prepared && _gameplayProductionWorkbenchUx6E.IsOpen &&
                      _gameplayProductionWorkbenchUx6E.UsesIsolatedEvidence &&
                      _gameplayProductionWorkbenchUx6E.PreviewReady &&
                      _gameplayProductionWorkbenchUx6E.HasExplicitSelection &&
                      _gameplayProductionWorkbenchUx6E.ShowsFourWorkflows &&
                      _gameplayProductionWorkbenchUx6E.ShowsHumanSurface &&
                      _gameplayProductionWorkbenchUx6E.EvidenceSourceUntouched;
        _gameplayProductionWorkbenchUx6E.CloseEvidence();
        var released = _gameplayProductionWorkbenchUx6E.EvidenceResourcesReleased;
        var sourceRestored = _gameplayProductionWorkbenchUx6E.SourceSessionRestored;
        var contract = _session.RunUx6EContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _gameplayProductionWorkbenchUx6E.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplayProductionApprovalUx6E(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureGameplayProductionApprovalUx6E() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayProductionWorkbenchUx6E.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_E_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleData.ToString().ToLowerInvariant()} " +
                 $"zero_gravity={contract.ZeroGravityValidation.ToString().ToLowerInvariant()} " +
                 $"vehicles={contract.VehicleValidation.ToString().ToLowerInvariant()} " +
                 $"mounts={contract.MountValidation.ToString().ToLowerInvariant()} " +
                 $"achievements={contract.AchievementValidation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"runtime_preview={contract.RuntimePreview.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbor_profiles={contract.NeighborProfilesPreserved.ToString().ToLowerInvariant()} " +
                 $"stable_ids={contract.StableIdentities.ToString().ToLowerInvariant()} " +
                 $"neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
