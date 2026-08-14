#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Packages;
using Godot;

namespace Forge.Toolset.Editor;

public partial class DistributionProfilesWindow : Window
{
    private readonly List<ModuleTargetProfile> _profiles = [];
    private readonly List<ThirdPartyDependencyApproval> _dependencies = [];
    private readonly ModuleTargetKind[] _targets = Enum.GetValues<ModuleTargetKind>();

    private Func<ProjectState?>? _getState;
    private Func<ModuleDistributionConfiguration, bool>? _apply;
    private Action<ModuleTargetKind>? _selectExportTarget;
    private Action? _createGameLauncher;
    private Action<Window>? _brandWindow;
    private ModuleDistributionConfigurationId? _configurationId;
    private ModuleReleaseConfiguration? _release;
    private GameLauncherConfiguration? _launcher;
    private ThirdPartyDependencyId? _dependencyId;
    private ModuleDistributionConfiguration? _approvedConfiguration;
    private bool _suppressSignals;

    private OptionButton? _target;
    private CheckButton? _enabled;
    private CheckButton? _editorData;
    private CheckButton? _debugSymbols;
    private CheckButton? _optimize;
    private CheckButton? _approvedLicenses;
    private LineEdit? _outputName;
    private LineEdit? _secretsProfile;
    private ItemList? _dependencyList;
    private LineEdit? _dependencyName;
    private LineEdit? _dependencyVersion;
    private LineEdit? _dependencyLicense;
    private LineEdit? _dependencyOwner;
    private LineEdit? _dependencySource;
    private LineEdit? _dependencyCadence;
    private LineEdit? _dependencyTargets;
    private OptionButton? _dependencyApprovalState;
    private OptionButton? _dependencyRisk;
    private LineEdit? _dependencyReviewers;
    private LineEdit? _dependencyRequiredApprovals;
    private LineEdit? _dependencySecurityAudit;
    private TextEdit? _dependencyRationale;
    private TextEdit? _dependencyRemovalPlan;
    private OptionButton? _releaseChannel;
    private LineEdit? _releasePlatforms;
    private OptionButton? _signingMode;
    private LineEdit? _signingProfile;
    private LineEdit? _trustedKeys;
    private LineEdit? _revokedKeys;
    private CheckButton? _allowUnsignedDevelopment;
    private CheckButton? _deltaEnabled;
    private LineEdit? _deltaChunkKilobytes;
    private CheckButton? _deltaResume;
    private CheckButton? _deltaDeduplicate;
    private CheckButton? _deltaRollback;
    private LineEdit? _deltaRetainedVersions;
    private OptionButton? _windowsMode;
    private LineEdit? _windowsPrerequisites;
    private CheckButton? _windowsDetectPrerequisites;
    private CheckButton? _windowsInstallLog;
    private CheckButton? _windowsUninstaller;
    private CheckButton? _launcherEnabled;
    private LineEdit? _launcherDisplayName;
    private LineEdit? _launcherTagline;
    private LineEdit? _launcherExecutableName;
    private LineEdit? _launcherAccentColor;
    private LineEdit? _launcherBackgroundImage;
    private LineEdit? _launcherNewsUrl;
    private LineEdit? _launcherSupportUrl;
    private CheckButton? _launcherSettingsEnabled;
    private OptionButton? _launcherMultiplayer;
    private LineEdit? _launcherServerPort;
    private Button? _createLauncherButton;
    private ItemList? _diagnostics;
    private Label? _summary;
    private Label? _status;
    private Button? _applyButton;
    private TextEdit? _noticePreview;
    private string? _capturePath;
    private double _captureElapsed;
    private bool _captureOpened;

    public int ProfileCount => _profiles.Count;

    public int DependencyCount => _dependencies.Count;

    public int ComplianceDiagnosticCount { get; private set; }

    public ModuleTargetKind SelectedTarget => _targets[_target?.Selected ?? 0];

