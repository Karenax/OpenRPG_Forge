using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNewProjectWizard : Control
{
    private static readonly string[] StepTitles =
    [
        "1  Type de projet",
        "2  Identité publique",
        "3  Monde de départ",
        "4  Technologie",
        "5  Résumé avant création",
    ];

    private readonly StudioSessionController _session;
    private readonly ItemList _steps;
    private readonly Label _stepTitle;
    private readonly Label _stepHint;
    private readonly Label _status;
    private readonly VBoxContainer[] _stepPanels;
    private readonly Button _back;
    private readonly Button _next;
    private readonly Button _create;
    private readonly OptionButton _template;
    private readonly LineEdit _title;
    private readonly LineEdit _summary;
    private readonly TextEdit _description;
    private readonly LineEdit _author;
    private readonly LineEdit _version;
    private readonly LineEdit _license;
    private readonly LineEdit _tags;
    private readonly LineEdit _classification;
    private readonly OptionButton _locale;
    private readonly CheckBox _prepareEnglish;
    private readonly Label _parentDirectory;
    private readonly CheckBox _createArea;
    private readonly VBoxContainer _areaFields;
    private readonly LineEdit _areaName;
    private readonly OptionButton _areaKind;
    private readonly SpinBox _areaWidth;
    private readonly SpinBox _areaDepth;
    private readonly CheckBox _coreKit;
    private readonly CheckBox _rpgKit;
    private readonly CheckBox _windows;
    private readonly CheckBox _linux;
    private readonly CheckBox _mac;
    private readonly OptionButton _render;
    private readonly OptionButton _network;
    private readonly SpinBox _orientation;
    private readonly RichTextLabel _summaryPreview;
    private readonly Guid _draftId = Guid.NewGuid();
    private string _parentPath;
    private int _step;

    internal event Action? Cancelled;

    internal event Action<string>? ProjectCreated;

    internal int StepCount => StepTitles.Length;

    internal bool ShowsHumanSurface =>
        StepCount == 5 && !_summaryPreview.Text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
        !_summaryPreview.Text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
        !_summaryPreview.Text.Contains("project.json", StringComparison.OrdinalIgnoreCase);

    internal StudioNewProjectWizard(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioNewProjectWizard";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _parentPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "OpenRPG Forge",
            "Projets");

        var background = new ColorRect { Color = StudioStyle.Background };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margin = StudioStyle.Margin(28, 22, 28, 22);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var brand = new HBoxContainer();
        brand.AddThemeConstantOverride("separation", 8);
        root.AddChild(brand);
        var mark = StudioStyle.Panel("WizardMark", StudioStyle.Accent, 10, 0);
        mark.CustomMinimumSize = new Vector2(40, 40);
        var markText = StudioStyle.Label("OF", 14, StudioStyle.Background);
        markText.HorizontalAlignment = HorizontalAlignment.Center;
        markText.VerticalAlignment = VerticalAlignment.Center;
        mark.AddChild(markText);
        brand.AddChild(mark);
        var brandText = new VBoxContainer();
        brandText.AddChild(StudioStyle.Label("Créer un projet", 22));
        brandText.AddChild(StudioStyle.Label(
            "Cinq étapes lisibles · aperçu sans écriture · identités internes gérées automatiquement",
            11,
            StudioStyle.Muted));
        brand.AddChild(brandText);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);

        var stepPanel = StudioStyle.Panel("WizardSteps", StudioStyle.Chrome, 9);
        stepPanel.CustomMinimumSize = new Vector2(280, 0);
        body.AddChild(stepPanel);
        var stepMargin = StudioStyle.Margin(10, 10, 10, 10);
        stepPanel.AddChild(stepMargin);
        var stepColumn = new VBoxContainer();
        stepColumn.AddThemeConstantOverride("separation", 8);
        stepMargin.AddChild(stepColumn);
        stepColumn.AddChild(StudioStyle.SectionLabel("ASSISTANT DE CRÉATION"));
        _steps = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _steps.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 7));
        foreach (var title in StepTitles) _steps.AddItem(title);
        _steps.ItemSelected += index => ShowStep((int)index);
        stepColumn.AddChild(_steps);
        var safety = StudioStyle.Panel("WizardSafety", StudioStyle.Selection, 7);
        stepColumn.AddChild(safety);
        safety.AddChild(StudioStyle.Label(
            "Le bouton Prévisualiser ne crée aucun dossier. La création reste une action explicite à la dernière étape.",
            10,
            StudioStyle.Success,
            wrap: true));

        var editorPanel = StudioStyle.Panel("WizardEditor", StudioStyle.Surface, 9);
        editorPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        editorPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(editorPanel);
        var editorMargin = StudioStyle.Margin(18, 14, 18, 12);
        editorPanel.AddChild(editorMargin);
        var editor = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        editor.AddThemeConstantOverride("separation", 8);
        editorMargin.AddChild(editor);
        _stepTitle = StudioStyle.Label(string.Empty, 20);
        editor.AddChild(_stepTitle);
        _stepHint = StudioStyle.Label(string.Empty, 11, StudioStyle.Muted, wrap: true);
        editor.AddChild(_stepHint);
        editor.AddChild(Separator());
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        editor.AddChild(scroll);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(panels);
        _stepPanels = new VBoxContainer[StepTitles.Length];
        for (var index = 0; index < _stepPanels.Length; index++)
        {
            _stepPanels[index] = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _stepPanels[index].AddThemeConstantOverride("separation", 8);
            panels.AddChild(_stepPanels[index]);
        }

        _template = Option("Aventure RPG", "Interface ou menu", "Simulation sans monde obligatoire");
        _template.ItemSelected += _ => UpdateTemplate();
        _stepPanels[0].AddChild(StudioStyle.Label("Quel produit voulez-vous construire ?", 16));
        _stepPanels[0].AddChild(_template);
        _stepPanels[0].AddChild(ChoiceCard(
            "Aventure RPG",
            "Crée une première zone jouable, un sol provisoire et un point de départ du joueur.",
            StudioStyle.Success));
        _stepPanels[0].AddChild(ChoiceCard(
            "Interface ou menu",
            "Pour un lanceur, un menu principal ou une expérience sans monde 3D obligatoire.",
            StudioStyle.Accent));
        _stepPanels[0].AddChild(ChoiceCard(
            "Simulation",
            "Pour des règles, outils ou simulations autonomes ; une zone pourra être ajoutée plus tard.",
            StudioStyle.Warning));

        var identityGrid = Grid();
        _stepPanels[1].AddChild(identityGrid);
        _title = AddField(identityGrid, "Nom du projet", Edit("Mon aventure", "Nom visible par les joueurs"));
        _summary = AddField(identityGrid, "Résumé", Edit("Une aventure originale", "Une phrase courte"));
        _description = AddField(identityGrid, "Description", Multiline("Décrivez l’expérience proposée", 92));
        _author = AddField(identityGrid, "Auteur ou équipe", Edit("Non renseigné", "Nom public"));
        _version = AddField(identityGrid, "Version", Edit("0.1.0", "Format majeur.mineur.correctif"));
        _license = AddField(identityGrid, "Licence", Edit("UNLICENSED", "Exemple : CC-BY-4.0"));
        _tags = AddField(identityGrid, "Mots-clés", Edit("aventure, coop", "Séparés par des virgules"));
        _classification = AddField(identityGrid, "Classification", Edit("Non classé", "Public visé"));
        _locale = AddField(identityGrid, "Langue principale", Option("Français", "English"));
        _prepareEnglish = Check("Préparer aussi une fiche anglaise à compléter dans la page Projet");
        _stepPanels[1].AddChild(_prepareEnglish);
        var locationCard = ChoiceCard(
            "Emplacement du projet",
            string.Empty,
            StudioStyle.Muted,
            out _parentDirectory);
        _stepPanels[1].AddChild(locationCard);
        var chooseParent = StudioStyle.Button("Choisir un autre dossier…", compact: true);
        chooseParent.Pressed += BrowseParentDirectory;
        _stepPanels[1].AddChild(chooseParent);

        _createArea = Check("Créer une première zone maintenant");
        _createArea.ButtonPressed = true;
        _createArea.Toggled += _ => UpdateAreaVisibility();
        _stepPanels[2].AddChild(_createArea);
        _areaFields = new VBoxContainer();
        _areaFields.AddThemeConstantOverride("separation", 7);
        _stepPanels[2].AddChild(_areaFields);
        var areaGrid = Grid();
        _areaFields.AddChild(areaGrid);
        _areaName = AddField(areaGrid, "Nom de la première zone", Edit("Première zone", "Nom visible dans l’explorateur"));
        _areaKind = AddField(areaGrid, "Type de zone", Option("Extérieure", "Intérieure"));
        _areaWidth = AddField(areaGrid, "Largeur", DimensionBox());
        _areaDepth = AddField(areaGrid, "Profondeur", DimensionBox());
        _orientation = AddField(areaGrid, "Orientation du joueur", new SpinBox
        {
            MinValue = 0,
            MaxValue = 359,
            Step = 15,
            Suffix = "°",
        });
        StudioStyle.ApplySpinBox(_orientation);
        _areaFields.AddChild(StudioStyle.Label(
            "Le point de départ sera créé et relié automatiquement à cette zone. Vous pourrez ensuite le déplacer directement dans la vue 3D.",
            10,
            StudioStyle.Success,
            wrap: true));

        var technology = new HBoxContainer();
        technology.AddThemeConstantOverride("separation", 18);
        _stepPanels[3].AddChild(technology);
        var kits = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        kits.AddChild(StudioStyle.Label("Kits de fonctions", 13));
        _coreKit = Check("Socle OpenRPG Forge");
        _coreKit.ButtonPressed = true;
        _rpgKit = Check("Fonctions RPG");
        _rpgKit.ButtonPressed = true;
        kits.AddChild(_coreKit);
        kits.AddChild(_rpgKit);
        technology.AddChild(kits);
        var platforms = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        platforms.AddChild(StudioStyle.Label("Plateformes visées", 13));
        _windows = Check("Windows 64 bits");
        _windows.ButtonPressed = true;
        _linux = Check("Linux 64 bits");
        _mac = Check("macOS Apple Silicon");
        platforms.AddChild(_windows);
        platforms.AddChild(_linux);
        platforms.AddChild(_mac);
        technology.AddChild(platforms);
        var technologyGrid = Grid();
        _stepPanels[3].AddChild(technologyGrid);
        _render = AddField(technologyGrid, "Qualité de rendu", Option("Équilibrée", "Qualité élevée", "Performances"));
        _network = AddField(technologyGrid, "Mode réseau", Option("Hors ligne", "Partie hébergée", "Serveur dédié"));

        _stepPanels[4].AddChild(StudioStyle.Label(
            "Vérifiez ce qui sera créé. Les identités techniques et le rangement des fichiers restent entièrement gérés par le Studio.",
            11,
            StudioStyle.Muted,
            wrap: true));
        _summaryPreview = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 250),
            SelectionEnabled = true,
        };
        _summaryPreview.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Chrome, StudioStyle.Border, 8));
        _summaryPreview.AddThemeColorOverride("default_color", StudioStyle.Text);
        _summaryPreview.AddThemeFontSizeOverride("normal_font_size", 12);
        _stepPanels[4].AddChild(_summaryPreview);
        var preview = StudioStyle.Button("Prévisualiser sans créer de dossier", compact: true);
        preview.Pressed += Preview;
        _stepPanels[4].AddChild(preview);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 6);
        root.AddChild(footer);
        var cancel = StudioStyle.Button("Annuler", compact: true);
        cancel.Pressed += () => Cancelled?.Invoke();
        footer.AddChild(cancel);
        _status = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        _back = StudioStyle.Button("Précédent", compact: true);
        _back.Pressed += () => ShowStep(_step - 1);
        footer.AddChild(_back);
        _next = StudioStyle.Button("Suivant", primary: true, compact: true);
        _next.Pressed += () => ShowStep(_step + 1);
        footer.AddChild(_next);
        _create = StudioStyle.Button("Créer et ouvrir", primary: true, compact: true);
        _create.Pressed += Create;
        footer.AddChild(_create);

        _title.Text = "Mon aventure";
        _summary.Text = "Une nouvelle aventure OpenRPG Forge";
        _description.Text = "Décrivez ici le monde et la promesse faite aux joueurs.";
        _author.Text = "Non renseigné";
        _version.Text = "0.1.0";
        _license.Text = "UNLICENSED";
        _classification.Text = "Non classé";
        _areaName.Text = "Première zone";
        _areaWidth.Value = 64;
        _areaDepth.Value = 64;
        _parentDirectory.Text = FriendlyPath(_parentPath);
        ShowStep(0);
    }

    internal bool PrepareEvidence(string mode)
    {
        _title.Text = "Les Échos de Clairval";
        _summary.Text = "Une aventure coopérative dans une vallée oubliée";
        _description.Text = "Explorez Clairval, restaurez son refuge et choisissez comment réveiller les anciennes routes.";
        _author.Text = "Atelier Forge";
        _version.Text = "1.0.0";
        _license.Text = "CC-BY-4.0";
        _tags.Text = "aventure, coopération, exploration";
        _classification.Text = "Tout public";
        _areaName.Text = "Refuge de Clairval";
        _linux.ButtonPressed = true;
        _prepareEnglish.ButtonPressed = true;
        if (string.Equals(mode, "ux0-e-summary", StringComparison.OrdinalIgnoreCase))
        {
            ShowStep(4);
            Preview();
        }
        else
        {
            ShowStep(1);
        }
        return ShowsHumanSurface;
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, StepTitles.Length - 1);
        for (var index = 0; index < _stepPanels.Length; index++)
        {
            _stepPanels[index].Visible = index == _step;
        }
        _steps.Select(_step);
        _stepTitle.Text = StepTitles[_step][3..];
        _stepHint.Text = _step switch
        {
            0 => "Le modèle détermine seulement le point de départ ; toutes les options restent modifiables ensuite.",
            1 => "Ces informations sont destinées aux joueurs et aux personnes qui reprendront le projet.",
            2 => "Une aventure RPG exige une zone et un point de départ ; les autres modèles peuvent commencer sans zone.",
            3 => "Choisissez les fonctions, plateformes, performances et capacités réseau attendues.",
            _ => "Aucune écriture ne se produit avant le bouton Créer et ouvrir.",
        };
        _back.Disabled = _step == 0;
        _next.Visible = _step < StepTitles.Length - 1;
        _create.Visible = _step == StepTitles.Length - 1;
        if (_step == StepTitles.Length - 1)
        {
            RefreshSummary();
        }
    }

    private void UpdateTemplate()
    {
        var rpg = _template.Selected == 0;
        _createArea.ButtonPressed = rpg || _createArea.ButtonPressed;
        _createArea.Disabled = rpg;
        _rpgKit.ButtonPressed = rpg;
        _rpgKit.Disabled = rpg;
        UpdateAreaVisibility();
    }

    private void UpdateAreaVisibility() => _areaFields.Visible = _createArea.ButtonPressed;

    private void BrowseParentDirectory()
    {
        var dialog = new FileDialog
        {
            Title = "Choisir le dossier parent des projets",
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            CurrentDir = Directory.Exists(_parentPath) ? _parentPath : string.Empty,
        };
        dialog.DirSelected += path =>
        {
            _parentPath = Path.GetFullPath(path);
            _parentDirectory.Text = FriendlyPath(_parentPath);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCenteredRatio(0.75f);
    }

    private void Preview()
    {
        var result = _session.PreviewNewProject(BuildDraft());
        RefreshSummary(result);
        SetStatus(result.Message, !result.Succeeded);
    }

    private void Create()
    {
        var result = _session.CreateNewProject(BuildDraft());
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded && result.WorkspaceDirectory is not null)
        {
            ProjectCreated?.Invoke(result.WorkspaceDirectory);
        }
    }

    private void RefreshSummary(StudioProjectPreview? preview = null)
    {
        var template = _template.Selected switch
        {
            0 => "Aventure RPG",
            1 => "Interface ou menu",
            _ => "Simulation",
        };
        var area = _createArea.ButtonPressed ? _areaName.Text : "Aucune zone initiale";
        var languages = _prepareEnglish.ButtonPressed ? "Français + anglais à compléter" : _locale.Selected == 0 ? "Français" : "Anglais";
        var platforms = new[]
        {
            _windows.ButtonPressed ? "Windows" : null,
            _linux.ButtonPressed ? "Linux" : null,
            _mac.ButtonPressed ? "macOS" : null,
        }.Where(item => item is not null);
        _summaryPreview.Text =
            $"[font_size=20]{Escape(_title.Text)}[/font_size]\n" +
            $"[color=#aeb5a8]{Escape(_summary.Text)}[/color]\n\n" +
            $"[color=#ef7b42]TYPE[/color]  {template}\n" +
            $"[color=#ef7b42]LANGUES[/color]  {languages}\n" +
            $"[color=#ef7b42]MONDE DE DÉPART[/color]  {Escape(area)}\n" +
            $"[color=#ef7b42]PLATEFORMES[/color]  {string.Join(", ", platforms)}\n" +
            $"[color=#ef7b42]RÉSEAU[/color]  {_network.GetItemText(_network.Selected)}\n" +
            $"[color=#ef7b42]EMPLACEMENT[/color]  {Escape(FriendlyPath(_parentPath))}\n\n" +
            (preview is null
                ? "[color=#e2b85e]Prévisualisez pour valider les choix sans créer de dossier.[/color]"
                : preview.Succeeded
                    ? $"[color=#7bd094]✓ {Escape(preview.Message)}[/color]\n{Escape(preview.Summary)}"
                    : $"[color=#e56f6f]À corriger · {Escape(preview.Message)}[/color]");
    }

    private StudioNewProjectDraft BuildDraft()
    {
        var locale = _locale.Selected == 0 ? "fr" : "en";
        var primary = new StudioLocalizedProjectDraft(
            locale,
            _title.Text,
            _summary.Text,
            _description.Text,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            _license.Text,
            string.Empty,
            "OpenRPG Forge 1.x");
        var localizations = new List<StudioLocalizedProjectDraft> { primary };
        if (_prepareEnglish.ButtonPressed && locale != "en")
        {
            localizations.Add(new StudioLocalizedProjectDraft(
                "en",
                _title.Text,
                "English summary to complete",
                "English description to complete in the Project page.",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                _license.Text,
                string.Empty,
                "OpenRPG Forge 1.x"));
        }
        var template = (ProjectTemplateKind)Math.Clamp(_template.Selected, 0, 2);
        var kits = new List<string>();
        if (_coreKit.ButtonPressed) kits.Add("forge-core");
        if (_rpgKit.ButtonPressed) kits.Add("forge-rpg-kit");
        var platforms = new List<ModuleDistributionPlatform>();
        if (_windows.ButtonPressed) platforms.Add(ModuleDistributionPlatform.WindowsX64);
        if (_linux.ButtonPressed) platforms.Add(ModuleDistributionPlatform.LinuxX64);
        if (_mac.ButtonPressed) platforms.Add(ModuleDistributionPlatform.MacOSArm64);
        return new StudioNewProjectDraft(
            _draftId,
            _parentPath,
            template,
            _createArea.ButtonPressed,
            locale,
            localizations,
            _author.Text,
            _version.Text,
            _license.Text,
            null,
            SplitValues(_tags.Text),
            _classification.Text,
            _createArea.ButtonPressed ? _areaName.Text : string.Empty,
            _areaKind.Selected == 0 ? AreaKind.Exterior : AreaKind.Interior,
            new AreaDimensions((int)_areaWidth.Value, (int)_areaDepth.Value),
            kits,
            platforms,
            template == ProjectTemplateKind.Rpg ? "forge.generic" : "forge.core",
            (ProjectRenderPreset)Math.Clamp(_render.Selected, 0, 2),
            (ProjectNetworkMode)Math.Clamp(_network.Selected, 0, 2),
            (float)_orientation.Value,
            "campaign",
            "adventurer",
            "default");
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
    }

    private static string FriendlyPath(string path)
    {
        var documents = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        return !string.IsNullOrWhiteSpace(documents) && path.StartsWith(documents, StringComparison.OrdinalIgnoreCase)
            ? "Documents" + path[documents.Length..]
            : path;
    }

    private static string Escape(string value) => value
        .Replace("[", "(", StringComparison.Ordinal)
        .Replace("]", ")", StringComparison.Ordinal);

    private static string[] SplitValues(string value) => value
        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static PanelContainer ChoiceCard(string title, string description, Color accent) =>
        ChoiceCard(title, description, accent, out _);

    private static PanelContainer ChoiceCard(string title, string description, Color accent, out Label descriptionLabel)
    {
        var card = StudioStyle.Panel("WizardChoice", StudioStyle.Chrome, 7);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        card.AddChild(margin);
        var column = new VBoxContainer();
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label(title, 13, accent));
        descriptionLabel = StudioStyle.Label(description, 10, StudioStyle.Muted, wrap: true);
        column.AddChild(descriptionLabel);
        return card;
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var caption = StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true);
        caption.CustomMinimumSize = new Vector2(170, 0);
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
        var edit = new TextEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(0, height) };
        edit.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 5));
        edit.AddThemeStyleboxOverride("focus", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Accent, 5, 2));
        edit.AddThemeColorOverride("font_color", StudioStyle.Text);
        edit.AddThemeColorOverride("font_placeholder_color", StudioStyle.Muted);
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

    private static CheckBox Check(string label) => new() { Text = label, FocusMode = FocusModeEnum.All };

    private static SpinBox DimensionBox()
    {
        var box = new SpinBox { MinValue = 16, MaxValue = 512, Step = 8, Suffix = " m" };
        StudioStyle.ApplySpinBox(box);
        return box;
    }

    private static HSeparator Separator()
    {
        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator", StudioStyle.Border);
        return separator;
    }
}
