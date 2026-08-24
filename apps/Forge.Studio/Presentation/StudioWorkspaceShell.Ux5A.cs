using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioDialogueFoundationPage _dialogueFoundationPageUx5A = null!;
    private bool _preparingUx5AEvidence;
    private bool _restoringUx5ASmoke;

    private void InitializeUx5APage(Control pageStack)
    {
        ArgumentNullException.ThrowIfNull(pageStack);
        _dialogueFoundationPageUx5A = new StudioDialogueFoundationPage(_session);
        _dialogueFoundationPageUx5A.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dialogueFoundationPageUx5A.StatusChanged += SetStatus;
        _dialogueFoundationPageUx5A.DocumentChanged += RefreshAfterUx5ADialogueMutation;
        pageStack.AddChild(_dialogueFoundationPageUx5A);
    }

    private void UpdateUx5APage(string pageId, bool preserveApproval = false)
    {
        if (_dialogueFoundationPageUx5A is null)
        {
            return;
        }
        preserveApproval |= _restoringUx5ASmoke;
        if (string.Equals(pageId, "dialogues", StringComparison.Ordinal))
        {
            _worldSplit.Visible = false;
            _specializedPage.Visible = false;
            _projectPage.Visible = false;
            _assetLibraryPage.Visible = false;
            if (!_preparingUx5AEvidence)
            {
                if (preserveApproval)
                {
                    _dialogueFoundationPageUx5A.RestoreSourceSurfaceAfterSmoke();
                }
                else
                {
                    _dialogueFoundationPageUx5A.Open();
                }
            }
            SetStatus(
                "Dialogues UX5-A · sélection explicite · lignes et choix · dry-run obligatoire");
            return;
        }
        if (preserveApproval)
        {
            _dialogueFoundationPageUx5A.HideSourceSurfaceAfterSmoke();
        }
        else
        {
            CloseUx5APage();
        }
    }

    private void CloseUx5APage()
    {
        if (_dialogueFoundationPageUx5A is not null)
        {
            _dialogueFoundationPageUx5A.Close();
        }
    }

    private void RefreshAfterUx5ADialogueMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
    }

    internal bool PrepareUx5AEvidence(string mode)
    {
        _preparingUx5AEvidence = true;
        try
        {
            SwitchPage("dialogues", notify: false);
            UpdateUx5APage("dialogues");
            return _dialogueFoundationPageUx5A.PrepareEvidence(mode);
        }
        finally
        {
            _preparingUx5AEvidence = false;
        }
    }

    internal bool RunUx5ASmoke()
    {
        var originalPageId = PageId;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalCanUndo = originalHistory?.CanUndo ?? false;
        var originalCanRedo = originalHistory?.CanRedo ?? false;
        var originalDirty = _session.IsDirty;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var originalApproval = _session.CaptureDialogueApprovalUx5A();

        var uiTransaction = _dialogueFoundationPageUx5A.RunUiTransactionEvidence();
        var uiTransactionSourceUntouched = _dialogueFoundationPageUx5A.EvidenceSourceUntouched;
        var uiTransactionResourcesReleased = _dialogueFoundationPageUx5A.EvidenceResourcesReleased;
        var uiTransactionSessionRestored = _dialogueFoundationPageUx5A.SourceSessionRestored;

        var evidencePrepared = PrepareUx5AEvidence("ux5-a-dialogue-foundation");
        var uiReady = evidencePrepared && PageId == "dialogues" &&
                      _dialogueFoundationPageUx5A.IsOpen &&
                      _dialogueFoundationPageUx5A.UsesIsolatedEvidence &&
                      _dialogueFoundationPageUx5A.EmptyStateVerified &&
                      _dialogueFoundationPageUx5A.HasExplicitSelection &&
                      _dialogueFoundationPageUx5A.PreviewReady &&
                      _dialogueFoundationPageUx5A.MiniMapReadOnly &&
                      _dialogueFoundationPageUx5A.KeyboardReorderAvailable &&
                      _dialogueFoundationPageUx5A.KeyboardReorderExercised &&
                      _dialogueFoundationPageUx5A.DragDropReorderExercised &&
                      _dialogueFoundationPageUx5A.ProtectedSpeakerPreserved &&
                      _dialogueFoundationPageUx5A.SubgraphRoleProtected &&
                      _dialogueFoundationPageUx5A.PaginationIsInventoryOnly &&
                      _dialogueFoundationPageUx5A.ShowsHumanSurface &&
                      _dialogueFoundationPageUx5A.EvidenceSourceUntouched;
        var keyboardUi = _dialogueFoundationPageUx5A.KeyboardReorderExercised;
        var dragDropUi = _dialogueFoundationPageUx5A.DragDropReorderExercised;
        _dialogueFoundationPageUx5A.Close();
        var evidenceResourcesReleased = _dialogueFoundationPageUx5A.EvidenceResourcesReleased;
        var evidenceSourceUntouched = _dialogueFoundationPageUx5A.EvidenceSourceUntouched;
        var evidenceSessionRestored = _dialogueFoundationPageUx5A.SourceSessionRestored;

        var result = _session.RunUx5AContractSmoke();
        _restoringUx5ASmoke = true;
        try
        {
            SwitchPage(originalPageId, notify: false);
            UpdateUx5APage(originalPageId, preserveApproval: true);
        }
        finally
        {
            _restoringUx5ASmoke = false;
        }
        var surfaceRestored = PageId == originalPageId &&
                              (string.Equals(originalPageId, "dialogues", StringComparison.Ordinal)
                                  ? _dialogueFoundationPageUx5A.IsOpen
                                  : !_dialogueFoundationPageUx5A.IsOpen);
        var currentHistory = _session.History;
        var sourceUntouched = ReferenceEquals(originalProject, _session.Project) &&
                              originalHistory is not null && currentHistory is not null &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory.State.Revision == originalRevision &&
                              currentHistory.Journal.SequenceEqual(originalJournal) &&
                              currentHistory.CanUndo == originalCanUndo &&
                              currentHistory.CanRedo == originalCanRedo &&
                              _session.IsDirty == originalDirty &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              Equals(_session.CaptureDialogueApprovalUx5A(), originalApproval);
        var valid = result.Succeeded && uiTransaction && uiReady &&
                    uiTransactionSourceUntouched && uiTransactionResourcesReleased &&
                    uiTransactionSessionRestored && evidenceSourceUntouched &&
                    evidenceResourcesReleased && evidenceSessionRestored && sourceUntouched && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX5A_{(valid ? "PASS" : "FAILED")} " +
            $"explicit_selection={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"new_dialogue={result.NewDialogue.ToString().ToLowerInvariant()} " +
            $"protected_speaker={result.ProtectedSpeakerPreserved.ToString().ToLowerInvariant()} " +
            $"subgraph_protected={result.SubgraphProtected.ToString().ToLowerInvariant()} " +
            $"structural_choice_diff={result.StructuralChoiceDiff.ToString().ToLowerInvariant()} " +
            $"identity_diff={result.IdentityDiffHuman.ToString().ToLowerInvariant()} " +
            $"branches_unlimited={result.BranchesUnlimited.ToString().ToLowerInvariant()} " +
            $"multiple_entries={result.MultipleEntries.ToString().ToLowerInvariant()} " +
            $"reference_cycle={result.ReferenceCycleAccepted.ToString().ToLowerInvariant()} " +
            $"flow_cycle_blocked={result.FlowCycleBlocked.ToString().ToLowerInvariant()} " +
            $"missing_target_blocked={result.MissingTargetBlocked.ToString().ToLowerInvariant()} " +
            $"negative_priority_blocked={result.NegativePriorityBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRunPure.ToString().ToLowerInvariant()} " +
            $"stale_revision={result.StaleRevisionBlocked.ToString().ToLowerInvariant()} " +
            $"fingerprint={result.FingerprintGuard.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"edges_canonical={result.EdgesCanonical.ToString().ToLowerInvariant()} " +
            $"human_reorder_diff={result.ReorderDiffHuman.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"roundtrip={result.RoundTrip.ToString().ToLowerInvariant()} " +
            $"preservation={result.StructuralPreservation.ToString().ToLowerInvariant()} " +
            $"keyboard_ui={keyboardUi.ToString().ToLowerInvariant()} " +
            $"drag_drop_ui={dragDropUi.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} " +
            $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_untouched={sourceUntouched.ToString().ToLowerInvariant()} " +
            $"resources_released={(result.ResourcesReleased && evidenceResourcesReleased).ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
