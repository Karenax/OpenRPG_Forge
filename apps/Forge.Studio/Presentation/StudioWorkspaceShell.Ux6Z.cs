using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAssistedAbilityWorkbenchUx6Z _assistedAbilityUx6Z = null!;
    private Button _assistedAbilityButtonUx6Z = null!;

    private void BuildUx6ZActions(HBoxContainer headingRow)
    {
        _assistedAbilityButtonUx6Z = StudioStyle.Button("Créer un pouvoir", compact: true);
        _assistedAbilityButtonUx6Z.TooltipText =
            "Ciblage, coûts, effets, dépendances, cinq tests de combat, équilibrage et plan approuvé.";
        _assistedAbilityButtonUx6Z.Pressed += OpenAssistedAbilityUx6Z;
        headingRow.AddChild(_assistedAbilityButtonUx6Z);
    }

    private void InitializeUx6ZOverlay()
    {
        _assistedAbilityUx6Z = new StudioAssistedAbilityWorkbenchUx6Z(_session);
        _assistedAbilityUx6Z.StatusChanged += SetStatus;
        _assistedAbilityUx6Z.AbilityChanged += RefreshAfterUx6ZMutation;
        AddChild(_assistedAbilityUx6Z);
    }

    private void OpenAssistedAbilityUx6Z()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de créer un pouvoir.", error: true);
            return;
        }
        if (PageId != "abilities") SwitchPage("abilities", notify: false);
        CloseUx6EOverlay();
        _assistedAbilityUx6Z.Open();
        SetStatus("UX6-Z · capacité canonique, cinq tests de combat et approbation explicite.");
    }

    private void RefreshAfterUx6ZMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6ZActionState();
    }

    private void UpdateUx6ZActionState()
    {
        if (_assistedAbilityButtonUx6Z is not null)
            _assistedAbilityButtonUx6Z.Visible = PageId == "abilities" && _session.Project is not null;
    }

    private void CloseUx6ZOverlay()
    {
        if (_assistedAbilityUx6Z is not null) _assistedAbilityUx6Z.Close();
    }

    internal bool PrepareUx6ZEvidence(string mode)
    {
        if (PageId != "abilities") SwitchPage("abilities", notify: false);
        CloseUx6EOverlay();
        _assistedAbilityUx6Z.Open();
        return _assistedAbilityUx6Z.PrepareEvidence(mode);
    }

    internal bool RunUx6ZSmoke()
    {
        var originalPage = PageId;
        var originalEOpen = _gameplayProductionWorkbenchUx6E.IsOpen;
        var originalZOpen = _assistedAbilityUx6Z.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var approvalE = _session.CaptureGameplayProductionApprovalUx6E();
        var approvalZ = _session.CaptureAbilityApprovalUx6Z();

        var uiTransaction = _assistedAbilityUx6Z.RunUiTransactionEvidence();
        var prepared = PrepareUx6ZEvidence("ux6-z-assisted-ability");
        var uiReady = prepared && _assistedAbilityUx6Z.IsOpen &&
                      _assistedAbilityUx6Z.UsesIsolatedEvidence &&
                      (_assistedAbilityUx6Z.UsesRealModuleEvidence ^
                       _assistedAbilityUx6Z.UsesCanonicalFallbackEvidence) &&
                      _assistedAbilityUx6Z.HasExplicitSelection &&
                      _assistedAbilityUx6Z.PreviewReady &&
                      _assistedAbilityUx6Z.ShowsThreeWorkflows &&
                      _assistedAbilityUx6Z.ShowsFiveTests &&
                      _assistedAbilityUx6Z.ShowsBalance &&
                      _assistedAbilityUx6Z.ShowsGovernance &&
                      _assistedAbilityUx6Z.ShowsHumanSurface &&
                      _assistedAbilityUx6Z.EvidenceSourceUntouched;
        _assistedAbilityUx6Z.CloseEvidence();
        var released = _assistedAbilityUx6Z.EvidenceResourcesReleased;
        var sourceRestored = _assistedAbilityUx6Z.SourceSessionRestored;
        var contract = _session.RunUx6ZContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalEOpen) _gameplayProductionWorkbenchUx6E.RestoreSourceSurfaceAfterSmoke();
        if (originalZOpen) _assistedAbilityUx6Z.RestoreSourceSurfaceAfterSmoke();
        else _assistedAbilityUx6Z.HideSourceSurfaceAfterSmoke();
        _session.RestoreGameplayProductionApprovalUx6E(approvalE);
        _session.RestoreAbilityApprovalUx6Z(approvalZ);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo &&
                              currentHistory?.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive &&
                              _session.CaptureGameplayProductionApprovalUx6E() == approvalE &&
                              _session.CaptureAbilityApprovalUx6Z() == approvalZ;
        var surfaceRestored = PageId == originalPage &&
                              _gameplayProductionWorkbenchUx6E.IsOpen == originalEOpen &&
                              _assistedAbilityUx6Z.IsOpen == originalZOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_Z_{(valid ? "PASS" : "FAILED")} " +
                 "route=AI-020 " +
                 $"selection_explicit={contract.ExplicitOwnerSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.UsesRealModuleEvidence.ToString().ToLowerInvariant()} " +
                 $"canonical_fallback={contract.UsesCanonicalFallbackEvidence.ToString().ToLowerInvariant()} " +
                 $"script_resolved={contract.ScriptResolved.ToString().ToLowerInvariant()} " +
                 $"optional_item={contract.OptionalItemResolved.ToString().ToLowerInvariant()} " +
                 $"invalid_configuration={contract.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"approval_required={contract.ApprovalRequired.ToString().ToLowerInvariant()} " +
                 $"permission_required={contract.PermissionRequired.ToString().ToLowerInvariant()} " +
                 $"five_combat_tests={contract.FiveCombatTests.ToString().ToLowerInvariant()} " +
                 $"balance={contract.BalanceResolved.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={contract.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"runtime_document={contract.RuntimeDocument.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"owner_neighbors={contract.OwnerNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"module_neighbors={contract.ModuleNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"project_neighbors={contract.ProjectNeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"human={contract.HumanText.ToString().ToLowerInvariant()} " +
                 $"bounded={contract.RuntimeClaimsBounded.ToString().ToLowerInvariant()} " +
                 $"ui_surface={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
