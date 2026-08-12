#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ModuleConfigurationWindow : Window
{
    private readonly List<ProjectLocalizedMetadata> _localizations = [];
    private readonly List<ProjectVariableDefinition> _variables = [];
    private readonly ProjectVariableValueKind[] _variableKinds = Enum.GetValues<ProjectVariableValueKind>();
    private readonly ProjectVariableScope[] _variableScopes = Enum.GetValues<ProjectVariableScope>();
    private readonly ProjectVariableReplicationMode[] _variableReplications =
        Enum.GetValues<ProjectVariableReplicationMode>();

    private Func<ProjectState?>? _getState;
    private Func<ProjectMetadata, IReadOnlyList<ProjectVariableDefinition>, bool>? _apply;
    private Action<Window>? _brandWindow;
    private ProjectMetadataId? _metadataId;
    private ProjectVariableId? _variableId;
    private ProjectMetadata? _approvedMetadata;
    private ProjectFoundationProfile? _foundation;
    private ProjectVariableDefinition[]? _approvedVariables;
    private bool _suppressSignals;
    private string? _capturePath;
    private string _captureMarker = "BLACKBOARD";
    private double _captureDelay;
    private bool _captureOpened;

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
    private TextEdit? _installation;
    private TextEdit? _rules;
    private TextEdit? _controls;
    private TextEdit? _licenses;
    private TextEdit? _warnings;
    private TextEdit? _compatibility;
    private ItemList? _variableList;
    private LineEdit? _variableKey;
    private OptionButton? _variableKind;
    private OptionButton? _variableScope;
    private OptionButton? _variableReplication;
    private CheckBox? _variableDebugVisible;
    private CheckBox? _variableRequired;
    private LineEdit? _variableDefault;
    private LineEdit? _variableDescription;
    private Label? _previewSummary;
    private Label? _operationsSummary;
    private Label? _status;
    private Button? _applyButton;

    public int LocalizationCount => _localizations.Count;

    public int VariableCount => _variables.Count;

    public string OperationalSummary => _operationsSummary?.Text ?? string.Empty;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<ProjectMetadata, IReadOnlyList<ProjectVariableDefinition>, bool> apply,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Configuration du module · OpenRPG Forge";
        MinSize = new Vector2I(1440, 920);
        Size = new Vector2I(1440, 920);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r025-project-operations-ui-capture=", StringComparison.Ordinal));
        var capturePrefix = "--r025-project-operations-ui-capture=";
        if (capture is not null) _captureMarker = "R025_PROJECT_OPERATIONS";
        capture ??= OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--blackboard-ui-capture=", StringComparison.Ordinal));
        if (_captureMarker == "BLACKBOARD") capturePrefix = "--blackboard-ui-capture=";
        capture ??= OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r052-project-docs-ui-capture=", StringComparison.Ordinal));
        if (capture is not null && capture.StartsWith("--r052-project-docs-ui-capture=", StringComparison.Ordinal))
        {
            capturePrefix = "--r052-project-docs-ui-capture=";
            _captureMarker = "R052_PROJECT_DOCS";
        }
        if (capture is not null)
        {
            _capturePath = capture[capturePrefix.Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            Open();
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_{_captureMarker}_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1440, 920));
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
        _foundation = metadata.Foundation;
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
        RefreshOperationalSummary(state.Project);
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
        _foundation = metadata.Foundation;
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

        var operationsCard = new PanelContainer();
        operationsCard.AddThemeStyleboxOverride("panel", Card("1d1728", "4f3d6b", 1, 8));
        root.AddChild(operationsCard);
        var operationsMargin = new MarginContainer();
        operationsMargin.AddThemeConstantOverride("margin_left", 12);
        operationsMargin.AddThemeConstantOverride("margin_top", 8);
        operationsMargin.AddThemeConstantOverride("margin_right", 12);
        operationsMargin.AddThemeConstantOverride("margin_bottom", 8);
        operationsCard.AddChild(operationsMargin);
        _operationsSummary = new Label
        {
            Text = "PRÉPARATION OPÉRATIONNELLE · chargez un projet pour auditer ses contrats.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _operationsSummary.AddThemeColorOverride("font_color", new Color("cdbbe8"));
        operationsMargin.AddChild(_operationsSummary);

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
        localization.AddChild(new Label { Text = "Pages joueur localisées" });
        var pages = new TabContainer { CustomMinimumSize = new Vector2(0, 155), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        localization.AddChild(pages);
        _description = AddPage(pages, "Synopsis");
        _installation = AddPage(pages, "Installation");
        _rules = AddPage(pages, "Règles");
        _controls = AddPage(pages, "Contrôles");
        _credits = AddPage(pages, "Crédits");
        _licenses = AddPage(pages, "Licences");
        _warnings = AddPage(pages, "Avertissements");
        _compatibility = AddPage(pages, "Compatibilité");
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
        variables.AddChild(new Label { Text = "Type, scope et réplication" });
        var typeRow = new HBoxContainer();
        typeRow.AddThemeConstantOverride("separation", 8);
        variables.AddChild(typeRow);
        _variableKind = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var kind in _variableKinds) _variableKind.AddItem(FormatKind(kind));
        typeRow.AddChild(_variableKind);
        _variableScope = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var scope in _variableScopes) _variableScope.AddItem(FormatScope(scope));
        typeRow.AddChild(_variableScope);
        _variableReplication = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var replication in _variableReplications) _variableReplication.AddItem(FormatReplication(replication));
        typeRow.AddChild(_variableReplication);
        _variableDefault = AddField(variables, "Valeur par défaut", "false");
        _variableDescription = AddField(variables, "Description", "Usage de cette variable");
        var policies = new HBoxContainer();
        _variableDebugVisible = new CheckBox { Text = "Visible dans le debug", ButtonPressed = true };
        policies.AddChild(_variableDebugVisible);
        _variableRequired = new CheckBox { Text = "Requise au runtime" };
        policies.AddChild(_variableRequired);
        variables.AddChild(policies);
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

    private void RefreshOperationalSummary(ProjectDefinition project)
    {
        var module = project.Module;
        var packageResolution = ContentPackageResolver.Resolve(module.Content.Packages);
        var packageErrors = packageResolution.Diagnostics.Count;
        var profiles = module.Distribution.TargetProfiles;
        var separatedSecrets = profiles.Select(item => item.SecretsProfile)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() == profiles.Count;
        var variablePolicy = module.Variables.Count == 0 ? "legacy borné (aucun schéma)" : "schéma strict";
        var targetState = profiles.Count == Enum.GetValues<ModuleTargetKind>().Length && separatedSecrets
            ? "5 profils · secrets séparés"
            : $"{profiles.Count} profils · configuration à corriger";
        var packageState = packageErrors == 0
            ? $"{module.Content.Packages.Packages.Count} package(s) · lock {packageResolution.LockEntries.Count}"
            : $"{packageErrors} erreur(s) de résolution";

        _operationsSummary!.Text =
            "PRÉPARATION OPÉRATIONNELLE · " +
            "Cycle ProjectLoaded → WorldStarted (Editor/Client/Server)  ·  " +
            $"Variables {module.Variables.Count} · {variablePolicy}  ·  " +
            $"Contenu {packageState}\n" +
            "Scripts cache source + API + compilateur  ·  " +
            $"Cibles {targetState}";
        _operationsSummary.AddThemeColorOverride("font_color",
            packageErrors == 0 && separatedSecrets ? new Color("9ee6b3") : new Color("ffb26b"));
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
                _credits!.Text,
                _installation!.Text,
                _rules!.Text,
                _controls!.Text,
                _licenses!.Text,
                _warnings!.Text,
                _compatibility!.Text);
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
                "creator-command",
                ProjectVariableDefinition.CurrentSchemaVersion,
                _variableReplications[_variableReplication!.Selected],
                _variableDebugVisible!.ButtonPressed,
                _variableRequired!.ButtonPressed);
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
                "creator-command",
                foundation: _foundation);
            var documentationDiagnostic = ProjectDocumentationValidator.Validate(_approvedMetadata).FirstOrDefault();
            if (documentationDiagnostic is not null)
                throw new ArgumentException(
                    $"{documentationDiagnostic.Code} · {documentationDiagnostic.Path} · {documentationDiagnostic.Message}");
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
            _variableList.AddItem($"{item.Key} · {FormatKind(item.Kind)} · {FormatScope(item.Scope)} · " +
                $"{FormatReplication(item.Replication)} · {item.DefaultValue}");
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
        _installation!.Text = item.Installation;
        _rules!.Text = item.Rules;
        _controls!.Text = item.Controls;
        _licenses!.Text = item.Licenses;
        _warnings!.Text = item.Warnings;
        _compatibility!.Text = item.Compatibility;
    }

    private void LoadVariable(int index)
    {
        if (index < 0 || index >= _variables.Count) return;
        var item = _variables[index];
        _variableId = item.Id;
        _variableKey!.Text = item.Key;
        _variableKind!.Selected = Array.IndexOf(_variableKinds, item.Kind);
        _variableScope!.Selected = Array.IndexOf(_variableScopes, item.Scope);
        _variableReplication!.Selected = Array.IndexOf(_variableReplications, item.Replication);
        _variableDefault!.Text = item.DefaultValue;
        _variableDescription!.Text = item.Description;
        _variableDebugVisible!.ButtonPressed = item.DebugVisible;
        _variableRequired!.ButtonPressed = item.RequiredAtRuntime;
    }

    private void ClearLocalizationDraft()
    {
        _locale!.Text = string.Empty;
        _localizedName!.Text = string.Empty;
        _summary!.Text = string.Empty;
        _description!.Text = string.Empty;
        _credits!.Text = string.Empty;
        _installation!.Text = string.Empty;
        _rules!.Text = string.Empty;
        _controls!.Text = string.Empty;
        _licenses!.Text = string.Empty;
        _warnings!.Text = string.Empty;
        _compatibility!.Text = string.Empty;
    }

    private void ClearVariableDraft()
    {
        _variableId = null;
        _variableKey!.Text = string.Empty;
        _variableKind!.Selected = 0;
        _variableScope!.Selected = 0;
        _variableReplication!.Selected = 0;
        _variableDefault!.Text = string.Empty;
        _variableDescription!.Text = string.Empty;
        _variableDebugVisible!.ButtonPressed = true;
        _variableRequired!.ButtonPressed = false;
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

    private static TextEdit AddPage(TabContainer pages, string name)
    {
        var edit = new TextEdit
        {
            Name = name,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            PlaceholderText = $"Page {name.ToLowerInvariant()} en Markdown",
        };
        pages.AddChild(edit);
        return edit;
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

    private static string FormatReplication(ProjectVariableReplicationMode replication) => replication switch
    {
        ProjectVariableReplicationMode.None => "Locale",
        ProjectVariableReplicationMode.Owner => "Propriétaire",
        ProjectVariableReplicationMode.Party => "Groupe",
        ProjectVariableReplicationMode.AllClients => "Tous clients",
        ProjectVariableReplicationMode.ServerAuthority => "Serveur autoritaire",
        _ => replication.ToString(),
    };
}
#endif
