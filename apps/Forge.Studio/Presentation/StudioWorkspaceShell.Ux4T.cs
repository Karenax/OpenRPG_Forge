using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioMerchantAuthoringOverlayUx4T _merchantAuthoringOverlayUx4T = null!;
    private Button _merchantAuthoringButtonUx4T = null!;

    private void BuildUx4TActions(HBoxContainer headingRow)
    {
        _merchantAuthoringButtonUx4T = StudioStyle.Button("Marchands", compact: true);
        _merchantAuthoringButtonUx4T.TooltipText =
            "Boutique, stock, économie, transactions, interface et audit dans une commande canonique.";
        _merchantAuthoringButtonUx4T.Pressed += OpenMerchantAuthoringUx4T;
        headingRow.AddChild(_merchantAuthoringButtonUx4T);
    }

    private void InitializeUx4TOverlay()
    {
        _merchantAuthoringOverlayUx4T = new(_session);
        _merchantAuthoringOverlayUx4T.StatusChanged += SetStatus;
        _merchantAuthoringOverlayUx4T.MerchantChanged += RefreshAfterUx4TMutation;
        AddChild(_merchantAuthoringOverlayUx4T);
    }

    private void OpenMerchantAuthoringUx4T()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’éditer ses marchands.", error: true);
            return;
        }
        SwitchPage("social-content", notify: false);
        CloseUx4AOverlay(); CloseUx4BOverlay(); CloseUx4COverlay(); CloseUx4UOverlay();
        _merchantAuthoringOverlayUx4T.Open();
        SetStatus("Marchands · sélection ou création explicite, cinq onglets et aperçu canonique.", false);
    }

    private void RefreshAfterUx4TMutation()
    {
        RefreshAfterUx1AMutation(); UpdateUx4TActionState();
    }

    private void UpdateUx4TActionState()
    {
        if (_merchantAuthoringButtonUx4T is not null)
            _merchantAuthoringButtonUx4T.Visible = PageId == "social-content" && _session.Project is not null;
    }

    private void CloseUx4TOverlay()
    {
        if (_merchantAuthoringOverlayUx4T is not null) _merchantAuthoringOverlayUx4T.Close();
    }

    internal bool PrepareUx4TEvidence(string mode)
    {
        if (PageId != "social-content") SwitchPage("social-content", notify: false);
        CloseUx4AOverlay(); CloseUx4BOverlay(); CloseUx4COverlay(); CloseUx4UOverlay();
        _merchantAuthoringOverlayUx4T.Open();
        return _merchantAuthoringOverlayUx4T.PrepareEvidence(mode);
    }

    internal void ReleaseUx4TAutomatedEvidence() => _merchantAuthoringOverlayUx4T.CloseEvidence();

    internal bool RunUx4TSmoke()
    {
        var originalPageId = PageId; var originalSurfaceOpen = _merchantAuthoringOverlayUx4T.IsOpen;
        var originalProject = _session.Project; var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalAreaId = _session.ActiveAreaId; var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId; var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo; var originalCanRedo = originalHistory?.CanRedo;
        var originalApproval = _session.CaptureMerchantApprovalUx4T();
        var uiTransaction = _merchantAuthoringOverlayUx4T.RunUiTransactionEvidence();
        var evidencePrepared = PrepareUx4TEvidence("ux4-t-merchant-authoring");
        var overlayOpen = _merchantAuthoringOverlayUx4T.IsOpen;
        var uiReady = evidencePrepared && PageId == "social-content" && overlayOpen &&
                      _merchantAuthoringOverlayUx4T.MerchantChoiceCount > 0 &&
                      _merchantAuthoringOverlayUx4T.HasExplicitSelection &&
                      _merchantAuthoringOverlayUx4T.CanonicalPreviewReady &&
                      _merchantAuthoringOverlayUx4T.ShowsStoreWizard &&
                      _merchantAuthoringOverlayUx4T.ShowsStockPricing &&
                      _merchantAuthoringOverlayUx4T.ShowsEconomy &&
                      _merchantAuthoringOverlayUx4T.ShowsTransactions &&
                      _merchantAuthoringOverlayUx4T.ShowsExperience &&
                      _merchantAuthoringOverlayUx4T.ShowsHumanSurface &&
                      _merchantAuthoringOverlayUx4T.UsesIsolatedEvidence &&
                      _merchantAuthoringOverlayUx4T.EvidencePreviewDryRun &&
                      _merchantAuthoringOverlayUx4T.EvidenceSourceUntouched &&
                      (_merchantAuthoringOverlayUx4T.UsesRealModuleEvidence ^
                       _merchantAuthoringOverlayUx4T.UsesCanonicalFallbackEvidence);
        var failure = _merchantAuthoringOverlayUx4T.LastEvidenceFailure;
        _merchantAuthoringOverlayUx4T.CloseEvidence();
        var released = _merchantAuthoringOverlayUx4T.EvidenceResourcesReleased;
        var restored = _merchantAuthoringOverlayUx4T.SourceSessionRestored;
        var result = _session.RunUx4TContractSmoke(); SwitchPage(originalPageId, notify: false);
        if (originalSurfaceOpen) _merchantAuthoringOverlayUx4T.RestoreSourceSurfaceAfterSmoke();
        else _merchantAuthoringOverlayUx4T.HideSourceSurfaceAfterSmoke();
        _session.RestoreMerchantApprovalUx4T(originalApproval); var currentHistory = _session.History;
        var surfaceRestored = PageId == originalPageId &&
                              _merchantAuthoringOverlayUx4T.IsOpen == originalSurfaceOpen;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              currentHistory?.CanUndo == originalCanUndo && currentHistory?.CanRedo == originalCanRedo &&
                              _session.ActiveAreaId == originalAreaId &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalActive && _session.IsDirty == originalDirty &&
                              _session.CaptureMerchantApprovalUx4T() == originalApproval;
        if (!uiReady)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4T_UI_SURFACE_FAILED prepared={evidencePrepared.ToString().ToLowerInvariant()} " +
                        $"open={overlayOpen.ToString().ToLowerInvariant()} failure={failure}");
        var valid = result.Succeeded && result.TemporaryWorkspaceReleased && uiReady && uiTransaction &&
                    sourceUnchanged && released && restored && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX4T_{(valid ? "PASS" : "FAILED")} " +
                 $"selection_explicit={result.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"store_wizard={result.StoreWizard.ToString().ToLowerInvariant()} " +
                 $"stock_pricing={result.StockPricing.ToString().ToLowerInvariant()} " +
                 $"treasury_purchase={result.TreasuryPurchase.ToString().ToLowerInvariant()} " +
                 $"stolen_restrictions={result.StolenRestrictions.ToString().ToLowerInvariant()} " +
                 $"restock_events={result.RestockEvents.ToString().ToLowerInvariant()} " +
                 $"atomic_transactions={result.AtomicTransactions.ToString().ToLowerInvariant()} " +
                 $"currencies_negotiation_market={result.CurrenciesNegotiationMarket.ToString().ToLowerInvariant()} " +
                 $"services_interface_audit={result.ServicesInterfaceAudit.ToString().ToLowerInvariant()} " +
                 $"validation={result.Validation.ToString().ToLowerInvariant()} " +
                 $"dry_run={result.DryRun.ToString().ToLowerInvariant()} " +
                 $"stale_source={result.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
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
