using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioNarrativeWorkbenchUx5B _narrativeWorkbenchUx5B = null!;
    private Button _narrativeWorkbenchButtonUx5B = null!;

    private void BuildUx5BActions(HBoxContainer headingRow)
    {
        _narrativeWorkbenchButtonUx5B = StudioStyle.Button("Production narrative", compact: true);
        _narrativeWorkbenchButtonUx5B.TooltipText =
            "Ouvrir les workflows complets Dialogue, Quête, Localisation, Lore et Cinématiques.";
        _narrativeWorkbenchButtonUx5B.Pressed += OpenNarrativeWorkbenchUx5B;
        headingRow.AddChild(_narrativeWorkbenchButtonUx5B);
    }

    private void InitializeUx5BOverlay()
    {
        _narrativeWorkbenchUx5B = new StudioNarrativeWorkbenchUx5B(_session);
        _narrativeWorkbenchUx5B.StatusChanged += SetStatus;
        _narrativeWorkbenchUx5B.NarrativeChanged += RefreshAfterUx5BNarrativeMutation;
        AddChild(_narrativeWorkbenchUx5B);
    }

    private void OpenNarrativeWorkbenchUx5B()
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant la production narrative.", error: true);
            return;
        }
        _globalSearchOverlay.Close();
        _safeRenameOverlay.Close();
        _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Close();
        _viewportToolsOverlay.Close();
        _narrativeWorkbenchUx5B.Open(SectionForPageUx5B(PageId));
        SetStatus("Production narrative UX5 · sélection explicite et dry-run obligatoires");
    }

    private void RefreshAfterUx5BNarrativeMutation()
    {
        UpdateCommandState();
        RebuildExplorer();
        RefreshInspector();
        NavigationChanged?.Invoke();
        UpdateUx5BActionState();
    }

    private void UpdateUx5BActionState()
    {
        if (_narrativeWorkbenchButtonUx5B is null) return;
        _narrativeWorkbenchButtonUx5B.Visible = IsNarrativePageUx5B(PageId) && _session.Project is not null;
    }

    private void CloseUx5BOverlay()
    {
        if (_narrativeWorkbenchUx5B is not null) _narrativeWorkbenchUx5B.Close();
    }

    internal bool PrepareUx5BEvidence(string mode)
    {
        var section = StudioNarrativeSectionForModeUx5B(mode);
        SwitchPage(PageForSectionUx5B(section), notify: false);
        return _narrativeWorkbenchUx5B.PrepareEvidence(mode);
    }

    internal bool RunUx5BSmoke()
    {
        var originalPage = PageId;
        var originalOpen = _narrativeWorkbenchUx5B.IsOpen;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalArea = _session.ActiveAreaId;
        var originalApproval = _session.CaptureNarrativeApprovalUx5B();

        var uiTransaction = _narrativeWorkbenchUx5B.RunUiTransactionEvidence();
        var uiPrepared = PrepareUx5BEvidence("ux5-b-dialogue-production");
        var uiReady = uiPrepared && _narrativeWorkbenchUx5B.IsOpen &&
                      _narrativeWorkbenchUx5B.UsesIsolatedEvidence &&
                      _narrativeWorkbenchUx5B.PreviewReady &&
                      _narrativeWorkbenchUx5B.HasExplicitSelection &&
                      _narrativeWorkbenchUx5B.ShowsFiveWorkflows &&
                      _narrativeWorkbenchUx5B.ShowsHumanSurface &&
                      _narrativeWorkbenchUx5B.EvidenceSourceUntouched;
        _narrativeWorkbenchUx5B.CloseEvidence();
        var released = _narrativeWorkbenchUx5B.EvidenceResourcesReleased;
        var sessionRestored = _narrativeWorkbenchUx5B.SourceSessionRestored;
        var contract = _session.RunUx5BContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _narrativeWorkbenchUx5B.RestoreSourceSurfaceAfterSmoke(SectionForPageUx5B(originalPage));
        else _narrativeWorkbenchUx5B.HideSourceSurfaceAfterSmoke();
        _session.RestoreNarrativeApprovalUx5B(originalApproval);
        var currentHistory = _session.History;
        var sourceUntouched = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty && _session.ActiveAreaId == originalArea &&
                              Equals(_session.CaptureNarrativeApprovalUx5B(), originalApproval);
        var surfaceRestored = PageId == originalPage && _narrativeWorkbenchUx5B.IsOpen == originalOpen;
        var valid = contract.Succeeded && uiTransaction && uiReady && released && sessionRestored &&
                    sourceUntouched && surfaceRestored;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX5B_{(valid ? "PASS" : "FAILED")} " +
            $"dialogue_authoring={contract.DialogueAuthoring.ToString().ToLowerInvariant()} " +
            $"dialogue_presentation={contract.DialoguePresentation.ToString().ToLowerInvariant()} " +
            $"dialogue_experience={contract.DialogueExperience.ToString().ToLowerInvariant()} " +
            $"dialogue_production={contract.DialogueProduction.ToString().ToLowerInvariant()} " +
            $"dialogue_simulation={contract.DialogueSimulation.ToString().ToLowerInvariant()} " +
            $"quest_foundation={contract.QuestFoundation.ToString().ToLowerInvariant()} " +
            $"quest_authoring={contract.QuestAuthoring.ToString().ToLowerInvariant()} " +
            $"quest_experience={contract.QuestExperience.ToString().ToLowerInvariant()} " +
            $"quest_quality={contract.QuestQuality.ToString().ToLowerInvariant()} " +
            $"quest_simulation={contract.QuestSimulation.ToString().ToLowerInvariant()} " +
            $"localization={contract.LocalizationCatalog.ToString().ToLowerInvariant()} " +
            $"typography={contract.AccessibleTypography.ToString().ToLowerInvariant()} " +
            $"lore={contract.LoreIndex.ToString().ToLowerInvariant()} " +
            $"reference_graph={contract.LoreReferenceGraph.ToString().ToLowerInvariant()} " +
            $"cinematic_startup={contract.CinematicStartup.ToString().ToLowerInvariant()} " +
            $"timeline={contract.CinematicTimeline.ToString().ToLowerInvariant()} " +
            $"control_locks={contract.ControlLocks.ToString().ToLowerInvariant()} " +
            $"dry_run={contract.DryRun.ToString().ToLowerInvariant()} " +
            $"stale_source={contract.StaleSourceBlocked.ToString().ToLowerInvariant()} " +
            $"stale_approval={contract.StaleApprovalBlocked.ToString().ToLowerInvariant()} " +
            $"canonical_commands={contract.CanonicalCommands.ToString().ToLowerInvariant()} " +
            $"undo_redo={contract.UndoRedo.ToString().ToLowerInvariant()} " +
            $"round_trip={contract.Persistence.ToString().ToLowerInvariant()} " +
            $"neighbors={contract.NeighborsPreserved.ToString().ToLowerInvariant()} " +
            $"ui={uiReady.ToString().ToLowerInvariant()} ui_transaction={uiTransaction.ToString().ToLowerInvariant()} " +
            $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
            $"source_untouched={sourceUntouched.ToString().ToLowerInvariant()} " +
            $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    private static bool IsNarrativePageUx5B(string page) => page is
        "dialogues" or "quests" or "localization" or "lore" or "cinematics";

    private static StudioNarrativeSectionUx5B SectionForPageUx5B(string page) => page switch
    {
        "quests" => StudioNarrativeSectionUx5B.Quest,
        "localization" => StudioNarrativeSectionUx5B.Localization,
        "lore" => StudioNarrativeSectionUx5B.Lore,
        "cinematics" => StudioNarrativeSectionUx5B.Cinematic,
        _ => StudioNarrativeSectionUx5B.Dialogue,
    };

    private static StudioNarrativeSectionUx5B StudioNarrativeSectionForModeUx5B(string mode) => mode switch
    {
        var value when value.Contains("quest", StringComparison.OrdinalIgnoreCase) => StudioNarrativeSectionUx5B.Quest,
        var value when value.Contains("local", StringComparison.OrdinalIgnoreCase) => StudioNarrativeSectionUx5B.Localization,
        var value when value.Contains("lore", StringComparison.OrdinalIgnoreCase) => StudioNarrativeSectionUx5B.Lore,
        var value when value.Contains("cinematic", StringComparison.OrdinalIgnoreCase) => StudioNarrativeSectionUx5B.Cinematic,
        _ => StudioNarrativeSectionUx5B.Dialogue,
    };

    private static string PageForSectionUx5B(StudioNarrativeSectionUx5B section) => section switch
    {
        StudioNarrativeSectionUx5B.Quest => "quests",
        StudioNarrativeSectionUx5B.Localization => "localization",
        StudioNarrativeSectionUx5B.Lore => "lore",
        StudioNarrativeSectionUx5B.Cinematic => "cinematics",
        _ => "dialogues",
    };
}
