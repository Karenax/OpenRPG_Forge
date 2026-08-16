using Forge.Application.Creator;
using Forge.Domain.Validation;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioDiagnosticsPage : Control
{
    private readonly StudioSessionController _session;
    private readonly LineEdit _textFilter;
    private readonly OptionButton _severityFilter;
    private readonly OptionButton _categoryFilter;
    private readonly OptionButton _correlationFilter;
    private readonly Label _summary;
    private readonly ItemList _issues;
    private readonly Label _empty;
    private readonly Label _detailTitle;
    private readonly Label _detailMessage;
    private readonly Label _detailLocation;
    private readonly Label _detailCode;
    private readonly OptionButton _quickFixes;
    private readonly Label _fixSummary;
    private readonly Button _navigate;
    private readonly Button _previewFix;
    private readonly Button _applyFix;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly VBoxContainer _activity;
    private readonly Label _status;
    private readonly List<CreatorDiagnostic> _visibleDiagnostics = [];
    private IReadOnlyList<StudioDiagnosticCorrelation> _correlations = [];
    private CreatorDiagnostic? _selected;
    private bool _loading;

    internal event Action<CreatorSelection>? TargetRequested;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? ProjectChanged;

    internal int VisibleDiagnosticCount => _visibleDiagnostics.Count;

    internal bool HasQuickFix => _selected?.QuickFixes.Count > 0;

    internal bool ShowsHumanSurface =>
        !_detailTitle.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_detailMessage.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_detailLocation.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_status.Text.Contains("hash", StringComparison.OrdinalIgnoreCase);

    internal StudioDiagnosticsPage(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioDiagnosticsPage";
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = StudioStyle.Background,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = StudioStyle.Margin(14, 12, 14, 12);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var summaryRow = new HBoxContainer();
        summaryRow.AddThemeConstantOverride("separation", 8);
        _summary = StudioStyle.Label("Validation du module", 14, StudioStyle.Text, wrap: true);
        _summary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        summaryRow.AddChild(_summary);
        var refresh = StudioStyle.Button("Actualiser", compact: true);
        refresh.TooltipText = "Relire le document canonique et les opérations récentes.";
        refresh.Pressed += Refresh;
        summaryRow.AddChild(refresh);
        var export = StudioStyle.Button("Exporter le diagnostic", compact: true);
        export.TooltipText = "Créer un bundle déterministe et expurgé dans le projet.";
        export.Pressed += Export;
        summaryRow.AddChild(export);
        root.AddChild(summaryRow);

        var filterPanel = StudioStyle.Panel("DiagnosticFilters", StudioStyle.Chrome, 7, 1);
        var filterMargin = StudioStyle.Margin(9, 7, 9, 7);
        filterPanel.AddChild(filterMargin);
        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 6);
        filterMargin.AddChild(filters);
        _textFilter = new LineEdit
        {
            PlaceholderText = "Filtrer par message, propriété ou référence…",
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220, 0),
        };
        StudioStyle.ApplyLineEdit(_textFilter);
        _textFilter.TextChanged += _ => Refresh();
        filters.AddChild(_textFilter);
        _severityFilter = new OptionButton { CustomMinimumSize = new Vector2(165, 0) };
        StudioStyle.ApplyOptionButton(_severityFilter);
        _severityFilter.AddItem("Toutes les gravités");
        _severityFilter.AddItem("Avertissements + erreurs");
        _severityFilter.AddItem("Erreurs seulement");
        _severityFilter.ItemSelected += _ => Refresh();
        filters.AddChild(_severityFilter);
        _categoryFilter = new OptionButton { CustomMinimumSize = new Vector2(155, 0) };
        StudioStyle.ApplyOptionButton(_categoryFilter);
        _categoryFilter.AddItem("Toutes les catégories");
        foreach (var category in Enum.GetValues<CreatorDiagnosticCategory>())
        {
            _categoryFilter.AddItem(CategoryLabel(category));
        }
        _categoryFilter.ItemSelected += _ => Refresh();
        filters.AddChild(_categoryFilter);
        _correlationFilter = new OptionButton { CustomMinimumSize = new Vector2(170, 0) };
        StudioStyle.ApplyOptionButton(_correlationFilter);
        _correlationFilter.ItemSelected += _ => Refresh();
        filters.AddChild(_correlationFilter);
        root.AddChild(filterPanel);

        var body = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffsets = [520],
        };
        root.AddChild(body);

        var listPanel = StudioStyle.Panel("DiagnosticList", StudioStyle.Surface, 8);
        listPanel.CustomMinimumSize = new Vector2(360, 0);
        body.AddChild(listPanel);
        var listMargin = StudioStyle.Margin(10, 10, 10, 10);
        listPanel.AddChild(listMargin);
        var listColumn = new VBoxContainer();
        listColumn.AddThemeConstantOverride("separation", 6);
        listMargin.AddChild(listColumn);
        listColumn.AddChild(StudioStyle.SectionLabel("PROBLÈMES ET MESSAGES"));
        _issues = new ItemList
        {
            AllowReselect = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(330, 260),
        };
        _issues.ItemSelected += SelectIssue;
        _issues.ItemActivated += _ => Navigate();
        listColumn.AddChild(_issues);
        _empty = StudioStyle.Label(
            "Aucun problème pour ces filtres. Le statut et les opérations récentes restent disponibles à droite.",
            11,
            StudioStyle.Success,
            wrap: true);
        listColumn.AddChild(_empty);

        var detailPanel = StudioStyle.Panel("DiagnosticDetails", StudioStyle.SurfaceRaised, 8);
        detailPanel.CustomMinimumSize = new Vector2(360, 0);
        body.AddChild(detailPanel);
        var detailMargin = StudioStyle.Margin(12, 10, 12, 10);
        detailPanel.AddChild(detailMargin);
        var detailScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        detailMargin.AddChild(detailScroll);
        var detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detail.AddThemeConstantOverride("separation", 7);
        detailScroll.AddChild(detail);
        detail.AddChild(StudioStyle.SectionLabel("PROBLÈME SÉLECTIONNÉ"));
        _detailTitle = StudioStyle.Label("Sélectionnez un message", 16, StudioStyle.Text, wrap: true);
        detail.AddChild(_detailTitle);
        _detailMessage = StudioStyle.Label(
            "Les erreurs sont localisées et n’exposent pas les identifiants internes dans le parcours normal.",
            11,
            StudioStyle.Muted,
            wrap: true);
        detail.AddChild(_detailMessage);
        _detailLocation = StudioStyle.Label(string.Empty, 10, StudioStyle.Accent, wrap: true);
        detail.AddChild(_detailLocation);
        _detailCode = StudioStyle.Label(string.Empty, 9, StudioStyle.Muted, wrap: true);
        _detailCode.TooltipText = "Référence stable destinée au support et aux rapports automatisés.";
        detail.AddChild(_detailCode);
        _navigate = StudioStyle.Button("Aller à la cible", compact: true);
        _navigate.Disabled = true;
        _navigate.Pressed += Navigate;
        detail.AddChild(_navigate);
        detail.AddChild(new HSeparator());
        detail.AddChild(StudioStyle.SectionLabel("CORRECTION PRÉVISUALISABLE"));
        _quickFixes = new OptionButton();
        StudioStyle.ApplyOptionButton(_quickFixes);
        _quickFixes.ItemSelected += _ => InvalidateFixPreview();
        detail.AddChild(_quickFixes);
        _fixSummary = StudioStyle.Label(
            "Aucune correction automatique sélectionnée.",
            10,
            StudioStyle.Muted,
            wrap: true);
        detail.AddChild(_fixSummary);
        var fixActions = new HBoxContainer();
        fixActions.AddThemeConstantOverride("separation", 5);
        _previewFix = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _previewFix.Disabled = true;
        _previewFix.Pressed += PreviewFix;
        fixActions.AddChild(_previewFix);
        _applyFix = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _applyFix.Disabled = true;
        _applyFix.Pressed += ApplyFix;
        fixActions.AddChild(_applyFix);
        detail.AddChild(fixActions);
        var historyActions = new HBoxContainer();
        historyActions.AddThemeConstantOverride("separation", 5);
        _undo = StudioStyle.Button("Annuler la correction", compact: true);
        _undo.Pressed += Undo;
        historyActions.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        historyActions.AddChild(_redo);
        detail.AddChild(historyActions);
        detail.AddChild(new HSeparator());
        detail.AddChild(StudioStyle.SectionLabel("ACTIVITÉ RÉCENTE"));
        _activity = new VBoxContainer();
        _activity.AddThemeConstantOverride("separation", 4);
        detail.AddChild(_activity);

        _status = StudioStyle.Label(
            "Prêt · sélectionnez un problème pour voir sa cible et ses corrections.",
            10,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_status);
    }

    internal void Activate()
    {
        Visible = true;
        Refresh();
    }

    internal void Deactivate()
    {
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        Activate();
        if (_visibleDiagnostics.Count == 0 && _session.Project is { } project)
        {
            var document = new CreatorSearchDocument(
                CreatorContentKind.Module,
                project.Module.Id.ToString(),
                project.Module.DisplayName,
                "module",
                project.Module.DisplayName,
                provenance: "studio:ux0-j-evidence");
            _session.PreviewSafeRename(document, project.Module.DisplayName, "Adresse Interne Refusée");
            _session.CancelSafeRename();
            Refresh();
        }

        var quickFixIndex = _visibleDiagnostics.FindIndex(item => item.QuickFixes.Count > 0);
        var selectedIndex = quickFixIndex >= 0 ? quickFixIndex : (_visibleDiagnostics.Count > 0 ? 0 : -1);
        if (selectedIndex >= 0)
        {
            _issues.Select(selectedIndex);
            SelectIssue(selectedIndex);
        }
        return VisibleDiagnosticCount > 0 && ShowsHumanSurface;
    }

    internal bool RunUiTransactionEvidence()
    {
        var project = _session.Project;
        if (project is null)
        {
            return false;
        }

        var document = new CreatorSearchDocument(
            CreatorContentKind.Module,
            project.Module.Id.ToString(),
            project.Module.DisplayName,
            "module",
            project.Module.DisplayName,
            provenance: "studio:ux0-j-ui-smoke");
        var rejected = _session.PreviewSafeRename(document, project.Module.DisplayName, "Adresse Interne Refusée");
        _session.CancelSafeRename();
        Refresh();
        return !rejected.Succeeded && ReferenceEquals(project, _session.Project) &&
               _visibleDiagnostics.Any(item => item.Code == SafeRenameHandler.InvalidSlugCode) && ShowsHumanSurface;
    }

    internal void Refresh()
    {
        if (_loading)
        {
            return;
        }
        _loading = true;
        try
        {
            var selectedCorrelation = SelectedCorrelationId();
            var query = new CreatorDiagnosticQuery(
                string.IsNullOrWhiteSpace(_textFilter.Text) ? null : _textFilter.Text,
                _categoryFilter.Selected <= 0
                    ? null
                    : Enum.GetValues<CreatorDiagnosticCategory>()[_categoryFilter.Selected - 1],
                _severityFilter.Selected switch
                {
                    1 => CreatorDiagnosticSeverity.Warning,
                    2 => CreatorDiagnosticSeverity.Error,
                    _ => CreatorDiagnosticSeverity.Information,
                },
                selectedCorrelation);
            var overview = _session.GetDiagnosticOverview(query);
            RebuildCorrelations(overview.Correlations, selectedCorrelation);
            _summary.Text = $"{overview.Errors} erreur(s) · {overview.Warnings} avertissement(s) · " +
                            $"{overview.Information} information(s)";
            _summary.AddThemeColorOverride(
                "font_color",
                overview.Errors > 0 ? StudioStyle.Danger : overview.Warnings > 0 ? StudioStyle.Warning : StudioStyle.Success);

            _visibleDiagnostics.Clear();
            _visibleDiagnostics.AddRange(overview.Diagnostics);
            _issues.Clear();
            foreach (var diagnostic in _visibleDiagnostics)
            {
                var target = diagnostic.Target?.DisplayName ?? HumanLocation(diagnostic);
                _issues.AddItem($"{SeverityLabel(diagnostic.Severity)} · {CategoryLabel(diagnostic.Category)}\n" +
                                $"{diagnostic.Message} · {target}");
                _issues.SetItemTooltip(_issues.ItemCount - 1, diagnostic.Message);
            }
            _empty.Visible = _visibleDiagnostics.Count == 0;
            RebuildActivity(overview.RecentMessages);

            if (_visibleDiagnostics.Count > 0)
            {
                var selected = _selected is null
                    ? 0
                    : _visibleDiagnostics.FindIndex(item => item.Id == _selected.Id);
                selected = selected < 0 ? 0 : selected;
                _issues.Select(selected);
                SelectIssue(selected);
            }
            else
            {
                ClearDetails();
            }
            _undo.Disabled = !_session.CanUndo;
            _redo.Disabled = !_session.CanRedo;
        }
        finally
        {
            _loading = false;
        }
    }

    private void SelectIssue(long index)
    {
        if (index < 0 || index >= _visibleDiagnostics.Count)
        {
            return;
        }

        _selected = _visibleDiagnostics[(int)index];
        _detailTitle.Text = $"{SeverityLabel(_selected.Severity)} · {CategoryLabel(_selected.Category)}";
        _detailTitle.AddThemeColorOverride("font_color", _selected.Severity switch
        {
            CreatorDiagnosticSeverity.Error => StudioStyle.Danger,
            CreatorDiagnosticSeverity.Warning => StudioStyle.Warning,
            _ => StudioStyle.Success,
        });
        _detailMessage.Text = _selected.Message;
        _detailLocation.Text = _selected.Target is null
            ? $"Emplacement : {HumanLocation(_selected)}"
            : $"Cible : {_selected.Target.DisplayName} · propriété {HumanProperty(_selected.Property)}";
        _detailCode.Text = $"Référence support : {_selected.Code}";
        _navigate.Disabled = _selected.Target is null;
        _quickFixes.Clear();
        foreach (var fix in _selected.QuickFixes)
        {
            _quickFixes.AddItem(fix.Label);
            _quickFixes.SetItemTooltip(_quickFixes.ItemCount - 1, fix.Description);
        }
        _quickFixes.Disabled = _selected.QuickFixes.Count == 0;
        _previewFix.Disabled = _selected.QuickFixes.Count == 0;
        _applyFix.Disabled = true;
        _fixSummary.Text = _selected.QuickFixes.Count == 0
            ? "Aucune correction automatique · utilisez la cible et le message pour corriger manuellement."
            : $"{_selected.QuickFixes.Count} correction(s) disponible(s) · aperçu obligatoire.";
    }

    private void Navigate()
    {
        if (_selected?.Target is null)
        {
            SetStatus("Ce message ne cible pas un objet éditable précis.", true);
            return;
        }
        TargetRequested?.Invoke(_selected.Target);
        SetStatus($"Cible ouverte · {_selected.Target.DisplayName}", false);
    }

    private void PreviewFix()
    {
        if (_selected is null || _quickFixes.Selected < 0 ||
            _quickFixes.Selected >= _selected.QuickFixes.Count)
        {
            SetStatus("Choisissez une correction avant de la prévisualiser.", true);
            return;
        }
        var result = _session.PreviewDiagnosticQuickFix(
            _selected,
            _selected.QuickFixes[_quickFixes.Selected]);
        _fixSummary.Text = string.IsNullOrWhiteSpace(result.Summary) ? result.Message : result.Summary;
        _applyFix.Disabled = !result.CanApply;
        SetStatus(result.Message, !result.Succeeded);
    }

    private void ApplyFix()
    {
        var result = _session.ApplyApprovedDiagnosticQuickFix();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            ProjectChanged?.Invoke();
            Refresh();
        }
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            ProjectChanged?.Invoke();
            Refresh();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            ProjectChanged?.Invoke();
            Refresh();
        }
    }

    private void Export()
    {
        var result = _session.ExportDiagnosticBundle();
        var suffix = result.Succeeded
            ? $" · {result.DiagnosticCount} message(s) · journal {(result.IncludesSessionLog ? "inclus" : "vide")}"
            : string.Empty;
        SetStatus(result.Message + suffix, !result.Succeeded);
    }

    private void InvalidateFixPreview()
    {
        _applyFix.Disabled = true;
        if (_selected is not null && _selected.QuickFixes.Count > 0)
        {
            _fixSummary.Text = "Correction choisie · prévisualisez avant d’appliquer.";
        }
    }

    private void RebuildCorrelations(
        IReadOnlyList<StudioDiagnosticCorrelation> correlations,
        string? selectedId)
    {
        _correlations = correlations;
        _correlationFilter.Clear();
        var selectedIndex = 0;
        for (var index = 0; index < correlations.Count; index++)
        {
            _correlationFilter.AddItem(correlations[index].Label);
            if (string.Equals(correlations[index].Id, selectedId, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = index;
            }
        }
        _correlationFilter.Selected = selectedIndex;
    }

    private string? SelectedCorrelationId() =>
        _correlationFilter.Selected >= 0 && _correlationFilter.Selected < _correlations.Count
            ? _correlations[_correlationFilter.Selected].Id
            : null;

    private void RebuildActivity(IReadOnlyList<StudioStatusMessage> messages)
    {
        ClearChildren(_activity);
        if (messages.Count == 0)
        {
            _activity.AddChild(StudioStyle.Label(
                "Aucune opération récente dans cette session.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }
        foreach (var message in messages.Take(5))
        {
            var card = StudioStyle.Panel("DiagnosticActivity", StudioStyle.Surface, 5);
            var text = StudioStyle.Label(
                $"{(message.Error ? "À vérifier" : "Terminé")} · {message.Message}",
                9,
                message.Error ? StudioStyle.Danger : StudioStyle.Muted,
                wrap: true);
            text.TooltipText = message.CorrelationLabel;
            card.AddChild(text);
            _activity.AddChild(card);
        }
    }

    private void ClearDetails()
    {
        _selected = null;
        _detailTitle.Text = "Aucun problème sélectionné";
        _detailTitle.AddThemeColorOverride("font_color", StudioStyle.Success);
        _detailMessage.Text = "Le module ne présente aucun problème pour les filtres actifs.";
        _detailLocation.Text = string.Empty;
        _detailCode.Text = string.Empty;
        _navigate.Disabled = true;
        _quickFixes.Clear();
        _quickFixes.Disabled = true;
        _previewFix.Disabled = true;
        _applyFix.Disabled = true;
        _fixSummary.Text = "Aucune correction nécessaire.";
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private static string SeverityLabel(CreatorDiagnosticSeverity severity) => severity switch
    {
        CreatorDiagnosticSeverity.Error => "Erreur",
        CreatorDiagnosticSeverity.Warning => "Avertissement",
        _ => "Information",
    };

    private static string CategoryLabel(CreatorDiagnosticCategory category) => category switch
    {
        CreatorDiagnosticCategory.Schema => "Format",
        CreatorDiagnosticCategory.Reference => "Référence",
        CreatorDiagnosticCategory.Graph => "Histoire et logique",
        CreatorDiagnosticCategory.Geometry => "Géométrie",
        CreatorDiagnosticCategory.Navigation => "Navigation",
        CreatorDiagnosticCategory.License => "Licence",
        _ => "Exécution",
    };

    private static string HumanLocation(CreatorDiagnostic diagnostic)
    {
        if (diagnostic.Target is not null)
        {
            return diagnostic.Target.DisplayName;
        }
        if (diagnostic.DocumentPath.StartsWith("module", StringComparison.OrdinalIgnoreCase))
        {
            return "Configuration du module";
        }
        if (diagnostic.DocumentPath.StartsWith("project", StringComparison.OrdinalIgnoreCase))
        {
            return "Projet";
        }
        if (diagnostic.DocumentPath.StartsWith("studio", StringComparison.OrdinalIgnoreCase))
        {
            return "Opération Studio";
        }
        return "Contenu du module";
    }

    private static string HumanProperty(string property) => property switch
    {
        "cellSize" => "taille des cellules",
        "position" => "position",
        "document" => "contenu",
        _ => property.Replace('_', ' '),
    };

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
