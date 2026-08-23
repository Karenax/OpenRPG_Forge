using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioRulesWorldWorkbenchUx6A _rulesWorldWorkbenchUx6A = null!;
    private Button _rulesWorldButtonUx6A = null!;

    private void BuildUx6AActions(HBoxContainer headingRow)
    {
        _rulesWorldButtonUx6A = StudioStyle.Button("Règles & temps", compact: true);
        _rulesWorldButtonUx6A.TooltipText =
            "Difficultés, mutateurs, plugins de règles et temps du monde par deux transactions explicites.";
        _rulesWorldButtonUx6A.Pressed += OpenRulesWorldUx6A;
        headingRow.AddChild(_rulesWorldButtonUx6A);
    }

    private void InitializeUx6AOverlay()
    {
        _rulesWorldWorkbenchUx6A = new StudioRulesWorldWorkbenchUx6A(_session);
        _rulesWorldWorkbenchUx6A.StatusChanged += SetStatus;
        _rulesWorldWorkbenchUx6A.RulesWorldChanged += RefreshAfterUx6AMutation;
        AddChild(_rulesWorldWorkbenchUx6A);
    }

    private void OpenRulesWorldUx6A()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant de composer ses règles.", error: true);
            return;
        }
        SwitchPage("rules", notify: false);
        _rulesWorldWorkbenchUx6A.Open();
        SetStatus("UX6-A · sélection explicite, deux dry-runs et aucune extension exécutée.");
    }

    private void RefreshAfterUx6AMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx6AActionState();
    }

    private void UpdateUx6AActionState()
    {
        if (_rulesWorldButtonUx6A is not null)
            _rulesWorldButtonUx6A.Visible = PageId == "rules" && _session.Project is not null;
    }

    private void CloseUx6AOverlay()
    {
        if (_rulesWorldWorkbenchUx6A is not null) _rulesWorldWorkbenchUx6A.Close();
    }

    internal bool PrepareUx6AEvidence(string mode)
    {
        if (PageId != "rules") SwitchPage("rules", notify: false);
        _rulesWorldWorkbenchUx6A.Open();
        return _rulesWorldWorkbenchUx6A.PrepareEvidence(mode);
    }

    internal bool RunUx6ASmoke()
    {
        var originalPage = PageId;
        var originalOpen = _rulesWorldWorkbenchUx6A.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureRulesWorldApprovalUx6A();

        var uiTransaction = _rulesWorldWorkbenchUx6A.RunUiTransactionEvidence();
        var prepared = PrepareUx6AEvidence("ux6-a-rules");
        var uiReady = prepared && _rulesWorldWorkbenchUx6A.IsOpen &&
                      _rulesWorldWorkbenchUx6A.UsesIsolatedEvidence &&
                      _rulesWorldWorkbenchUx6A.RulesPreviewReady &&
                      _rulesWorldWorkbenchUx6A.WorldPreviewReady &&
                      _rulesWorldWorkbenchUx6A.HasExplicitSelection &&
                      _rulesWorldWorkbenchUx6A.ShowsFourWorkflows &&
                      _rulesWorldWorkbenchUx6A.ShowsTwoTransactions &&
                      _rulesWorldWorkbenchUx6A.ShowsHumanSurface &&
                      _rulesWorldWorkbenchUx6A.EvidenceSourceUntouched;
        _rulesWorldWorkbenchUx6A.CloseEvidence();
        var released = _rulesWorldWorkbenchUx6A.EvidenceResourcesReleased;
        var sourceRestored = _rulesWorldWorkbenchUx6A.SourceSessionRestored;
        var contract = _session.RunUx6AContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _rulesWorldWorkbenchUx6A.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreRulesWorldApprovalUx6A(originalApproval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              _session.CaptureRulesWorldApprovalUx6A() == originalApproval;
        var surfaceRestored = PageId == originalPage && _rulesWorldWorkbenchUx6A.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sourceRestored &&
                    sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX6_A_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"real_module={contract.RealModuleData.ToString().ToLowerInvariant()} " +
                 $"difficulty={contract.DifficultyValidation.ToString().ToLowerInvariant()} " +
                 $"plugins={contract.PluginValidation.ToString().ToLowerInvariant()} " +
                 $"world_time={contract.WorldTimeValidation.ToString().ToLowerInvariant()} " +
                 $"rules_dry_run={contract.RulesDryRun.ToString().ToLowerInvariant()} " +
                 $"world_dry_run={contract.WorldTimeDryRun.ToString().ToLowerInvariant()} " +
                 $"runtime_preview={contract.RuntimePreview.ToString().ToLowerInvariant()} " +
                 $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_commands={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
                 $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
                 $"rules_tails={contract.RulesTailsPreserved.ToString().ToLowerInvariant()} " +
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
