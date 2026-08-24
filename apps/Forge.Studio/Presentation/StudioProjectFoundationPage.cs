using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal enum StudioProjectFoundationSection
{
    Identity,
    Globals,
    Events,
}

internal sealed partial class StudioProjectFoundationPage : Control
{
    private readonly StudioSessionController _session;
    private readonly ItemList _languages;
    private readonly LineEdit _newLocale;
    private readonly Button _removeLanguage;
    private readonly Label _identitySummary;
    private readonly Label _templateSummary;
    private readonly Label _status;
    private readonly Button _identityTab;
    private readonly Button _globalsTab;
    private readonly Button _eventsTab;
    private readonly VBoxContainer _identityContent;
    private readonly StudioProjectGlobalsPage _globalsPage;
    private readonly StudioProjectEventsPage _eventsPage;
    private readonly ScrollContainer _formScroll;
    private readonly VBoxContainer _advancedLocalization;
    private readonly VBoxContainer _playerStartSection;
    private readonly LineEdit _name;
    private readonly LineEdit _summary;
    private readonly TextEdit _description;
    private readonly TextEdit _credits;
    private readonly TextEdit _installation;
    private readonly TextEdit _rules;
    private readonly TextEdit _controls;
    private readonly TextEdit _licenses;
    private readonly TextEdit _warnings;
    private readonly TextEdit _compatibility;
    private readonly LineEdit _author;
    private readonly LineEdit _version;
    private readonly LineEdit _license;
    private readonly LineEdit _imagePath;
    private readonly LineEdit _tags;
    private readonly LineEdit _classification;
    private readonly CheckBox _coreKit;
    private readonly CheckBox _rpgKit;
    private readonly Button _extraKitsToggle;
    private readonly VBoxContainer _extraKitsPanel;
    private readonly LineEdit _extraKits;
    private readonly CheckBox _windows;
    private readonly CheckBox _linux;
    private readonly CheckBox _mac;
    private readonly OptionButton _rulesProvider;
    private readonly OptionButton _renderPreset;
    private readonly OptionButton _networkMode;
    private readonly OptionButton _startArea;
    private readonly OptionButton _startEntity;
    private readonly OptionButton _waypoint;
    private readonly SpinBox _orientation;
    private readonly OptionButton _gameMode;
    private readonly OptionButton _entryClass;
    private readonly OptionButton _selectionMode;
    private readonly Label _selectionValueLabel;
    private readonly LineEdit _selectionValue;
    private readonly List<StudioLocalizedProjectDraft> _localizations = [];
    private readonly List<AreaId> _areaIds = [];
    private readonly List<EntityId> _spawnIds = [];
    private readonly List<Guid?> _waypointIds = [];
    private readonly List<string> _rulesProviderValues = [];
    private readonly List<string> _gameModeValues = [];
    private readonly List<string> _entryClassValues = [];
    private int _activeLanguage = -1;
    private bool _loading;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? ProjectChanged;

    internal event Action<StudioProjectFoundationSection>? SectionChanged;

    internal int LanguageCount => _localizations.Count;

    internal bool ShowsEvents => _eventsPage.Visible;

    internal bool ShowsGlobals => _globalsPage.Visible;

    internal StudioProjectFoundationSection CurrentSection => _eventsPage.Visible
        ? StudioProjectFoundationSection.Events
        : _globalsPage.Visible
            ? StudioProjectFoundationSection.Globals
            : StudioProjectFoundationSection.Identity;

    internal bool ShowsHumanIdentity =>
        _identitySummary.Text.Contains("gérée automatiquement", StringComparison.OrdinalIgnoreCase) &&
        !_identitySummary.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_identitySummary.Text.Contains("hash", StringComparison.OrdinalIgnoreCase);

