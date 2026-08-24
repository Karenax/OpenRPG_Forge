using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAreaDocumentOperationsOverlay : Control
{
    private static readonly AreaResizeAnchor[] Anchors =
    [
        AreaResizeAnchor.Center,
        AreaResizeAnchor.NorthWest,
        AreaResizeAnchor.NorthEast,
        AreaResizeAnchor.SouthWest,
        AreaResizeAnchor.SouthEast,
    ];

    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly Label _source;
    private readonly Button _resizeTab;
    private readonly Button _cloneTab;
    private readonly VBoxContainer _resizePanel;
    private readonly VBoxContainer _clonePanel;
    private readonly SpinBox _width;
    private readonly SpinBox _depth;
    private readonly OptionButton _anchor;
    private readonly OptionButton _lossPolicy;
    private readonly Label _resizeSummary;
    private readonly Label _atRisk;
    private readonly LineEdit _cloneName;
    private readonly OptionButton _cloneMode;
    private readonly OptionButton _externalPortals;
    private readonly Label _cloneSummary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;
    private StudioAreaResizeDraft? _resizeDraft;
    private StudioAreaCloneDraft? _cloneDraft;
    private bool _updating;
    private bool _operationApplied;
    private int _section;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? AreaChanged;

    internal bool IsOpen => Visible;

    internal int SectionCount => 2;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(
                ' ',
                _source.Text,
                _resizeTab.Text,
                _cloneTab.Text,
                _resizeSummary.Text,
                _atRisk.Text,
                _cloneSummary.Text,
                _status.Text);
            return SectionCount == 2 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("project.json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioAreaDocumentOperationsOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioAreaDocumentOperationsOverlay";
        Visible = false;
        ZIndex = 435;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .76f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("AreaDocumentOperationsDialog", StudioStyle.Chrome, 12, 2);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("DOCUMENT DE ZONE", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Adapter ou réutiliser la zone active", 20));
        heading.AddChild(StudioStyle.Label(
            "Aperçu obligatoire · aucune écriture silencieuse · chaque opération reste annulable",
            10,
            StudioStyle.Muted));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer une opération en attente · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        _source = StudioStyle.Label(string.Empty, 12, StudioStyle.Success, wrap: true);
        root.AddChild(_source);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _resizeTab = StudioStyle.Button("Dimensions", compact: true);
        _resizeTab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _resizeTab.TooltipText = "Changer la largeur et la profondeur en contrôlant les objets hors limites.";
        _resizeTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_resizeTab);
        _cloneTab = StudioStyle.Button("Copier", compact: true);
        _cloneTab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _cloneTab.TooltipText = "Créer une zone complète ou un gabarit vide avec de nouvelles identités internes.";
        _cloneTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_cloneTab);

        var body = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _resizePanel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _resizePanel.AddThemeConstantOverride("separation", 9);
        panels.AddChild(_resizePanel);
        _resizePanel.AddChild(StudioStyle.Label("Redimensionner sans perdre le contrôle du contenu", 17));
        _resizePanel.AddChild(StudioStyle.Label(
            "Le point d’ancrage indique quel bord reste fixe. Le Studio liste les objets concernés avant toute application.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var resizeGrid = Grid();
        _resizePanel.AddChild(resizeGrid);
        _width = AddField(resizeGrid, "Largeur", DimensionBox());
        _depth = AddField(resizeGrid, "Profondeur", DimensionBox());
        _anchor = AddField(
            resizeGrid,
            "Point fixe",
            Option("Centre", "Coin nord-ouest", "Coin nord-est", "Coin sud-ouest", "Coin sud-est"));
        _lossPolicy = AddField(
            resizeGrid,
            "Objets hors limites",
            Option("Replacer à l’intérieur (recommandé)", "Retirer de la zone"));
        _resizePanel.AddChild(PreviewCard(
            "IMPACT DU REDIMENSIONNEMENT",
            out _resizeSummary,
            "Prévisualisez pour connaître les déplacements et retraits éventuels."));
        _atRisk = StudioStyle.Label("Objets concernés · aucun aperçu calculé", 10, StudioStyle.Muted, wrap: true);
        _resizePanel.AddChild(_atRisk);

        _clonePanel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _clonePanel.AddThemeConstantOverride("separation", 9);
        panels.AddChild(_clonePanel);
        _clonePanel.AddChild(StudioStyle.Label("Créer un nouveau document à partir de cette zone", 17));
        _clonePanel.AddChild(StudioStyle.Label(
            "La copie obtient ses propres objets, transitions, terrain, navigation et environnement. Les modèles restent partagés.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var cloneGrid = Grid();
        _clonePanel.AddChild(cloneGrid);
        _cloneName = AddField(cloneGrid, "Nom de la nouvelle zone", Edit("", "Nom affiché dans l’explorateur"));
        _cloneMode = AddField(cloneGrid, "Contenu", Option("Copie complète", "Gabarit vide"));
        _externalPortals = AddField(
            cloneGrid,
            "Transitions vers d’autres zones",
            Option("Conserver", "Retirer de la copie"));
        _clonePanel.AddChild(PreviewCard(
            "CONTENU DE LA COPIE",
            out _cloneSummary,
            "Prévisualisez pour compter les objets et transitions du futur document."));
        _clonePanel.AddChild(StudioStyle.Label(
            "La zone source et les zones voisines ne sont jamais modifiées par la copie.",
            10,
            StudioStyle.Success,
            wrap: true));

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler l’opération", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        footer.AddChild(_redo);

        _status = StudioStyle.Label("Prêt · aucun changement appliqué", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
        ShowSection(0);
    }

    internal void OpenResize() => Open(0);

    internal void OpenClone() => Open(1);

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }
        _session.CancelAreaDocumentOperation();
        _resizeDraft = null;
        _cloneDraft = null;
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        if (mode.EndsWith("copy", StringComparison.OrdinalIgnoreCase))
        {
            OpenClone();
            _updating = true;
            _cloneMode.Selected = 0;
            _externalPortals.Selected = 0;
            _updating = false;
            Preview();
        }
        else
        {
            OpenResize();
            var current = _resizeDraft!;
            _updating = true;
            _width.Value = Math.Min(AreaDimensions.MaximumSize, current.Width + 8);
            _depth.Value = Math.Min(AreaDimensions.MaximumSize, current.Depth + 8);
            _anchor.Selected = 0;
            _lossPolicy.Selected = 0;
            _updating = false;
            Preview();
        }
        return Visible && !_apply.Disabled && ShowsHumanSurface;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalArea = _session.ActiveArea;
        if (originalProject is null || originalAreaId is null || originalArea is null)
        {
            return false;
        }

        OpenResize();
        _updating = true;
        _width.Value = Math.Min(AreaDimensions.MaximumSize, originalArea.Dimensions.Width + 4);
        _depth.Value = Math.Min(AreaDimensions.MaximumSize, originalArea.Dimensions.Depth + 4);
        _updating = false;
        Preview();
        var resizePreview = !_apply.Disabled;
        Apply();
        var resizeApplied = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var resizeUndone = ReferenceEquals(originalProject, _session.Project) && !_redo.Disabled;
        Redo();
        var resizeRedone = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var resizeRestored = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        Close();

        _session.ActivateAreaTab(originalAreaId, persist: false);
        OpenClone();
        _updating = true;
        _cloneName.Text = "Preuve interface UX1-B";
        _cloneMode.Selected = 1;
        _externalPortals.Selected = 1;
        _updating = false;
        Preview();
        var clonePreview = !_apply.Disabled;
        Apply();
        var cloneApplied = _session.Project?.Areas.Count == originalProject.Areas.Count + 1 && !_undo.Disabled;
        Undo();
        var cloneUndone = ReferenceEquals(originalProject, _session.Project) && !_redo.Disabled;
        Redo();
        var cloneRedone = _session.Project?.Areas.Count == originalProject.Areas.Count + 1 && !_undo.Disabled;
        Undo();
        var cloneRestored = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        _session.ActivateAreaTab(originalAreaId, persist: false);
        Close();

        return resizePreview && resizeApplied && resizeUndone && resizeRedone && resizeRestored &&
               clonePreview && cloneApplied && cloneUndone && cloneRedone && cloneRestored && ShowsHumanSurface;
    }

    private void Open(int section)
    {
        _resizeDraft = _session.CreateAreaResizeDraft();
        _cloneDraft = _session.CreateAreaCloneDraft();
        _operationApplied = false;
        LoadDrafts();
        SetEditingEnabled(true);
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        ShowSection(section);
        SetStatus("Outil ouvert · prévisualisation obligatoire avant application", false);
        if (section == 0)
        {
            _width.GetLineEdit().GrabFocus();
        }
        else
        {
            _cloneName.GrabFocus();
        }
    }

    private void LoadDrafts()
    {
        var resize = _resizeDraft!;
        var clone = _cloneDraft!;
        var source = _session.ActiveArea!;
        _updating = true;
        _source.Text = $"Zone source · {source.DisplayName} · {source.Dimensions.Width} × {source.Dimensions.Depth} · " +
                       $"{source.Entities.Count} objet(s)";
        _width.Value = resize.Width;
        _depth.Value = resize.Depth;
        _anchor.Selected = Array.IndexOf(Anchors, resize.Anchor);
        _lossPolicy.Selected = resize.LossPolicy == StudioAreaResizeLossPolicy.RepositionInside ? 0 : 1;
        _cloneName.Text = clone.DisplayName;
        _cloneMode.Selected = clone.Mode == AreaCloneMode.Full ? 0 : 1;
        _externalPortals.Selected = clone.ExternalPortalPolicy == ExternalPortalPolicy.Preserve ? 0 : 1;
        _updating = false;
        InvalidatePreview();
    }

    private StudioAreaResizeDraft ReadResizeDraft() => _resizeDraft! with
    {
        Width = (int)_width.Value,
        Depth = (int)_depth.Value,
        Anchor = Anchors[Math.Clamp(_anchor.Selected, 0, Anchors.Length - 1)],
        LossPolicy = _lossPolicy.Selected == 0
            ? StudioAreaResizeLossPolicy.RepositionInside
            : StudioAreaResizeLossPolicy.Remove,
    };

    private StudioAreaCloneDraft ReadCloneDraft() => _cloneDraft! with
    {
        DisplayName = _cloneName.Text,
        Mode = _cloneMode.Selected == 0 ? AreaCloneMode.Full : AreaCloneMode.Template,
        ExternalPortalPolicy = _externalPortals.Selected == 0
            ? ExternalPortalPolicy.Preserve
            : ExternalPortalPolicy.Drop,
    };

    private void Preview()
    {
        if (_section == 0)
        {
            var result = _session.PreviewAreaResize(ReadResizeDraft());
            _resizeSummary.Text = string.IsNullOrWhiteSpace(result.Summary)
                ? "Aucun impact validé. Corrigez les dimensions puis relancez l’aperçu."
                : result.Summary;
            _resizeSummary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
            _atRisk.Text = result.AtRiskObjects is { Count: > 0 }
                ? "Objets concernés\n• " + string.Join("\n• ", result.AtRiskObjects)
                : "Objets concernés · aucun objet hors limites";
            _atRisk.AddThemeColorOverride(
                "font_color",
                result.AtRiskObjects is { Count: > 0 } ? StudioStyle.Accent : StudioStyle.Success);
            _apply.Disabled = !result.CanApply;
            SetStatus(result.Message, !result.Succeeded);
            return;
        }

        var clone = _session.PreviewAreaClone(ReadCloneDraft());
        _cloneSummary.Text = string.IsNullOrWhiteSpace(clone.Summary)
            ? "Aucun impact validé. Corrigez le nom puis relancez l’aperçu."
            : clone.Summary;
        _cloneSummary.AddThemeColorOverride("font_color", clone.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _apply.Disabled = !clone.CanApply;
        SetStatus(clone.Message, !clone.Succeeded);
    }

    private void Apply()
    {
        var result = _section == 0
            ? _session.ApplyApprovedAreaResize()
            : _session.ApplyApprovedAreaClone();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }

        _operationApplied = true;
        SetEditingEnabled(false);
        _apply.Disabled = true;
        _preview.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        AreaChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(
            result.Succeeded ? "Opération annulée · le document précédent est restauré." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = true;
            _redo.Disabled = false;
            AreaChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(
            result.Succeeded ? "Opération rétablie · enregistrez pour écrire les documents." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            AreaChanged?.Invoke();
        }
    }

    private void ShowSection(int section)
    {
        _section = Math.Clamp(section, 0, 1);
        _resizePanel.Visible = _section == 0;
        _clonePanel.Visible = _section == 1;
        UpdateTabStyles();
        if (!_operationApplied)
        {
            InvalidatePreview();
        }
    }

    private void UpdateTabStyles()
    {
        ApplyTabStyle(_resizeTab, _section == 0);
        ApplyTabStyle(_cloneTab, _section == 1);
    }

    private static void ApplyTabStyle(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            StudioStyle.Box(
                selected ? StudioStyle.Selection : StudioStyle.SurfaceRaised,
                selected ? StudioStyle.Success : StudioStyle.Border,
                6,
                selected ? 2 : 1));
    }

    private void ConnectInvalidation()
    {
        _width.ValueChanged += _ => InvalidatePreview();
        _depth.ValueChanged += _ => InvalidatePreview();
        _anchor.ItemSelected += _ => InvalidatePreview();
        _lossPolicy.ItemSelected += _ => InvalidatePreview();
        _cloneName.TextChanged += _ => InvalidatePreview();
        _cloneMode.ItemSelected += _ => InvalidatePreview();
        _externalPortals.ItemSelected += _ => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _operationApplied)
        {
            return;
        }
        _session.CancelAreaDocumentOperation();
        _apply.Disabled = true;
        _preview.Disabled = false;
        if (_section == 0)
        {
            _resizeSummary.Text = "Aperçu à recalculer · aucun changement appliqué.";
            _resizeSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
            _atRisk.Text = "Objets concernés · aperçu à recalculer";
            _atRisk.AddThemeColorOverride("font_color", StudioStyle.Muted);
        }
        else
        {
            _cloneSummary.Text = "Aperçu à recalculer · la nouvelle zone n’existe pas encore.";
            _cloneSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        }
    }

    private void SetEditingEnabled(bool enabled)
    {
        _width.Editable = enabled;
        _depth.Editable = enabled;
        _anchor.Disabled = !enabled;
        _lossPolicy.Disabled = !enabled;
        _cloneName.Editable = enabled;
        _cloneMode.Disabled = !enabled;
        _externalPortals.Disabled = !enabled;
        _resizeTab.Disabled = !enabled;
        _cloneTab.Disabled = !enabled;
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.75f : Size.X >= 2300 ? 1.25f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 760, 1040);
        var height = Math.Clamp((Size.Y - 72) / scale, 610, 760);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static PanelContainer PreviewCard(string title, out Label summary, string initial)
    {
        var card = StudioStyle.Panel(title.Replace(' ', '-'), StudioStyle.SurfaceRaised, 8);
        card.CustomMinimumSize = new Vector2(0, 142);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        card.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        column.AddChild(StudioStyle.SectionLabel(title));
        summary = StudioStyle.Label(initial, 12, StudioStyle.Muted, wrap: true);
        column.AddChild(summary);
        return card;
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(220, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.AddChild(title);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static LineEdit Edit(string text, string placeholder)
    {
        var edit = new LineEdit
        {
            Text = text,
            PlaceholderText = placeholder,
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton();
        foreach (var label in labels)
        {
            option.AddItem(label);
        }
        StudioStyle.ApplyOptionButton(option);
        return option;
    }

    private static SpinBox DimensionBox()
    {
        var spin = new SpinBox
        {
            MinValue = AreaDimensions.MinimumSize,
            MaxValue = AreaDimensions.MaximumSize,
            Step = 4,
            Suffix = " unités",
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }
}
