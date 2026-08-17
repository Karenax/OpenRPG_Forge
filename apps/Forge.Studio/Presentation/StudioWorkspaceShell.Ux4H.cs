using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioCreatureCapabilitiesOverlayUx4H _creatureCapabilitiesOverlayUx4H = null!;
    private Button _creatureCapabilitiesButtonUx4H = null!;

    private void BuildUx4HActions(HBoxContainer headingRow)
    {
        _creatureCapabilitiesButtonUx4H = StudioStyle.Button("Capacités et inventaire", compact: true);
        _creatureCapabilitiesButtonUx4H.TooltipText =
            "Éditer capacités spéciales, conteneurs, objets et équipement de créature.";
        _creatureCapabilitiesButtonUx4H.Pressed += OpenCreatureCapabilitiesUx4H;
        headingRow.AddChild(_creatureCapabilitiesButtonUx4H);
    }

    private void InitializeUx4HOverlay()
    {
        _creatureCapabilitiesOverlayUx4H = new StudioCreatureCapabilitiesOverlayUx4H(_session);
        _creatureCapabilitiesOverlayUx4H.StatusChanged += SetStatus;
        _creatureCapabilitiesOverlayUx4H.CreatureChanged += RefreshAfterUx4HMutation;
        AddChild(_creatureCapabilitiesOverlayUx4H);
    }

    private void OpenCreatureCapabilitiesUx4H()
    {
        if (_session.Project is null)
        { SetStatus("Ouvrez d’abord un module avant d’éditer ses créatures.", error: true); return; }
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        _creatureCapabilitiesOverlayUx4H.Open();
        SetStatus("Capacités et inventaire ouverts · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4HMutation() { RefreshAfterUx1AMutation(); UpdateUx4HActionState(); }
    private void UpdateUx4HActionState()
    { if (_creatureCapabilitiesButtonUx4H is not null) _creatureCapabilitiesButtonUx4H.Visible = PageId == "creatures" && _session.Project is not null; }
    private void CloseUx4HOverlay()
    { if (_creatureCapabilitiesOverlayUx4H is not null) _creatureCapabilitiesOverlayUx4H.Close(); }

    internal bool PrepareUx4HEvidence(string mode)
    {
        SwitchPage("creatures", notify: false);
        CloseUx4DOverlay(); CloseUx4EOverlay(); CloseUx4FOverlay(); CloseUx4GOverlay();
        return _creatureCapabilitiesOverlayUx4H.PrepareEvidence(mode);
    }

    internal bool RunUx4HSmoke()
    {
        var originalPageId = PageId; var originalSurfaceOpen = _creatureCapabilitiesOverlayUx4H.IsOpen;
        var originalProject = _session.Project; var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision; var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId; var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId; var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo; var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureCreatureCapabilityApprovalUx4H();

        var uiTransaction = _creatureCapabilitiesOverlayUx4H.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4HEvidence("ux4-h-creature-capabilities-inventory");
        var overlayOpen = _creatureCapabilitiesOverlayUx4H.IsOpen;
        var uiReady = evidencePrepared && PageId == "creatures" && overlayOpen &&
                      _creatureCapabilitiesOverlayUx4H.ChoiceCount >= 1 &&
                      _creatureCapabilitiesOverlayUx4H.HasExplicitSelection &&
                      _creatureCapabilitiesOverlayUx4H.CanonicalPreviewReady &&
                      _creatureCapabilitiesOverlayUx4H.ShowsAbilities &&
                      _creatureCapabilitiesOverlayUx4H.ShowsInventory &&
                      _creatureCapabilitiesOverlayUx4H.ShowsEquipment &&
                      _creatureCapabilitiesOverlayUx4H.ShowsHumanSurface &&
                      _creatureCapabilitiesOverlayUx4H.UsesIsolatedEvidence &&
                      _creatureCapabilitiesOverlayUx4H.EvidencePreviewDryRun &&
                      _creatureCapabilitiesOverlayUx4H.EvidenceSourceUntouched &&
                      (_creatureCapabilitiesOverlayUx4H.UsesRealProfileEvidence ^
                       _creatureCapabilitiesOverlayUx4H.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _creatureCapabilitiesOverlayUx4H.LastEvidenceFailure;
        _creatureCapabilitiesOverlayUx4H.CloseEvidence();
        var evidenceReleased = _creatureCapabilitiesOverlayUx4H.EvidenceResourcesReleased;
        var sourceRestored = _creatureCapabilitiesOverlayUx4H.SourceSessionRestored;

        var result = _session.RunUx4HContractSmoke(); SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _creatureCapabilitiesOverlayUx4H.RestoreSourceSurfaceAfterSmoke();
        _session.RestoreCreatureCapabilityApprovalUx4H(originalApproval);
        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId && _creatureCapabilitiesOverlayUx4H.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureCreatureCapabilityApprovalUx4H() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4H_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4H_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"abilities={result.Abilities.ToString().ToLowerInvariant()} " +
                 $"inventory={result.Inventory.ToString().ToLowerInvariant()} " +
                 $"equipment={result.Equipment.ToString().ToLowerInvariant()} " +
                 $"invalid_blocked={result.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
                 $"missing_item_blocked={result.MissingItemBlocked.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
                 $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
                 $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
                 $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
                 $"neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
                 $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
