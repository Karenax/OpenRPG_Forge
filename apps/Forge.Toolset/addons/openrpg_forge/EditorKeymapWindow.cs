#if TOOLS
using Forge.Application.Creator;
using Forge.Domain.Authoring;
using Godot;

namespace Forge.Toolset.Editor;

public partial class EditorKeymapWindow : Window
{
    private Func<EditorKeymapState?>? _getState;
    private Func<EditorKeymapProfile, bool, EditorKeymapCommandResult>? _apply;
    private Func<bool>? _undo;
    private Func<bool>? _redo;
    private Func<bool>? _canUndo;
    private Func<bool>? _canRedo;
    private Action<Window>? _brand;
    private readonly List<EditorCommandBinding> _visibleBindings = [];
    private EditorKeymapProfile? _draft;
    private OptionButton? _preset;
    private LineEdit? _search;
    private ItemList? _commands;
    private Label? _selectionTitle;
    private Label? _selectionIdentity;
    private LineEdit? _gesture;
    private RichTextLabel? _diagnostics;
    private Label? _identity;
    private Label? _status;
    private Button? _previewButton;
    private Button? _applyButton;
    private Button? _undoButton;
    private Button? _redoButton;
    private bool _refreshing;
    private string? _previewFingerprint;

    internal Guid LastCommandId { get; private set; }

    internal string LastPatchSummary { get; private set; } = string.Empty;

    internal EditorKeymapProfile? Draft => _draft;

    public void Initialize(
        Func<EditorKeymapState?> getState,
        Func<EditorKeymapProfile, bool, EditorKeymapCommandResult> apply,
        Func<bool> undo,
        Func<bool> redo,
        Func<bool> canUndo,
        Func<bool> canRedo,
        Action<Window> brand)
    {
        _getState = getState;
        _apply = apply;
        _undo = undo;
        _redo = redo;
        _canUndo = canUndo;
        _canRedo = canRedo;
        _brand = brand;
        Title = "Raccourcis · OpenRPG Forge";
        MinSize = new Vector2I(1180, 760);
        Size = MinSize;
        Transient = true;
        Visible = false;
        CloseRequested += Hide;
        BuildUi();
    }

    public void Open()
    {
        RefreshFromState();
        PopupCentered(MinSize);
        _brand?.Invoke(this);
    }

    internal bool ConfigureForSmokeTest()
    {
        var state = _getState?.Invoke();
        if (state is null) return false;
        _draft = EditorKeymapPlanner.UsePreset(state.Profile, EditorKeymapPreset.Aurora, "smoke:g13-a");
        _draft = EditorKeymapPlanner.Rebind(_draft, EditorKeymapCatalog.OpenSearch,
            new EditorKeyGesture("K", ctrl: true, shift: true), "smoke:g13-a");
        _previewFingerprint = null;
        RefreshDraft();
        return true;
    }

    internal bool PreviewForSmokeTest() => Preview();

