using Forge.Domain.Modules;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioProjectGlobalsPage : Control
{
    private static readonly ProjectVariableValueKind[] VariableKinds = Enum.GetValues<ProjectVariableValueKind>();
    private static readonly ProjectVariableScope[] VariableScopes = Enum.GetValues<ProjectVariableScope>();
    private static readonly ProjectVariableReplicationMode[] VariableReplications =
        Enum.GetValues<ProjectVariableReplicationMode>();
    private static readonly string[] GuidePages =
        ["Synopsis", "Installation", "Règles", "Contrôles", "Crédits", "Licences", "Avertissements", "Compatibilité"];

    private readonly StudioSessionController _session;
    private readonly Button _variablesTab;
    private readonly Button _cacheTab;
    private readonly Button _guideTab;
    private readonly VBoxContainer _variablesContent;
    private readonly VBoxContainer _cacheContent;
    private readonly VBoxContainer _guideContent;
    private readonly Label _summary;
    private readonly Label _status;

    private readonly List<ProjectVariableDefinition> _variables = [];
    private ItemList _variableList = null!;
    private Label _variableEmpty = null!;
    private LineEdit _variableName = null!;
    private OptionButton _variableKind = null!;
    private OptionButton _variableScope = null!;
    private LineEdit _variableDefault = null!;
    private TextEdit _variableDescription = null!;
    private OptionButton _variableReplication = null!;
    private CheckBox _variableDebugVisible = null!;
    private CheckBox _variableRequired = null!;
    private ProjectVariableId? _editingVariableId;
    private string _editingVariableKey = string.Empty;
    private string _originalVariableDisplayName = string.Empty;

    private OptionButton _cacheLocation = null!;
    private readonly List<string> _cacheLocationValues = [];
    private Label _cacheSummary = null!;
    private Label _cacheResult = null!;
    private Button _cacheUndo = null!;
    private Button _cacheRedo = null!;

    private readonly List<StudioLocalizedProjectDraft> _localizations = [];
    private OptionButton _guideLanguage = null!;
    private OptionButton _guidePage = null!;
    private TextEdit _guideText = null!;
    private Label _guideProgress = null!;
    private Label _guideEmpty = null!;
    private int _activeGuideLanguage = -1;
    private int _activeGuidePage;
    private bool _loading;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? ProjectChanged;

    internal int VariableCount => _variables.Count;

    internal int GuidePageCount => GuidePages.Length;

    internal int ScriptCount { get; private set; }

    internal bool ShowsHumanSurface =>
        !_summary.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_summary.Text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !_cacheSummary.Text.Contains(".forge", StringComparison.OrdinalIgnoreCase) &&
        (_variableList.ItemCount == 0 ||
         !_variableList.GetItemText(_variableList.ItemCount - 1).Contains('.'));

    internal StudioProjectGlobalsPage(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioProjectGlobalsPage";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        var banner = StudioStyle.Panel("ProjectGlobalsBanner", StudioStyle.AccentDark, 8);
        root.AddChild(banner);
        var bannerMargin = StudioStyle.Margin(11, 8, 11, 8);
        banner.AddChild(bannerMargin);
        var bannerRow = new HBoxContainer();
        bannerMargin.AddChild(bannerRow);
        var bannerText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bannerText.AddChild(StudioStyle.Label("Données lisibles du projet", 15));
        _summary = StudioStyle.Label(string.Empty, 10, StudioStyle.Success, wrap: true);
        bannerText.AddChild(_summary);
        bannerRow.AddChild(bannerText);
        bannerRow.AddChild(StudioStyle.Label("SCHÉMAS SÛRS", 11, StudioStyle.Accent));

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _variablesTab = StudioStyle.Button("●  Variables du projet", compact: true);
        _variablesTab.TooltipText = "Valeurs typées conservées pour une session, une sauvegarde, un joueur, un groupe ou le serveur";
        tabs.AddChild(_variablesTab);
        _cacheTab = StudioStyle.Button("Cache des scripts", compact: true);
        _cacheTab.TooltipText = "Compilation incrémentale et reproductible des scripts";
        tabs.AddChild(_cacheTab);
        _guideTab = StudioStyle.Button("Guide du joueur", compact: true);
        _guideTab.TooltipText = "Huit pages localisées directement visibles par les joueurs";
        tabs.AddChild(_guideTab);
        var hint = StudioStyle.Label("Aperçu avant application · aucune identité technique à saisir", 9,
            StudioStyle.Muted);
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        tabs.AddChild(hint);

        _variablesContent = BuildVariablesContent();
        root.AddChild(_variablesContent);
        _cacheContent = BuildCacheContent();
        root.AddChild(_cacheContent);
        _guideContent = BuildGuideContent();
        root.AddChild(_guideContent);

        _status = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(320, 24);
        root.AddChild(_status);

        _variablesTab.Pressed += () => ShowSection(0);
        _cacheTab.Pressed += () => ShowSection(1);
        _guideTab.Pressed += () => ShowSection(2);
        RefreshFromSession();
        ShowSection(0);
    }

    internal void RefreshFromSession()
    {
        if (!_session.IsOpen || _session.Project is null)
        {
            return;
        }

        _loading = true;
        _variables.Clear();
        _variables.AddRange(_session.CaptureProjectVariables());
        PopulateVariableList();
        if (_variables.Count > 0)
        {
            _variableList.Select(0);
            LoadVariable(0);
        }
        else
        {
            ClearVariableForm();
        }

        var cache = _session.CaptureScriptCacheSettings();
        PopulateCacheLocations(cache);
        ScriptCount = cache.ScriptCount;
        _cacheSummary.Text = cache.ScriptCount == 0
            ? "Aucun script pour le moment · le premier sera compilé automatiquement."
            : $"{cache.ScriptCount} script(s) · compilation incrémentale · réutilisation vérifiée.";
        _cacheResult.Text = "Le cache compare automatiquement la source, l’API et le compilateur.";
        RefreshCacheHistoryButtons();

        var configuration = _session.CaptureProjectConfiguration();
        _localizations.Clear();
        _localizations.AddRange(configuration.Localizations);
        PopulateGuideLanguages(configuration.DefaultLocale);
        _activeGuidePage = Math.Clamp(_activeGuidePage, 0, GuidePages.Length - 1);
        _guidePage.Select(_activeGuidePage);
        LoadGuidePage();
        _summary.Text = $"{_variables.Count} variable(s) · {cache.ScriptCount} script(s) · " +
                        $"{_localizations.Count} langue(s) pour le guide joueur";
        _loading = false;
        SetStatus("Données réelles chargées · aucune modification appliquée");
    }

    internal bool PrepareEvidence(string mode)
    {
        RefreshFromSession();
        if (mode.Contains("cache", StringComparison.OrdinalIgnoreCase))
        {
            ShowSection(1);
            return ScriptCount > 0 && ShowsHumanSurface;
        }
        if (mode.Contains("guide", StringComparison.OrdinalIgnoreCase))
        {
            ShowSection(2);
            PrepareGuideExample();
            return GuidePageCount == 8 && ShowsHumanSurface;
        }

        ShowSection(0);
        if (_variables.Count == 0)
        {
            _variables.Add(new ProjectVariableDefinition(
                ProjectVariableId.From(Guid.Parse("d0250000-0000-4000-8000-000000000419")),
                "world.chapter",
                ProjectVariableValueKind.Integer,
                ProjectVariableScope.Save,
                "1",
                "Chapitre courant de l’aventure",
                "studio:ux0-i-evidence",
                ProjectVariableDefinition.CurrentSchemaVersion,
                ProjectVariableReplicationMode.AllClients,
                debugVisible: true,
                requiredAtRuntime: true));
            PopulateVariableList();
            _variableList.Select(0);
            LoadVariable(0);
            SetStatus("Exemple local préparé pour l’inspection · projet inchangé");
        }
        return VariableCount > 0 && ShowsHumanSurface;
    }

    internal bool RunTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalCache = _session.BuildProfile;
        var variables = _session.CaptureProjectVariables().ToList();
        variables.Add(new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("d0250000-0000-4000-8000-000000000420")),
            "world.ux0-i-evidence",
            ProjectVariableValueKind.Boolean,
            ProjectVariableScope.Save,
            "false",
            "Preuve d’interface temporaire",
            "studio:ux0-i-ui-evidence"));
        var variablePreview = _session.PreviewProjectVariables(variables);
        var variableApply = _session.ApplyProjectVariables(variables);
        var variableUndo = _session.Undo();

        var configuration = _session.CaptureProjectConfiguration();
        var guide = configuration.Localizations.Select((item, index) => index == 0
            ? item with { Installation = item.Installation + " · UX0-I UI" }
            : item).ToArray();
        var guideDraft = configuration with { Localizations = guide };
        var guidePreview = _session.PreviewProjectConfiguration(guideDraft);
        var guideApply = _session.ApplyProjectConfiguration(guideDraft);
        var guideUndo = _session.Undo();

        var currentCache = _session.CaptureScriptCacheSettings().LocationKey;
        var targetCache = currentCache == StudioSessionController.IsolatedCacheLocation
            ? StudioSessionController.ProjectCacheLocation
            : StudioSessionController.IsolatedCacheLocation;
        var cachePreview = _session.PreviewScriptCacheSettings(targetCache);
        var cacheApply = _session.ApplyScriptCacheSettings(targetCache);
        var cacheUndo = _session.UndoScriptCacheProfile();
        var cacheRedo = _session.RedoScriptCacheProfile();
        var cacheRestore = _session.UndoScriptCacheProfile();
        RefreshFromSession();
        return variablePreview.Succeeded && variableApply.Succeeded && variableUndo.Succeeded &&
               guidePreview.Succeeded && guideApply.Succeeded && guideUndo.Succeeded &&
               cachePreview.Succeeded && cacheApply.Succeeded && cacheUndo.Succeeded && cacheRedo.Succeeded &&
               cacheRestore.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
               ReferenceEquals(originalCache, _session.BuildProfile) && !_session.IsDirty;
    }

    internal void PreviewActiveConfiguration()
    {
        if (_cacheContent.Visible)
        {
            PreviewCache();
        }
        else if (_guideContent.Visible)
        {
            PreviewGuide();
        }
        else
        {
            PreviewVariables();
        }
    }

    private VBoxContainer BuildVariablesContent()
    {
        var content = Content();
        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);
        content.AddChild(body);

        var listPanel = StudioStyle.Panel("ProjectVariableList", StudioStyle.Chrome, 8);
        listPanel.CustomMinimumSize = new Vector2(330, 0);
        body.AddChild(listPanel);
        var listMargin = StudioStyle.Margin(10, 10, 10, 10);
        listPanel.AddChild(listMargin);
        var listColumn = new VBoxContainer();
        listColumn.AddThemeConstantOverride("separation", 6);
        listMargin.AddChild(listColumn);
        listColumn.AddChild(StudioStyle.SectionLabel("VARIABLES DU PROJET"));
        listColumn.AddChild(StudioStyle.Label(
            "Les noms restent humains ; le Studio conserve seul leurs identités stables.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _variableList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 210),
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _variableList.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        _variableList.ItemSelected += index => LoadVariable((int)index);
        listColumn.AddChild(_variableList);
        _variableEmpty = StudioStyle.Label("Aucune variable · créez la première ci-contre.", 10, StudioStyle.Muted,
            wrap: true);
        listColumn.AddChild(_variableEmpty);
        var listActions = new HBoxContainer();
        listActions.AddThemeConstantOverride("separation", 5);
        var add = StudioStyle.Button("Nouvelle", primary: true, compact: true);
        add.Pressed += ClearVariableForm;
        listActions.AddChild(add);
        var remove = StudioStyle.Button("Retirer", compact: true);
        remove.Pressed += RemoveVariable;
        listActions.AddChild(remove);
        listColumn.AddChild(listActions);

        var editorPanel = StudioStyle.Panel("ProjectVariableEditor", StudioStyle.Surface, 8);
        editorPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        editorPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(editorPanel);
        var editorMargin = StudioStyle.Margin(12, 10, 12, 10);
        editorPanel.AddChild(editorMargin);
        var editor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editor.AddThemeConstantOverride("separation", 7);
        editorMargin.AddChild(editor);
        editor.AddChild(StudioStyle.SectionLabel("VALEUR TYPÉE"));
        editor.AddChild(StudioStyle.Label(
            "Choisissez où la valeur vit et qui peut la recevoir. Une valeur incompatible est refusée avant application.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var grid = Grid();
        editor.AddChild(grid);
        _variableName = AddField(grid, "Nom visible", Edit("Chapitre courant", "Nom affiché dans le Studio"));
        _variableKind = AddField(grid, "Type de valeur", Option(VariableKinds.Select(KindLabel).ToArray()));
        _variableScope = AddField(grid, "Conservation", Option(VariableScopes.Select(ScopeLabel).ToArray()));
        _variableDefault = AddField(grid, "Valeur initiale", Edit("1", "Valeur créée au premier lancement"));
        _variableReplication = AddField(grid, "Partage réseau",
            Option(VariableReplications.Select(ReplicationLabel).ToArray()));
        _variableDescription = AddField(grid, "Explication", Multiline("À quoi sert cette valeur ?", 80));
        var policies = new HBoxContainer();
        policies.AddThemeConstantOverride("separation", 18);
        _variableDebugVisible = new CheckBox { Text = "Visible dans le diagnostic", ButtonPressed = true };
        policies.AddChild(_variableDebugVisible);
        _variableRequired = new CheckBox { Text = "Requise pour lancer le jeu" };
        policies.AddChild(_variableRequired);
        editor.AddChild(policies);
        var formActions = new HBoxContainer();
        formActions.AddThemeConstantOverride("separation", 6);
        var keep = StudioStyle.Button("Ajouter ou mettre à jour", primary: true, compact: true);
        keep.Pressed += UpsertVariable;
        formActions.AddChild(keep);
        var preview = StudioStyle.Button("Prévisualiser", compact: true);
        preview.Pressed += PreviewVariables;
        formActions.AddChild(preview);
        var apply = StudioStyle.Button("Appliquer au projet", compact: true);
        apply.Pressed += ApplyVariables;
        formActions.AddChild(apply);
        editor.AddChild(formActions);
        return content;
    }

    private VBoxContainer BuildCacheContent()
    {
        var content = Content(visible: false);
        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);
        content.AddChild(body);

        var settingsPanel = StudioStyle.Panel("ScriptCacheSettings", StudioStyle.Surface, 8);
        settingsPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(settingsPanel);
        var settingsMargin = StudioStyle.Margin(14, 12, 14, 12);
        settingsPanel.AddChild(settingsMargin);
        var settings = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        settings.AddThemeConstantOverride("separation", 9);
        settingsMargin.AddChild(settings);
        settings.AddChild(StudioStyle.SectionLabel("CACHE FORGESCRIPT AUTOMATIQUE"));
        settings.AddChild(StudioStyle.Label(
            "Chaque script compilé est réutilisé tant que sa source, l’API et le compilateur restent identiques.",
            11,
            StudioStyle.Text,
            wrap: true));
        _cacheSummary = StudioStyle.Label(string.Empty, 11, StudioStyle.Success, wrap: true);
        settings.AddChild(_cacheSummary);
        var cacheGrid = Grid();
        settings.AddChild(cacheGrid);
        _cacheLocation = AddField(cacheGrid, "Organisation locale", Option("Cache compact du projet"));
        settings.AddChild(Checkline("Build incrémental activé"));
        settings.AddChild(Checkline("Seuls les éléments affectés sont recompilés"));
        settings.AddChild(Checkline("Reproductibilité vérifiée avant export"));
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        var preview = StudioStyle.Button("Prévisualiser", compact: true);
        preview.Pressed += PreviewCache;
        actions.AddChild(preview);
        var apply = StudioStyle.Button("Enregistrer ce réglage", primary: true, compact: true);
        apply.Pressed += ApplyCache;
        actions.AddChild(apply);
        _cacheUndo = StudioStyle.Button("Annuler", compact: true);
        _cacheUndo.Pressed += UndoCache;
        actions.AddChild(_cacheUndo);
        _cacheRedo = StudioStyle.Button("Rétablir", compact: true);
        _cacheRedo.Pressed += RedoCache;
        actions.AddChild(_cacheRedo);
        settings.AddChild(actions);

        var verifyPanel = StudioStyle.Panel("ScriptCacheVerification", StudioStyle.Chrome, 8);
        verifyPanel.CustomMinimumSize = new Vector2(390, 0);
        verifyPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(verifyPanel);
        var verifyMargin = StudioStyle.Margin(14, 12, 14, 12);
        verifyPanel.AddChild(verifyMargin);
        var verify = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        verify.AddThemeConstantOverride("separation", 10);
        verifyMargin.AddChild(verify);
        verify.AddChild(StudioStyle.SectionLabel("VÉRIFICATION RÉELLE"));
        verify.AddChild(StudioStyle.Label(
            "Le Studio compile les scripts du projet puis vérifie leur réutilisation immédiate. Une erreur nomme directement le script concerné.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var warm = StudioStyle.Button("Vérifier et réchauffer le cache", primary: true);
        warm.Pressed += WarmCache;
        verify.AddChild(warm);
        _cacheResult = StudioStyle.Label(string.Empty, 11, StudioStyle.Muted, wrap: true);
        _cacheResult.CustomMinimumSize = new Vector2(300, 100);
        verify.AddChild(_cacheResult);
        verify.AddChild(StudioStyle.Label(
            "Le cache est jetable et n’entre jamais dans le module exporté.",
            9,
            StudioStyle.Muted,
            wrap: true));
        return content;
    }

    private VBoxContainer BuildGuideContent()
    {
        var content = Content(visible: false);
        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);
        content.AddChild(body);

        var navigationPanel = StudioStyle.Panel("PlayerGuideNavigation", StudioStyle.Chrome, 8);
        navigationPanel.CustomMinimumSize = new Vector2(330, 0);
        body.AddChild(navigationPanel);
        var navigationMargin = StudioStyle.Margin(12, 11, 12, 11);
        navigationPanel.AddChild(navigationMargin);
        var navigation = new VBoxContainer();
        navigation.AddThemeConstantOverride("separation", 8);
        navigationMargin.AddChild(navigation);
        navigation.AddChild(StudioStyle.SectionLabel("GUIDE LOCALISÉ"));
        navigation.AddChild(StudioStyle.Label(
            "Chaque langue possède les huit pages que le lanceur et le jeu peuvent présenter aux joueurs.",
            10,
            StudioStyle.Muted,
            wrap: true));
        navigation.AddChild(StudioStyle.Label("Langue", 10, StudioStyle.Muted));
        _guideLanguage = Option("Langue principale");
        _guideLanguage.ItemSelected += SelectGuideLanguage;
        navigation.AddChild(_guideLanguage);
        navigation.AddChild(StudioStyle.Label("Page", 10, StudioStyle.Muted));
        _guidePage = Option(GuidePages);
        _guidePage.ItemSelected += SelectGuidePage;
        navigation.AddChild(_guidePage);
        _guideProgress = StudioStyle.Label(string.Empty, 11, StudioStyle.Success, wrap: true);
        navigation.AddChild(_guideProgress);
        navigation.AddChild(StudioStyle.Label(
            "Le nom, le résumé court et la langue principale restent dans Identité et configuration.",
            9,
            StudioStyle.Muted,
            wrap: true));

        var editorPanel = StudioStyle.Panel("PlayerGuideEditor", StudioStyle.Surface, 8);
        editorPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        editorPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(editorPanel);
        var editorMargin = StudioStyle.Margin(14, 12, 14, 12);
        editorPanel.AddChild(editorMargin);
        var editor = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        editor.AddThemeConstantOverride("separation", 8);
        editorMargin.AddChild(editor);
        editor.AddChild(StudioStyle.SectionLabel("TEXTE AFFICHÉ AU JOUEUR"));
        _guideEmpty = StudioStyle.Label("Cette page est vide · ajoutez son contenu ci-dessous.", 10,
            StudioStyle.Muted, wrap: true);
        editor.AddChild(_guideEmpty);
        _guideText = Multiline("Écrivez ici un texte clair destiné aux joueurs.", 300);
        _guideText.SizeFlagsVertical = SizeFlags.ExpandFill;
        _guideText.TextChanged += UpdateGuideEmptyState;
        editor.AddChild(_guideText);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        var preview = StudioStyle.Button("Prévisualiser sans modifier", compact: true);
        preview.Pressed += PreviewGuide;
        actions.AddChild(preview);
        var apply = StudioStyle.Button("Appliquer le guide", primary: true, compact: true);
        apply.Pressed += ApplyGuide;
        actions.AddChild(apply);
        editor.AddChild(actions);
        return content;
    }

    private void ShowSection(int index)
    {
        _variablesContent.Visible = index == 0;
        _cacheContent.Visible = index == 1;
        _guideContent.Visible = index == 2;
        _variablesTab.Text = index == 0 ? "●  Variables du projet" : "Variables du projet";
        _cacheTab.Text = index == 1 ? "●  Cache des scripts" : "Cache des scripts";
        _guideTab.Text = index == 2 ? "●  Guide du joueur" : "Guide du joueur";
    }

    private void PopulateVariableList()
    {
        _variableList.Clear();
        foreach (var variable in _variables)
        {
            _variableList.AddItem(StudioUx0IVariableText.DisplayName(variable));
        }
        _variableEmpty.Visible = _variables.Count == 0;
    }

    private void LoadVariable(int index)
    {
        if (index < 0 || index >= _variables.Count)
        {
            return;
        }
        var variable = _variables[index];
        _editingVariableId = variable.Id;
        _editingVariableKey = variable.Key;
        _originalVariableDisplayName = StudioUx0IVariableText.DisplayName(variable);
        _variableName.Text = _originalVariableDisplayName;
        _variableKind.Select(Array.IndexOf(VariableKinds, variable.Kind));
        _variableScope.Select(Array.IndexOf(VariableScopes, variable.Scope));
        _variableDefault.Text = variable.DefaultValue;
        _variableDescription.Text = variable.Description;
        _variableReplication.Select(Array.IndexOf(VariableReplications, variable.Replication));
        _variableDebugVisible.ButtonPressed = variable.DebugVisible;
        _variableRequired.ButtonPressed = variable.RequiredAtRuntime;
    }

    private void ClearVariableForm()
    {
        _editingVariableId = null;
        _editingVariableKey = string.Empty;
        _originalVariableDisplayName = string.Empty;
        _variableName.Text = string.Empty;
        _variableKind.Select(0);
        _variableScope.Select((int)ProjectVariableScope.Save);
        _variableDefault.Text = string.Empty;
        _variableDescription.Text = string.Empty;
        _variableReplication.Select(0);
        _variableDebugVisible.ButtonPressed = true;
        _variableRequired.ButtonPressed = false;
        if (IsInsideTree())
        {
            _variableName.GrabFocus();
        }
        SetStatus("Nouvelle variable préparée · nommez-la puis ajoutez-la au formulaire");
    }

    private void UpsertVariable()
    {
        try
        {
            var displayName = _variableName.Text.Trim();
            if (displayName.Length == 0)
            {
                SetStatus("Donnez un nom visible à la variable.", error: true);
                return;
            }
            var key = _editingVariableId is not null &&
                      string.Equals(displayName, _originalVariableDisplayName, StringComparison.Ordinal)
                ? _editingVariableKey
                : StudioUx0IVariableText.BuildKey(displayName);
            var variable = new ProjectVariableDefinition(
                _editingVariableId ?? ProjectVariableId.From(Guid.NewGuid()),
                key,
                VariableKinds[Math.Clamp(_variableKind.Selected, 0, VariableKinds.Length - 1)],
                VariableScopes[Math.Clamp(_variableScope.Selected, 0, VariableScopes.Length - 1)],
                _variableDefault.Text,
                _variableDescription.Text,
                "studio:ux0-i-project-variable",
                ProjectVariableDefinition.CurrentSchemaVersion,
                VariableReplications[Math.Clamp(_variableReplication.Selected, 0, VariableReplications.Length - 1)],
                _variableDebugVisible.ButtonPressed,
                _variableRequired.ButtonPressed);
            var candidate = _variables.Where(item => item.Id != variable.Id).Append(variable).ToArray();
            var diagnostic = ProjectVariableValidator.Validate(candidate).FirstOrDefault();
            if (diagnostic is not null)
            {
                SetStatus(HumanizeVariableDiagnostic(diagnostic.Code), error: true);
                return;
            }
            var index = _variables.FindIndex(item => item.Id == variable.Id);
            if (index >= 0) _variables[index] = variable;
            else _variables.Add(variable);
            _variables.Sort((left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(
                StudioUx0IVariableText.DisplayName(left), StudioUx0IVariableText.DisplayName(right)));
            PopulateVariableList();
            var selected = _variables.FindIndex(item => item.Id == variable.Id);
            _variableList.Select(selected);
            LoadVariable(selected);
            SetStatus("Variable ajoutée au formulaire · prévisualisez ou appliquez pour modifier le projet");
        }
        catch (ArgumentException exception)
        {
            SetStatus(HumanizeVariableException(exception), error: true);
        }
    }

    private void RemoveVariable()
    {
        var selected = _variableList.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _variables.Count)
        {
            SetStatus("Sélectionnez d’abord une variable à retirer.", error: true);
            return;
        }
        _variables.RemoveAt(selected);
        PopulateVariableList();
        if (_variables.Count > 0)
        {
            var next = Math.Min(selected, _variables.Count - 1);
            _variableList.Select(next);
            LoadVariable(next);
        }
        else
        {
            ClearVariableForm();
        }
        SetStatus("Variable retirée du formulaire · appliquez pour confirmer");
    }

    private void PreviewVariables()
    {
        var result = _session.PreviewProjectVariables(_variables.ToArray());
        SetStatus(result.Succeeded ? $"{result.Message} · {result.Summary}" : result.Message, !result.Succeeded);
    }

    private void ApplyVariables()
    {
        var result = _session.ApplyProjectVariables(_variables.ToArray());
        SetStatus(result.Succeeded ? $"{result.Message} · {result.Summary}" : result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshFromSession();
            ProjectChanged?.Invoke();
        }
    }

    private void PopulateCacheLocations(StudioScriptCacheSettings settings)
    {
        _cacheLocation.Clear();
        _cacheLocationValues.Clear();
        AddCacheLocation(StudioSessionController.ProjectCacheLocation, "Cache compact du projet");
        AddCacheLocation(StudioSessionController.IsolatedCacheLocation, "Cache isolé pour les scripts");
        if (settings.ExistingConfigurationPreserved)
        {
            AddCacheLocation(StudioSessionController.PreservedCacheLocation,
                "Organisation existante · préservée");
        }
        var selected = _cacheLocationValues.FindIndex(item => item == settings.LocationKey);
        _cacheLocation.Select(Math.Max(0, selected));
    }

    private void AddCacheLocation(string value, string label)
    {
        _cacheLocationValues.Add(value);
        _cacheLocation.AddItem(label);
    }

    private string SelectedCacheLocation() =>
        _cacheLocation.Selected >= 0 && _cacheLocation.Selected < _cacheLocationValues.Count
            ? _cacheLocationValues[_cacheLocation.Selected]
            : StudioSessionController.ProjectCacheLocation;

    private void PreviewCache()
    {
        var result = _session.PreviewScriptCacheSettings(SelectedCacheLocation());
        SetStatus(result.Message, !result.Succeeded);
    }

    private void ApplyCache()
    {
        var result = _session.ApplyScriptCacheSettings(SelectedCacheLocation());
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshFromSession();
        }
    }

    private void UndoCache()
    {
        var result = _session.UndoScriptCacheProfile();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) RefreshFromSession();
    }

    private void RedoCache()
    {
        var result = _session.RedoScriptCacheProfile();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) RefreshFromSession();
    }

    private void RefreshCacheHistoryButtons()
    {
        _cacheUndo.Disabled = !_session.CanUndoScriptCacheProfile;
        _cacheRedo.Disabled = !_session.CanRedoScriptCacheProfile;
    }

    private void WarmCache()
    {
        _cacheResult.Text = "Compilation et vérification en cours…";
        var result = _session.WarmScriptCache();
        _cacheResult.Text = result.Succeeded
            ? $"{result.Message}\n{result.Compiled} compilation(s) nécessaire(s) · {result.Reused} réutilisation(s) vérifiée(s)."
            : result.Message;
        _cacheResult.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatus(result.Message, !result.Succeeded);
    }

    private void PopulateGuideLanguages(string defaultLocale)
    {
        _guideLanguage.Clear();
        foreach (var localization in _localizations)
        {
            _guideLanguage.AddItem(LocaleLabel(localization.Locale) +
                                   (string.Equals(localization.Locale, defaultLocale,
                                       StringComparison.OrdinalIgnoreCase) ? " · principale" : string.Empty));
        }
        _activeGuideLanguage = Math.Clamp(
            _localizations.FindIndex(item => string.Equals(item.Locale, defaultLocale,
                StringComparison.OrdinalIgnoreCase)),
            0,
            Math.Max(0, _localizations.Count - 1));
        if (_localizations.Count > 0) _guideLanguage.Select(_activeGuideLanguage);
    }

    private void SelectGuideLanguage(long index)
    {
        if (index < 0 || index >= _localizations.Count)
        {
            return;
        }
        if (!_loading) SaveGuidePage();
        _activeGuideLanguage = (int)index;
        LoadGuidePage();
    }

    private void SelectGuidePage(long index)
    {
        if (index < 0 || index >= GuidePages.Length)
        {
            return;
        }
        if (!_loading) SaveGuidePage();
        _activeGuidePage = (int)index;
        LoadGuidePage();
    }

    private void LoadGuidePage()
    {
        if (_activeGuideLanguage < 0 || _activeGuideLanguage >= _localizations.Count)
        {
            return;
        }
        _loading = true;
        _guideText.Text = GuideValue(_localizations[_activeGuideLanguage], _activeGuidePage);
        _loading = false;
        UpdateGuideEmptyState();
        UpdateGuideProgress();
    }

    private void SaveGuidePage()
    {
        if (_loading || _activeGuideLanguage < 0 || _activeGuideLanguage >= _localizations.Count)
        {
            return;
        }
        _localizations[_activeGuideLanguage] = WithGuideValue(
            _localizations[_activeGuideLanguage],
            _activeGuidePage,
            _guideText.Text);
        UpdateGuideProgress();
    }

    private void UpdateGuideEmptyState()
    {
        _guideEmpty.Visible = string.IsNullOrWhiteSpace(_guideText.Text);
        if (!_loading) SaveGuidePage();
    }

    private void UpdateGuideProgress()
    {
        if (_activeGuideLanguage < 0 || _activeGuideLanguage >= _localizations.Count)
        {
            _guideProgress.Text = "Aucune langue disponible";
            return;
        }
        var localization = _localizations[_activeGuideLanguage];
        var completed = Enumerable.Range(0, GuidePages.Length)
            .Count(index => !string.IsNullOrWhiteSpace(GuideValue(localization, index)));
        _guideProgress.Text = $"{completed} / {GuidePages.Length} pages renseignées";
        _guideProgress.AddThemeColorOverride("font_color", completed == GuidePages.Length
            ? StudioStyle.Success
            : StudioStyle.Accent);
    }

    private StudioProjectConfigurationDraft BuildGuideDraft()
    {
        SaveGuidePage();
        return _session.CaptureProjectConfiguration() with { Localizations = _localizations.ToArray() };
    }

    private void PreviewGuide()
    {
        var result = _session.PreviewProjectConfiguration(BuildGuideDraft());
        SetStatus(result.Succeeded ? $"{result.Message} · huit pages localisées" : result.Message,
            !result.Succeeded);
    }

    private void ApplyGuide()
    {
        var result = _session.ApplyProjectConfiguration(BuildGuideDraft());
        SetStatus(result.Succeeded ? $"{result.Message} · guide joueur mis à jour" : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshFromSession();
            ProjectChanged?.Invoke();
        }
    }

    private void PrepareGuideExample()
    {
        if (_activeGuideLanguage < 0 || _activeGuideLanguage >= _localizations.Count)
        {
            return;
        }
        var value = _localizations[_activeGuideLanguage];
        _localizations[_activeGuideLanguage] = value with
        {
            Description = string.IsNullOrWhiteSpace(value.Description)
                ? "Explorez une forêt oubliée, réveillez ses gardiens et choisissez ce qui mérite d’être sauvé."
                : value.Description,
            Installation = string.IsNullOrWhiteSpace(value.Installation)
                ? "Installez le module depuis le lanceur, puis choisissez Nouvelle partie."
                : value.Installation,
            Rules = string.IsNullOrWhiteSpace(value.Rules) ? "L’exploration et les choix priment sur le combat." : value.Rules,
            Controls = string.IsNullOrWhiteSpace(value.Controls) ? "Clavier, souris et manette sont pris en charge." : value.Controls,
            Credits = string.IsNullOrWhiteSpace(value.Credits) ? "Créé avec OpenRPG Forge." : value.Credits,
            Licenses = string.IsNullOrWhiteSpace(value.Licenses) ? "Les attributions sont incluses avec le module." : value.Licenses,
            Warnings = string.IsNullOrWhiteSpace(value.Warnings) ? "Violence fantastique modérée." : value.Warnings,
            Compatibility = string.IsNullOrWhiteSpace(value.Compatibility) ? "Compatible avec OpenRPG Forge 1.x." : value.Compatibility,
        };
        _activeGuidePage = 0;
        _guidePage.Select(0);
        LoadGuidePage();
        SetStatus("Aperçu local du guide préparé · projet inchangé");
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private static string GuideValue(StudioLocalizedProjectDraft value, int page) => page switch
    {
        0 => value.Description,
        1 => value.Installation,
        2 => value.Rules,
        3 => value.Controls,
        4 => value.Credits,
        5 => value.Licenses,
        6 => value.Warnings,
        _ => value.Compatibility,
    };

    private static StudioLocalizedProjectDraft WithGuideValue(
        StudioLocalizedProjectDraft value,
        int page,
        string text) => page switch
        {
            0 => value with { Description = text },
            1 => value with { Installation = text },
            2 => value with { Rules = text },
            3 => value with { Controls = text },
            4 => value with { Credits = text },
            5 => value with { Licenses = text },
            6 => value with { Warnings = text },
            _ => value with { Compatibility = text },
        };

    private static string LocaleLabel(string locale) => locale.ToLowerInvariant() switch
    {
        "fr" or "fr-fr" => "Français",
        "en" or "en-us" or "en-gb" => "English",
        "de" or "de-de" => "Deutsch",
        "es" or "es-es" => "Español",
        "it" or "it-it" => "Italiano",
        _ => $"Langue {locale}",
    };

    private static string KindLabel(ProjectVariableValueKind kind) => kind switch
    {
        ProjectVariableValueKind.Boolean => "Oui / non",
        ProjectVariableValueKind.Integer => "Nombre entier",
        ProjectVariableValueKind.Number => "Nombre décimal",
        _ => "Texte",
    };

    private static string ScopeLabel(ProjectVariableScope scope) => scope switch
    {
        ProjectVariableScope.Session => "Cette session",
        ProjectVariableScope.Save => "Sauvegarde",
        ProjectVariableScope.Player => "Chaque joueur",
        ProjectVariableScope.Party => "Groupe",
        ProjectVariableScope.Server => "Serveur",
        _ => "Projet",
    };

    private static string ReplicationLabel(ProjectVariableReplicationMode mode) => mode switch
    {
        ProjectVariableReplicationMode.Owner => "Joueur concerné",
        ProjectVariableReplicationMode.Party => "Membres du groupe",
        ProjectVariableReplicationMode.AllClients => "Tous les joueurs",
        ProjectVariableReplicationMode.ServerAuthority => "Serveur uniquement",
        _ => "Aucun partage",
    };

    private static string HumanizeVariableDiagnostic(string code) => code switch
    {
        ProjectVariableValidator.DuplicateIdCode or ProjectVariableValidator.DuplicateKeyCode =>
            "Deux variables portent le même nom. Choisissez un nom distinct.",
        ProjectVariableValidator.RequiredSessionCode =>
            "Une variable requise doit survivre à la session : choisissez Sauvegarde, Joueur, Groupe ou Serveur.",
        ProjectVariableValidator.ServerReplicationCode =>
            "Une valeur contrôlée par le serveur doit être conservée sur le Serveur.",
        _ => "La variable n’est pas valide. Vérifiez son type, sa conservation et son partage réseau.",
    };

    private static string HumanizeVariableException(ArgumentException exception)
    {
        if (exception.Message.Contains("boolean", StringComparison.OrdinalIgnoreCase))
            return "Pour Oui / non, saisissez true ou false.";
        if (exception.Message.Contains("integer", StringComparison.OrdinalIgnoreCase))
            return "Saisissez un nombre entier, sans texte ni décimales.";
        if (exception.Message.Contains("finite number", StringComparison.OrdinalIgnoreCase))
            return "Saisissez un nombre décimal fini.";
        if (exception.Message.Contains("at most", StringComparison.OrdinalIgnoreCase))
            return "La valeur est trop longue.";
        return exception.Message;
    }

    private static VBoxContainer Content(bool visible = true) => new()
    {
        Visible = visible,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill,
    };

    private static HBoxContainer Checkline(string text)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(StudioStyle.Label("✓", 13, StudioStyle.Success));
        var label = StudioStyle.Label(text, 11, StudioStyle.Text, wrap: true);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.CustomMinimumSize = new Vector2(280, 0);
        row.AddChild(label);
        return row;
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var caption = StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true);
        caption.CustomMinimumSize = new Vector2(152, 0);
        grid.AddChild(caption);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static LineEdit Edit(string placeholder, string tooltip)
    {
        var edit = new LineEdit { PlaceholderText = placeholder, TooltipText = tooltip, ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private static TextEdit Multiline(string placeholder, float height)
    {
        var edit = new TextEdit
        {
            PlaceholderText = placeholder,
            CustomMinimumSize = new Vector2(0, height),
            ScrollFitContentHeight = false,
        };
        edit.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 5));
        edit.AddThemeStyleboxOverride("focus", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Accent, 5, 2));
        edit.AddThemeColorOverride("font_color", StudioStyle.Text);
        edit.AddThemeColorOverride("font_placeholder_color", StudioStyle.Muted);
        edit.AddThemeColorOverride("caret_color", StudioStyle.Accent);
        edit.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        return edit;
    }

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton();
        StudioStyle.ApplyOptionButton(option);
        foreach (var label in labels) option.AddItem(label);
        return option;
    }
}
