using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCanonicalValidationWorkbenchUx7A : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx7A? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitSelection;
    private bool _lastApplySucceeded;
    private bool _lastUndoSucceeded;
    private bool _lastRedoSucceeded;
    private bool _lastSaveSucceeded;

    private PanelContainer _dialog = null!;
    private OptionButton _areaChoice = null!;
    private GridContainer _categoryGrid = null!;
    private readonly Dictionary<string, Button> _categoryButtons = new(StringComparer.Ordinal);
    private ItemList _issues = null!;
    private Label _summary = null!;
    private Label _severity = null!;
    private Label _message = null!;
    private Label _path = null!;
    private Label _authority = null!;
    private Label _code = null!;
    private Button _openTarget = null!;
    private OptionButton _quickFixes = null!;
    private Label _fixSummary = null!;
    private Label _status = null!;
    private Button _previewFix = null!;
    private Button _apply = null!;
    private Button _undo = null!;
    private Button _redo = null!;

    private StudioUx7AReport? _report;
    private StudioUx7AIssue? _selectedIssue;
    private readonly List<StudioUx7AIssue> _visibleIssues = [];
    private string _selectedCategory = "clickable";
    private StudioDiagnosticFixPreview? _fixPreview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ProjectChanged;
    internal event Action<CreatorSelection>? TargetRequested;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                            !ReferenceEquals(_session, _sourceSession);
    internal bool HasExplicitSelection => _explicitSelection && _report is not null;
    internal bool TenCategoriesVisible => _report?.Categories.Count == 10 &&
                                          _categoryButtons.Count == 10 &&
                                          _categoryButtons.Values.All(button => button.Visible);
    internal bool ReportReady => _report is
    {
        Categories.Count: 10,
        ReadOnlyValidation: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ClickableDiagnosticReady => _selectedIssue is
    {
        Target: not null,
        Actionable: not null,
        QuickFixes.Count: > 0,
    };
    internal bool DeepLinkReady => ClickableDiagnosticReady && !_openTarget.Disabled;
    internal bool QuickFixPreviewReady => _fixPreview is { Succeeded: true, CanApply: true };
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible && text.Contains("Schémas", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Références", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Graphes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Géométrie", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Navigation", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Localisation", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Licences", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Zone prête", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Audio", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Diagnostics cliquables", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun build", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioCanonicalValidationWorkbenchUx7A(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.03f, 0.94f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("Ux7ACanonicalValidation", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("VALIDATION CANONIQUE · UX7-A", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Schémas, références, graphes, géométrie, navigation, localisation, licences, zone et audio",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Validation et préparation uniquement · aucun build, package, runtime, réseau ou playback audio n’est exécuté",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddChild(StudioStyle.Label("ZONE À QUALIFIER", 9, StudioStyle.Accent));
        _areaChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _areaChoice.ItemSelected += index => SelectArea((int)index);
        selection.AddChild(_areaChoice);
        var refresh = StudioStyle.Button("Relancer les validations", compact: true);
        refresh.Pressed += RefreshReport;
        selection.AddChild(refresh);
        root.AddChild(selection);

        _categoryGrid = new GridContainer
        {
            Columns = 5,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddChild(_categoryGrid);
        foreach (var category in CategoryDefinitions())
        {
            var key = category.Key;
            var button = StudioStyle.Button($"{category.Route}\n{category.Label}", compact: true);
            button.CustomMinimumSize = new Vector2(188, 48);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.TooltipText = category.Description;
            button.Pressed += () => SelectCategory(key);
            _categoryButtons.Add(key, button);
            _categoryGrid.AddChild(button);
        }

        var bodyHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 360),
            CustomMaximumSize = new Vector2(-1, 610),
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 10);
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var issuePanel = StudioStyle.Panel("Ux7AIssues", StudioStyle.SurfaceRaised, 8, 1);
        issuePanel.CustomMinimumSize = new Vector2(500, 0);
        issuePanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        var issueMargin = StudioStyle.Margin(9, 8, 9, 8);
        issuePanel.AddChild(issueMargin);
        var issueColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        issueColumn.AddChild(StudioStyle.Label("MESSAGES LOCALISÉS", 9, StudioStyle.Accent));
        _summary = StudioStyle.Label("Choisissez une zone pour lancer les dix contrôles.", 9,
            StudioStyle.Muted, wrap: true);
        issueColumn.AddChild(_summary);
        _issues = new ItemList
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _issues.ItemSelected += index => SelectIssue((int)index);
        issueColumn.AddChild(_issues);
        issueMargin.AddChild(issueColumn);
        body.AddChild(issuePanel);

        var detailPanel = StudioStyle.Panel("Ux7ADetail", StudioStyle.SurfaceRaised, 8, 1);
        detailPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detailPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        var detailMargin = StudioStyle.Margin(10, 8, 10, 8);
        detailPanel.AddChild(detailMargin);
        var detailScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        detailMargin.AddChild(detailScroll);
        var detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detail.AddThemeConstantOverride("separation", 8);
        detailScroll.AddChild(detail);
        _severity = StudioStyle.Label("Aucun message sélectionné", 11, StudioStyle.Muted, wrap: true);
        _message = StudioStyle.Label("Sélectionnez une ligne pour lire son diagnostic humain.", 14,
            StudioStyle.Text, wrap: true);
        _path = StudioStyle.Label("Cible : —", 10, StudioStyle.Muted, wrap: true);
        _authority = StudioStyle.Label("Autorité : —", 9, StudioStyle.Muted, wrap: true);
        _code = StudioStyle.Label("Code : —", 9, StudioStyle.Muted, wrap: true);
        detail.AddChild(_severity);
        detail.AddChild(_message);
        detail.AddChild(_path);
        detail.AddChild(_authority);
        detail.AddChild(_code);
        _openTarget = StudioStyle.Button("Ouvrir la cible dans Studio", compact: true);
        _openTarget.Disabled = true;
        _openTarget.Pressed += OpenTarget;
        detail.AddChild(_openTarget);
        detail.AddChild(StudioStyle.Label("CORRECTION CANONIQUE", 9, StudioStyle.Accent));
        _quickFixes = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _quickFixes.ItemSelected += _ => ClearFixPreview();
        detail.AddChild(_quickFixes);
        _fixSummary = StudioStyle.Label(
            "Les corrections ne sont jamais appliquées sans aperçu lié à la révision courante.", 10,
            StudioStyle.Muted, wrap: true);
        detail.AddChild(_fixSummary);
        body.AddChild(detailPanel);

        _status = StudioStyle.Label("Sélectionnez explicitement une zone.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        _previewFix = ActionButton("Aperçu de la correction", PreviewFix, 190);
        _apply = ActionButton("Appliquer", Apply, 120, primary: true);
        _undo = ActionButton("Annuler", Undo, 110);
        _redo = ActionButton("Rétablir", Redo, 116);
        actions.AddChild(_previewFix);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(ActionButton("Enregistrer", Save, 126));
        root.AddChild(actions);
        UpdateButtons();
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false);

    internal void RestoreSourceSurfaceAfterSmoke() => OpenBound(_sourceSession, evidence: false);

    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        _session = _sourceSession;
        ResetControls();
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelValidationApprovalUx7A();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux7-a-ui-{Guid.NewGuid():N}");
        _evidenceReleased = false;
        try
        {
            if (!ProjectSession.Create(_evidenceRoot, _sourceSession.Project).Succeeded)
            {
                ReleaseEvidence();
                return false;
            }
            _evidenceSession = new StudioSessionController();
            if (!_evidenceSession.Open(_evidenceRoot).Succeeded ||
                !_evidenceSession.SeedValidationEvidenceUx7A())
            {
                ReleaseEvidence();
                return false;
            }
            OpenBound(_evidenceSession, evidence: true);
            var choices = _evidenceSession.ValidationAreaChoicesUx7A();
            var area = _evidenceSession.Project!.Areas.First(candidate => candidate.Entities.Count > 0);
            var index = choices.ToList().FindIndex(choice => choice.AreaId == area.Id);
            if (index < 0)
            {
                ReleaseEvidence();
                return false;
            }
            _areaChoice.Select(index + 1);
            SelectArea(index + 1);
            var requested = RequestedCategory(mode);
            SelectCategory(requested);
            if (!SelectFirstActionableIssue())
            {
                SelectCategory("clickable");
                SelectFirstActionableIssue();
            }
            if (_selectedIssue?.QuickFixes.Count > 0)
            {
                _quickFixes.Select(0);
                PreviewFix();
            }
            var ready = HasExplicitSelection && TenCategoriesVisible && ReportReady &&
                        ClickableDiagnosticReady && DeepLinkReady && QuickFixPreviewReady &&
                        ShowsHumanSurface && SourceMatches();
            if (!ready)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_A_UI_FAILED " +
                            $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                            $"categories={TenCategoriesVisible.ToString().ToLowerInvariant()} " +
                            $"report={ReportReady.ToString().ToLowerInvariant()} " +
                            $"clickable={ClickableDiagnosticReady.ToString().ToLowerInvariant()} " +
                            $"deep_link={DeepLinkReady.ToString().ToLowerInvariant()} " +
                            $"preview={QuickFixPreviewReady.ToString().ToLowerInvariant()} " +
                            $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatches().ToString().ToLowerInvariant()}");
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX7_A_EVIDENCE_FAILED type={exception.GetType().Name}");
            ReleaseEvidence();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var ready = PrepareEvidence("ux7-a-canonical-validation");
        if (!ready || !UsesIsolatedEvidence || !QuickFixPreviewReady || _evidenceSession is null)
        {
            CloseEvidence();
            return false;
        }
        try
        {
            var isolated = _evidenceSession;
            var before = isolated.Project;
            Apply();
            var after = isolated.Project;
            Undo();
            var undone = ReferenceEquals(before, isolated.Project);
            Redo();
            var redone = ReferenceEquals(after, isolated.Project);
            Save();
            var reopened = new StudioSessionController();
            try
            {
                var opened = _evidenceRoot is not null && reopened.Open(_evidenceRoot).Succeeded;
                var persisted = opened && after is not null && reopened.Project is not null &&
                                ProjectJsonSerializer.Serialize(reopened.Project) ==
                                ProjectJsonSerializer.Serialize(after);
                return _lastApplySucceeded && _lastUndoSucceeded && undone && _lastRedoSucceeded && redone &&
                       _lastSaveSucceeded && persisted && SourceMatches();
            }
            finally
            {
                reopened.Close();
            }
        }
        finally
        {
            CloseEvidence();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree()) UpdateBounds();
    }

    private void OpenBound(StudioSessionController controller, bool evidence)
    {
        _session = controller;
        ResetControls(keepReleaseState: evidence);
        RebuildAreaChoices();
        Visible = true;
        UpdateBounds();
    }

    private void RebuildAreaChoices()
    {
        _updating = true;
        _areaChoice.Clear();
        _areaChoice.AddItem("Choisir explicitement une zone…");
        foreach (var choice in _session.ValidationAreaChoicesUx7A()) _areaChoice.AddItem(choice.Label);
        _areaChoice.Select(0);
        _updating = false;
    }

    private void SelectArea(int index)
    {
        if (_updating) return;
        var choices = _session.ValidationAreaChoicesUx7A();
        _explicitSelection = index > 0 && index <= choices.Count;
        _report = _explicitSelection ? _session.BuildValidationReportUx7A(choices[index - 1].AreaId) : null;
        _selectedIssue = null;
        _fixPreview = null;
        RefreshCategories();
        SelectCategory(_selectedCategory);
        SetStatus(_report is null
            ? "Sélectionnez explicitement une zone."
            : $"Dix validations exécutées en lecture seule · {_report.Errors} erreur(s), " +
              $"{_report.Warnings} avertissement(s).", _report is null);
    }

    private void RefreshReport()
    {
        if (!_explicitSelection || _areaChoice.Selected <= 0)
        {
            SetStatus("Sélectionnez d’abord une zone.", true);
            return;
        }
        SelectArea(_areaChoice.Selected);
    }

    private void RefreshCategories()
    {
        foreach (var definition in CategoryDefinitions())
        {
            var category = _report?.Categories.FirstOrDefault(item => item.Key == definition.Key);
            var suffix = category is null ? "non exécuté" : category.Passed ? "conforme" :
                $"{category.IssueCount} message(s)";
            var button = _categoryButtons[definition.Key];
            button.Text = $"{definition.Route} · {definition.Label}\n{suffix}";
            button.Disabled = _report is null;
            button.Modulate = category?.Passed == true ? StudioStyle.Success : Colors.White;
        }
    }

    private void SelectCategory(string key)
    {
        _selectedCategory = _categoryButtons.ContainsKey(key) ? key : "clickable";
        _visibleIssues.Clear();
        _issues.Clear();
        ClearIssueDetails();
        if (_report is null)
        {
            _summary.Text = "Choisissez une zone pour lancer les dix contrôles.";
            return;
        }
        _visibleIssues.AddRange(_report.Issues.Where(issue => issue.CategoryKey == _selectedCategory));
        var category = _report.Categories.First(item => item.Key == _selectedCategory);
        _summary.Text = $"{string.Join(" + ", category.Routes)} · {category.Description} · " +
                        (category.Passed ? "aucun diagnostic bloquant" :
                            $"{category.IssueCount} message(s)");
        if (_visibleIssues.Count == 0)
        {
            _issues.AddItem("✓ Conforme · aucun diagnostic dans cette famille");
            _issues.SetItemDisabled(0, true);
            return;
        }
        foreach (var issue in _visibleIssues)
        {
            var prefix = issue.Severity switch
            {
                StudioUx7ASeverity.Error => "ERREUR",
                StudioUx7ASeverity.Warning => "ATTENTION",
                _ => "INFO",
            };
            _issues.AddItem($"{prefix} · {issue.Message}\n{issue.HumanPath}");
        }
    }

    private bool SelectFirstActionableIssue()
    {
        var index = _visibleIssues.FindIndex(issue => issue.Actionable is not null &&
                                                       issue.Target is not null &&
                                                       issue.QuickFixes.Count > 0);
        if (index < 0) return false;
        _issues.Select(index);
        SelectIssue(index);
        return true;
    }

    private void SelectIssue(int index)
    {
        if (index < 0 || index >= _visibleIssues.Count)
        {
            ClearIssueDetails();
            return;
        }
        _selectedIssue = _visibleIssues[index];
        _severity.Text = _selectedIssue.Severity switch
        {
            StudioUx7ASeverity.Error => "ERREUR · correction requise",
            StudioUx7ASeverity.Warning => "AVERTISSEMENT · décision auteur requise",
            _ => "INFORMATION",
        };
        _severity.Modulate = _selectedIssue.Severity switch
        {
            StudioUx7ASeverity.Error => StudioStyle.Danger,
            StudioUx7ASeverity.Warning => StudioStyle.Warning,
            _ => StudioStyle.Accent,
        };
        _message.Text = _selectedIssue.Message;
        _path.Text = $"Cible humaine : {_selectedIssue.HumanPath}";
        _authority.Text = $"Autorité : {_selectedIssue.Authority}";
        _code.Text = $"Code stable : {_selectedIssue.Code}";
        _openTarget.Disabled = _selectedIssue.Target is null;
        _quickFixes.Clear();
        foreach (var fix in _selectedIssue.QuickFixes)
        {
            _quickFixes.AddItem(fix.Label);
            _quickFixes.SetItemTooltip(_quickFixes.ItemCount - 1, fix.Description);
        }
        _quickFixes.Disabled = _selectedIssue.QuickFixes.Count == 0;
        if (_quickFixes.ItemCount > 0) _quickFixes.Select(0);
        ClearFixPreview();
    }

    private void OpenTarget()
    {
        if (_selectedIssue?.Target is not { } target)
        {
            SetStatus("Ce diagnostic ne cible pas un objet éditable précis.", true);
            return;
        }
        TargetRequested?.Invoke(target);
        SetStatus($"Cible demandée · {target.DisplayName}");
    }

    private void PreviewFix()
    {
        if (_selectedIssue is null || _quickFixes.Selected < 0 ||
            _quickFixes.Selected >= _selectedIssue.QuickFixes.Count)
        {
            SetStatus("Choisissez une correction canonique.", true);
            return;
        }
        _fixPreview = _session.PreviewValidationFixUx7A(_selectedIssue,
            _selectedIssue.QuickFixes[_quickFixes.Selected]);
        _fixSummary.Text = string.IsNullOrWhiteSpace(_fixPreview.Summary)
            ? _fixPreview.Message
            : _fixPreview.Summary;
        SetStatus(_fixPreview.Message, !_fixPreview.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        var result = _session.ApplyValidationFixUx7A();
        _lastApplySucceeded = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        ProjectChanged?.Invoke();
        RefreshReportAfterMutation();
    }

    private void Undo()
    {
        var result = _session.Undo();
        _lastUndoSucceeded = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        ProjectChanged?.Invoke();
        RefreshReportAfterMutation();
    }

    private void Redo()
    {
        var result = _session.Redo();
        _lastRedoSucceeded = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        ProjectChanged?.Invoke();
        RefreshReportAfterMutation();
    }

    private void Save()
    {
        var result = _session.Save();
        _lastSaveSucceeded = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        UpdateButtons();
    }

    private void RefreshReportAfterMutation()
    {
        _fixPreview = null;
        if (_areaChoice.Selected > 0) SelectArea(_areaChoice.Selected);
        UpdateButtons();
    }

    private void ClearIssueDetails()
    {
        _selectedIssue = null;
        _severity.Text = "Aucun message sélectionné";
        _severity.Modulate = StudioStyle.Muted;
        _message.Text = "Sélectionnez une ligne pour lire son diagnostic humain.";
        _path.Text = "Cible : —";
        _authority.Text = "Autorité : —";
        _code.Text = "Code : —";
        _openTarget.Disabled = true;
        _quickFixes.Clear();
        _quickFixes.Disabled = true;
        ClearFixPreview();
    }

    private void ClearFixPreview()
    {
        _fixPreview = null;
        _session.CancelValidationApprovalUx7A();
        _fixSummary.Text = _selectedIssue?.QuickFixes.Count > 0
            ? "Aperçu obligatoire avant application · aucun changement n’a été écrit."
            : "Aucune correction automatique · ouvrez la cible pour corriger manuellement.";
        UpdateButtons();
    }

    private void ResetControls(bool keepReleaseState = false)
    {
        _updating = true;
        _explicitSelection = false;
        _report = null;
        _selectedIssue = null;
        _visibleIssues.Clear();
        _fixPreview = null;
        _lastApplySucceeded = false;
        _lastUndoSucceeded = false;
        _lastRedoSucceeded = false;
        _lastSaveSucceeded = false;
        _areaChoice?.Clear();
        _issues?.Clear();
        _quickFixes?.Clear();
        if (_summary is not null) _summary.Text = "Choisissez une zone pour lancer les dix contrôles.";
        if (_status is not null) _status.Text = "Sélectionnez explicitement une zone.";
        if (_severity is not null) ClearIssueDetails();
        if (!keepReleaseState) _evidenceReleased = true;
        _updating = false;
        UpdateButtons();
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (_apply is null) return;
        _previewFix.Disabled = _selectedIssue?.QuickFixes.Count is not > 0;
        _apply.Disabled = !QuickFixPreviewReady;
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
    }

    private SourceSnapshotUx7A CaptureSource()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureValidationApprovalUx7A());
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.AreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureValidationApprovalUx7A() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = _evidenceRoot is null ||
                       StudioSessionController.DeleteTemporaryBlueprintUx3A(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        _session = _sourceSession;
        if (!released) GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_A_UI_CLEANUP_FAILED");
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 54, 1160, 1840);
        var height = Math.Clamp(viewport.Y - 40, 780, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static string RequestedCategory(string mode)
    {
        foreach (var category in CategoryDefinitions())
            if (mode.Contains(category.Key, StringComparison.OrdinalIgnoreCase)) return category.Key;
        return "clickable";
    }

    private static IReadOnlyList<(string Key, string Route, string Label, string Description)>
        CategoryDefinitions() =>
        [
            ("schema", "ORF-QA-001", "Schémas", "Documents JSON versionnés et lisibles."),
            ("references", "ORF-QA-002", "Références", "Identités et liens typés résolus."),
            ("graphs", "ORF-QA-003", "Graphes", "Dialogues et quêtes connectés."),
            ("geometry", "ORF-QA-004", "Géométrie", "Volumes et placements bornés."),
            ("navigation", "ORF-QA-005", "Navigation", "Surfaces, portails et apparitions."),
            ("localization", "ORF-QA-006", "Localisation", "Couverture et qualité des textes."),
            ("license", "ORF-QA-007", "Licences", "SPDX et autorisations de distribution."),
            ("area", "ORF-AREA-037", "Zone prête", "Neuf contrôles de préparation."),
            ("audio", "ORF-AUD-020", "Audio", "Catalogues, chemins et empreintes."),
            ("clickable", "ORF-QA-021", "Diagnostics cliquables", "Cibles et corrections."),
        ];

    private static Button ActionButton(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static IEnumerable<string> DescendantText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case LineEdit lineEdit when lineEdit.Visible: yield return lineEdit.Text; break;
                case TextEdit textEdit when textEdit.Visible: yield return textEdit.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case ItemList list when list.Visible:
                    for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private sealed record SourceSnapshotUx7A(
        ProjectDefinition Project,
        ProjectCommandHistory History,
        ProjectRevision Revision,
        IReadOnlyList<CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        bool CanUndo,
        bool CanRedo,
        AreaId? AreaId,
        IReadOnlyList<EntityId> SelectedEntityIds,
        EntityId? SelectedEntityId,
        StudioUx7AApprovalSnapshot Approval);
}
