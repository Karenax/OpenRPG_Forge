using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioFactionRuntimeOverlayUx4B _factionRuntimeOverlayUx4B = null!;
    private Button _factionRuntimeButtonUx4B = null!;

    private void BuildUx4BActions(HBoxContainer headingRow)
    {
        _factionRuntimeButtonUx4B = StudioStyle.Button("Réputation vivante", compact: true);
        _factionRuntimeButtonUx4B.TooltipText =
            "Éditer réputation personnelle et globale, témoins, affiliations et factions dynamiques.";
        _factionRuntimeButtonUx4B.Pressed += OpenFactionRuntimeUx4B;
        headingRow.AddChild(_factionRuntimeButtonUx4B);
    }

    private void InitializeUx4BOverlay()
    {
        _factionRuntimeOverlayUx4B = new StudioFactionRuntimeOverlayUx4B(_session);
        _factionRuntimeOverlayUx4B.StatusChanged += SetStatus;
        _factionRuntimeOverlayUx4B.FactionChanged += RefreshAfterUx4BMutation;
        AddChild(_factionRuntimeOverlayUx4B);
    }

    private void OpenFactionRuntimeUx4B()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses politiques de faction.", error: true);
            return;
        }

        CloseUx4AOverlay();
        SwitchPage("social-content", notify: false);
        _factionRuntimeOverlayUx4B.Open();
        SetStatus("Réputation vivante ouverte · sélection explicite et aperçu canonique obligatoires");
    }

    private void RefreshAfterUx4BMutation()
    {
        RefreshAfterUx1AMutation();
        UpdateUx4BActionState();
    }

    private void UpdateUx4BActionState()
    {
        if (_factionRuntimeButtonUx4B is not null)
        {
            _factionRuntimeButtonUx4B.Visible = PageId == "social-content" && _session.Project is not null;
        }
    }

    private void CloseUx4BOverlay()
    {
        if (_factionRuntimeOverlayUx4B is not null)
        {
            _factionRuntimeOverlayUx4B.Close();
        }
    }

    internal bool PrepareUx4BEvidence(string mode)
    {
        SwitchPage("social-content", notify: false);
        CloseUx4AOverlay();
        return _factionRuntimeOverlayUx4B.PrepareEvidence(mode);
    }

    internal bool RunUx4BSmoke()
    {
        var originalPageId = PageId;
        var originalSurfaceOpen = _factionRuntimeOverlayUx4B.IsOpen;
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
        var originalApproval = _session.CaptureFactionRuntimeApprovalUx4B();

        var uiTransaction = _factionRuntimeOverlayUx4B.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4BEvidence("ux4-b-faction-runtime");
        var overlayOpen = _factionRuntimeOverlayUx4B.IsOpen;
        var uiReady = evidencePrepared && PageId == "social-content" && overlayOpen &&
                      _factionRuntimeOverlayUx4B.ChoiceCount >= 3 &&
                      _factionRuntimeOverlayUx4B.HasExplicitSelection &&
                      _factionRuntimeOverlayUx4B.CanonicalPreviewReady &&
                      _factionRuntimeOverlayUx4B.ShowsPersonalReputation &&
                      _factionRuntimeOverlayUx4B.ShowsGlobalReputation &&
                      _factionRuntimeOverlayUx4B.ShowsWitnesses &&
                      _factionRuntimeOverlayUx4B.ShowsMemberships &&
                      _factionRuntimeOverlayUx4B.ShowsDynamicFactions &&
                      _factionRuntimeOverlayUx4B.ShowsHumanSurface &&
                      _factionRuntimeOverlayUx4B.UsesIsolatedEvidence &&
                      _factionRuntimeOverlayUx4B.EvidencePreviewDryRun &&
                      _factionRuntimeOverlayUx4B.EvidenceSourceUntouched &&
                      (_factionRuntimeOverlayUx4B.UsesRealCryptEvidence ^
                       _factionRuntimeOverlayUx4B.UsesCanonicalFallbackEvidence);
        var evidenceFailure = _factionRuntimeOverlayUx4B.LastEvidenceFailure;
        _factionRuntimeOverlayUx4B.CloseEvidence();
        var evidenceReleased = _factionRuntimeOverlayUx4B.EvidenceResourcesReleased;
        var sourceRestored = _factionRuntimeOverlayUx4B.SourceSessionRestored;

        var result = _session.RunUx4BContractSmoke();
        SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen)
        {
            _factionRuntimeOverlayUx4B.RestoreSourceSurfaceAfterSmoke();
        }
        _session.RestoreFactionRuntimeApprovalUx4B(originalApproval);

        var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _factionRuntimeOverlayUx4B.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureFactionRuntimeApprovalUx4B() == originalApproval;

        if (!uiReady)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX4B_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                $"open={overlayOpen.ToString().ToLowerInvariant()} failure={evidenceFailure}");
        }

        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && evidenceReleased && sourceRestored && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX4B_{(valid ? "PASS" : "FAILED")} " +
            $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
            $"personal_reputation={result.PersonalReputation.ToString().ToLowerInvariant()} " +
            $"global_reputation={result.GlobalReputation.ToString().ToLowerInvariant()} " +
            $"witnesses={result.WitnessPropagation.ToString().ToLowerInvariant()} " +
            $"memberships={result.MultipleMemberships.ToString().ToLowerInvariant()} " +
            $"dynamic_factions={result.DynamicFactions.ToString().ToLowerInvariant()} " +
            $"invalid_blocked={result.InvalidConfigurationBlocked.ToString().ToLowerInvariant()} " +
            $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_draft={result.StaleDraftBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={result.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_command={result.CanonicalCommand.ToString().ToLowerInvariant()} " +
            $"undo_redo={result.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={result.Persistence.ToString().ToLowerInvariant()} " +
            $"neighbors_preserved={result.NeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"temp_workspace_released={result.TemporaryWorkspaceReleased.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} " +
            $"source_unchanged={sourceUnchanged.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }
}