    internal StudioProjectFoundationPage(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioProjectFoundationPage";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = StudioStyle.Background, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margin = StudioStyle.Margin(10, 9, 10, 9);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var subnavigationPanel = StudioStyle.Panel("ProjectSubnavigation", StudioStyle.Chrome, 7);
        root.AddChild(subnavigationPanel);
        var subnavigationMargin = StudioStyle.Margin(6, 5, 6, 5);
        subnavigationPanel.AddChild(subnavigationMargin);
        var subnavigation = new HBoxContainer();
        subnavigation.AddThemeConstantOverride("separation", 6);
        subnavigationMargin.AddChild(subnavigation);
        _identityTab = StudioStyle.Button("●  Identité et configuration", compact: true);
        _identityTab.TooltipText = "Nom, langues, publication, kits et point de départ";
        subnavigation.AddChild(_identityTab);
        _globalsTab = StudioStyle.Button("Données et guide joueur", compact: true);
        _globalsTab.TooltipText = "Variables typées, cache ForgeScript et huit pages destinées aux joueurs";
        subnavigation.AddChild(_globalsTab);
        _eventsTab = StudioStyle.Button("Événements du module", compact: true);
        _eventsTab.TooltipText = "Chargement, connexions, inventaire, utilisation, mort et réapparition";
        subnavigation.AddChild(_eventsTab);
        var subnavigationHint = StudioStyle.Label(
            "Une seule surface de travail · changements prévisualisables et annulables",
            9,
            StudioStyle.Muted);
        subnavigationHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        subnavigationHint.HorizontalAlignment = HorizontalAlignment.Right;
        subnavigation.AddChild(subnavigationHint);

        _identityContent = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _identityContent.AddThemeConstantOverride("separation", 8);
        root.AddChild(_identityContent);

        var banner = StudioStyle.Panel("ProjectIdentityBanner", StudioStyle.AccentDark, 8);
        _identityContent.AddChild(banner);
        var bannerMargin = StudioStyle.Margin(10, 7, 10, 7);
        banner.AddChild(bannerMargin);
        var bannerRow = new HBoxContainer();
        bannerMargin.AddChild(bannerRow);
        var bannerText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        bannerText.AddChild(StudioStyle.Label("Projet lisible par les auteurs", 15));
        _identitySummary = StudioStyle.Label(string.Empty, 10, StudioStyle.Success, wrap: true);
        bannerText.AddChild(_identitySummary);
        bannerRow.AddChild(bannerText);
        _templateSummary = StudioStyle.Label(string.Empty, 11, StudioStyle.Accent);
        bannerRow.AddChild(_templateSummary);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 8);
        _identityContent.AddChild(body);

        var languagePanel = StudioStyle.Panel("ProjectLanguages", StudioStyle.Chrome, 8);
        languagePanel.CustomMinimumSize = new Vector2(224, 0);
        body.AddChild(languagePanel);
        var languageMargin = StudioStyle.Margin(9, 9, 9, 9);
        languagePanel.AddChild(languageMargin);
        var languageColumn = new VBoxContainer();
        languageColumn.AddThemeConstantOverride("separation", 6);
        languageMargin.AddChild(languageColumn);
        languageColumn.AddChild(StudioStyle.SectionLabel("LANGUES DU PROJET"));
        languageColumn.AddChild(StudioStyle.Label(
            "Chaque langue conserve son nom, son résumé, sa description et ses crédits.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _languages = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0, 140),
        };
        _languages.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        _languages.ItemSelected += SelectLanguage;
        languageColumn.AddChild(_languages);
        _newLocale = Edit("fr-FR", "Code de langue, par exemple en ou fr-FR");
        languageColumn.AddChild(_newLocale);
        var languageActions = new HBoxContainer();
        languageActions.AddThemeConstantOverride("separation", 4);
        var addLanguage = StudioStyle.Button("Ajouter", compact: true);
        addLanguage.Pressed += AddLanguage;
        languageActions.AddChild(addLanguage);
        _removeLanguage = StudioStyle.Button("Retirer", compact: true);
        _removeLanguage.Pressed += RemoveLanguage;
        languageActions.AddChild(_removeLanguage);
        languageColumn.AddChild(languageActions);
        languageColumn.AddChild(StudioStyle.Label(
            "La langue principale reste toujours disponible.",
            9,
            StudioStyle.Muted,
            wrap: true));

        var formPanel = StudioStyle.Panel("ProjectForm", StudioStyle.Surface, 8);
        formPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        formPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(formPanel);
        _formScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        formPanel.AddChild(_formScroll);
        var formMargin = StudioStyle.Margin(14, 12, 14, 12);
        formMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _formScroll.AddChild(formMargin);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8);
        formMargin.AddChild(form);

        form.AddChild(StudioStyle.SectionLabel("PRÉSENTATION DANS LA LANGUE SÉLECTIONNÉE"));
        var localizedGrid = Grid();
        form.AddChild(localizedGrid);
        _name = AddField(localizedGrid, "Nom public", Edit("Mon aventure", "Nom affiché aux joueurs"));
        _summary = AddField(localizedGrid, "Résumé", Edit("Une aventure…", "Promesse courte du projet"));
        _description = AddField(localizedGrid, "Description", Multiline("Description complète", 84));
        _credits = AddField(localizedGrid, "Crédits", Multiline("Contributeurs et attributions", 66));
        _advancedLocalization = new VBoxContainer { Visible = false };
        _advancedLocalization.AddThemeConstantOverride("separation", 6);
        var advancedToggle = StudioStyle.Button("Afficher les textes avancés", compact: true);
        advancedToggle.Pressed += () =>
        {
            _advancedLocalization.Visible = !_advancedLocalization.Visible;
            advancedToggle.Text = _advancedLocalization.Visible
                ? "Masquer les textes avancés"
                : "Afficher les textes avancés";
        };
        form.AddChild(advancedToggle);
        form.AddChild(_advancedLocalization);
        var advancedGrid = Grid();
        _advancedLocalization.AddChild(advancedGrid);
        _installation = AddField(advancedGrid, "Installation", Multiline("Instructions d’installation", 58));
        _rules = AddField(advancedGrid, "Règles", Multiline("Règles particulières", 58));
        _controls = AddField(advancedGrid, "Contrôles", Multiline("Clavier, souris et manette", 58));
        _licenses = AddField(advancedGrid, "Licences détaillées", Multiline("Attributions par contenu", 58));
        _warnings = AddField(advancedGrid, "Avertissements", Multiline("Contenu sensible ou limites", 58));
        _compatibility = AddField(advancedGrid, "Compatibilité", Multiline("Versions ou extensions attendues", 58));

