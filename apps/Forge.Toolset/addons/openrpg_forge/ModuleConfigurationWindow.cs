#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Modules;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ModuleConfigurationWindow : Window
{
    private readonly List<ProjectLocalizedMetadata> _localizations = [];
    private readonly List<ProjectVariableDefinition> _variables = [];
    private readonly ProjectVariableValueKind[] _variableKinds = Enum.GetValues<ProjectVariableValueKind>();
    private readonly ProjectVariableScope[] _variableScopes = Enum.GetValues<ProjectVariableScope>();

    private Func<ProjectState?>? _getState;
    private Func<ProjectMetadata, IReadOnlyList<ProjectVariableDefinition>, bool>? _apply;
    private Action<Window>? _brandWindow;
    private ProjectMetadataId? _metadataId;
    private ProjectVariableId? _variableId;
    private ProjectMetadata? _approvedMetadata;
    private ProjectVariableDefinition[]? _approvedVariables;
    private bool _suppressSignals;

    private LineEdit? _author;
    private LineEdit? _version;
    private LineEdit? _license;
    private LineEdit? _classification;
    private LineEdit? _imagePath;
    private LineEdit? _tags;
    private LineEdit? _defaultLocale;
    private ItemList? _localizationList;
    private LineEdit? _locale;
    private LineEdit? _localizedName;
    private LineEdit? _summary;
    private TextEdit? _description;
    private TextEdit? _credits;
    private ItemList? _variableList;
    private LineEdit? _variableKey;
    private OptionButton? _variableKind;
    private OptionButton? _variableScope;
    private LineEdit? _variableDefault;
    private LineEdit? _variableDescription;
    private Label? _previewSummary;
    private Label? _status;
    private Button? _applyButton;

    public int LocalizationCount => _localizations.Count;

    public int VariableCount => _variables.Count;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<ProjectMetadata, IReadOnlyList<ProjectVariableDefinition>, bool> apply,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Configuration du module · OpenRPG Forge";
        MinSize = new Vector2I(1240, 790);
        Size = new Vector2I(1240, 790);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1240, 790));
        _brandWindow?.Invoke(this);
    }

    public void Refresh()
    {
        var state = _getState?.Invoke();
        if (state is null)
        {
            SetStatus("Aucun projet ouvert.", true);
            return;
        }

        var metadata = state.Project.Module.Metadata;
        _suppressSignals = true;
        _metadataId = metadata.Id;
        _author!.Text = metadata.Author;
        _version!.Text = metadata.Version;
        _license!.Text = metadata.License;
        _classification!.Text = metadata.Classification;
        _imagePath!.Text = metadata.ImagePath ?? string.Empty;
        _tags!.Text = string.Join(", ", metadata.Tags);
        _defaultLocale!.Text = metadata.DefaultLocale;
        _localizations.Clear();
        _localizations.AddRange(metadata.Localizations);
        _variables.Clear();
        _variables.AddRange(state.Project.Module.Variables);
        RefreshLocalizationList();
        RefreshVariableList();
        if (_localizations.Count > 0)
        {
            _localizationList!.Select(0);
            LoadLocalization(0);
        }
        else
        {
            ClearLocalizationDraft();
        }

        if (_variables.Count > 0)
        {
            _variableList!.Select(0);
            LoadVariable(0);
        }
        else
        {
            ClearVariableDraft();
        }

        _suppressSignals = false;
        InvalidatePreview();
        SetStatus($"Configuration chargée · schéma métadonnées v{metadata.SchemaVersion}.", false);
    }

    public bool ConfigureForSmokeTest(
        ProjectMetadata metadata,
        IReadOnlyList<ProjectVariableDefinition> variables)
    {
        _suppressSignals = true;
        _metadataId = metadata.Id;
        _author!.Text = metadata.Author;
        _version!.Text = metadata.Version;
        _license!.Text = metadata.License;
        _classification!.Text = metadata.Classification;
        _imagePath!.Text = metadata.ImagePath ?? string.Empty;
        _tags!.Text = string.Join(",", metadata.Tags);
        _defaultLocale!.Text = metadata.DefaultLocale;
        _localizations.Clear();
        _localizations.AddRange(metadata.Localizations);
        _variables.Clear();
        _variables.AddRange(variables);
        RefreshLocalizationList();
        RefreshVariableList();
        _suppressSignals = false;
        Preview();
        return _approvedMetadata is not null && _approvedVariables is not null && !_applyButton!.Disabled;
    }

    public bool ApplyForSmokeTest()
    {
        if (_approvedMetadata is null || _approvedVariables is null)
        {
            return false;
        }

        Apply();
        return _approvedMetadata is null;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("15111f", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "CONFIGURATION DU MODULE", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Préparez la publication, les langues et l'état global sans éditer les fichiers canoniques.",
        });

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);
        var publication = AddCard(body, new Vector2(350, 0));
        publication.AddChild(Section("PUBLICATION"));
        _author = AddField(publication, "Auteur", "Nom ou collectif");
        _version = AddField(publication, "Version", "1.0.0");
        _license = AddField(publication, "Licence", "CC0-1.0, MIT, propriétaire…");
        _classification = AddField(publication, "Classification", "Tout public");
        _imagePath = AddField(publication, "Image", "assets/module-cover.png");
        _tags = AddField(publication, "Tags", "aventure, coop, fantasy");
        _defaultLocale = AddField(publication, "Langue par défaut", "fr");
        var publicationHelp = new Label
        {
            Text = "UUID stable, provenance et schéma sont conservés automatiquement.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        publicationHelp.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        publication.AddChild(publicationHelp);

        var localization = AddCard(body, new Vector2(400, 0), expand: true);
        localization.AddChild(Section("TEXTES LOCALISÉS"));
        _localizationList = new ItemList { CustomMinimumSize = new Vector2(0, 120) };
        localization.AddChild(_localizationList);
        _locale = AddField(localization, "Langue", "fr ou en-US");
        _localizedName = AddField(localization, "Nom", "Nom public du module");
        _summary = AddField(localization, "Résumé", "Une phrase courte");
        localization.AddChild(new Label { Text = "Description" });
        _description = new TextEdit { CustomMinimumSize = new Vector2(0, 72), WrapMode = TextEdit.LineWrappingMode.Boundary };
        localization.AddChild(_description);
        localization.AddChild(new Label { Text = "Crédits localisés" });
        _credits = new TextEdit { CustomMinimumSize = new Vector2(0, 58), WrapMode = TextEdit.LineWrappingMode.Boundary };
        localization.AddChild(_credits);
        var localizationActions = new HBoxContainer();
        localizationActions.AddThemeConstantOverride("separation", 8);
        localization.AddChild(localizationActions);
        var upsertLocalization = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        upsertLocalization.Pressed += UpsertLocalization;
        localizationActions.AddChild(upsertLocalization);
        var removeLocalization = new Button { Text = "Retirer" };
        removeLocalization.Pressed += RemoveLocalization;
        localizationActions.AddChild(removeLocalization);

        var variables = AddCard(body, new Vector2(390, 0));
        variables.AddChild(Section("VARIABLES TYPÉES"));
        _variableList = new ItemList { CustomMinimumSize = new Vector2(0, 160), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        variables.AddChild(_variableList);
        _variableKey = AddField(variables, "Clé", "world.quest.completed");
        variables.AddChild(new Label { Text = "Type et scope" });
        var typeRow = new HBoxContainer();
        typeRow.AddThemeConstantOverride("separation", 8);
        variables.AddChild(typeRow);
        _variableKind = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var kind in _variableKinds) _variableKind.AddItem(FormatKind(kind));
        typeRow.AddChild(_variableKind);
        _variableScope = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var scope in _variableScopes) _variableScope.AddItem(FormatScope(scope));
        typeRow.AddChild(_variableScope);
        _variableDefault = AddField(variables, "Valeur par défaut", "false");
        _variableDescription = AddField(variables, "Description", "Usage de cette variable");
        var variableActions = new HBoxContainer();
        variableActions.AddThemeConstantOverride("separation", 8);
        variables.AddChild(variableActions);
        var upsertVariable = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        upsertVariable.Pressed += UpsertVariable;
        variableActions.AddChild(upsertVariable);
        var removeVariable = new Button { Text = "Retirer" };
        removeVariable.Pressed += RemoveVariable;
        variableActions.AddChild(removeVariable);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        var footerText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        footer.AddChild(footerText);
        _previewSummary = new Label { Text = "Prévisualisation requise.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _previewSummary.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        footerText.AddChild(_previewSummary);
        _status = new Label { Text = "Prêt." };
        _status.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        footerText.AddChild(_status);
        var previewButton = new Button { Text = "Prévisualiser", CustomMinimumSize = new Vector2(150, 44) };
        previewButton.Pressed += Preview;
        footer.AddChild(previewButton);
        _applyButton = new Button
        {
            Text = "Enregistrer la configuration",
            CustomMinimumSize = new Vector2(230, 44),
            Disabled = true,
        };
        _applyButton.AddThemeColorOverride("font_color", Colors.White);
        _applyButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        _applyButton.Pressed += Apply;
        footer.AddChild(_applyButton);
    }

    private void ConnectSignals()
    {
        CloseRequested += Hide;
        foreach (var field in new[] { _author, _version, _license, _classification, _imagePath, _tags, _defaultLocale })
        {
            field!.TextChanged += _ => InvalidatePreview();
        }
        _localizationList!.ItemSelected += index => LoadLocalization((int)index);
        _variableList!.ItemSelected += index => LoadVariable((int)index);
    }

    private void UpsertLocalization()
    {
        try
        {
            var draft = new ProjectLocalizedMetadata(
                _locale!.Text,
                _localizedName!.Text,
                _summary!.Text,
                _description!.Text,
                _credits!.Text);
            var index = _localizations.FindIndex(item =>
                string.Equals(item.Locale, draft.Locale, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _localizations[index] = draft;
            else _localizations.Add(draft);
            _localizations.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Locale, right.Locale));
            RefreshLocalizationList();
            var selected = _localizations.FindIndex(item => item.Locale == draft.Locale);
            _localizationList!.Select(selected);
            InvalidatePreview();
            SetStatus($"Texte {draft.Locale} prêt dans le brouillon.", false);
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveLocalization()
    {
        var selected = _localizationList!.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _localizations.Count) return;
        _localizations.RemoveAt(selected);
        RefreshLocalizationList();
        ClearLocalizationDraft();
        InvalidatePreview();
    }

    private void UpsertVariable()
    {
        try
        {
            var draft = new ProjectVariableDefinition(
                _variableId ?? ProjectVariableId.From(Guid.NewGuid()),
                _variableKey!.Text,
                _variableKinds[_variableKind!.Selected],
                _variableScopes[_variableScope!.Selected],
                _variableDefault!.Text,
                _variableDescription!.Text,
                "creator-command");
            var duplicate = _variables.FindIndex(item =>
                item.Id != draft.Id && string.Equals(item.Key, draft.Key, StringComparison.Ordinal));
            if (duplicate >= 0)
            {
                SetStatus($"La clé '{draft.Key}' existe déjà.", true);
                return;
            }
            var index = _variables.FindIndex(item => item.Id == draft.Id);
            if (index >= 0) _variables[index] = draft;
            else _variables.Add(draft);
            _variables.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
            RefreshVariableList();
            var selected = _variables.FindIndex(item => item.Id == draft.Id);
            _variableList!.Select(selected);
            _variableId = draft.Id;
            InvalidatePreview();
            SetStatus($"Variable '{draft.Key}' prête dans le brouillon.", false);
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveVariable()
    {
        var selected = _variableList!.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _variables.Count) return;
        _variables.RemoveAt(selected);
        RefreshVariableList();
        ClearVariableDraft();
        InvalidatePreview();
    }

    private void Preview()
    {
        try
        {
            _approvedMetadata = new ProjectMetadata(
                _metadataId ?? ProjectMetadataId.From(Guid.NewGuid()),
                _defaultLocale!.Text,
                _localizations,
                _author!.Text,
                _version!.Text,
                _license!.Text,
                string.IsNullOrWhiteSpace(_imagePath!.Text) ? null : _imagePath.Text,
                _tags!.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                _classification!.Text,
                "creator-command");
            _approvedVariables = _variables.ToArray();
            _applyButton!.Disabled = false;
            _previewSummary!.Text =
                $"Aperçu · {_approvedMetadata.Localizations.Count} langue(s) · " +
                $"{_approvedVariables.Length} variable(s) · version {_approvedMetadata.Version}";
            SetStatus("Configuration valide, prête pour la commande transactionnelle.", false);
        }
        catch (ArgumentException exception)
        {
            InvalidatePreview();
            SetStatus(exception.Message, true);
        }
    }

    private void Apply()
    {
        var metadata = _approvedMetadata;
        var variables = _approvedVariables;
        if (metadata is null || variables is null) return;
        if (_apply?.Invoke(metadata, variables) != true)
        {
            SetStatus("Configuration refusée par la validation du projet.", true);
            return;
        }

        _approvedMetadata = null;
        _approvedVariables = null;
        _applyButton!.Disabled = true;
        Refresh();
        SetStatus("Configuration enregistrée dans l'historique du projet.", false);
    }

    private void RefreshLocalizationList()
    {
        _localizationList!.Clear();
        foreach (var item in _localizations) _localizationList.AddItem($"{item.Locale} · {item.Name}");
    }

    private void RefreshVariableList()
    {
        _variableList!.Clear();
        foreach (var item in _variables)
        {
            _variableList.AddItem($"{item.Key}\n{FormatKind(item.Kind)} · {FormatScope(item.Scope)} · {item.DefaultValue}");
            _variableList.SetItemTooltip(_variableList.ItemCount - 1, item.Id.ToString());
        }
    }

    private void LoadLocalization(int index)
    {
        if (index < 0 || index >= _localizations.Count) return;
        var item = _localizations[index];
        _locale!.Text = item.Locale;
        _localizedName!.Text = item.Name;
        _summary!.Text = item.Summary;
        _description!.Text = item.Description;
        _credits!.Text = item.Credits;
    }

    private void LoadVariable(int index)
    {
        if (index < 0 || index >= _variables.Count) return;
        var item = _variables[index];
        _variableId = item.Id;
        _variableKey!.Text = item.Key;
        _variableKind!.Selected = Array.IndexOf(_variableKinds, item.Kind);
        _variableScope!.Selected = Array.IndexOf(_variableScopes, item.Scope);
        _variableDefault!.Text = item.DefaultValue;
        _variableDescription!.Text = item.Description;
    }

    private void ClearLocalizationDraft()
    {
        _locale!.Text = string.Empty;
        _localizedName!.Text = string.Empty;
        _summary!.Text = string.Empty;
        _description!.Text = string.Empty;
        _credits!.Text = string.Empty;
    }

    private void ClearVariableDraft()
    {
        _variableId = null;
        _variableKey!.Text = string.Empty;
        _variableKind!.Selected = 0;
        _variableScope!.Selected = 0;
        _variableDefault!.Text = string.Empty;
        _variableDescription!.Text = string.Empty;
    }

    private void InvalidatePreview()
    {
        if (_suppressSignals) return;
        _approvedMetadata = null;
        _approvedVariables = null;
        if (_applyButton is not null) _applyButton.Disabled = true;
        if (_previewSummary is not null) _previewSummary.Text = "Prévisualisation requise après chaque changement.";
    }

    private void SetStatus(string message, bool error)
    {
        _status!.Text = message;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8c8c" : "9ee6b3"));
    }

    private static LineEdit AddField(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder };
        parent.AddChild(field);
        return field;
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color("ff9858"));
        return label;
    }

    private static VBoxContainer AddCard(HBoxContainer parent, Vector2 minimumSize, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimumSize,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("211a2d", "47365e", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);
        return column;
    }

    private static StyleBoxFlat Card(string background, string border, int borderWidth, int radius) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };

    private static string FormatKind(ProjectVariableValueKind kind) => kind switch
    {
        ProjectVariableValueKind.String => "Texte",
        ProjectVariableValueKind.Boolean => "Booléen",
        ProjectVariableValueKind.Integer => "Entier",
        ProjectVariableValueKind.Number => "Nombre",
        _ => kind.ToString(),
    };

    private static string FormatScope(ProjectVariableScope scope) => scope switch
    {
        ProjectVariableScope.Session => "Session",
        ProjectVariableScope.Save => "Sauvegarde",
        ProjectVariableScope.Player => "Joueur",
        ProjectVariableScope.Party => "Groupe",
        ProjectVariableScope.Server => "Serveur",
        _ => scope.ToString(),
    };
}
#endif
