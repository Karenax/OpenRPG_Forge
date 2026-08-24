using Forge.Application.Blueprints;
using Forge.Application.Creator;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAddEntityInspector : VBoxContainer
{
    private readonly StudioSessionController _session;
    private readonly Vector3Value _position;
    private readonly OptionButton _view;
    private readonly LineEdit _search;
    private OptionButton _kind = null!;
    private LineEdit _kit = null!;
    private LineEdit _style = null!;
    private LineEdit _license = null!;
    private LineEdit _compatibility = null!;
    private LineEdit _tags = null!;
    private CheckButton _favoritesOnly = null!;
    private readonly VBoxContainer _results;
    private readonly Label _resultSummary;
    private readonly Label _selectionSummary;
    private readonly Label _previewSummary;
    private readonly Button _placeButton;
    private OptionButton _distribution = null!;
    private OptionButton _alignment = null!;
    private SpinBox _count = null!;
    private SpinBox _spacing = null!;
    private SpinBox _columns = null!;
    private SpinBox _seed = null!;
    private SpinBox _surfaceOffset = null!;
    private SpinBox _budgetMb = null!;
    private LineEdit _requiredSocket = null!;
    private CheckButton _brush = null!;
    private CheckButton _continuous = null!;
    private CheckButton _autoCorrect = null!;
    private LineEdit _paletteName = null!;
    private CheckButton _taskScoped = null!;
    private Button _removePalette = null!;
    private readonly Dictionary<BlueprintId, Button> _buttons = [];
    private readonly IReadOnlyList<BlueprintKind> _kindValues = Enum.GetValues<BlueprintKind>();
    private readonly List<PaletteChoice> _paletteChoices = [];
    private IReadOnlyList<BlueprintPaletteItem> _visibleItems = [];
    private BlueprintId? _selectedBlueprintId;

    internal int PaletteViewCount => _paletteChoices.Count;

    internal int VisibleResultCount => _visibleItems.Count;

    internal int FilterDimensionCount => 6;

    internal bool PlacementControlsReady => _distribution.ItemCount == 3 && _alignment.ItemCount == 3;

    internal event Action<StudioRepeatedPlacementRequest>? PlaceRequested;

    internal event Action<StudioRepeatedPlacementRequest>? PlacementToolRequested;

    internal event Action<StudioPlacementPreview>? PreviewChanged;

    internal event Action<Vector3Value, SemanticPasteMode>? PasteRequested;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? Cancelled;

    internal StudioAddEntityInspector(StudioSessionController session, Vector3Value position)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _position = position;
        AddThemeConstantOverride("separation", 7);

        AddChild(StudioStyle.SectionLabel("PALETTE CONTEXTUELLE"));
        AddChild(StudioStyle.Label(
            $"Point choisi · X {position.X:0.##} · Y {position.Y:0.##} · Z {position.Z:0.##}",
            11,
            StudioStyle.Muted));

        BuildClipboardActions();

        _view = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_view);
        _view.ItemSelected += _ => RebuildResults();
        AddChild(_view);

        _search = new LineEdit
        {
            PlaceholderText = "Rechercher par nom, type, kit, style ou tag…",
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(_search);
        _search.TextChanged += _ => RebuildResults();
        AddChild(_search);

        var preciseFilters = new CheckButton
        {
            Text = "Filtres précis",
            TooltipText = "Type, kit, style, licence, compatibilité et tags",
        };
        AddChild(preciseFilters);
        var filterPanel = BuildPreciseFilters();
        filterPanel.Visible = false;
        preciseFilters.Toggled += enabled => filterPanel.Visible = enabled;
        AddChild(filterPanel);

        _resultSummary = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        AddChild(_resultSummary);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 245),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);
        _results = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _results.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_results);

        _selectionSummary = StudioStyle.Label("Choisissez un élément dans la liste.", 11, StudioStyle.Muted, wrap: true);
        AddChild(_selectionSummary);

        AddChild(BuildNamedPaletteControls());
        AddChild(BuildPlacementControls());

        _previewSummary = StudioStyle.Label("Choisissez un élément pour préparer l’aperçu.", 10, StudioStyle.Muted, wrap: true);
        AddChild(_previewSummary);
        _placeButton = StudioStyle.Button("Placer ici", primary: true);
        _placeButton.Disabled = true;
        _placeButton.Pressed += RequestPlacement;
        AddChild(_placeButton);

        var savePlacement = StudioStyle.Button("Mémoriser ces réglages de placement", compact: true);
        savePlacement.Pressed += SavePlacementProfile;
        AddChild(savePlacement);
        var cancel = StudioStyle.Button("Fermer la palette", compact: true);
        cancel.Pressed += () => Cancelled?.Invoke();
        AddChild(cancel);

        LoadCurrentProfileValues();
        PopulatePaletteChoices();
        RebuildResults();
    }

    internal void PublishPreview() => RefreshPreview();

    internal void ConfigureEvidencePlacement(StudioRepeatedPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _selectedBlueprintId = request.BlueprintId;
        _count.SetValueNoSignal(request.Count);
        _spacing.SetValueNoSignal(request.Profile.Spacing);
        _columns.SetValueNoSignal(request.Profile.Columns);
        _seed.SetValueNoSignal(request.Profile.Seed);
        _surfaceOffset.SetValueNoSignal(request.Profile.SurfaceOffset);
        _distribution.Select(request.Profile.Distribution switch
        {
            EditorPlacementDistribution.Line => 0,
            EditorPlacementDistribution.Grid => 1,
            _ => 2,
        });
        _alignment.Select(request.Profile.Alignment switch
        {
            EditorPlacementAlignment.SurfaceNormal => 1,
            EditorPlacementAlignment.Socket => 2,
            _ => 0,
        });
        _brush.SetPressedNoSignal(request.Profile.BrushMode);
        _continuous.Disabled = !request.Profile.BrushMode;
        _continuous.SetPressedNoSignal(request.Profile.ContinuousStroke);
        _autoCorrect.SetPressedNoSignal(request.ApplyCorrections);
        _requiredSocket.Text = request.RequiredSocket ?? string.Empty;
        _budgetMb.SetValueNoSignal(request.AvailableBudgetBytes is null
            ? 0
            : request.AvailableBudgetBytes.Value / 1024d / 1024d);
        RefreshSelection();
    }

    private void BuildClipboardActions()
    {
        var state = _session.ClipboardState;
        if (!state.HasContent)
        {
            return;
        }

        var panel = StudioStyle.Panel("ClipboardPlacement", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(7, 6, 7, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label(state.Description, 10, StudioStyle.Text, wrap: true));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        var instances = StudioStyle.Button("Coller ici · instances", primary: true, compact: true);
        instances.TooltipText = "Conserve les blueprints partagés et remappe les références internes";
        instances.Pressed += () => PasteRequested?.Invoke(_position, SemanticPasteMode.Instance);
        row.AddChild(instances);
        var independent = StudioStyle.Button("Copies indépendantes", compact: true);
        independent.TooltipText = "Crée aussi des blueprints de projet indépendants";
        independent.Pressed += () => PasteRequested?.Invoke(_position, SemanticPasteMode.IndependentCopy);
        row.AddChild(independent);
        column.AddChild(row);
        AddChild(panel);
    }

    private Control BuildPreciseFilters()
    {
        var panel = StudioStyle.Panel("PalettePreciseFilters", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(7, 6, 7, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);

        _kind = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _kind.AddItem("Tous les types");
        foreach (var kind in _kindValues)
        {
            _kind.AddItem(KindLabel(kind));
        }
        StudioStyle.ApplyOptionButton(_kind);
        _kind.ItemSelected += _ => RebuildResults();
        column.AddChild(Labeled("TYPE", _kind));

        _kit = FilterEdit("Kit", "ex. forêt, donjon…");
        _style = FilterEdit("Style", "ex. pierre, sombre…");
        _license = FilterEdit("Licence", "ex. CC0, MIT…");
        _compatibility = FilterEdit("Compatibilité", "ex. extérieur…");
        _tags = FilterEdit("Tags", "séparés par des virgules");
        column.AddChild(Labeled("KIT", _kit));
        column.AddChild(Labeled("STYLE", _style));
        column.AddChild(Labeled("LICENCE", _license));
        column.AddChild(Labeled("COMPATIBILITÉ", _compatibility));
        column.AddChild(Labeled("TAGS", _tags));
        _favoritesOnly = new CheckButton { Text = "Favoris seulement" };
        _favoritesOnly.Toggled += _ => RebuildResults();
        column.AddChild(_favoritesOnly);
        var remember = StudioStyle.Button("Mémoriser ces filtres", compact: true);
        remember.Pressed += SavePaletteFilter;
        column.AddChild(remember);
        return panel;
    }

    private Control BuildNamedPaletteControls()
    {
        var panel = StudioStyle.Panel("NamedPaletteControls", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(7, 6, 7, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);
        column.AddChild(StudioStyle.SectionLabel("PALETTE CUSTOM"));
        column.AddChild(StudioStyle.Label(
            "Enregistrez les résultats visibles comme palette humaine réutilisable.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _paletteName = new LineEdit { PlaceholderText = "Nom de la palette" };
        StudioStyle.ApplyLineEdit(_paletteName);
        column.AddChild(_paletteName);
        _taskScoped = new CheckButton { Text = "Temporaire pour cette tâche" };
        column.AddChild(_taskScoped);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        var save = StudioStyle.Button("Enregistrer les résultats", compact: true);
        save.Pressed += SaveNamedPalette;
        row.AddChild(save);
        _removePalette = StudioStyle.Button("Supprimer la palette", compact: true);
        _removePalette.Visible = false;
        _removePalette.Pressed += RemoveSelectedNamedPalette;
        row.AddChild(_removePalette);
        column.AddChild(row);
        return panel;
    }

    private Control BuildPlacementControls()
    {
        var panel = StudioStyle.Panel("RepeatedPlacementControls", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(7, 6, 7, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);
        column.AddChild(StudioStyle.SectionLabel("PLACEMENT ET APERÇU"));

        _count = Spin(1, 64, 1, 1);
        _spacing = Spin(.25, 16, .25, 2);
        _columns = Spin(1, 16, 1, 4);
        _seed = Spin(0, 99_999_999, 1, 2026);
        _surfaceOffset = Spin(-4, 4, .05, 0);
        _budgetMb = Spin(0, 4096, 1, 0);
        _budgetMb.Suffix = " Mo";
        column.AddChild(TwoColumns("QUANTITÉ", _count, "ESPACEMENT", _spacing));

        _distribution = new OptionButton();
        _distribution.AddItem("Ligne");
        _distribution.AddItem("Grille");
        _distribution.AddItem("Dispersion déterministe");
        StudioStyle.ApplyOptionButton(_distribution);
        _distribution.Selected = 1;
        _distribution.ItemSelected += _ => RefreshPreview();
        _alignment = new OptionButton();
        _alignment.AddItem("Plan de grille");
        _alignment.AddItem("Surface");
        _alignment.AddItem("Raccord");
        StudioStyle.ApplyOptionButton(_alignment);
        _alignment.ItemSelected += _ => RefreshPreview();
        column.AddChild(Labeled("DISTRIBUTION", _distribution));
        column.AddChild(Labeled("ALIGNEMENT", _alignment));
        column.AddChild(TwoColumns("COLONNES", _columns, "GRAINE", _seed));
        column.AddChild(TwoColumns("DÉCALAGE SURFACE", _surfaceOffset, "BUDGET RESTANT", _budgetMb));

        _requiredSocket = new LineEdit
        {
            PlaceholderText = _session.AvailablePlacementSockets.Count == 0
                ? "Raccord requis (facultatif)"
                : "Raccord requis · disponibles : " + string.Join(", ", _session.AvailablePlacementSockets),
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(_requiredSocket);
        _requiredSocket.TextChanged += _ => RefreshPreview();
        column.AddChild(Labeled("RACCORD", _requiredSocket));

        _brush = new CheckButton { Text = "Pinceau dans la vue 3D" };
        _continuous = new CheckButton { Text = "Continuer après chaque tracé", Disabled = true };
        _autoCorrect = new CheckButton { Text = "Proposer et appliquer les corrections libres", ButtonPressed = true };
        _brush.Toggled += enabled =>
        {
            _continuous.Disabled = !enabled;
            if (!enabled)
            {
                _continuous.ButtonPressed = false;
            }
            RefreshPreview();
        };
        _continuous.Toggled += _ => RefreshPreview();
        _autoCorrect.Toggled += _ => RefreshPreview();
        column.AddChild(_brush);
        column.AddChild(_continuous);
        column.AddChild(_autoCorrect);
        return panel;
    }

    private void PopulatePaletteChoices()
    {
        _paletteChoices.Clear();
        _paletteChoices.AddRange(
        [
            new("Tout · Standard + Custom", BlueprintPaletteView.All, null),
            new("Standard · packs installés", BlueprintPaletteView.InstalledPacks, null),
            new("Custom · contenu du projet", BlueprintPaletteView.ProjectContent, null),
            new("Générés", BlueprintPaletteView.Generated, null),
            new("Favoris", BlueprintPaletteView.Favorites, null),
            new("Récents", BlueprintPaletteView.Recent, null),
            new("Temporaires", BlueprintPaletteView.Temporary, null),
        ]);
        _paletteChoices.AddRange(_session.WorkspaceProfile.NamedPaletteCollections.Select(collection =>
            new PaletteChoice(
                (collection.TaskScoped ? "Tâche · " : "Palette · ") + collection.Name,
                BlueprintPaletteView.All,
                collection.Id)));
        _view.Clear();
        foreach (var choice in _paletteChoices)
        {
            _view.AddItem(choice.Label);
        }
        if (_view.ItemCount > 0)
        {
            _view.Select(0);
        }
    }

    private void LoadCurrentProfileValues()
    {
        var filter = _session.WorkspaceProfile.Shell.PaletteFilter;
        if (filter.Kinds.Count == 1)
        {
            var kindIndex = Array.IndexOf(_kindValues.ToArray(), filter.Kinds[0]);
            _kind.Select(kindIndex < 0 ? 0 : kindIndex + 1);
        }
        else
        {
            _kind.Select(0);
        }
        _kit.Text = string.Join(", ", filter.Kits);
        _style.Text = string.Join(", ", filter.Styles);
        _license.Text = string.Join(", ", filter.Licenses);
        _compatibility.Text = string.Join(", ", filter.Compatibility);
        _tags.Text = string.Join(", ", filter.Tags);
        _favoritesOnly.SetPressedNoSignal(filter.FavoritesOnly);

        var placement = _session.WorkspaceProfile.RepeatedPlacementProfile;
        _spacing.SetValueNoSignal(placement.Spacing);
        _columns.SetValueNoSignal(placement.Columns);
        _seed.SetValueNoSignal(placement.Seed);
        _surfaceOffset.SetValueNoSignal(placement.SurfaceOffset);
        _distribution.Select(placement.Distribution switch
        {
            EditorPlacementDistribution.Line => 0,
            EditorPlacementDistribution.Grid => 1,
            _ => 2,
        });
        _alignment.Select(placement.Alignment switch
        {
            EditorPlacementAlignment.SurfaceNormal => 1,
            EditorPlacementAlignment.Socket => 2,
            _ => 0,
        });
        _brush.SetPressedNoSignal(placement.BrushMode);
        _continuous.Disabled = !placement.BrushMode;
        _continuous.SetPressedNoSignal(placement.ContinuousStroke);
    }

    private void RebuildResults()
    {
        ClearChildren(_results);
        _buttons.Clear();
        var choice = CurrentPaletteChoice;
        _visibleItems = _session.BuildBlueprintPalette(
            choice.View,
            _search.Text,
            BuildFilter(),
            choice.NamedCollectionId);
        _resultSummary.Text = _visibleItems.Count == 0
            ? "Aucun élément ne correspond aux filtres."
            : $"{_visibleItems.Count} élément(s) · type, kit, style, licence, compatibilité et tags vérifiés";

        foreach (var item in _visibleItems)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 3);
            var badge = item.Origin == BlueprintPaletteOrigin.InstalledPack ? "Standard" : "Custom";
            var access = item.IsReadOnly ? "lecture seule" : "modifiable";
            var button = StudioStyle.Button(
                $"{item.DisplayName}\n{KindLabel(item.Kind)} · {badge} · {access}",
                compact: true);
            button.Alignment = HorizontalAlignment.Left;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.TooltipText = HumanTooltip(item);
            button.Pressed += () => SelectBlueprint(item.BlueprintId);
            _buttons[item.BlueprintId] = button;
            row.AddChild(button);
            var favorite = StudioStyle.Button(item.IsFavorite ? "★" : "☆", compact: true);
            favorite.TooltipText = item.IsFavorite ? "Retirer des favoris" : "Ajouter aux favoris";
            favorite.Pressed += () => ToggleFavorite(item.BlueprintId);
            row.AddChild(favorite);
            _results.AddChild(row);
        }

        if (_visibleItems.Count == 0)
        {
            _selectedBlueprintId = null;
            _selectionSummary.Text = "Élargissez les filtres ou choisissez une autre palette.";
            _placeButton.Disabled = true;
            _removePalette.Visible = choice.NamedCollectionId is not null;
            RefreshPreview();
            return;
        }

        if (_selectedBlueprintId is null || _visibleItems.All(item => item.BlueprintId != _selectedBlueprintId))
        {
            _selectedBlueprintId = _visibleItems[0].BlueprintId;
        }
        _removePalette.Visible = choice.NamedCollectionId is not null;
        if (choice.NamedCollectionId is not null)
        {
            _paletteName.Text = choice.Label[(choice.Label.IndexOf('·') + 1)..].Trim();
        }
        RefreshSelection();
    }

    private void SelectBlueprint(BlueprintId blueprintId)
    {
        _selectedBlueprintId = blueprintId;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        var selected = _visibleItems.FirstOrDefault(item => item.BlueprintId == _selectedBlueprintId);
        _selectionSummary.Text = selected is null
            ? "Choisissez un élément dans la liste."
            : $"Sélection · {selected.DisplayName}\n{KindLabel(selected.Kind)} · " +
              $"{OriginLabel(selected.Origin)} · licence {selected.Provenance.LicenseSpdx}";
        foreach (var pair in _buttons)
        {
            var active = pair.Key == _selectedBlueprintId;
            pair.Value.AddThemeStyleboxOverride(
                "normal",
                StudioStyle.Box(
                    active ? StudioStyle.Selection : StudioStyle.SurfaceRaised,
                    active ? StudioStyle.Success : StudioStyle.Border,
                    5,
                    active ? 2 : 1));
        }
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_selectedBlueprintId is null)
        {
            _previewSummary.Text = "Choisissez un élément pour préparer l’aperçu.";
            _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
            _placeButton.Disabled = true;
            PreviewChanged?.Invoke(new StudioPlacementPreview(false, _previewSummary.Text, [], 0));
            return;
        }

        StudioRepeatedPlacementRequest request;
        try
        {
            request = BuildPlacementRequest();
        }
        catch (ArgumentException exception)
        {
            _previewSummary.Text = "Réglage refusé · " + exception.Message;
            _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            _placeButton.Disabled = true;
            return;
        }
        var preview = _session.PreviewRepeatedPlacement(request);
        _previewSummary.Text = preview.Message +
            (preview.CorrectedCount == 0 ? string.Empty : $" · {preview.CorrectedCount} correction(s)");
        _previewSummary.AddThemeColorOverride(
            "font_color",
            preview.IsValid ? StudioStyle.Success : StudioStyle.Danger);
        _placeButton.Disabled = !preview.IsValid;
        _placeButton.Text = request.Profile.BrushMode
            ? "Activer le pinceau dans la vue 3D"
            : request.Count == 1 ? "Placer ici" : $"Placer {request.Count} objets ici";
        PreviewChanged?.Invoke(preview);
    }

    private void RequestPlacement()
    {
        if (_selectedBlueprintId is null)
        {
            return;
        }
        var request = BuildPlacementRequest();
        if (request.Profile.BrushMode)
        {
            PlacementToolRequested?.Invoke(request);
        }
        else
        {
            PlaceRequested?.Invoke(request);
        }
    }

    private StudioRepeatedPlacementRequest BuildPlacementRequest()
    {
        var blueprintId = _selectedBlueprintId ?? throw new InvalidOperationException("No blueprint selected.");
        var profile = BuildRepeatedPlacementProfile();
        var budget = _budgetMb.Value <= 0
            ? null
            : (long?)Math.Round(_budgetMb.Value * 1024 * 1024);
        var blueprint = _session.Project!.Blueprints.Single(candidate => candidate.Id == blueprintId);
        return new StudioRepeatedPlacementRequest(
            blueprintId,
            _position,
            profile,
            (int)_count.Value,
            _autoCorrect.ButtonPressed,
            string.IsNullOrWhiteSpace(_requiredSocket.Text) ? null : _requiredSocket.Text.Trim(),
            _session.AvailablePlacementSockets,
            budget,
            budget is null ? null : blueprint.Authoring.PlatformByteBudget);
    }

    private EditorRepeatedPlacementProfile BuildRepeatedPlacementProfile()
    {
        var current = _session.WorkspaceProfile.RepeatedPlacementProfile;
        return new EditorRepeatedPlacementProfile(
            current.Id,
            EditorRepeatedPlacementProfile.CurrentSchemaVersion,
            "studio:ux0-d-palette",
            enabled: true,
            brushMode: _brush.ButtonPressed,
            continuousStroke: _brush.ButtonPressed && _continuous.ButtonPressed,
            distribution: _distribution.Selected switch
            {
                0 => EditorPlacementDistribution.Line,
                1 => EditorPlacementDistribution.Grid,
                _ => EditorPlacementDistribution.DeterministicScatter,
            },
            alignment: _alignment.Selected switch
            {
                1 => EditorPlacementAlignment.SurfaceNormal,
                2 => EditorPlacementAlignment.Socket,
                _ => EditorPlacementAlignment.GridPlane,
            },
            spacing: (float)_spacing.Value,
            columns: (int)_columns.Value,
            seed: (int)_seed.Value,
            surfaceOffset: (float)_surfaceOffset.Value);
    }

    private EditorPaletteFilter BuildFilter()
    {
        var kinds = _kind.Selected <= 0
            ? _kindValues
            : [_kindValues[_kind.Selected - 1]];
        return new EditorPaletteFilter(
            kinds,
            ParseTokens(_kit.Text),
            ParseTokens(_style.Text),
            ParseTokens(_license.Text),
            ParseTokens(_compatibility.Text),
            ParseTokens(_tags.Text),
            _favoritesOnly.ButtonPressed);
    }

    private void ToggleFavorite(BlueprintId blueprintId)
    {
        var result = _session.ToggleBlueprintFavorite(blueprintId);
        StatusChanged?.Invoke(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RebuildResults();
        }
    }

    private void SavePaletteFilter()
    {
        var result = _session.ConfigurePaletteFilter(BuildFilter());
        StatusChanged?.Invoke(result.Succeeded
            ? "Filtres de palette mémorisés dans le profil Studio"
            : result.Message, !result.Succeeded);
    }

    private void SaveNamedPalette()
    {
        var result = _session.SaveNamedPalette(
            _paletteName.Text,
            _visibleItems.Select(item => item.BlueprintId).ToArray(),
            _taskScoped.ButtonPressed);
        StatusChanged?.Invoke(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            PopulatePaletteChoices();
            var index = _paletteChoices.FindIndex(choice => choice.NamedCollectionId is not null &&
                string.Equals(choice.Label[(choice.Label.IndexOf('·') + 1)..].Trim(),
                    _paletteName.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _view.Select(index);
            }
            RebuildResults();
        }
    }

    private void RemoveSelectedNamedPalette()
    {
        var choice = CurrentPaletteChoice;
        if (choice.NamedCollectionId is null)
        {
            return;
        }
        var result = _session.RemoveNamedPalette(choice.NamedCollectionId.Value);
        StatusChanged?.Invoke(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            PopulatePaletteChoices();
            RebuildResults();
        }
    }

    private void SavePlacementProfile()
    {
        var result = _session.ConfigureRepeatedPlacement(BuildRepeatedPlacementProfile());
        StatusChanged?.Invoke(result.Succeeded
            ? "Réglages de placement mémorisés dans le profil Studio"
            : result.Message, !result.Succeeded);
    }

    private PaletteChoice CurrentPaletteChoice => _paletteChoices.Count == 0
        ? new PaletteChoice("Tout", BlueprintPaletteView.All, null)
        : _paletteChoices[Math.Clamp(_view.Selected, 0, _paletteChoices.Count - 1)];

    private LineEdit FilterEdit(string name, string placeholder)
    {
        var edit = new LineEdit { Name = name, PlaceholderText = placeholder, ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(edit);
        edit.TextChanged += _ => RebuildResults();
        return edit;
    }

    private SpinBox Spin(double min, double max, double step, double value)
    {
        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value };
        StudioStyle.ApplySpinBox(spin);
        spin.ValueChanged += _ => RefreshPreview();
        return spin;
    }

    private static Control Labeled(string label, Control control)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(StudioStyle.SectionLabel(label));
        column.AddChild(control);
        return column;
    }

    private static Control TwoColumns(string leftLabel, Control left, string rightLabel, Control right)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        var leftColumn = (Control)Labeled(leftLabel, left);
        leftColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var rightColumn = (Control)Labeled(rightLabel, right);
        rightColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(leftColumn);
        row.AddChild(rightColumn);
        return row;
    }

    private static IReadOnlyList<string> ParseTokens(string text) => text
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(token => token.Length > 0)
        .ToArray();

    private static string HumanTooltip(BlueprintPaletteItem item)
    {
        var details = new List<string>
        {
            $"Kit : {item.Kit}",
            $"Licence : {item.Provenance.LicenseSpdx}",
        };
        if (item.Styles.Count > 0) details.Add("Styles : " + string.Join(", ", item.Styles));
        if (item.Compatibility.Count > 0) details.Add("Compatibilité : " + string.Join(", ", item.Compatibility));
        if (item.Tags.Count > 0) details.Add("Tags : " + string.Join(", ", item.Tags));
        return string.Join("\n", details);
    }

    private static string OriginLabel(BlueprintPaletteOrigin origin) => origin switch
    {
        BlueprintPaletteOrigin.InstalledPack => "Standard · pack installé",
        BlueprintPaletteOrigin.ProjectContent => "Custom · projet",
        BlueprintPaletteOrigin.Generated => "Généré",
        BlueprintPaletteOrigin.Temporary => "Temporaire",
        _ => "Projet",
    };

    private static string KindLabel(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Creature => "Créature",
        BlueprintKind.PlayerSpawn => "Départ joueur",
        BlueprintKind.Item => "Objet",
        BlueprintKind.Trigger => "Déclencheur",
        BlueprintKind.Door => "Porte",
        BlueprintKind.Placeable => "Plaçable",
        _ => kind.ToString(),
    };

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private sealed record PaletteChoice(
        string Label,
        BlueprintPaletteView View,
        Guid? NamedCollectionId);
}