        form.AddChild(Separator());
        form.AddChild(StudioStyle.SectionLabel("PUBLICATION ET CLASSEMENT"));
        var publicationGrid = Grid();
        form.AddChild(publicationGrid);
        _author = AddField(publicationGrid, "Auteur ou équipe", Edit("Atelier", "Nom public de l’auteur"));
        _version = AddField(publicationGrid, "Version", Edit("0.1.0", "Format majeur.mineur.correctif"));
        _license = AddField(publicationGrid, "Licence", Edit("UNLICENSED", "Exemple : CC-BY-4.0"));
        _imagePath = AddField(publicationGrid, "Image du projet", Edit("assets/cover.png", "Chemin interne facultatif"));
        _tags = AddField(publicationGrid, "Mots-clés", Edit("aventure, coopération", "Séparés par des virgules"));
        _classification = AddField(publicationGrid, "Classification", Edit("Tout public", "Public visé ou classement"));

        form.AddChild(Separator());
        form.AddChild(StudioStyle.SectionLabel("SOCLE TECHNIQUE CHOISI PAR L’AUTEUR"));
        var choices = new HBoxContainer();
        choices.AddThemeConstantOverride("separation", 16);
        form.AddChild(choices);
        var kits = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        kits.AddChild(StudioStyle.Label("Kits de fonctions", 11));
        _coreKit = Check("Socle OpenRPG Forge");
        _rpgKit = Check("Fonctions RPG");
        kits.AddChild(_coreKit);
        kits.AddChild(_rpgKit);
        _extraKitsToggle = StudioStyle.Button("Extensions avancées", compact: true);
        kits.AddChild(_extraKitsToggle);
        _extraKitsPanel = new VBoxContainer { Visible = false };
        _extraKitsPanel.AddThemeConstantOverride("separation", 4);
        _extraKitsPanel.AddChild(StudioStyle.Label(
            "Identifiants d’extensions réservés aux intégrations avancées.",
            9,
            StudioStyle.Muted,
            wrap: true));
        _extraKits = Edit(string.Empty, "Kits supplémentaires (avancé)");
        _extraKitsPanel.AddChild(_extraKits);
        kits.AddChild(_extraKitsPanel);
        _extraKitsToggle.Pressed += () =>
        {
            _extraKitsPanel.Visible = !_extraKitsPanel.Visible;
            _extraKitsToggle.Text = _extraKitsPanel.Visible
                ? "Masquer les extensions avancées"
                : BuildExtraKitsToggleText();
        };
        choices.AddChild(kits);
        var platforms = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        platforms.AddChild(StudioStyle.Label("Plateformes visées", 11));
        _windows = Check("Windows 64 bits");
        _linux = Check("Linux 64 bits");
        _mac = Check("macOS Apple Silicon");
        platforms.AddChild(_windows);
        platforms.AddChild(_linux);
        platforms.AddChild(_mac);
        choices.AddChild(platforms);
        var technicalGrid = Grid();
        form.AddChild(technicalGrid);
        _rulesProvider = AddField(technicalGrid, "Famille de règles", Option(
            "Règles RPG génériques", "Socle sans règles de jeu"));
        _renderPreset = AddField(technicalGrid, "Qualité de rendu", Option(
            "Équilibrée", "Qualité élevée", "Performances"));
        _networkMode = AddField(technicalGrid, "Mode réseau", Option(
            "Hors ligne", "Partie hébergée", "Serveur dédié"));

