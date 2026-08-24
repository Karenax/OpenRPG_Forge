using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioDisplayFilterOverlay : Control
{
    private readonly Dictionary<BlueprintKind, CheckBox> _kindChecks = [];
    private readonly List<Guid> _layerIds = [];
    private readonly List<Guid> _profileIds = [];
    private readonly PanelContainer _panel;
    private readonly OptionButton _profiles;
    private readonly LineEdit _tags;
    private readonly LineEdit _author;
    private readonly LineEdit _state;
    private readonly CheckBox _isolate;
    private readonly CheckBox _ghost;
    private readonly ItemList _layers;
    private readonly ItemList _volumes;
    private readonly LineEdit _presetName;
    private readonly Label _status;
    private EditorDisplayFilter _source = EditorDisplayFilter.ShowAll;
    private bool _loading;

    internal event Action<EditorDisplayFilter>? ApplyRequested;

    internal event Action<EditorDisplayFilter>? ResetRequested;

    internal event Action<string, EditorDisplayFilter>? SavePresetRequested;

    internal event Action<Guid>? ProfileSelected;

    internal event Action? CloseRequested;

    internal bool IsOpen => Visible;

    internal StudioDisplayFilterOverlay()
    {
        Name = "StudioDisplayFilterOverlay";
        Visible = false;
        ZIndex = 450;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var backdrop = new ColorRect
        {
            Color = new Color(0, 0, 0, .42f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true })
            {
                Close();
            }
        };
        AddChild(backdrop);

        _panel = StudioStyle.Panel("DisplayFilterPanel", StudioStyle.Chrome, 9, 2);
        _panel.ZIndex = 1;
        AddChild(_panel);
        var margin = StudioStyle.Margin(15, 13, 15, 13);
        _panel.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer();
        heading.AddChild(StudioStyle.Label("FILTRES D’AFFICHAGE", 12, StudioStyle.Accent));
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);
        root.AddChild(StudioStyle.Label(
            "Combinez les facettes. Le viewport reste la source de vérité visuelle.",
            10,
            StudioStyle.Muted,
            wrap: true));

        root.AddChild(StudioStyle.SectionLabel("VUE ENREGISTRÉE"));
        _profiles = new OptionButton();
        StudioStyle.ApplyOptionButton(_profiles);
        _profiles.ItemSelected += SelectProfile;
        root.AddChild(_profiles);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(scroll);
        var fields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        fields.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(fields);

        fields.AddChild(StudioStyle.SectionLabel("TYPES VISIBLES"));
        var kinds = new GridContainer { Columns = 2 };
        kinds.AddThemeConstantOverride("h_separation", 10);
        kinds.AddThemeConstantOverride("v_separation", 3);
        foreach (var kind in Enum.GetValues<BlueprintKind>())
        {
            var check = new CheckBox { Text = KindName(kind) };
            _kindChecks[kind] = check;
            kinds.AddChild(check);
        }
        fields.AddChild(kinds);

        fields.AddChild(StudioStyle.SectionLabel("TAGS REQUIS"));
        _tags = new LineEdit { PlaceholderText = "ex. npc, hostile" };
        StudioStyle.ApplyLineEdit(_tags);
        fields.AddChild(_tags);

        var metadata = new HBoxContainer();
        metadata.AddThemeConstantOverride("separation", 6);
        _author = new LineEdit
        {
            PlaceholderText = "Auteur",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _state = new LineEdit
        {
            PlaceholderText = "État / origine",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(_author);
        StudioStyle.ApplyLineEdit(_state);
        metadata.AddChild(_author);
        metadata.AddChild(_state);
        fields.AddChild(metadata);

        _isolate = new CheckBox { Text = "Isoler la sélection" };
        _ghost = new CheckBox { Text = "Garder le reste en fantôme" };
        fields.AddChild(_isolate);
        fields.AddChild(_ghost);

        fields.AddChild(StudioStyle.SectionLabel("CALQUES VISIBLES · AUCUN = TOUS"));
        _layers = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Multi,
            CustomMinimumSize = new Vector2(0, 96),
        };
        fields.AddChild(_layers);

        fields.AddChild(StudioStyle.SectionLabel("FAMILLES DE VOLUMES"));
        _volumes = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Multi,
            CustomMinimumSize = new Vector2(0, 84),
        };
        foreach (var volume in Enum.GetValues<EditorVolumeKind>())
        {
            _volumes.AddItem(VolumeName(volume));
        }
        fields.AddChild(_volumes);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        var reset = StudioStyle.Button("Tout afficher", compact: true);
        reset.Pressed += () => ResetRequested?.Invoke(EditorDisplayFilter.ShowAll);
        actions.AddChild(reset);
        var apply = StudioStyle.Button("Appliquer et mémoriser", primary: true, compact: true);
        apply.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        apply.Pressed += () => ApplyRequested?.Invoke(BuildFilter("studio:ux0-a-display-filter"));
        actions.AddChild(apply);
        root.AddChild(actions);

        root.AddChild(new HSeparator());
        root.AddChild(StudioStyle.SectionLabel("ENREGISTRER UNE NOUVELLE VUE"));
        var presetRow = new HBoxContainer();
        presetRow.AddThemeConstantOverride("separation", 6);
        _presetName = new LineEdit
        {
            PlaceholderText = "Nom de la vue",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(_presetName);
        presetRow.AddChild(_presetName);
        var savePreset = StudioStyle.Button("Enregistrer", compact: true);
        savePreset.Pressed += () => SavePresetRequested?.Invoke(
            _presetName.Text,
            BuildFilter("studio:ux0-a-saved-view"));
        presetRow.AddChild(savePreset);
        root.AddChild(presetRow);

        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Success, wrap: true);
        root.AddChild(_status);

        Resized += UpdatePanelBounds;
        UpdatePanelBounds();
    }

    internal void Open(
        EditorWorkspaceProfile active,
        IReadOnlyList<EditorWorkspaceProfile> profiles,
        IReadOnlyList<AreaDefinition> areas)
    {
        Load(active, profiles, areas);
        Visible = true;
    }

    internal void Load(
        EditorWorkspaceProfile active,
        IReadOnlyList<EditorWorkspaceProfile> profiles,
        IReadOnlyList<AreaDefinition> areas)
    {
        _loading = true;
        _source = active.DisplayFilter;
        _profiles.Clear();
        _profileIds.Clear();
        var orderedProfiles = profiles.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var selectedProfile = 0;
        for (var index = 0; index < orderedProfiles.Length; index++)
        {
            var profile = orderedProfiles[index];
            _profiles.AddItem($"{profile.Name} · {profile.Role}");
            _profileIds.Add(profile.Id.Value);
            if (profile.Id == active.Id)
            {
                selectedProfile = index;
            }
        }
        _profiles.Select(selectedProfile);

        foreach (var pair in _kindChecks)
        {
            pair.Value.ButtonPressed = _source.VisibleKinds.Contains(pair.Key);
        }
        _tags.Text = string.Join(", ", _source.RequiredTags);
        _author.Text = _source.Author;
        _state.Text = _source.State;
        _isolate.ButtonPressed = _source.IsolateSelection;
        _ghost.ButtonPressed = _source.GhostHidden;

        _layerIds.Clear();
        _layers.Clear();
        foreach (var area in areas.OrderBy(area => area.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var layer in area.Authoring.Layers)
            {
                _layerIds.Add(layer.Id);
                _layers.AddItem($"{area.DisplayName} / {layer.Name}");
            }
        }
        if (_layerIds.Count == 0)
        {
            _layers.AddItem("Aucun calque nommé");
            _layers.SetItemDisabled(0, true);
        }
        for (var index = 0; index < _layerIds.Count; index++)
        {
            if (_source.VisibleLayerIds.Contains(_layerIds[index]))
            {
                _layers.Select(index, false);
            }
        }

        _volumes.DeselectAll();
        foreach (var volume in _source.VisibleVolumeKinds)
        {
            _volumes.Select((int)volume, false);
        }
        _presetName.Text = string.Empty;
        _status.Text = $"Vue active · {active.Name} · {_source.VisibleKinds.Count} types affichés";
        _status.AddThemeColorOverride("font_color", StudioStyle.Success);
        _loading = false;
    }

    internal void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
    }

    internal EditorDisplayFilter BuildFilter(string provenance) => new(
        _kindChecks.Where(pair => pair.Value.ButtonPressed).Select(pair => pair.Key).ToArray(),
        _tags.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        _author.Text,
        _state.Text,
        _isolate.ButtonPressed,
        _ghost.ButtonPressed,
        _layers.GetSelectedItems().Where(index => index < _layerIds.Count).Select(index => _layerIds[index]).ToArray(),
        _volumes.GetSelectedItems().Select(index => (EditorVolumeKind)index).ToArray(),
        _source.Id,
        EditorDisplayFilter.CurrentSchemaVersion,
        provenance);

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }
        Visible = false;
        CloseRequested?.Invoke();
    }

    private void SelectProfile(long index)
    {
        if (_loading || index < 0 || index >= _profileIds.Count)
        {
            return;
        }
        ProfileSelected?.Invoke(_profileIds[(int)index]);
    }

    private void UpdatePanelBounds()
    {
        var width = Math.Clamp(Size.X * .38f, 400, 520);
        _panel.Position = new Vector2(Size.X - width - 12, 58);
        _panel.Size = new Vector2(width, Math.Max(420, Size.Y - 82));
    }

    private static string KindName(BlueprintKind kind) => kind switch
    {
        BlueprintKind.PlayerSpawn => "Départ joueur",
        BlueprintKind.Placeable => "Décor",
        BlueprintKind.Door => "Porte",
        BlueprintKind.Creature => "Créature",
        BlueprintKind.Item => "Objet",
        BlueprintKind.Trigger => "Trigger",
        _ => kind.ToString(),
    };

    private static string VolumeName(EditorVolumeKind kind) => kind switch
    {
        EditorVolumeKind.Trigger => "Triggers",
        EditorVolumeKind.Navigation => "Navigation",
        EditorVolumeKind.Audio => "Audio",
        EditorVolumeKind.Lighting => "Éclairage",
        EditorVolumeKind.Fog => "Brouillard",
        EditorVolumeKind.Gameplay => "Gameplay",
        _ => kind.ToString(),
    };

}
