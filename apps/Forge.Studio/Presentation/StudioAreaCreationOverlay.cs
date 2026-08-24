using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAreaCreationOverlay : Control
{
    private static readonly string[] StepTitles =
    [
        "1  Forme et usage",
        "2  Ambiance et budget",
        "3  Vérifier puis créer",
    ];

    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly ItemList _steps;
    private readonly Label _stepTitle;
    private readonly Label _stepHint;
    private readonly VBoxContainer[] _panels;
    private readonly LineEdit _name;
    private readonly OptionButton _kind;
    private readonly OptionButton _template;
    private readonly SpinBox _width;
    private readonly SpinBox _depth;
    private readonly VBoxContainer _spawnBlock;
    private readonly OptionButton _spawn;
    private readonly OptionButton _kit;
    private readonly OptionButton _terrain;
    private readonly OptionButton _lighting;
    private readonly OptionButton _navigation;
    private readonly OptionButton _streaming;
    private readonly OptionButton _biome;
    private readonly SpinBox _seed;
    private readonly SpinBox _budget;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _back;
    private readonly Button _next;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private StudioAreaCreationDraft? _draft;
    private IReadOnlyList<StudioAreaSpawnChoice> _spawnChoices = [];
    private IReadOnlyList<StudioAreaPresetChoice> _kitChoices = [];
    private IReadOnlyList<StudioAreaPresetChoice> _terrainChoices = [];
    private IReadOnlyList<StudioAreaPresetChoice> _lightingChoices = [];
    private IReadOnlyList<StudioAreaPresetChoice> _navigationChoices = [];
    private IReadOnlyList<StudioAreaPresetChoice> _streamingChoices = [];
    private IReadOnlyList<StudioAreaPresetChoice> _biomeChoices = [];
    private bool _updating;
    private int _step;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? AreaChanged;

    internal bool IsOpen => Visible;

    internal int StepCount => StepTitles.Length;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', _stepTitle.Text, _stepHint.Text, _summary.Text, _status.Text);
            return StepCount == 3 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("project.json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioAreaCreationOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioAreaCreationOverlay";
        Visible = false;
        ZIndex = 430;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .76f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("AreaCreationDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("NOUVELLE ZONE", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Créer un espace jouable sans quitter le monde", 20));
        heading.AddChild(StudioStyle.Label(
            "Trois étapes · aperçu sans écriture · document indépendant à l’enregistrement",
            10,
            StudioStyle.Muted));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer l’assistant sans appliquer de changement · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);

        var railPanel = StudioStyle.Panel("AreaCreationSteps", StudioStyle.Surface, 8);
        railPanel.CustomMinimumSize = new Vector2(250, 0);
        body.AddChild(railPanel);
        var railMargin = StudioStyle.Margin(10, 10, 10, 10);
        railPanel.AddChild(railMargin);
        var rail = new VBoxContainer();
        rail.AddThemeConstantOverride("separation", 8);
        railMargin.AddChild(rail);
        rail.AddChild(StudioStyle.SectionLabel("ASSISTANT DE ZONE"));
        _steps = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _steps.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        foreach (var title in StepTitles)
        {
            _steps.AddItem(title);
        }
        _steps.ItemSelected += index => ShowStep((int)index);
        rail.AddChild(_steps);
        var safety = StudioStyle.Panel("AreaCreationSafety", StudioStyle.Selection, 7);
        safety.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Créer reste annulable jusqu’à l’enregistrement explicite.",
            10,
            StudioStyle.Success,
            wrap: true));
        rail.AddChild(safety);

        var editorPanel = StudioStyle.Panel("AreaCreationEditor", StudioStyle.Surface, 8);
        editorPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        editorPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(editorPanel);
        var editorMargin = StudioStyle.Margin(16, 12, 16, 10);
        editorPanel.AddChild(editorMargin);
        var editor = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        editor.AddThemeConstantOverride("separation", 7);
        editorMargin.AddChild(editor);
        _stepTitle = StudioStyle.Label(string.Empty, 18);
        editor.AddChild(_stepTitle);
        _stepHint = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        editor.AddChild(_stepHint);
        editor.AddChild(Separator());
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        editor.AddChild(scroll);
        var panelHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(panelHost);
        _panels = new VBoxContainer[StepTitles.Length];
        for (var index = 0; index < _panels.Length; index++)
        {
            _panels[index] = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _panels[index].AddThemeConstantOverride("separation", 8);
            panelHost.AddChild(_panels[index]);
        }

        var identityGrid = Grid();
        _panels[0].AddChild(identityGrid);
        _name = AddField(identityGrid, "Nom visible", Edit("Nouvelle zone", "Nom affiché dans l’explorateur"));
        _kind = AddField(identityGrid, "Environnement", Option("Extérieure", "Intérieure"));
        _template = AddField(identityGrid, "Point de départ", Option("Zone vide", "Jouable automatiquement"));
        _width = AddField(identityGrid, "Largeur", DimensionBox());
        _depth = AddField(identityGrid, "Profondeur", DimensionBox());
        _spawnBlock = new VBoxContainer();
        _spawnBlock.AddThemeConstantOverride("separation", 4);
        _spawnBlock.AddChild(StudioStyle.Label("Personnage de départ", 10, StudioStyle.Muted));
        _spawn = new OptionButton();
        StudioStyle.ApplyOptionButton(_spawn);
        _spawnBlock.AddChild(_spawn);
        _panels[0].AddChild(_spawnBlock);
        _panels[0].AddChild(StudioStyle.Label(
            "Une zone jouable reçoit automatiquement un point de départ au centre. Une zone vide ne reçoit aucun objet.",
            10,
            StudioStyle.Muted,
            wrap: true));

        var environmentGrid = Grid();
        _panels[1].AddChild(environmentGrid);
        _kit = AddField(environmentGrid, "Kit de construction", new OptionButton());
        _terrain = AddField(environmentGrid, "Terrain initial", new OptionButton());
        _lighting = AddField(environmentGrid, "Éclairage", new OptionButton());
        _navigation = AddField(environmentGrid, "Déplacements", new OptionButton());
        _streaming = AddField(environmentGrid, "Chargement", new OptionButton());
        _biome = AddField(environmentGrid, "Ambiance", new OptionButton());
        foreach (var option in new[] { _kit, _terrain, _lighting, _navigation, _streaming, _biome })
        {
            StudioStyle.ApplyOptionButton(option);
        }
        _seed = AddField(environmentGrid, "Variation du décor", new SpinBox
        {
            MinValue = int.MinValue,
            MaxValue = int.MaxValue,
            Step = 1,
        });
        StudioStyle.ApplySpinBox(_seed);
        _budget = AddField(environmentGrid, "Budget mémoire", new SpinBox
        {
            MinValue = 32,
            MaxValue = 65_536,
            Step = 32,
            Suffix = " MiB",
        });
        StudioStyle.ApplySpinBox(_budget);
        _panels[1].AddChild(StudioStyle.Label(
            "Les profils proposés viennent des valeurs canoniques ou des zones déjà présentes dans ce projet.",
            10,
            StudioStyle.Success,
            wrap: true));

        var previewCard = StudioStyle.Panel("AreaCreationPreview", StudioStyle.SurfaceRaised, 8);
        previewCard.CustomMinimumSize = new Vector2(0, 150);
        _panels[2].AddChild(previewCard);
        var previewMargin = StudioStyle.Margin(14, 12, 14, 12);
        previewCard.AddChild(previewMargin);
        var previewColumn = new VBoxContainer();
        previewColumn.AddThemeConstantOverride("separation", 8);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.SectionLabel("IMPACT AVANT APPLICATION"));
        _summary = StudioStyle.Label(
            "Aucun aperçu calculé. Vérifiez les deux premières étapes puis choisissez Prévisualiser.",
            12,
            StudioStyle.Muted,
            wrap: true);
        previewColumn.AddChild(_summary);
        _panels[2].AddChild(StudioStyle.Label(
            "Après création, la zone apparaît dans l’explorateur et la vue 3D. Ctrl+S écrit son document indépendant ; Ctrl+Z restaure le projet précédent.",
            10,
            StudioStyle.Muted,
            wrap: true));

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _back = StudioStyle.Button("Précédent", compact: true);
        _back.Pressed += Previous;
        footer.AddChild(_back);
        _next = StudioStyle.Button("Suivant", compact: true);
        _next.Pressed += Next;
        footer.AddChild(_next);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Créer la zone", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler la création", compact: true);
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
        ShowStep(0);
    }

    internal void Open()
    {
        _draft = _session.CreateAreaCreationDraft();
        _spawnChoices = _session.AreaSpawnChoices;
        _kitChoices = _session.AreaKitChoices;
        _terrainChoices = _session.AreaTerrainChoices;
        _lightingChoices = _session.AreaLightingChoices;
        _navigationChoices = _session.AreaNavigationChoices;
        _streamingChoices = _session.AreaStreamingChoices;
        _biomeChoices = _session.AreaBiomeChoices;
        PopulateOptions();
        LoadDraft(_draft);
        _undo.Disabled = true;
        _redo.Disabled = true;
        _preview.Disabled = false;
        Visible = true;
        ShowStep(0);
        _name.GrabFocus();
        SetStatus("Assistant ouvert · prévisualisation obligatoire avant création", false);
    }

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }
        _session.CancelAreaCreation();
        _draft = null;
        Visible = false;
    }

    internal bool PrepareEvidence()
    {
        Open();
        _updating = true;
        _name.Text = "Bosquet des Brumes";
        _kind.Selected = 0;
        _template.Selected = _spawnChoices.Count > 0 ? 1 : 0;
        _width.Value = 96;
        _depth.Value = 72;
        SelectValue(_terrain, _terrainChoices, "heightmap");
        SelectValue(_lighting, _lightingChoices, "daylight");
        SelectValue(_biome, _biomeChoices, "temperate");
        _seed.Value = 731;
        _budget.Value = 768;
        _updating = false;
        UpdateSpawnVisibility();
        ShowStep(2);
        Preview();
        return Visible && !_apply.Disabled && ShowsHumanSurface;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        if (originalProject is null)
        {
            return false;
        }

        Open();
        _updating = true;
        _name.Text = "Preuve interface UX1-A";
        _template.Selected = 0;
        _width.Value = 28;
        _depth.Value = 20;
        _updating = false;
        UpdateSpawnVisibility();
        ShowStep(2);
        Preview();
        var previewReady = !_apply.Disabled;
        Apply();
        var applied = _session.Project?.Areas.Count == originalProject.Areas.Count + 1 && !_undo.Disabled;
        Undo();
        var undone = ReferenceEquals(originalProject, _session.Project) && !_redo.Disabled;
        Redo();
        var redone = _session.Project?.Areas.Count == originalProject.Areas.Count + 1 && !_undo.Disabled;
        Undo();
        var restored = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        if (originalAreaId is not null)
        {
            _session.ActivateAreaTab(originalAreaId, persist: false);
        }
        Close();
        return previewReady && applied && undone && redone && restored && ShowsHumanSurface;
    }

    private void PopulateOptions()
    {
        Populate(_spawn, _spawnChoices.Select(item => item.Label));
        Populate(_kit, _kitChoices.Select(item => item.Label));
        Populate(_terrain, _terrainChoices.Select(item => item.Label));
        Populate(_lighting, _lightingChoices.Select(item => item.Label));
        Populate(_navigation, _navigationChoices.Select(item => item.Label));
        Populate(_streaming, _streamingChoices.Select(item => item.Label));
        Populate(_biome, _biomeChoices.Select(item => item.Label));
        _template.SetItemDisabled(1, _spawnChoices.Count == 0);
        _template.SetItemTooltip(1, _spawnChoices.Count == 0
            ? "Ajoutez d’abord un blueprint de point de départ du joueur."
            : "Ajoute automatiquement le point de départ sélectionné.");
    }

    private void LoadDraft(StudioAreaCreationDraft draft)
    {
        _updating = true;
        _name.Text = draft.DisplayName;
        _kind.Selected = draft.Kind == AreaKind.Exterior ? 0 : 1;
        _template.Selected = draft.Template == AreaTemplateKind.Playable && _spawnChoices.Count > 0 ? 1 : 0;
        _width.Value = draft.Width;
        _depth.Value = draft.Depth;
        _spawn.Selected = Math.Max(0, _spawnChoices.ToList().FindIndex(item => item.Id == draft.SpawnBlueprintId));
        SelectValue(_kit, _kitChoices, draft.KitKey);
        SelectValue(_terrain, _terrainChoices, draft.TerrainProfile);
        SelectValue(_lighting, _lightingChoices, draft.LightingProfile);
        SelectValue(_navigation, _navigationChoices, draft.NavigationProfile);
        SelectValue(_streaming, _streamingChoices, draft.StreamingProfile);
        SelectValue(_biome, _biomeChoices, draft.Biome);
        _seed.Value = draft.Seed;
        _budget.Value = draft.RuntimeBudgetMegabytes;
        _updating = false;
        UpdateSpawnVisibility();
        InvalidatePreview();
    }

    private StudioAreaCreationDraft ReadDraft()
    {
        var draft = _draft ?? throw new InvalidOperationException("The area wizard is not open.");
        return draft with
        {
            DisplayName = _name.Text,
            Kind = _kind.Selected == 0 ? AreaKind.Exterior : AreaKind.Interior,
            Width = (int)_width.Value,
            Depth = (int)_depth.Value,
            Template = _template.Selected == 1 ? AreaTemplateKind.Playable : AreaTemplateKind.Empty,
            SpawnBlueprintId = _template.Selected == 1 && _spawn.Selected >= 0 && _spawn.Selected < _spawnChoices.Count
                ? _spawnChoices[_spawn.Selected].Id
                : null,
            KitKey = SelectedValue(_kit, _kitChoices),
            TerrainProfile = SelectedValue(_terrain, _terrainChoices),
            LightingProfile = SelectedValue(_lighting, _lightingChoices),
            NavigationProfile = SelectedValue(_navigation, _navigationChoices),
            StreamingProfile = SelectedValue(_streaming, _streamingChoices),
            Biome = SelectedValue(_biome, _biomeChoices),
            Seed = (int)_seed.Value,
            RuntimeBudgetMegabytes = (int)_budget.Value,
        };
    }

    private void Preview()
    {
        if (_draft is null)
        {
            return;
        }
        var result = _session.PreviewAreaCreation(ReadDraft());
        _summary.Text = string.IsNullOrWhiteSpace(result.Summary)
            ? "Aucun impact validé. Corrigez les champs puis relancez l’aperçu."
            : result.Summary;
        _summary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _apply.Disabled = !result.CanApply;
        SetStatus(result.Message, !result.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedAreaCreation();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }
        _apply.Disabled = true;
        _preview.Disabled = true;
        _back.Disabled = true;
        _next.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        AreaChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Succeeded ? "Création annulée · le projet précédent est restauré." : result.Message,
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
        SetStatus(result.Succeeded ? "Zone rétablie · enregistrez pour écrire son document." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            AreaChanged?.Invoke();
        }
    }

    private void Previous()
    {
        if (_step > 0)
        {
            ShowStep(_step - 1);
        }
    }

    private void Next()
    {
        if (_step < StepTitles.Length - 1)
        {
            ShowStep(_step + 1);
        }
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, StepTitles.Length - 1);
        for (var index = 0; index < _panels.Length; index++)
        {
            _panels[index].Visible = index == _step;
        }
        _steps.Select(_step);
        _stepTitle.Text = StepTitles[_step];
        _stepHint.Text = _step switch
        {
            0 => "Nommez la zone, choisissez intérieur ou extérieur, ses dimensions et son point de départ.",
            1 => "Choisissez des profils lisibles ; leurs identités internes restent gérées par le Studio.",
            _ => "Prévisualisez le document complet. Créer ne sauvegarde pas silencieusement le module.",
        };
        _back.Disabled = _step == 0;
        _next.Disabled = _step == StepTitles.Length - 1;
        _preview.Visible = _step == StepTitles.Length - 1;
        _apply.Visible = _step == StepTitles.Length - 1;
    }

    private void ConnectInvalidation()
    {
        _name.TextChanged += _ => InvalidatePreview();
        _kind.ItemSelected += _ =>
        {
            if (!_updating)
            {
                var defaults = AreaCreationSettings.CreateDefault(_kind.Selected == 0
                    ? AreaKind.Exterior
                    : AreaKind.Interior);
                _updating = true;
                SelectValue(_terrain, _terrainChoices, defaults.TerrainProfile);
                SelectValue(_lighting, _lightingChoices, defaults.LightingProfile);
                SelectValue(_biome, _biomeChoices, defaults.Biome);
                _updating = false;
            }
            InvalidatePreview();
        };
        _template.ItemSelected += _ =>
        {
            UpdateSpawnVisibility();
            InvalidatePreview();
        };
        _spawn.ItemSelected += _ => InvalidatePreview();
        foreach (var option in new[] { _kit, _terrain, _lighting, _navigation, _streaming, _biome })
        {
            option.ItemSelected += _ => InvalidatePreview();
        }
        _width.ValueChanged += _ => InvalidatePreview();
        _depth.ValueChanged += _ => InvalidatePreview();
        _seed.ValueChanged += _ => InvalidatePreview();
        _budget.ValueChanged += _ => InvalidatePreview();
    }

    private void UpdateSpawnVisibility()
    {
        _spawnBlock.Visible = _template.Selected == 1;
    }

    private void InvalidatePreview()
    {
        if (_updating)
        {
            return;
        }
        _session.CancelAreaCreation();
        _apply.Disabled = true;
        _preview.Disabled = false;
        _summary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
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
        var height = Math.Clamp((Size.Y - 72) / scale, 620, 780);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(170, 0);
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
            Value = 64,
            Suffix = " unités",
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }

    private static HSeparator Separator()
    {
        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator", StudioStyle.Border);
        return separator;
    }

    private static void Populate(OptionButton option, IEnumerable<string> labels)
    {
        option.Clear();
        foreach (var label in labels)
        {
            option.AddItem(label);
        }
    }

    private static void SelectValue(
        OptionButton option,
        IReadOnlyList<StudioAreaPresetChoice> choices,
        string value)
    {
        var index = choices.ToList().FindIndex(item => string.Equals(item.Value, value, StringComparison.Ordinal));
        option.Selected = index < 0 ? 0 : index;
    }

    private static string SelectedValue(
        OptionButton option,
        IReadOnlyList<StudioAreaPresetChoice> choices) =>
        option.Selected >= 0 && option.Selected < choices.Count ? choices[option.Selected].Value : string.Empty;
}
