using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private StudioAssistantWorkbenchUx10 _assistantUx10 = null!;
    private Button _assistantContextButtonUx10 = null!;
    private Button _assistantPlansButtonUx10 = null!;
    private Button _assistantSafetyButtonUx10 = null!;
    private Button _assistantCreativeButtonUx10 = null!;

    private void BuildUx10Actions(HBoxContainer headingRow)
    {
        _assistantContextButtonUx10 = AssistantButtonUx10(
            "Contexte",
            "Ouvrir le contexte structuré, les outils, fournisseurs et budgets.",
            () => OpenAssistantUx10(StudioUx10Mode.Context));
        _assistantPlansButtonUx10 = AssistantButtonUx10(
            "Plans",
            "Ouvrir les plans, dry-runs, diffs et transactions atomiques.",
            () => OpenAssistantUx10(StudioUx10Mode.Plans));
        _assistantSafetyButtonUx10 = AssistantButtonUx10(
            "Audit & sécurité",
            "Ouvrir l’audit, les prompts, résultats structurés et le mode hors ligne.",
            () => OpenAssistantUx10(StudioUx10Mode.Safety));
        _assistantCreativeButtonUx10 = AssistantButtonUx10(
            "3D & narration",
            "Ouvrir la création 3D gouvernée et la cohérence narrative.",
            () => OpenAssistantUx10(StudioUx10Mode.Creative));
        headingRow.AddChild(_assistantContextButtonUx10);
        headingRow.AddChild(_assistantPlansButtonUx10);
        headingRow.AddChild(_assistantSafetyButtonUx10);
        headingRow.AddChild(_assistantCreativeButtonUx10);
    }

    private void InitializeUx10Overlay()
    {
        _assistantUx10 = new StudioAssistantWorkbenchUx10(_session);
        _assistantUx10.StatusChanged += SetStatus;
        _assistantUx10.WorkspaceChanged += RefreshAfterUx10Mutation;
        AddChild(_assistantUx10);
    }

    private void OpenAssistantUx10(StudioUx10Mode mode)
    {
        if (_session.Project is null)
        {
            SetStatus("Ouvrez d’abord un module avant d’utiliser l’Assistant.", error: true);
            return;
        }
        var page = PageForModeUx10(mode);
        if (PageId != page) SwitchPage(page, notify: false);
        _assistantUx10.Open(mode);
        SetStatus($"{ModeTitleUx10(mode)} · workflow gouverné et sandbox isolée.");
    }

    private void RefreshAfterUx10Mutation()
    {
        UpdateCommandState();
        RefreshInspector();
        UpdateUx10ActionState();
    }

    private void UpdateUx10ActionState()
    {
        if (_assistantContextButtonUx10 is null) return;
        var ready = _session.Project is not null;
        _assistantContextButtonUx10.Visible = ready && PageId == "contextual";
        _assistantPlansButtonUx10.Visible = ready && PageId == "plans";
        _assistantSafetyButtonUx10.Visible = ready && PageId is "contextual" or "plans";
        _assistantCreativeButtonUx10.Visible = ready && PageId == "assisted-3d";
    }

    private void CloseUx10Overlay()
    {
        if (_assistantUx10 is not null) _assistantUx10.Close();
    }

    internal bool PrepareUx10Evidence(string mode)
    {
        var target = ParseModeUx10(mode);
        var page = PageForModeUx10(target);
        if (PageId != page) SwitchPage(page, notify: false);
        return _assistantUx10.PrepareEvidence(mode);
    }

    internal bool RunUx10Smoke()
    {
        var originalPage = PageId;
        var originalOpen = _assistantUx10.IsOpen;
        var originalMode = _assistantUx10.Mode;
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        var originalRevision = originalHistory?.State.Revision;
        var originalJournal = originalHistory?.Journal.ToArray() ?? [];
        var originalDirty = _session.IsDirty;
        var originalCanUndo = originalHistory?.CanUndo ?? false;
        var originalCanRedo = originalHistory?.CanRedo ?? false;
        var originalArea = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalSelected = _session.SelectedEntityId;
        var approval = _session.CaptureAssistantApprovalUx10();

        var uiModes = true;
        var human = true;
        var preview = true;
        var run = true;
        var sourceUntouched = true;
        var released = true;
        foreach (var mode in Enum.GetValues<StudioUx10Mode>())
        {
            var prepared = PrepareUx10Evidence($"ux10-{ModeCodeUx10(mode).ToLowerInvariant()}");
            uiModes &= prepared && _assistantUx10.Mode == mode;
            human &= prepared && _assistantUx10.ShowsHumanSurface;
            preview &= prepared && _assistantUx10.PreviewReady;
            run &= prepared && _assistantUx10.RunUiEvidence();
            sourceUntouched &= prepared && _assistantUx10.EvidenceSourceUntouched;
            released &= prepared && _session.LastUx10TemporaryReleased;
            _assistantUx10.CloseEvidence();
        }
        var contract = _session.RunUx10ContractSmoke();

        SwitchPage(originalPage, notify: false);
        if (originalOpen) _assistantUx10.RestoreSourceSurfaceAfterSmoke(originalMode);
        else _assistantUx10.HideSourceSurfaceAfterSmoke();
        _session.RestoreAssistantApprovalUx10(approval);
        var currentHistory = _session.History;
        var sourceUnchanged = ReferenceEquals(originalProject, _session.Project) &&
                              ReferenceEquals(originalHistory, currentHistory) &&
                              currentHistory?.State.Revision == originalRevision &&
                              (currentHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                              _session.IsDirty == originalDirty &&
                              (currentHistory?.CanUndo ?? false) == originalCanUndo &&
                              (currentHistory?.CanRedo ?? false) == originalCanRedo &&
                              _session.ActiveAreaId == originalArea &&
                              _session.SelectedEntityIds.SequenceEqual(originalSelection) &&
                              _session.SelectedEntityId == originalSelected &&
                              _session.CaptureAssistantApprovalUx10() == approval;
        var surfaceRestored = PageId == originalPage && _assistantUx10.IsOpen == originalOpen &&
                              (!originalOpen || _assistantUx10.Mode == originalMode);
        var uiReady = uiModes && human && preview && run && sourceUntouched && released;
        var valid = contract.Succeeded && uiReady && sourceUnchanged && surfaceRestored;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX10_{(valid ? "PASS" : "FAILED")} " +
                 $"routes={contract.SeventeenRoutes.ToString().ToLowerInvariant()} " +
                 $"context={contract.Context.ToString().ToLowerInvariant()} " +
                 $"plans={contract.Plans.ToString().ToLowerInvariant()} " +
                 $"safety={contract.Safety.ToString().ToLowerInvariant()} " +
                 $"creative={contract.Creative.ToString().ToLowerInvariant()} " +
                 $"selection={contract.ExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"dry_run={contract.CanonicalDryRun.ToString().ToLowerInvariant()} " +
                 $"approval={contract.ApprovalRequired.ToString().ToLowerInvariant()} " +
                 $"tests={contract.TestsPassed.ToString().ToLowerInvariant()} " +
                 $"revert={contract.Reverted.ToString().ToLowerInvariant()} " +
                 $"audit={contract.Audited.ToString().ToLowerInvariant()} " +
                 $"offline={contract.Offline.ToString().ToLowerInvariant()} " +
                 $"ui={uiReady.ToString().ToLowerInvariant()} " +
                 $"human={human.ToString().ToLowerInvariant()} " +
                 $"resources_released={(released && contract.TemporaryWorkspaceReleased).ToString().ToLowerInvariant()} " +
                 $"source_unchanged={(contract.SourceUnchanged && sourceUnchanged).ToString().ToLowerInvariant()} " +
                 $"surface_restored={surfaceRestored.ToString().ToLowerInvariant()} windows=1");
        return valid;
    }

    internal void ReleaseUx10AutomatedEvidence()
    {
        if (_assistantUx10 is not null) _assistantUx10.CloseEvidence();
    }

    private static Button AssistantButtonUx10(string text, string tooltip, Action action)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.TooltipText = tooltip;
        button.Pressed += action;
        return button;
    }

    private static string PageForModeUx10(StudioUx10Mode mode) => mode switch
    {
        StudioUx10Mode.Context => "contextual",
        StudioUx10Mode.Plans => "plans",
        StudioUx10Mode.Safety => "plans",
        StudioUx10Mode.Creative => "assisted-3d",
        _ => "contextual",
    };

    private static StudioUx10Mode ParseModeUx10(string mode)
    {
        if (mode.StartsWith("ux10-b", StringComparison.OrdinalIgnoreCase)) return StudioUx10Mode.Plans;
        if (mode.StartsWith("ux10-c", StringComparison.OrdinalIgnoreCase)) return StudioUx10Mode.Safety;
        if (mode.StartsWith("ux10-d", StringComparison.OrdinalIgnoreCase)) return StudioUx10Mode.Creative;
        return StudioUx10Mode.Context;
    }

    private static string ModeCodeUx10(StudioUx10Mode mode) => mode switch
    {
        StudioUx10Mode.Context => "A",
        StudioUx10Mode.Plans => "B",
        StudioUx10Mode.Safety => "C",
        StudioUx10Mode.Creative => "D",
        _ => "X",
    };

    private static string ModeTitleUx10(StudioUx10Mode mode) => mode switch
    {
        StudioUx10Mode.Context => "UX10-A · Contexte et fournisseurs",
        StudioUx10Mode.Plans => "UX10-B · Plans et transactions",
        StudioUx10Mode.Safety => "UX10-C · Audit et sécurité",
        StudioUx10Mode.Creative => "UX10-D · 3D et narration",
        _ => "UX10",
    };
}