        form.AddChild(Separator());
        _playerStartSection = new VBoxContainer();
        _playerStartSection.AddThemeConstantOverride("separation", 7);
        form.AddChild(_playerStartSection);
        _playerStartSection.AddChild(StudioStyle.SectionLabel("POINT DE DÉPART DU JOUEUR"));
        _playerStartSection.AddChild(StudioStyle.Label(
            "Les listes utilisent les noms visibles des zones et des points de départ ; leurs identités internes restent gérées par le Studio.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var startGrid = Grid();
        _playerStartSection.AddChild(startGrid);
        _startArea = AddField(startGrid, "Zone de départ", Option("Aucune zone"));
        _startArea.ItemSelected += SelectStartArea;
        _startEntity = AddField(startGrid, "Point de départ", Option("Aucun point"));
        _waypoint = AddField(startGrid, "Waypoint facultatif", Option("Position du point de départ"));
        _orientation = AddField(startGrid, "Orientation en degrés", new SpinBox
        {
            MinValue = 0,
            MaxValue = 359,
            Step = 1,
            Suffix = "°",
        });
        StudioStyle.ApplySpinBox(_orientation);
        _gameMode = AddField(startGrid, "Mode de jeu", Option("Campagne", "Coopération", "Solo"));
        _entryClass = AddField(startGrid, "Rôle à l’entrée", Option("Aventurier", "Spectateur", "Tous les rôles"));
        _selectionMode = AddField(startGrid, "Joueurs concernés", Option(
            "Tous les joueurs", "Un joueur nommé", "Les joueurs portant un tag"));
        _selectionMode.ItemSelected += _ => UpdateSelectionValue();
        _selectionValueLabel = StudioStyle.Label("Nom ou tag", 10, StudioStyle.Muted, wrap: true);
        _selectionValueLabel.CustomMinimumSize = new Vector2(148, 0);
        startGrid.AddChild(_selectionValueLabel);
        _selectionValue = Edit(string.Empty, "Utilisé seulement pour une sélection ciblée");
        _selectionValue.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        startGrid.AddChild(_selectionValue);

        form.AddChild(Separator());
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        var preview = StudioStyle.Button("Prévisualiser sans modifier", compact: true);
        preview.Pressed += PreviewConfiguration;
        actions.AddChild(preview);
        var apply = StudioStyle.Button("Appliquer au projet", primary: true, compact: true);
        apply.Pressed += ApplyConfiguration;
        actions.AddChild(apply);
        _status = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.CustomMinimumSize = new Vector2(300, 0);
        actions.AddChild(_status);
        form.AddChild(actions);

        _eventsPage = new StudioProjectEventsPage(_session)
        {
            Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _eventsPage.StatusChanged += (message, error) => StatusChanged?.Invoke(message, error);
        _eventsPage.ProjectChanged += () =>
        {
            RefreshFromSession();
            ProjectChanged?.Invoke();
        };
        root.AddChild(_eventsPage);
        _globalsPage = new StudioProjectGlobalsPage(_session)
        {
            Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _globalsPage.StatusChanged += (message, error) => StatusChanged?.Invoke(message, error);
        _globalsPage.ProjectChanged += () =>
        {
            RefreshFromSession();
            ProjectChanged?.Invoke();
        };
        root.AddChild(_globalsPage);
        _identityTab.Pressed += () => ShowProjectSection(StudioProjectFoundationSection.Identity);
        _globalsTab.Pressed += () => ShowProjectSection(StudioProjectFoundationSection.Globals);
        _eventsTab.Pressed += () => ShowProjectSection(StudioProjectFoundationSection.Events);

        RefreshFromSession();
    }

    internal void RefreshFromSession()
    {
        if (!_session.IsOpen || _session.Project is null)
        {
            return;
        }
        _loading = true;
        var draft = _session.CaptureProjectConfiguration();
        _localizations.Clear();
        _localizations.AddRange(draft.Localizations);
        PopulateLanguages(draft.DefaultLocale);
        _author.Text = draft.Author;
        _version.Text = draft.Version;
        _license.Text = draft.License;
        _imagePath.Text = draft.ImagePath ?? string.Empty;
        _tags.Text = string.Join(", ", draft.Tags);
        _classification.Text = draft.Classification;
        _coreKit.ButtonPressed = draft.Kits.Contains("forge-core", StringComparer.Ordinal);
        _rpgKit.ButtonPressed = draft.Kits.Contains("forge-rpg-kit", StringComparer.Ordinal);
        _extraKits.Text = string.Join(", ", draft.Kits.Where(item => item is not "forge-core" and not "forge-rpg-kit"));
        _extraKitsPanel.Visible = false;
        _extraKitsToggle.Text = BuildExtraKitsToggleText();
        _windows.ButtonPressed = draft.TargetPlatforms.Contains(ModuleDistributionPlatform.WindowsX64);
        _linux.ButtonPressed = draft.TargetPlatforms.Contains(ModuleDistributionPlatform.LinuxX64);
        _mac.ButtonPressed = draft.TargetPlatforms.Contains(ModuleDistributionPlatform.MacOSArm64);
        PopulateSemanticOption(
            _rulesProvider,
            _rulesProviderValues,
            draft.RulesProvider,
            [("forge.generic", "Règles RPG génériques"), ("forge.core", "Socle sans règles de jeu")]);
        SelectOption(_renderPreset, (int)draft.RenderPreset);
        SelectOption(_networkMode, (int)draft.NetworkMode);
        PopulateStartAreas(draft.PlayerStartAreaId, draft.PlayerStartEntityId, draft.PlayerStartWaypointId);
        _orientation.Value = draft.PlayerOrientationDegrees;
        PopulateSemanticOption(
            _gameMode,
            _gameModeValues,
            draft.GameMode,
            [("campaign", "Campagne"), ("coop", "Coopération"), ("solo", "Solo")]);
        PopulateSemanticOption(
            _entryClass,
            _entryClassValues,
            draft.EntryClass,
            [("adventurer", "Aventurier"), ("spectator", "Spectateur"), ("any", "Tous les rôles")]);
        if (draft.PlayerSelectionRule.StartsWith("player:", StringComparison.Ordinal))
        {
            _selectionMode.Select(1);
            _selectionValue.Text = draft.PlayerSelectionRule[7..];
        }
        else if (draft.PlayerSelectionRule.StartsWith("tag:", StringComparison.Ordinal))
        {
            _selectionMode.Select(2);
            _selectionValue.Text = draft.PlayerSelectionRule[4..];
        }
        else
        {
            _selectionMode.Select(0);
            _selectionValue.Text = string.Empty;
        }
        var foundation = _session.Project.Module.Metadata.Foundation;
        var template = foundation?.Template ?? (_session.Project.Areas.Count == 0
            ? ProjectTemplateKind.CoreSimulation
            : ProjectTemplateKind.Rpg);
        _templateSummary.Text = template switch
        {
            ProjectTemplateKind.Rpg => "AVENTURE RPG",
            ProjectTemplateKind.CoreMenu => "INTERFACE / MENU",
            _ => "SIMULATION",
        };
        _playerStartSection.Visible = template == ProjectTemplateKind.Rpg;
        _identitySummary.Text =
            $"Identité gérée automatiquement · compatibilité 1.x · révision de travail {_session.History?.State.Revision.Value ?? 0}";
        UpdateSelectionValue();
        SelectLanguage(Math.Max(0, _activeLanguage));
        _loading = false;
        _eventsPage.RefreshFromSession();
        _globalsPage.RefreshFromSession();
        SetStatus("Données réelles chargées · aucune modification appliquée");
    }

    internal bool PrepareEvidence(string mode)
    {
        ShowProjectSection(StudioProjectFoundationSection.Identity);
        RefreshFromSession();
        if (string.Equals(mode, "ux0-e-localization", StringComparison.OrdinalIgnoreCase))
        {
            if (_localizations.All(item => !string.Equals(item.Locale, "en", StringComparison.OrdinalIgnoreCase)))
            {
                _localizations.Add(new StudioLocalizedProjectDraft(
                    "en", "Crypt of Ashes", "A besieged crypt adventure", "Localized preview prepared without applying.",
                    "OpenRPG Forge", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    "OpenRPG Forge 1.x"));
                PopulateLanguages(_session.Project!.Module.Metadata.DefaultLocale);
            }
            SelectLanguage(_localizations.FindIndex(item => item.Locale == "en"));
            return LanguageCount >= 2 && ShowsHumanIdentity;
        }
        if (string.Equals(mode, "ux0-e-player-start", StringComparison.OrdinalIgnoreCase))
        {
            _formScroll.ScrollVertical = 100_000;
            _formScroll.SetDeferred("scroll_vertical", 100_000);
            return _playerStartSection.Visible && _startArea.ItemCount > 0 && _startEntity.ItemCount > 0;
        }
        _formScroll.ScrollVertical = 0;
        return LanguageCount > 0 && ShowsHumanIdentity;
    }

    internal bool PrepareEventEvidence(string mode)
    {
        ShowProjectSection(StudioProjectFoundationSection.Events);
        return _eventsPage.PrepareEvidence(mode);
    }

    internal bool PrepareGlobalsEvidence(string mode)
    {
        ShowProjectSection(StudioProjectFoundationSection.Globals);
        return _globalsPage.PrepareEvidence(mode);
    }

    internal bool RunEventTransactionEvidence() => _eventsPage.RunTransactionEvidence();

    internal bool RunUx0GEventTransactionEvidence() => _eventsPage.RunUx0GTransactionEvidence();

    internal bool RunUx0HEventTransactionEvidence() => _eventsPage.RunUx0HTransactionEvidence();

    internal bool RunUx0IGlobalsTransactionEvidence() => _globalsPage.RunTransactionEvidence();

    internal int Ux0IVariableCount => _globalsPage.VariableCount;

    internal int Ux0IGuidePageCount => _globalsPage.GuidePageCount;

    internal int Ux0IScriptCount => _globalsPage.ScriptCount;

    internal bool ShowsHumanGlobalsSurface => _globalsPage.ShowsHumanSurface;

    internal void PreviewActiveConfiguration()
    {
        if (_eventsPage.Visible)
        {
            _eventsPage.PreviewActiveConfiguration();
            return;
        }
        if (_globalsPage.Visible)
        {
            _globalsPage.PreviewActiveConfiguration();
            return;
        }
        PreviewConfiguration();
    }

    internal int EventCardCount => _eventsPage.CardCount;

    internal int EventBindingRowCount => _eventsPage.BindingRowCount;

    internal int Ux0FEventCardCount => _eventsPage.Ux0FCardCount;

    internal int Ux0FEventBindingRowCount => _eventsPage.Ux0FBindingRowCount;

    internal int EventScriptChoiceCount => _eventsPage.ScriptChoiceCount;

    internal bool ShowsHumanEventSurface => _eventsPage.ShowsHumanSurface;

    internal void PreviewConfiguration()
    {
        var result = _session.PreviewProjectConfiguration(BuildDraft());
        SetStatus(result.Succeeded ? $"{result.Message} · {result.Summary}" : result.Message, !result.Succeeded);
    }

    internal bool PreservesSemanticChoices(string rulesProvider, string gameMode, string entryClass)
    {
        var draft = BuildDraft();
        return string.Equals(draft.RulesProvider, rulesProvider, StringComparison.Ordinal) &&
               string.Equals(draft.GameMode, gameMode, StringComparison.Ordinal) &&
               string.Equals(draft.EntryClass, entryClass, StringComparison.Ordinal) &&
               _rulesProvider.GetItemText(_rulesProvider.Selected) == "Profil existant (préservé)" &&
               _gameMode.GetItemText(_gameMode.Selected) == "Profil existant (préservé)" &&
               _entryClass.GetItemText(_entryClass.Selected) == "Profil existant (préservé)";
    }

    internal bool RunTransactionEvidence()
    {
        var original = _session.Project;
        var originalAuthor = _author.Text;
        _author.Text = originalAuthor + " · UX0-E UI";
        var draft = BuildDraft();
        var preview = _session.PreviewProjectConfiguration(draft);
        var previewDidNotMutate = ReferenceEquals(original, _session.Project);
        var applied = _session.ApplyProjectConfiguration(draft);
        var formReachedApplication = applied.Succeeded &&
                                     _session.Project?.Module.Metadata.Author.EndsWith("UX0-E UI", StringComparison.Ordinal) == true;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(original, _session.Project);
        RefreshFromSession();
        return preview.Succeeded && previewDidNotMutate && formReachedApplication && restored && !_session.IsDirty;
    }

    private void ApplyConfiguration()
    {
        var result = _session.ApplyProjectConfiguration(BuildDraft());
        SetStatus(result.Succeeded ? $"{result.Message} · {result.Summary}" : result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshFromSession();
            ProjectChanged?.Invoke();
        }
    }

    private void ShowProjectSection(StudioProjectFoundationSection section)
    {
        _identityContent.Visible = section == StudioProjectFoundationSection.Identity;
        _globalsPage.Visible = section == StudioProjectFoundationSection.Globals;
        _eventsPage.Visible = section == StudioProjectFoundationSection.Events;
        _identityTab.Text = section == StudioProjectFoundationSection.Identity
            ? "●  Identité et configuration"
            : "Identité et configuration";
        _globalsTab.Text = section == StudioProjectFoundationSection.Globals
            ? "●  Données et guide joueur"
            : "Données et guide joueur";
        _eventsTab.Text = section == StudioProjectFoundationSection.Events
            ? "●  Événements du module"
            : "Événements du module";
        SectionChanged?.Invoke(section);
    }

    private StudioProjectConfigurationDraft BuildDraft()
    {
        SaveActiveLocalization();
        var kits = new List<string>();
        if (_coreKit.ButtonPressed) kits.Add("forge-core");
        if (_rpgKit.ButtonPressed) kits.Add("forge-rpg-kit");
        kits.AddRange(SplitValues(_extraKits.Text));
        var platforms = new List<ModuleDistributionPlatform>();
        if (_windows.ButtonPressed) platforms.Add(ModuleDistributionPlatform.WindowsX64);
        if (_linux.ButtonPressed) platforms.Add(ModuleDistributionPlatform.LinuxX64);
        if (_mac.ButtonPressed) platforms.Add(ModuleDistributionPlatform.MacOSArm64);
        var areaId = _startArea.Selected >= 0 && _startArea.Selected < _areaIds.Count
            ? _areaIds[_startArea.Selected]
            : null;
        var spawnId = _startEntity.Selected >= 0 && _startEntity.Selected < _spawnIds.Count
            ? _spawnIds[_startEntity.Selected]
            : null;
        var waypointId = _waypoint.Selected >= 0 && _waypoint.Selected < _waypointIds.Count
            ? _waypointIds[_waypoint.Selected]
            : null;
        var selection = _selectionMode.Selected switch
        {
            1 => "player:" + NormalizeKey(_selectionValue.Text, "player"),
            2 => "tag:" + NormalizeKey(_selectionValue.Text, "tag"),
            _ => "default",
        };
        return new StudioProjectConfigurationDraft(
            _session.Project!.Module.Metadata.DefaultLocale,
            _localizations.ToArray(),
            _author.Text,
            _version.Text,
            _license.Text,
            string.IsNullOrWhiteSpace(_imagePath.Text) ? null : _imagePath.Text,
            SplitValues(_tags.Text),
            _classification.Text,
            kits.Distinct(StringComparer.Ordinal).ToArray(),
            platforms.Distinct().ToArray(),
            SelectedSemanticValue(_rulesProvider, _rulesProviderValues, "forge.generic"),
            (ProjectRenderPreset)Math.Clamp(_renderPreset.Selected, 0, 2),
            (ProjectNetworkMode)Math.Clamp(_networkMode.Selected, 0, 2),
            areaId,
            spawnId,
            waypointId,
            (float)_orientation.Value,
            SelectedSemanticValue(_gameMode, _gameModeValues, "campaign"),
            SelectedSemanticValue(_entryClass, _entryClassValues, "adventurer"),
            selection);
    }

    private void PopulateLanguages(string defaultLocale)
    {
        _languages.Clear();
        foreach (var localization in _localizations)
        {
            _languages.AddItem(localization.Locale +
                               (string.Equals(localization.Locale, defaultLocale, StringComparison.OrdinalIgnoreCase)
                                   ? " · principale"
                                   : string.Empty));
        }
        _activeLanguage = Math.Clamp(
            _localizations.FindIndex(item => string.Equals(item.Locale, defaultLocale, StringComparison.OrdinalIgnoreCase)),
            0,
            Math.Max(0, _localizations.Count - 1));
        if (_localizations.Count > 0)
        {
            _languages.Select(_activeLanguage);
        }
    }

    private void SelectLanguage(long index)
    {
        if (index < 0 || index >= _localizations.Count)
        {
            return;
        }
        if (!_loading)
        {
            SaveActiveLocalization();
        }
        _activeLanguage = (int)index;
        _languages.Select(_activeLanguage);
        var value = _localizations[_activeLanguage];
        _name.Text = value.Name;
        _summary.Text = value.Summary;
        _description.Text = value.Description;
        _credits.Text = value.Credits;
        _installation.Text = value.Installation;
        _rules.Text = value.Rules;
        _controls.Text = value.Controls;
        _licenses.Text = value.Licenses;
        _warnings.Text = value.Warnings;
        _compatibility.Text = value.Compatibility;
        _removeLanguage.Disabled = string.Equals(
            value.Locale,
            _session.Project!.Module.Metadata.DefaultLocale,
            StringComparison.OrdinalIgnoreCase);
    }

    private void SaveActiveLocalization()
    {
        if (_activeLanguage < 0 || _activeLanguage >= _localizations.Count)
        {
            return;
        }
        var current = _localizations[_activeLanguage];
        _localizations[_activeLanguage] = current with
        {
            Name = _name.Text,
            Summary = _summary.Text,
            Description = _description.Text,
            Credits = _credits.Text,
            Installation = _installation.Text,
            Rules = _rules.Text,
            Controls = _controls.Text,
            Licenses = _licenses.Text,
            Warnings = _warnings.Text,
            Compatibility = _compatibility.Text,
        };
    }

    private void AddLanguage()
    {
        SaveActiveLocalization();
        var locale = _newLocale.Text.Trim();
        if (locale.Length == 0 || _localizations.Any(item =>
                string.Equals(item.Locale, locale, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus("Saisissez une langue absente de la liste, par exemple en ou fr-FR.", error: true);
            return;
        }
        try
        {
            _ = new ProjectLocalizedMetadata(locale, "Nouveau nom", string.Empty, string.Empty, string.Empty);
        }
        catch (ArgumentException)
        {
            SetStatus("Le code de langue doit ressembler à fr, en ou fr-FR.", error: true);
            return;
        }
        _localizations.Add(new StudioLocalizedProjectDraft(
            locale,
            "Nouveau nom",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty));
        PopulateLanguages(_session.Project!.Module.Metadata.DefaultLocale);
        SelectLanguage(_localizations.Count - 1);
        _newLocale.Text = string.Empty;
        SetStatus("Langue ajoutée au formulaire · appliquez pour l’enregistrer");
    }

    private void RemoveLanguage()
    {
        if (_activeLanguage < 0 || _activeLanguage >= _localizations.Count || _removeLanguage.Disabled)
        {
            return;
        }
        _localizations.RemoveAt(_activeLanguage);
        PopulateLanguages(_session.Project!.Module.Metadata.DefaultLocale);
        SelectLanguage(Math.Max(0, _activeLanguage - 1));
        SetStatus("Langue retirée du formulaire · appliquez pour confirmer");
    }

    private void PopulateStartAreas(AreaId? selectedAreaId, EntityId? selectedEntityId, Guid? selectedWaypointId)
    {
        _areaIds.Clear();
        _startArea.Clear();
        var selected = 0;
        foreach (var area in _session.Project!.Areas)
        {
            if (!HasPlayerSpawn(area))
            {
                continue;
            }
            _areaIds.Add(area.Id);
            _startArea.AddItem(area.DisplayName);
            if (area.Id == selectedAreaId) selected = _areaIds.Count - 1;
        }
        if (_areaIds.Count == 0)
        {
            _startArea.AddItem("Aucune zone avec un point de départ");
            _startArea.Disabled = true;
            _startEntity.Clear();
            _startEntity.AddItem("Ajoutez d’abord un point de départ");
            _startEntity.Disabled = true;
            _waypoint.Clear();
            _waypoint.AddItem("Aucun waypoint disponible");
            _waypoint.Disabled = true;
            return;
        }
        _startArea.Disabled = false;
        _startArea.Select(selected);
        PopulateStartEntities(_areaIds[selected], selectedEntityId, selectedWaypointId);
    }

    private void SelectStartArea(long index)
    {
        if (_loading || index < 0 || index >= _areaIds.Count)
        {
            return;
        }
        PopulateStartEntities(_areaIds[(int)index], null, null);
    }

    private void PopulateStartEntities(AreaId areaId, EntityId? selectedEntityId, Guid? selectedWaypointId)
    {
        _spawnIds.Clear();
        _startEntity.Clear();
        var area = _session.Project!.Areas.Single(item => item.Id == areaId);
        var selected = 0;
        foreach (var entity in area.Entities)
        {
            var blueprint = _session.Project.Blueprints.FirstOrDefault(item => item.Id == entity.BlueprintId);
            if (blueprint?.Kind != BlueprintKind.PlayerSpawn)
            {
                continue;
            }
            _spawnIds.Add(entity.Id);
            _startEntity.AddItem(blueprint.DisplayName);
            if (entity.Id == selectedEntityId) selected = _spawnIds.Count - 1;
        }
        _startEntity.Disabled = _spawnIds.Count == 0;
        if (_spawnIds.Count > 0) _startEntity.Select(selected);
        PopulateWaypoints(area, selectedWaypointId);
    }

    private void PopulateWaypoints(AreaDefinition area, Guid? selectedWaypointId)
    {
        _waypointIds.Clear();
        _waypoint.Clear();
        _waypointIds.Add(null);
        _waypoint.AddItem("Utiliser directement le point de départ");
        var definitions = area.Navigation.Anchors.Definitions.ToDictionary(item => item.Id);
        var selected = 0;
        foreach (var instance in area.Navigation.Anchors.Instances)
        {
            if (!definitions.TryGetValue(instance.DefinitionId, out var definition) ||
                definition.Kind != AnchorKind.Waypoint)
            {
                continue;
            }
            _waypointIds.Add(instance.Id);
            _waypoint.AddItem(definition.Label.Fallback);
            if (instance.Id == selectedWaypointId) selected = _waypointIds.Count - 1;
        }
        _waypoint.Disabled = _waypointIds.Count == 1;
        _waypoint.Select(selected);
    }

    private bool HasPlayerSpawn(AreaDefinition area) => area.Entities.Any(entity =>
        _session.Project!.Blueprints.FirstOrDefault(blueprint => blueprint.Id == entity.BlueprintId)?.Kind ==
        BlueprintKind.PlayerSpawn);

    private void UpdateSelectionValue()
    {
        _selectionValueLabel.Visible = _selectionMode.Selected > 0;
        _selectionValue.Visible = _selectionMode.Selected > 0;
        _selectionValue.Editable = _selectionMode.Selected > 0;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private static string[] SplitValues(string value) => value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => item.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private string BuildExtraKitsToggleText()
    {
        var count = SplitValues(_extraKits.Text).Length;
        return count == 0 ? "Extensions avancées" : $"Extensions avancées · {count} préservée(s)";
    }

    private static string NormalizeKey(string value, string fallback)
    {
        var normalized = string.Join('-', value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '-'));
        normalized = normalized.Trim('-');
        return normalized.Length == 0 ? fallback : normalized;
    }

    private static string SelectedSemanticValue(
        OptionButton option,
        IReadOnlyList<string> values,
        string fallback) =>
        option.Selected >= 0 && option.Selected < values.Count ? values[option.Selected] : fallback;

    private static void PopulateSemanticOption(
        OptionButton option,
        List<string> values,
        string current,
        IReadOnlyList<(string Value, string Label)> known)
    {
        option.Clear();
        values.Clear();
        foreach (var (value, label) in known)
        {
            values.Add(value);
            option.AddItem(label);
        }
        var index = values.FindIndex(item => string.Equals(item, current, StringComparison.Ordinal));
        if (index < 0)
        {
            values.Add(current);
            option.AddItem("Profil existant (préservé)");
            index = values.Count - 1;
        }
        option.Select(index);
    }

    private static void SelectOption(OptionButton option, int index) =>
        option.Select(Math.Clamp(index, 0, Math.Max(0, option.ItemCount - 1)));

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var caption = StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true);
        caption.CustomMinimumSize = new Vector2(148, 0);
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

    private static CheckBox Check(string label) => new()
    {
        Text = label,
        FocusMode = FocusModeEnum.All,
    };

    private static HSeparator Separator()
    {
        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator", StudioStyle.Border);
        return separator;
    }
}
