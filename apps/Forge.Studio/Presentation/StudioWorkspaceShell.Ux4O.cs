using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureDamageValidationOverlayUx4O _creatureDamageOverlayUx4O = null!;
    private Button _creatureDamageButtonUx4O = null!;

    private void BuildUx4OActions(HBoxContainer headingRow)
    {
        _creatureDamageButtonUx4O = StudioStyle.Button("Dégâts & validation", compact: true);
        _creatureDamageButtonUx4O.TooltipText =
            "Éditer BeforeDamage, DamageApplied, EntityDied et revalider la créature ciblée.";
        _creatureDamageButtonUx4O.Pressed += OpenCreatureDamageUx4O;
        headingRow.AddChild(_creatureDamageButtonUx4O);
    }

    private void InitializeUx4OOverlay()
    {
        _creatureDamageOverlayUx4O = new StudioCreatureDamageValidationOverlayUx4O(_session);
        _creatureDamageOverlayUx4O.StatusChanged += SetStatus;
        _creatureDamageOverlayUx4O.CreatureChanged += RefreshAfterUx4OMutation;
        AddChild(_creatureDamageOverlayUx4O);
    }

    private void OpenCreatureDamageUx4O()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", true);
            return;
        }
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay();
        CloseUx4LOverlay(); CloseUx4MOverlay(); CloseUx4NOverlay();
        CloseUx4POverlay();
        CloseUx4QOverlay(); CloseUx4ROverlay(); _creatureDamageOverlayUx4O.Open();
        SetStatus("Dégâts, mort et validation ouverts · aperçu canonique obligatoire");
    }

    private void RefreshAfterUx4OMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4OActionState();
    }

    private void UpdateUx4OActionState()
    {
        if (_creatureDamageButtonUx4O is not null)
            _creatureDamageButtonUx4O.Visible = PageId == "creatures" && _session.Project is not null;
    }

    private void CloseUx4OOverlay()
    {
        if (_creatureDamageOverlayUx4O is not null) _creatureDamageOverlayUx4O.Close();
    }

    internal bool PrepareUx4OEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        CloseUx4HOverlay(); CloseUx4IOverlay(); CloseUx4JOverlay(); CloseUx4KOverlay();
        CloseUx4LOverlay(); CloseUx4MOverlay(); CloseUx4NOverlay();
        CloseUx4POverlay();
        return _creatureDamageOverlayUx4O.PrepareEvidence(mode);
    }

    internal bool RunUx4OSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _creatureDamageOverlayUx4O.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo;
        var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureCreatureDamageApprovalUx4O();

        var uiTransaction = _creatureDamageOverlayUx4O.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4OEvidence("ux4-o-creature-damage-validation");
        var overlayOpen = _creatureDamageOverlayUx4O.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureDamageOverlayUx4O.ChoiceCount >= 1 &&
                      _creatureDamageOverlayUx4O.HasExplicitSelection &&
                      _creatureDamageOverlayUx4O.CanonicalPreviewReady &&
                      _creatureDamageOverlayUx4O.ShowsDamageLifecycle &&
                      _creatureDamageOverlayUx4O.ShowsDeath &&
                      _creatureDamageOverlayUx4O.ShowsValidation &&
                      _creatureDamageOverlayUx4O.ShowsHumanSurface &&
                      _creatureDamageOverlayUx4O.UsesIsolatedEvidence &&
                      _creatureDamageOverlayUx4O.EvidencePreviewDryRun &&
                      _creatureDamageOverlayUx4O.EvidenceSourceUntouched &&
                      (_creatureDamageOverlayUx4O.UsesRealProfileEvidence ^
                       _creatureDamageOverlayUx4O.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureDamageOverlayUx4O.LastEvidenceFailure;
        _creatureDamageOverlayUx4O.CloseEvidence();
        var evidenceReleased = _creatureDamageOverlayUx4O.EvidenceResourcesReleased;
        var sourceRestored = _creatureDamageOverlayUx4O.SourceSessionRestored;

        var result = _session.RunUx4OContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureDamageOverlayUx4O.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureDamageApprovalUx4O(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _creatureDamageOverlayUx4O.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureDamageApprovalUx4O() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4O_UI_SURFACE_FAILED " +
                        $"prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4O_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"damage_lifecycle={result.DamageLifecycle.ToString().ToLowerInvariant()} " +
                 $"death_payload={result.DeathPayload.ToString().ToLowerInvariant()} " +
                 $"creature_validation={result.CreatureValidation.ToString().ToLowerInvariant()} " +
                 $"optional_removal={result.OptionalRemoval.ToString().ToLowerInvariant()} " +
                 $"invalid_budget={result.InvalidBudgetBlocked.ToString().ToLowerInvariant()} " +
                 $"invalid_ratio={result.InvalidRatioBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
                 $"runtime_projection={result.RuntimeProjection.ToString().ToLowerInvariant()} " +
                 $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
                 $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"ux4n_preserved={result.Ux4NBindingsPreserved.ToString().ToLowerInvariant()} " +
                 $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
                 $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
