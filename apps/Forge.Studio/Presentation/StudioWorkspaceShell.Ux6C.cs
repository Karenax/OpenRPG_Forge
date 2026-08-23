using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioGameplayCapabilitiesWorkbenchUx6C _gameplayCapabilitiesWorkbenchUx6C = null!;
    private Button _gameplayCapabilitiesButtonUx6C = null!;

    private void BuildUx6CActions(HBoxContainer headingRow)
    {
        _gameplayCapabilitiesButtonUx6C = StudioStyle.Button("Capacités gameplay", compact: true);
        _gameplayCapabilitiesButtonUx6C.TooltipText =
            "Nage, escalade, menu radial, temps tactique et modes de combat.";
        _gameplayCapabilitiesButtonUx6C.Pressed += OpenGameplayCapabilitiesUx6C;
        headingRow.AddChild(_gameplayCapabilitiesButtonUx6C);
    }

    private void InitializeUx6COverlay()
    {
        _gameplayCapabilitiesWorkbenchUx6C = new StudioGameplayCapabilitiesWorkbenchUx6C(_session);
        _gameplayCapabilitiesWorkbenchUx6C.StatusChanged += SetStatus;
        _gameplayCapabilitiesWorkbenchUx6C.GameplayCapabilitiesChanged += RefreshAfterUx6CMutation;
        AddChild(_gameplayCapabilitiesWorkbenchUx6C);
    }

    private void OpenGameplayCapabilitiesUx6C()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de configurer ses capacités gameplay.", error: true);
            return;
        }
        SwitchPage("runtime", notify: false);
        CloseUx6BOverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        CloseUx6WOverlay();
        CloseUx6XOverlay();
        CloseUx6YOverlay();
        _gameplayCapabilitiesWorkbenchUx6C.Open();
        SetStatus("UX6-C · cinq profils, dry-run canonique et résolveurs déterministes.");
    }

    private void RefreshAfterUx6CMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6CActionState();
    }

    private void UpdateUx6CActionState()
    {
        if (_gameplayCapabilitiesButtonUx6C is not null)
            _gameplayCapabilitiesButtonUx6C.Visible = PageId == "runtime" && _session.Project is not null;
    }

    private void CloseUx6COverlay()
    {
        if (_gameplayCapabilitiesWorkbenchUx6C is not null) _gameplayCapabilitiesWorkbenchUx6C.Close();
    }

    internal bool PrepareUx6CEvidence(string mode)
    {
        if (PageId != "runtime") SwitchPage("runtime", notify: false);
        CloseUx6BOverlay();
        CloseUx6DOverlay();
        CloseUx6EOverlay();
        CloseUx6ROverlay();
        CloseUx6SOverlay();
        CloseUx6TOverlay();
        CloseUx6UOverlay();
        CloseUx6VOverlay();
        _gameplayCapabilitiesWorkbenchUx6C.Open();
        return _gameplayCapabilitiesWorkbenchUx6C.PrepareEvidence(mode);
    }

    internal bool RunUx6CSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _gameplayCapabilitiesWorkbenchUx6C.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureGameplayCapabilityApprovalUx6C();

        var uiTransaction = _gameplayCapabilitiesWorkbenchUx6C.RunUiTransactionEvidence();
        var prepared = PrepareUx6CEvidence("ux6-c-gameplay-capabilities");
        var uiReady = prepared && _gameplayCapabilitiesWorkbenchUx6C.IsOpen &&
                      _gameplayCapabilitiesWorkbenchUx6C.UsesIsolatedEvidence &&
                      _gameplayCapabilitiesWorkbenchUx6C.PreviewReady &&
                      _gameplayCapabilitiesWorkbenchUx6C.HasExplicitSelection &&
                      _gameplayCapabilitiesWorkbenchUx6C.ShowsFiveWorkflows &&
                      _gameplayCapabilitiesWorkbenchUx6C.ShowsHumanSurface &&
                      _gameplayCapabilitiesWorkbenchUx6C.EvidenceSourceUntouched;
        _gameplayCapabilitiesWorkbenchUx6C.CloseEvidence();
        var released = _gameplayCapabilitiesWorkbenchUx6C.EvidenceResourcesReleased;
        var sourceRestored = _gameplayCapabilitiesWorkbenchUx6C.SourceSessionRestored;
        var contract = _session.RunUx6CContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _gameplayCapabilitiesWorkbenchUx6C.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreGameplayCapabilityApprovalUx6C(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureGameplayCapabilityApprovalUx6C() == originalApproval;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayCapabilitiesWorkbenchUx6C.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_C_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleData.ToString().ToLowerInvariant()} " +
                 $"swimming={contract.SwimmingValidation.ToString().ToLowerInvariant()} " +
                 $"climbing={contract.ClimbingValidation.ToString().ToLowerInvariant()} " +
                 $"radial={contract.RadialValidation.ToString().ToLowerInvariant()} " +
                 $"time_control={contract.TimeControlValidation.ToString().ToLowerInvariant()} " +
                 $"combat={contract.CombatValidation.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"runtime_preview={contract.RuntimePreview.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"foundation_tail={contract.FoundationPreserved.ToString().ToLowerInvariant()} " +
                 $"gameplay_tails={contract.GameplayTailsPreserved.ToString().ToLowerInvariant()} " +
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