    internal bool ApplyForSmokeTest() => Apply();

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("0d0a16", "65488a"));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 22);
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var title = new Label { Text = "RACCOURCIS & COMMANDES", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Rechercher une commande, choisir un profil ou attribuer une touche sans éditer de fichier.",
        });

        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12);
        root.AddChild(columns);

        var profiles = Column(columns, "PROFIL");
        profiles.CustomMinimumSize = new Vector2(250, 0);
        profiles.AddChild(new Label { Text = "Disposition de départ" });
        _preset = new OptionButton();
        foreach (var preset in Enum.GetValues<EditorKeymapPreset>()) _preset.AddItem(PresetName(preset));
        _preset.ItemSelected += SelectPreset;
        profiles.AddChild(_preset);
        profiles.AddChild(new Label
        {
            Text = "Standard privilégie les conventions desktop. Aurora optimise le Toolset. Blender conserve les réflexes de navigation 3D.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        profiles.AddChild(new HSeparator());
        _identity = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        profiles.AddChild(_identity);
        profiles.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        profiles.AddChild(new Label
        {
            Text = "Les raccourcis sont locaux au workspace et n’entrent jamais dans le module exporté.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var catalogue = Column(columns, "COMMANDES RECHERCHABLES");
        catalogue.CustomMinimumSize = new Vector2(430, 0);
        _search = new LineEdit { PlaceholderText = "Nom, catégorie, identifiant ou raccourci…" };
        _search.TextChanged += _ => RefreshBindings();
        catalogue.AddChild(_search);
        _commands = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _commands.ItemSelected += SelectBinding;
        catalogue.AddChild(_commands);

        var editor = Column(columns, "ATTRIBUTION & VALIDATION");
        editor.CustomMinimumSize = new Vector2(360, 0);
        _selectionTitle = new Label { Text = "Aucune commande sélectionnée", ThemeTypeVariation = "HeaderSmall" };
        editor.AddChild(_selectionTitle);
        _selectionIdentity = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        editor.AddChild(_selectionIdentity);
        editor.AddChild(new Label { Text = "Nouveau raccourci" });
        _gesture = new LineEdit { PlaceholderText = "Ctrl+Shift+K, F6, Delete…" };
        editor.AddChild(_gesture);
        var assign = new Button { Text = "Attribuer au brouillon" };
        assign.Pressed += RebindSelected;
        editor.AddChild(assign);
        editor.AddChild(new HSeparator());
        _diagnostics = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 190),
        };
        editor.AddChild(_diagnostics);

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeColorOverride("font_color", new Color("79d5ad"));
        root.AddChild(_status);
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        _undoButton = new Button { Text = "Annuler" };
        _undoButton.Pressed += Undo;
        footer.AddChild(_undoButton);
        _redoButton = new Button { Text = "Rétablir" };
        _redoButton.Pressed += Redo;
        footer.AddChild(_redoButton);
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _previewButton = new Button { Text = "Prévisualiser le diff" };
        _previewButton.Pressed += () => Preview();
        footer.AddChild(_previewButton);
        _applyButton = new Button { Text = "Appliquer", Disabled = true };
        _applyButton.AddThemeColorOverride("font_color", new Color("fff3e8"));
        _applyButton.Pressed += () => Apply();
        footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        footer.AddChild(close);
        root.AddChild(footer);
    }

    private void RefreshFromState()
    {
        var state = _getState?.Invoke();
        _draft = state?.Profile;
        _previewFingerprint = null;
        if (_draft is null)
        {
            _status!.Text = "Aucun projet ouvert · ouvrez un workspace Forge pour configurer ses raccourcis.";
            _commands!.Clear();
            SetActionsEnabled(false);
            return;
        }

        SetActionsEnabled(true);
        RefreshDraft();
        _status!.Text = "Profil chargé · sélectionnez une commande ou un preset.";
    }

    private void RefreshDraft(string? preferredCommandId = null)
    {
        if (_draft is null) return;
        _refreshing = true;
        _preset!.Selected = (int)_draft.Preset;
        _identity!.Text = $"UUID · {_draft.Id}\nSchéma · v{_draft.SchemaVersion}\nProvenance · {_draft.Provenance}\nCommandes · {_draft.Bindings.Count}";
        _refreshing = false;
        RefreshBindings(preferredCommandId);
        RefreshValidation();
        RefreshHistoryButtons();
    }

    private void RefreshBindings(string? preferredCommandId = null)
    {
        if (_commands is null || _draft is null) return;
        var selectedId = preferredCommandId ?? SelectedBinding()?.CommandId;
        _visibleBindings.Clear();
        _visibleBindings.AddRange(EditorKeymapPlanner.Search(_draft, _search?.Text));
        _commands.Clear();
        foreach (var binding in _visibleBindings)
        {
            _commands.AddItem($"{binding.Category}  ·  {binding.DisplayName}    {binding.Gesture.CanonicalText}");
            _commands.SetItemTooltip(_commands.ItemCount - 1, binding.CommandId);
        }

        var index = _visibleBindings.FindIndex(binding => binding.CommandId == selectedId);
        if (index < 0 && _visibleBindings.Count > 0) index = 0;
        if (index >= 0)
        {
            _commands.Select(index);
            ShowBinding(index);
        }
        else
        {
            _selectionTitle!.Text = "Aucun résultat";
            _selectionIdentity!.Text = "Modifiez la recherche pour afficher une commande.";
            _gesture!.Text = string.Empty;
        }
    }

    private void SelectPreset(long index)
    {
        if (_refreshing || _draft is null) return;
        var preset = (EditorKeymapPreset)index;
        _draft = EditorKeymapPlanner.UsePreset(_draft, preset, "forge-ui:g13-a");
        _previewFingerprint = null;
        RefreshDraft();
        _status!.Text = $"Brouillon {PresetName(preset)} chargé · prévisualisez avant application.";
    }

    private void SelectBinding(long index) => ShowBinding((int)index);

    private void ShowBinding(int index)
    {
        if (index < 0 || index >= _visibleBindings.Count) return;
        var binding = _visibleBindings[index];
        _selectionTitle!.Text = binding.DisplayName;
        _selectionIdentity!.Text = $"{binding.Category}\n{binding.CommandId}";
        _gesture!.Text = binding.Gesture.CanonicalText;
    }

    private void RebindSelected()
    {
        var selected = SelectedBinding();
        if (_draft is null || selected is null) return;
        if (!EditorKeyGesture.TryParse(_gesture!.Text, out var gesture) || gesture is null)
        {
            _status!.Text = "Raccourci invalide · exemple attendu : Ctrl+Shift+K, F6 ou Delete.";
            return;
        }

        _draft = EditorKeymapPlanner.Rebind(_draft, selected.CommandId, gesture, "forge-ui:g13-a");
        _previewFingerprint = null;
        RefreshDraft(selected.CommandId);
        _status!.Text = "Attribution ajoutée au brouillon · les conflits sont affichés avant application.";
    }

    private void RefreshValidation()
    {
        if (_draft is null) return;
        var diagnostics = EditorKeymapProfileValidator.Validate(_draft);
        if (diagnostics.Count == 0)
        {
            _diagnostics!.Text = "[color=#79d5ad]✓ Profil valide[/color]\n\n13 commandes accessibles au clavier, sans conflit.";
            _previewButton!.Disabled = false;
        }
        else
        {
            _diagnostics!.Text = "[color=#ff8c8c]Configuration refusée[/color]\n\n" +
                string.Join('\n', diagnostics.Select(item => $"• {item.Code}\n  {item.Message}"));
            _previewButton!.Disabled = true;
            _previewFingerprint = null;
        }
        _applyButton!.Disabled = _previewFingerprint != Fingerprint(_draft) || diagnostics.Count > 0;
    }

    private bool Preview()
    {
        if (_draft is null || _apply is null) return false;
        var result = _apply(_draft, true);
        LastCommandId = result.Audit.CommandId.Value;
        LastPatchSummary = result.Patch?.Summary ?? string.Empty;
        if (!result.Succeeded)
        {
            _previewFingerprint = null;
            _status!.Text = FormatDiagnostics(result);
            RefreshValidation();
            return false;
        }

        _previewFingerprint = Fingerprint(_draft);
        _status!.Text = $"✓ Diff prêt · {LastPatchSummary}";
        RefreshValidation();
        return true;
    }

    private bool Apply()
    {
        if (_draft is null || _apply is null || _previewFingerprint != Fingerprint(_draft))
        {
            if (_status is not null) _status.Text = "Prévisualisez exactement ce brouillon avant de l’appliquer.";
            return false;
        }
        var result = _apply(_draft, false);
        LastCommandId = result.Audit.CommandId.Value;
        LastPatchSummary = result.Patch?.Summary ?? string.Empty;
        if (!result.Succeeded)
        {
            _status!.Text = FormatDiagnostics(result);
            return false;
        }

        _previewFingerprint = null;
        _draft = result.State.Profile;
        RefreshDraft();
        _status!.Text = $"✓ Profil appliqué · révision {result.State.Revision.Value}";
        return true;
    }

    private void Undo()
    {
        _status!.Text = _undo?.Invoke() == true ? "✓ Profil précédent restauré" : "Aucune modification à annuler.";
        RefreshAfterHistory();
    }

    private void Redo()
    {
        _status!.Text = _redo?.Invoke() == true ? "✓ Profil réappliqué" : "Aucune modification à rétablir.";
        RefreshAfterHistory();
    }

    private void RefreshAfterHistory()
    {
        _draft = _getState?.Invoke()?.Profile;
        _previewFingerprint = null;
        RefreshDraft();
    }

    private EditorCommandBinding? SelectedBinding()
    {
        var selected = _commands?.GetSelectedItems();
        return selected is { Length: > 0 } && selected[0] >= 0 && selected[0] < _visibleBindings.Count
            ? _visibleBindings[selected[0]]
            : null;
    }

    private void RefreshHistoryButtons()
    {
        _undoButton!.Disabled = _canUndo?.Invoke() != true;
        _redoButton!.Disabled = _canRedo?.Invoke() != true;
    }

    private void SetActionsEnabled(bool enabled)
    {
        _preset!.Disabled = !enabled;
        _search!.Editable = enabled;
        _gesture!.Editable = enabled;
        _previewButton!.Disabled = !enabled;
        _applyButton!.Disabled = true;
        _undoButton!.Disabled = !enabled;
        _redoButton!.Disabled = !enabled;
    }

    private static string Fingerprint(EditorKeymapProfile profile) =>
        string.Join('|', profile.Id, profile.SchemaVersion, profile.Preset, profile.Provenance,
            string.Join(';', profile.Bindings.Select(binding => $"{binding.CommandId}={binding.Gesture.CanonicalText}")));

    private static string FormatDiagnostics(EditorKeymapCommandResult result) =>
        result.Diagnostics.Count == 0
            ? "Configuration refusée."
            : string.Join(" · ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));

    private static string PresetName(EditorKeymapPreset preset) => preset switch
    {
        EditorKeymapPreset.Aurora => "Aurora",
        EditorKeymapPreset.Blender => "Blender",
        EditorKeymapPreset.Custom => "Personnalisé",
        _ => "Standard",
    };

    private static VBoxContainer Column(Container parent, string heading)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171122", "46325f"));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 14);
        panel.AddChild(margin);
        var content = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        margin.AddChild(content);
        var label = new Label { Text = heading };
        label.AddThemeColorOverride("font_color", new Color("ffb477"));
        content.AddChild(label);
        return content;
    }

    private static StyleBoxFlat Card(string background, string border) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
    };
}
#endif