    public void Initialize(
        Func<ProjectState?> getState,
        Func<ModuleDistributionConfiguration, bool> apply,
        Action<ModuleTargetKind> selectExportTarget,
        Action createGameLauncher,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _selectExportTarget = selectExportTarget ?? throw new ArgumentNullException(nameof(selectExportTarget));
        _createGameLauncher = createGameLauncher ?? throw new ArgumentNullException(nameof(createGameLauncher));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Validation & distribution · OpenRPG Forge";
        MinSize = new Vector2I(1280, 820);
        Size = new Vector2I(1280, 820);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(argument =>
            argument.StartsWith("--r050-notices-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _capturePath = capture["--r050-notices-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureElapsed += delta;
        if (!_captureOpened && _captureElapsed >= 1)
        {
            Open();
            _captureOpened = true;
        }
        if (!_captureOpened || _captureElapsed < 3) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var error = GetViewport().GetTexture().GetImage().SavePng(destination);
        GD.Print($"OPENRPG_FORGE_R050_NOTICES_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
        _capturePath = null;
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1280, 820));
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

        LoadConfiguration(state.Project.Module.Distribution);
        Preview();
    }

    public bool ConfigureForSmokeTest(
        ModuleDistributionConfiguration configuration,
        ModuleTargetKind target)
    {
        LoadConfiguration(configuration);
        _target!.Selected = Array.IndexOf(_targets, target);
        LoadProfile(_target.Selected);
        Preview();
        return _approvedConfiguration is not null && !_applyButton!.Disabled;
    }

    public bool ApplyForSmokeTest()
    {
        if (_approvedConfiguration is null)
        {
            return false;
        }

        Apply();
        return _approvedConfiguration is null;
    }

    private void LoadConfiguration(ModuleDistributionConfiguration configuration)
    {
        _suppressSignals = true;
        _configurationId = configuration.Id;
        _profiles.Clear();
        _profiles.AddRange(configuration.TargetProfiles);
        _dependencies.Clear();
        _dependencies.AddRange(configuration.Dependencies);
        _release = configuration.Release;
        _launcher = configuration.Launcher;
        _target!.Selected = Math.Max(_target.Selected, 0);
        LoadProfile(_target.Selected);
        RefreshDependencyList();
        if (_dependencies.Count > 0)
        {
            _dependencyList!.Select(0);
            LoadDependency(0);
        }
        else
        {
            ClearDependencyDraft();
        }

        LoadRelease(configuration.Release);
        LoadLauncher(configuration.Launcher);

        _suppressSignals = false;
        InvalidatePreview();
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("100d1b", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 20);
        }

        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "VALIDATION & DISTRIBUTION", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Cinq cibles canoniques, secrets séparés et conformité de licence avant publication.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        var targetCard = AddCard(body, new Vector2(330, 0));
        targetCard.AddChild(Section("PROFIL DE CIBLE"));
        _target = new OptionButton();
        foreach (var target in _targets)
        {
            _target.AddItem(FormatTarget(target));
        }

        targetCard.AddChild(_target);
        _enabled = AddToggle(targetCard, "Cible activée");
        _editorData = AddToggle(targetCard, "Inclure les données d’édition");
        _debugSymbols = AddToggle(targetCard, "Inclure les symboles de debug");
        _optimize = AddToggle(targetCard, "Optimiser le contenu");
        _approvedLicenses = AddToggle(targetCard, "Exiger les licences approuvées");
        _outputName = AddField(targetCard, "Nom de sortie", "shipping");
        _secretsProfile = AddField(targetCard, "Profil de secrets", "shipping");
        var secretsHelp = new Label
        {
            Text = "Seule la référence du profil est canonique. Aucune valeur secrète n’entre dans le projet ou le package.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        secretsHelp.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        targetCard.AddChild(secretsHelp);
        var updateProfile = new Button { Text = "Mettre à jour le profil", CustomMinimumSize = new Vector2(0, 38) };
        updateProfile.Pressed += UpdateProfile;
        targetCard.AddChild(updateProfile);

        var dependencyCard = AddCard(body, new Vector2(470, 0), expand: true);
        dependencyCard.AddChild(Section("REGISTRE DES DÉPENDANCES"));
        _dependencyList = new ItemList { CustomMinimumSize = new Vector2(0, 135) };
        dependencyCard.AddChild(_dependencyList);
        var dependencyGrid = new GridContainer { Columns = 2 };
        dependencyGrid.AddThemeConstantOverride("h_separation", 8);
        dependencyGrid.AddThemeConstantOverride("v_separation", 4);
        dependencyCard.AddChild(dependencyGrid);
        _dependencyName = AddGridField(dependencyGrid, "Nom", "bibliothèque");
        _dependencyVersion = AddGridField(dependencyGrid, "Version", "1.0.0");
        _dependencyLicense = AddGridField(dependencyGrid, "Licence SPDX", "MIT");
        _dependencyOwner = AddGridField(dependencyGrid, "Propriétaire", "Équipe responsable");
        _dependencySource = AddGridField(dependencyGrid, "Source", "https://…");
        _dependencyCadence = AddGridField(dependencyGrid, "Cadence", "trimestrielle");
        _dependencyTargets = AddGridField(dependencyGrid, "Cibles", "client, shipping");
        _dependencyApprovalState = AddGridEnum<DependencyApprovalState>(dependencyGrid, "Approbation");
        _dependencyRisk = AddGridEnum<DependencyRiskLevel>(dependencyGrid, "Risque");
        _dependencyReviewers = AddGridField(dependencyGrid, "Rôles de revue", "owner, security-review");
        _dependencyRequiredApprovals = AddGridField(dependencyGrid, "Revues requises", "2");
        _dependencySecurityAudit = AddGridField(dependencyGrid, "Audit sécurité", "rapport ou URL");
        dependencyCard.AddChild(new Label { Text = "Justification" });
        _dependencyRationale = new TextEdit { CustomMinimumSize = new Vector2(0, 54) };
        dependencyCard.AddChild(_dependencyRationale);
        dependencyCard.AddChild(new Label { Text = "Plan de retrait" });
        _dependencyRemovalPlan = new TextEdit { CustomMinimumSize = new Vector2(0, 54) };
        dependencyCard.AddChild(_dependencyRemovalPlan);
        var dependencyActions = new HBoxContainer();
        dependencyActions.AddThemeConstantOverride("separation", 8);
        dependencyCard.AddChild(dependencyActions);
        var upsertDependency = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        upsertDependency.Pressed += UpsertDependency;
        dependencyActions.AddChild(upsertDependency);
        var removeDependency = new Button { Text = "Retirer" };
        removeDependency.Pressed += RemoveDependency;
        dependencyActions.AddChild(removeDependency);

        var validationCard = AddCard(body, new Vector2(390, 0));
        validationCard.AddChild(Section("APERÇU DE CONFORMITÉ"));
        var validationScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        validationCard.AddChild(validationScroll);
        var validationContent = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        validationContent.AddThemeConstantOverride("separation", 5);
        validationScroll.AddChild(validationContent);
        var validationHelp = new Label
        {
            Text = "Canal, plateformes, signature, delta et cycle Windows sont canoniques. Stable/Shipping exige confiance, reprise et rollback.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        validationHelp.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        validationContent.AddChild(validationHelp);
        validationContent.AddChild(Section("APERÇU THIRD-PARTY NOTICE"));
        _noticePreview = new TextEdit
        {
            Editable = false,
            CustomMinimumSize = new Vector2(0, 210),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        validationContent.AddChild(_noticePreview);
        _releaseChannel = AddEnumOptions<ModuleReleaseChannel>(validationContent, "Canal de release");
        _releasePlatforms = AddField(validationContent, "Plateformes", "windows-x64, linux-x64");
        _signingMode = AddEnumOptions<ModuleSigningMode>(validationContent, "Politique de signature");
        _signingProfile = AddField(validationContent, "Profil de signature", "shipping-signing");
        _trustedKeys = AddField(validationContent, "Empreintes approuvées", "sha256, sha256");
        _revokedKeys = AddField(validationContent, "Empreintes révoquées", "sha256, sha256");
        _allowUnsignedDevelopment = AddToggle(validationContent, "Autoriser le non-signé en développement");
        _deltaEnabled = AddToggle(validationContent, "Activer les mises à jour delta");
        _deltaChunkKilobytes = AddField(validationContent, "Taille de chunk (Kio)", "1024");
        _deltaResume = AddToggle(validationContent, "Reprise des téléchargements");
        _deltaDeduplicate = AddToggle(validationContent, "Déduplication par hash");
        _deltaRollback = AddToggle(validationContent, "Rollback vers une version retenue");
        _deltaRetainedVersions = AddField(validationContent, "Versions retenues", "2");
        _windowsMode = AddEnumOptions<WindowsPackageMode>(validationContent, "Packaging Windows");
        _windowsPrerequisites = AddField(validationContent, "Prérequis Windows", "windows-x64, vulkan-or-d3d12");
        _windowsDetectPrerequisites = AddToggle(validationContent, "Détecter les prérequis");
        _windowsInstallLog = AddToggle(validationContent, "Journal d'installation");
        _windowsUninstaller = AddToggle(validationContent, "Inclure la désinstallation");
        var updateRelease = new Button { Text = "Mettre à jour la release", CustomMinimumSize = new Vector2(0, 36) };
        updateRelease.Pressed += UpdateRelease;
        validationContent.AddChild(updateRelease);
        validationContent.AddChild(Section("LANCEUR DU JEU · WINDOWS X64"));
        var launcherHelp = new Label
        {
            Text = "Crée une application d'accueil personnalisée avec Jouer, Configuration et Multijoueur. " +
                   "L'hébergement local est fonctionnel ; la connexion distante reste explicitement en préparation.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        launcherHelp.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        validationContent.AddChild(launcherHelp);
        _launcherEnabled = AddToggle(validationContent, "Créer un lanceur avec le jeu");
        _launcherDisplayName = AddField(validationContent, "Titre du lanceur", "Le Royaume des Cendres");
        _launcherTagline = AddField(validationContent, "Slogan", "Rassemblez votre groupe avant de partir");
        _launcherExecutableName = AddField(validationContent, "Nom de l'exécutable", "Royaume_des_Cendres");
        _launcherAccentColor = AddField(validationContent, "Couleur d'accent #RRGGBB", "#C77A45");
        _launcherBackgroundImage = AddField(validationContent, "Image PNG/JPEG du projet", "branding/launcher.png");
        _launcherNewsUrl = AddField(validationContent, "Lien actualités (facultatif)", "https://…");
        _launcherSupportUrl = AddField(validationContent, "Lien support (facultatif)", "https://…");
        _launcherSettingsEnabled = AddToggle(validationContent, "Menu de configuration joueur");
        _launcherMultiplayer = AddEnumOptions<GameLauncherMultiplayerMode>(validationContent, "Menu multijoueur");
        _launcherServerPort = AddField(validationContent, "Port serveur local", "5121");
        var updateLauncher = new Button { Text = "Mettre à jour le lanceur", CustomMinimumSize = new Vector2(0, 36) };
        updateLauncher.Pressed += UpdateLauncher;
        validationContent.AddChild(updateLauncher);
        _createLauncherButton = new Button
        {
            Text = "Créer le lanceur Windows…",
            CustomMinimumSize = new Vector2(0, 42),
            Disabled = true,
        };
        _createLauncherButton.Pressed += CreateLauncher;
        validationContent.AddChild(_createLauncherButton);
        _diagnostics = new ItemList { CustomMinimumSize = new Vector2(0, 180) };
        validationContent.AddChild(_diagnostics);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        var footerText = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        footer.AddChild(footerText);
        _summary = new Label { Text = "Prévisualisation requise.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _summary.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        footerText.AddChild(_summary);
        _status = new Label { Text = "Prêt." };
        footerText.AddChild(_status);
        var previewButton = new Button { Text = "Prévisualiser", CustomMinimumSize = new Vector2(150, 44) };
        previewButton.Pressed += Preview;
        footer.AddChild(previewButton);
        _applyButton = new Button
        {
            Text = "Enregistrer et choisir cette cible",
            CustomMinimumSize = new Vector2(265, 44),
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
        _target!.ItemSelected += index =>
        {
            LoadProfile((int)index);
            InvalidatePreview();
        };
        _dependencyList!.ItemSelected += index => LoadDependency((int)index);
        foreach (var field in new[]
                 {
                     _outputName, _secretsProfile, _dependencyName, _dependencyVersion, _dependencyLicense,
                     _dependencyOwner, _dependencySource, _dependencyCadence, _dependencyTargets,
                     _dependencyReviewers, _dependencyRequiredApprovals, _dependencySecurityAudit,
                     _releasePlatforms, _signingProfile, _trustedKeys, _revokedKeys, _deltaChunkKilobytes,
                     _deltaRetainedVersions, _windowsPrerequisites, _launcherDisplayName, _launcherTagline,
                     _launcherExecutableName, _launcherAccentColor, _launcherBackgroundImage, _launcherNewsUrl,
                     _launcherSupportUrl, _launcherServerPort,
                 })
        {
            field!.TextChanged += _ => InvalidatePreview();
        }

        foreach (var toggle in new[]
                 {
                     _enabled, _editorData, _debugSymbols, _optimize, _approvedLicenses,
                     _allowUnsignedDevelopment, _deltaEnabled, _deltaResume, _deltaDeduplicate, _deltaRollback,
                     _windowsDetectPrerequisites, _windowsInstallLog, _windowsUninstaller, _launcherEnabled,
                     _launcherSettingsEnabled,
                 })
        {
            toggle!.Toggled += _ => InvalidatePreview();
        }

        foreach (var options in new[] { _releaseChannel, _signingMode, _windowsMode,
                     _dependencyApprovalState, _dependencyRisk, _launcherMultiplayer })
        {
            options!.ItemSelected += _ => InvalidatePreview();
        }
    }

    private void UpdateProfile()
    {
        if (CommitProfileDraft())
        {
            InvalidatePreview();
            SetStatus($"Profil {FormatTarget(SelectedTarget)} prêt dans le brouillon.", false);
        }
    }

    private void UpdateRelease()
    {
        if (CommitReleaseDraft())
        {
            InvalidatePreview();
            SetStatus("Configuration de release prête dans le brouillon.", false);
        }
    }

    private void UpdateLauncher()
    {
        if (CommitLauncherDraft())
        {
            InvalidatePreview();
            SetStatus("Configuration du lanceur prête dans le brouillon.", false);
        }
    }

    private void CreateLauncher()
    {
        var state = _getState?.Invoke();
        if (state?.Project.Module.Distribution.Launcher.Enabled != true)
        {
            SetStatus("Activez, prévisualisez et enregistrez d'abord le lanceur.", true);
            return;
        }

        _createGameLauncher?.Invoke();
    }

    private bool CommitProfileDraft()
    {
        try
        {
            var target = SelectedTarget;
            var previous = _profiles.Single(profile => profile.Target == target);
            var profile = new ModuleTargetProfile(
                previous.Id,
                target,
                _enabled!.ButtonPressed,
                _editorData!.ButtonPressed,
                _debugSymbols!.ButtonPressed,
                _optimize!.ButtonPressed,
                _approvedLicenses!.ButtonPressed,
                _outputName!.Text,
                _secretsProfile!.Text,
                "creator-command");
            _profiles[_profiles.FindIndex(item => item.Target == target)] = profile;
            return true;
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, true);
            return false;
        }
    }

    private void UpsertDependency()
    {
        try
        {
            var dependency = new ThirdPartyDependencyApproval(
                _dependencyId ?? ThirdPartyDependencyId.From(Guid.NewGuid()),
                _dependencyName!.Text,
                _dependencyVersion!.Text,
                _dependencyLicense!.Text,
                _dependencyOwner!.Text,
                _dependencyRationale!.Text,
                _dependencyCadence!.Text,
                _dependencyRemovalPlan!.Text,
                _dependencySource!.Text,
                ParseTargets(_dependencyTargets!.Text),
                "creator-command",
                approvalState: (DependencyApprovalState)_dependencyApprovalState!.Selected,
                riskLevel: (DependencyRiskLevel)_dependencyRisk!.Selected,
                approverRoles: ParseCsv(_dependencyReviewers!.Text),
                requiredApprovals: int.Parse(_dependencyRequiredApprovals!.Text,
                    System.Globalization.CultureInfo.InvariantCulture),
                securityAuditReference: _dependencySecurityAudit!.Text);
            var duplicate = _dependencies.FindIndex(item =>
                item.Id != dependency.Id && string.Equals(item.Name, dependency.Name, StringComparison.OrdinalIgnoreCase));
            if (duplicate >= 0)
            {
                SetStatus($"La dépendance '{dependency.Name}' existe déjà.", true);
                return;
            }

            var index = _dependencies.FindIndex(item => item.Id == dependency.Id);
            if (index >= 0) _dependencies[index] = dependency;
            else _dependencies.Add(dependency);
            _dependencies.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));
            RefreshDependencyList();
            var selected = _dependencies.FindIndex(item => item.Id == dependency.Id);
            _dependencyList!.Select(selected);
            _dependencyId = dependency.Id;
            InvalidatePreview();
            SetStatus($"Dépendance '{dependency.Name}' prête dans le registre.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveDependency()
    {
        var selected = _dependencyList!.GetSelectedItems().FirstOrDefault(-1);
        if (selected < 0 || selected >= _dependencies.Count) return;
        _dependencies.RemoveAt(selected);
        RefreshDependencyList();
        ClearDependencyDraft();
        InvalidatePreview();
    }

    private void Preview()
    {
        var state = _getState?.Invoke();
        if (state is null || !CommitProfileDraft() || !CommitReleaseDraft() || !CommitLauncherDraft())
        {
            return;
        }

        try
        {
            _approvedConfiguration = new ModuleDistributionConfiguration(
                _configurationId ?? ModuleDistributionConfigurationId.From(Guid.NewGuid()),
                _profiles,
                _dependencies,
                "creator-command",
                _release,
                launcher: _launcher);
            var module = state.Project.Module;
            var candidateModule = new ModuleDefinition(
                module.Id,
                module.Slug,
                module.DisplayName,
                module.Description,
                module.EntryAreaId,
                module.Metadata,
                module.Variables,
                _approvedConfiguration,
                module.Localization,
                module.Content,
                module.ArtisticStyle,
                module.RuntimePolicies,
                module.Factions,
                module.WorldFeatures);
            var candidate = new ProjectDefinition(
                state.Project.Id,
                state.Project.Title,
                candidateModule,
                state.Project.Blueprints,
                state.Project.Areas,
                state.Project.Dialogues,
                state.Project.Quests);
            var compliance = DistributionComplianceValidator.Validate(candidate, SelectedTarget);
            _noticePreview!.Text = DistributionNoticeBuilder.Build(candidate, SelectedTarget);
            ComplianceDiagnosticCount = compliance.Count;
            _diagnostics!.Clear();
            foreach (var diagnostic in compliance)
            {
                _diagnostics.AddItem($"BLOQUANT · {diagnostic.Code}\n{diagnostic.Message}");
                _diagnostics.SetItemTooltip(_diagnostics.ItemCount - 1, diagnostic.Path);
            }

            if (compliance.Count == 0)
            {
                _diagnostics.AddItem("✓ Cible conforme · NOTICE prêt à générer");
                _diagnostics.SetItemSelectable(0, false);
            }

            _applyButton!.Disabled = false;
            _summary!.Text =
                $"{_profiles.Count} profils · {_release!.Channel} · {_release.Platforms.Count} plateforme(s) · " +
                $"signature {_release.Signing.Mode} · lanceur {(_launcher!.Enabled ? "actif" : "inactif")} · " +
                $"cible {FormatTarget(SelectedTarget)}";
            SetStatus(
                compliance.Count == 0
                    ? "✓ Configuration valide et cible publiable."
                    : $"Configuration enregistrable · export bloqué par {compliance.Count} diagnostic(s).",
                compliance.Count > 0);
        }
        catch (ArgumentException exception)
        {
            InvalidatePreview();
            SetStatus(exception.Message, true);
        }
    }

    private void Apply()
    {
        var configuration = _approvedConfiguration;
        if (configuration is null) return;
        if (_apply?.Invoke(configuration) != true)
        {
            SetStatus("Configuration refusée par la commande de projet.", true);
            return;
        }

        _selectExportTarget?.Invoke(SelectedTarget);
        _createLauncherButton!.Disabled = !configuration.Launcher.Enabled;
        _approvedConfiguration = null;
        _applyButton!.Disabled = true;
        SetStatus($"Configuration enregistrée · prochaine cible : {FormatTarget(SelectedTarget)}.", false);
    }

    private void LoadProfile(int index)
    {
        if (index < 0 || index >= _targets.Length || _profiles.Count == 0) return;
        var profile = _profiles.Single(item => item.Target == _targets[index]);
        _enabled!.SetPressedNoSignal(profile.Enabled);
        _editorData!.SetPressedNoSignal(profile.IncludeEditorData);
        _debugSymbols!.SetPressedNoSignal(profile.IncludeDebugSymbols);
        _optimize!.SetPressedNoSignal(profile.OptimizeContent);
        _approvedLicenses!.SetPressedNoSignal(profile.RequireApprovedLicenses);
        _outputName!.Text = profile.OutputName;
        _secretsProfile!.Text = profile.SecretsProfile;
    }

    private void LoadRelease(ModuleReleaseConfiguration release)
    {
        _releaseChannel!.Selected = (int)release.Channel;
        _releasePlatforms!.Text = string.Join(", ", release.Platforms.Select(platform => platform.ToString()));
        _signingMode!.Selected = (int)release.Signing.Mode;
        _signingProfile!.Text = release.Signing.SigningProfile;
        _trustedKeys!.Text = string.Join(", ", release.Signing.TrustedKeyFingerprints);
        _revokedKeys!.Text = string.Join(", ", release.Signing.RevokedKeyFingerprints);
        _allowUnsignedDevelopment!.SetPressedNoSignal(release.Signing.AllowUnsignedDevelopment);
        _deltaEnabled!.SetPressedNoSignal(release.Delta.Enabled);
        _deltaChunkKilobytes!.Text = (release.Delta.ChunkSizeBytes / 1024).ToString();
        _deltaResume!.SetPressedNoSignal(release.Delta.AllowResume);
        _deltaDeduplicate!.SetPressedNoSignal(release.Delta.DeduplicateByHash);
        _deltaRollback!.SetPressedNoSignal(release.Delta.AllowRollback);
        _deltaRetainedVersions!.Text = release.Delta.RetainedVersions.ToString();
        _windowsMode!.Selected = (int)release.WindowsPackaging.Mode;
        _windowsPrerequisites!.Text = string.Join(", ", release.WindowsPackaging.Prerequisites);
        _windowsDetectPrerequisites!.SetPressedNoSignal(release.WindowsPackaging.DetectPrerequisites);
        _windowsInstallLog!.SetPressedNoSignal(release.WindowsPackaging.WriteInstallLog);
        _windowsUninstaller!.SetPressedNoSignal(release.WindowsPackaging.IncludeUninstaller);
    }

    private void LoadLauncher(GameLauncherConfiguration launcher)
    {
        _launcherEnabled!.SetPressedNoSignal(launcher.Enabled);
        _launcherDisplayName!.Text = launcher.DisplayName;
        _launcherTagline!.Text = launcher.Tagline;
        _launcherExecutableName!.Text = launcher.ExecutableName;
        _launcherAccentColor!.Text = launcher.AccentColor;
        _launcherBackgroundImage!.Text = launcher.BackgroundImagePath ?? string.Empty;
        _launcherNewsUrl!.Text = launcher.NewsUrl ?? string.Empty;
        _launcherSupportUrl!.Text = launcher.SupportUrl ?? string.Empty;
        _launcherSettingsEnabled!.SetPressedNoSignal(launcher.SettingsEnabled);
        _launcherMultiplayer!.Selected = (int)launcher.Multiplayer;
        _launcherServerPort!.Text = launcher.DefaultServerPort.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        _createLauncherButton!.Disabled = !launcher.Enabled;
    }

    private bool CommitReleaseDraft()
    {
        try
        {
            var current = _release ?? throw new InvalidOperationException("Release configuration is not loaded.");
            if (!int.TryParse(_deltaChunkKilobytes!.Text, out var chunkKilobytes) || chunkKilobytes <= 0)
            {
                throw new ArgumentException("La taille de chunk doit être un entier positif en Kio.");
            }

            if (!int.TryParse(_deltaRetainedVersions!.Text, out var retainedVersions))
            {
                throw new ArgumentException("Le nombre de versions retenues doit être un entier.");
            }

            _release = new ModuleReleaseConfiguration(
                current.Id,
                (ModuleReleaseChannel)_releaseChannel!.Selected,
                ParsePlatforms(_releasePlatforms!.Text),
                new ModuleSigningConfiguration(
                    (ModuleSigningMode)_signingMode!.Selected,
                    _signingProfile!.Text,
                    ParseCsv(_trustedKeys!.Text),
                    ParseCsv(_revokedKeys!.Text),
                    _allowUnsignedDevelopment!.ButtonPressed),
                new ModuleDeltaConfiguration(
                    _deltaEnabled!.ButtonPressed,
                    checked(chunkKilobytes * 1024),
                    _deltaResume!.ButtonPressed,
                    _deltaDeduplicate!.ButtonPressed,
                    _deltaRollback!.ButtonPressed,
                    retainedVersions),
                new WindowsPackagingConfiguration(
                    (WindowsPackageMode)_windowsMode!.Selected,
                    _windowsDetectPrerequisites!.ButtonPressed,
                    _windowsInstallLog!.ButtonPressed,
                    _windowsUninstaller!.ButtonPressed,
                    ParseCsv(_windowsPrerequisites!.Text)),
                "creator-command");
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            SetStatus(exception.Message, true);
            return false;
        }
    }

    private bool CommitLauncherDraft()
    {
        try
        {
            var current = _launcher ?? throw new InvalidOperationException("Launcher configuration is not loaded.");
            if (!int.TryParse(_launcherServerPort!.Text, out var port))
            {
                throw new ArgumentException("Le port du lanceur doit être un entier.");
            }

            _launcher = new GameLauncherConfiguration(
                current.Id,
                _launcherEnabled!.ButtonPressed,
                _launcherDisplayName!.Text,
                _launcherTagline!.Text,
                _launcherExecutableName!.Text,
                _launcherAccentColor!.Text,
                _launcherBackgroundImage!.Text,
                _launcherNewsUrl!.Text,
                _launcherSupportUrl!.Text,
                _launcherSettingsEnabled!.ButtonPressed,
                (GameLauncherMultiplayerMode)_launcherMultiplayer!.Selected,
                port,
                "creator-command");
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            SetStatus(exception.Message, true);
            return false;
        }
    }

    private void RefreshDependencyList()
    {
        _dependencyList!.Clear();
        foreach (var dependency in _dependencies)
        {
            _dependencyList.AddItem(
                $"{dependency.Name} {dependency.Version}\n{dependency.LicenseSpdx} · {dependency.Owner} · " +
                $"{dependency.ApprovalState} · {dependency.RiskLevel} · " +
                string.Join(", ", dependency.RequiredForTargets.Select(FormatTarget)));
            _dependencyList.SetItemTooltip(
                _dependencyList.ItemCount - 1,
                $"UUID {dependency.Id}\nCadence {dependency.UpdateCadence}\n" +
                $"Revues {dependency.RequiredApprovals}: {string.Join(", ", dependency.ApproverRoles)}\n" +
                $"Audit {dependency.SecurityAuditReference}\nRetrait {dependency.RemovalPlan}");
        }
    }

    private void LoadDependency(int index)
    {
        if (index < 0 || index >= _dependencies.Count) return;
        var dependency = _dependencies[index];
        _dependencyId = dependency.Id;
        _dependencyName!.Text = dependency.Name;
        _dependencyVersion!.Text = dependency.Version;
        _dependencyLicense!.Text = dependency.LicenseSpdx;
        _dependencyOwner!.Text = dependency.Owner;
        _dependencySource!.Text = dependency.SourceReference;
        _dependencyCadence!.Text = dependency.UpdateCadence;
        _dependencyTargets!.Text = string.Join(", ", dependency.RequiredForTargets.Select(target => target.ToString()));
        _dependencyApprovalState!.Selected = (int)dependency.ApprovalState;
        _dependencyRisk!.Selected = (int)dependency.RiskLevel;
        _dependencyReviewers!.Text = string.Join(", ", dependency.ApproverRoles);
        _dependencyRequiredApprovals!.Text = dependency.RequiredApprovals.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        _dependencySecurityAudit!.Text = dependency.SecurityAuditReference;
        _dependencyRationale!.Text = dependency.Rationale;
        _dependencyRemovalPlan!.Text = dependency.RemovalPlan;
    }

    private void ClearDependencyDraft()
    {
        _dependencyId = null;
        foreach (var field in new[]
                 {
                     _dependencyName, _dependencyVersion, _dependencyLicense, _dependencyOwner, _dependencySource,
                     _dependencyCadence, _dependencyTargets,
                     _dependencyReviewers, _dependencyRequiredApprovals, _dependencySecurityAudit,
                 })
        {
            field!.Text = string.Empty;
        }

        _dependencyRationale!.Text = string.Empty;
        _dependencyRemovalPlan!.Text = string.Empty;
        _dependencyApprovalState!.Selected = (int)DependencyApprovalState.Approved;
        _dependencyRisk!.Selected = (int)DependencyRiskLevel.Low;
        _dependencyRequiredApprovals!.Text = "2";
    }

    private void InvalidatePreview()
    {
        if (_suppressSignals) return;
        _approvedConfiguration = null;
        ComplianceDiagnosticCount = 0;
        if (_applyButton is not null) _applyButton.Disabled = true;
        if (_summary is not null) _summary.Text = "Prévisualisation requise après chaque changement.";
    }

    private void SetStatus(string text, bool error)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff9f88" : "83d6ae"));
    }

    private static IReadOnlyList<ModuleTargetKind> ParseTargets(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var targets = new List<ModuleTargetKind>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = token.Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal);
            if (!Enum.TryParse<ModuleTargetKind>(normalized, ignoreCase: true, out var target) || !Enum.IsDefined(target))
            {
                throw new ArgumentException($"Cible inconnue : '{token}'.", nameof(value));
            }

            targets.Add(target);
        }

        return targets;
    }

    private static IReadOnlyList<ModuleDistributionPlatform> ParsePlatforms(string value)
    {
        var platforms = new List<ModuleDistributionPlatform>();
        foreach (var token in ParseCsv(value))
        {
            var normalized = token.Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal);
            if (!Enum.TryParse<ModuleDistributionPlatform>(normalized, ignoreCase: true, out var platform) ||
                !Enum.IsDefined(platform))
            {
                throw new ArgumentException($"Plateforme inconnue : '{token}'.", nameof(value));
            }

            platforms.Add(platform);
        }

        return platforms;
    }

    private static IReadOnlyList<string> ParseCsv(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static OptionButton AddEnumOptions<TEnum>(VBoxContainer parent, string label)
        where TEnum : struct, Enum
    {
        parent.AddChild(new Label { Text = label });
        var options = new OptionButton();
        foreach (var value in Enum.GetValues<TEnum>())
        {
            options.AddItem(value.ToString());
        }

        parent.AddChild(options);
        return options;
    }

    private static CheckButton AddToggle(VBoxContainer parent, string text)
    {
        var toggle = new CheckButton { Text = text };
        parent.AddChild(toggle);
        return toggle;
    }

    private static LineEdit AddField(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder };
        parent.AddChild(field);
        return field;
    }

    private static LineEdit AddGridField(GridContainer parent, string label, string placeholder)
    {
        parent.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(field);
        return field;
    }

    private static OptionButton AddGridEnum<TEnum>(GridContainer parent, string label)
        where TEnum : struct, Enum
    {
        parent.AddChild(new Label { Text = label });
        var options = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var value in Enum.GetValues<TEnum>()) options.AddItem(value.ToString());
        parent.AddChild(options);
        return options;
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color("ffb276"));
        return label;
    }

    private static VBoxContainer AddCard(Container parent, Vector2 minimum, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimum,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 13);
        }

        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        margin.AddChild(content);
        return content;
    }

    private static StyleBoxFlat Card(string background, string border, int width, int radius) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };

    private static string FormatTarget(ModuleTargetKind target) => target switch
    {
        ModuleTargetKind.DedicatedServer => "Serveur dédié",
        ModuleTargetKind.Shipping => "Publication",
        ModuleTargetKind.Editor => "Éditeur",
        ModuleTargetKind.Client => "Client",
        ModuleTargetKind.Test => "Test",
        _ => target.ToString(),
    };
}
#endif
