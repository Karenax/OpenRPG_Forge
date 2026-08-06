#if TOOLS
using System.Security.Cryptography;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreaturePresentationWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreaturePresentationProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private Func<string?>? _workspaceRoot;
    private ItemList? _creatures;
    private Label? _status;
    private RichTextLabel? _feedback;
    private Button? _previewButton;
    private Button? _applyButton;
    private LineEdit? _gender;
    private LineEdit? _bodyType;
    private LineEdit? _pronounSubject;
    private LineEdit? _pronounObject;
    private LineEdit? _pronounPossessive;
    private LineEdit? _pronounReflexive;
    private LineEdit? _portraitPath;
    private LineEdit? _portraitExpression;
    private LineEdit? _portraitLicense;
    private LineEdit? _portraitHash;
    private LineEdit? _portraitAlt;
    private TextEdit? _portraitVariants;
    private TextureRect? _portraitPreview;
    private LineEdit? _rig;
    private TextEdit? _bodySlots;
    private ColorPickerButton? _primaryColor;
    private LineEdit? _wings;
    private LineEdit? _tail;
    private LineEdit? _voiceBank;
    private LineEdit? _voiceLanguage;
    private LineEdit? _voiceLicense;
    private LineEdit? _voiceRights;
    private LineEdit? _voiceEmotions;
    private LineEdit? _voiceVariants;
    private LineEdit? _lipSync;
    private Node3D? _previewRoot;
    private BlueprintId? _selectedBlueprintId;
    private Guid _draftPresentationId;
    private bool _lastCommandSucceeded;

    public Guid LastCommandId { get; private set; }

    public string LastPatchSummary { get; private set; } = string.Empty;

    public BlueprintId? SelectedBlueprintId => _selectedBlueprintId;

    public int PreviewMeshCount => _previewRoot?.GetChildCount() ?? 0;

    public bool PortraitPreviewReady => _portraitPreview?.Texture is not null;

    public void Initialize(
        Func<ProjectState?> load,
        Func<string?> workspaceRoot,
        Func<BlueprintId, CreaturePresentationProfile, bool, CommandResult> mutate,
        Action undo,
        Action redo,
        Action<Window> applyIcon)
    {
        _load = load;
        _workspaceRoot = workspaceRoot;
        _mutate = mutate;
        _undo = undo;
        _redo = redo;
        _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Studio de créature · OpenRPG Forge";
        MinSize = new Vector2I(1120, 760);
        Size = new Vector2I(1320, 860);
        CloseRequested += Hide;
        Build();
        Refresh();
    }

    public void Open()
    {
        Refresh();
        PopupCentered();
        _applyIcon?.Invoke(this);
    }

    public bool ConfigureForSmokeTest(string portraitPath)
    {
        Refresh();
        if (_selectedBlueprintId is null) return false;
        _gender!.Text = "nonbinary";
        _bodyType!.Text = "humanoid-medium";
        _pronounSubject!.Text = "iel";
        _pronounObject!.Text = "iel";
        _pronounPossessive!.Text = "son";
        _pronounReflexive!.Text = "soi";
        _portraitExpression!.Text = "neutral";
        _portraitPath!.Text = portraitPath;
        _portraitLicense!.Text = "CC0-1.0";
        _portraitAlt!.Text = "Portrait de validation de la créature";
        _portraitVariants!.Text = $"angry|{portraitPath}|CC0-1.0|Portrait en colère";
        _rig!.Text = "rig.humanoid";
        _bodySlots!.Text = "head=asset.hood\ntorso=asset.armor";
        _primaryColor!.Color = new Color("7a5cff");
        _wings!.Text = "asset.wings";
        _tail!.Text = "asset.tail";
        _voiceBank!.Text = "audio.creature.core, audio.creature.combat";
        _voiceLanguage!.Text = "fr-FR";
        _voiceLicense!.Text = "CC0-1.0";
        _voiceRights!.Text = "author-recorded";
        _voiceEmotions!.Text = "calm, angry";
        _voiceVariants!.Text = "a, b";
        _lipSync!.Text = "lipsync.fr-v1";
        RefreshPortraitPreview();
        RebuildModelPreview();
        return true;
    }

    public bool PreviewForSmokeTest()
    {
        Mutate(true);
        return _lastCommandSucceeded;
    }

    public bool ApplyForSmokeTest()
    {
        Mutate(false);
        return _lastCommandSucceeded;
    }

    public void Refresh()
    {
        var state = _load?.Invoke();
        var previous = _selectedBlueprintId;
        _creatures?.Clear();
        if (state is null)
        {
            SetEmpty("Aucun projet ouvert.");
            return;
        }

        var candidates = state.Project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.IsReadOnly())
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        foreach (var blueprint in candidates)
        {
            var index = _creatures!.AddItem($"{blueprint.DisplayName}\n{blueprint.Slug}");
            _creatures.SetItemMetadata(index, blueprint.Id.ToString());
        }

        if (candidates.Length == 0)
        {
            SetEmpty("Aucun blueprint de créature. Créez-en un dans l’Atelier RPG Kit.");
            return;
        }

        var selectedIndex = Array.FindIndex(candidates, item => item.Id == previous);
        if (selectedIndex < 0) selectedIndex = 0;
        _creatures!.Select(selectedIndex);
        LoadBlueprint(candidates[selectedIndex]);
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 22);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "STUDIO DE CRÉATURE" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label { Text = "Identité inclusive, portraits gouvernés, corps modulaire, couleurs, appendices et voix — sauvegardés dans le module." });
        _status = new Label(); _status.AddThemeColorOverride("font_color", new Color("8ce0bd")); root.AddChild(_status);

        var split = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, SplitOffsets = [270] };
        root.AddChild(split);
        var catalog = Panel("CRÉATURES DU MODULE"); split.AddChild(catalog);
        _creatures = new ItemList { CustomMinimumSize = new Vector2(270, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _creatures.ItemSelected += OnCreatureSelected;
        catalog.GetChild<VBoxContainer>(0).AddChild(_creatures);

        var editor = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SplitOffsets = [590] };
        split.AddChild(editor);
        var formPanel = Panel("PRÉSENTATION CANONIQUE"); editor.AddChild(formPanel);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        formPanel.GetChild<VBoxContainer>(0).AddChild(tabs);
        BuildIdentityTab(tabs);
        BuildAppearanceTab(tabs);
        BuildVoiceTab(tabs);

        var previewPanel = Panel("APERÇU & VALIDATION"); editor.AddChild(previewPanel);
        var previewColumn = previewPanel.GetChild<VBoxContainer>(0);
        previewColumn.AddChild(BuildPreviewViewport());
        _portraitPreview = new TextureRect
        {
            CustomMinimumSize = new Vector2(0, 150),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        previewColumn.AddChild(_portraitPreview);
        _feedback = new RichTextLabel { BbcodeEnabled = true, FitContent = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(300, 130) };
        previewColumn.AddChild(_feedback);

        var actions = new HBoxContainer(); root.AddChild(actions);
        _previewButton = AddButton(actions, "Prévisualiser", () => Mutate(true));
        _applyButton = AddButton(actions, "Appliquer", () => Mutate(false), true);
        AddButton(actions, "Annuler", () => { _undo?.Invoke(); Refresh(); });
        AddButton(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(); });
        AddButton(actions, "Fermer", Hide);
    }

    private void BuildIdentityTab(TabContainer tabs)
    {
        var tab = AddFormTab(tabs, "Identité");
        _gender = Field(tab, "Genre extensible", "ex. woman, man, nonbinary, unspecified");
        _bodyType = Field(tab, "Type de corps", "ex. humanoid-medium");
        _pronounSubject = Field(tab, "Pronom sujet", "iel");
        _pronounObject = Field(tab, "Pronom objet", "iel");
        _pronounPossessive = Field(tab, "Pronom possessif", "son");
        _pronounReflexive = Field(tab, "Pronom réfléchi", "soi");
        _portraitExpression = Field(tab, "Expression du portrait principal", "neutral");
        _portraitPath = Field(tab, "Portrait (chemin relatif optionnel)", "assets/portraits/hero.png");
        _portraitLicense = Field(tab, "Licence SPDX du portrait", "CC0-1.0");
        _portraitHash = Field(tab, "SHA-256 du portrait", "calculé automatiquement si le fichier existe");
        _portraitAlt = Field(tab, "Texte alternatif", "Portrait du personnage");
        tab.AddChild(new Label { Text = "Expressions/résolutions supplémentaires · expression|chemin|licence|texte alternatif" });
        _portraitVariants = new TextEdit { PlaceholderText = "angry|assets/portraits/hero-angry-512.png|CC0-1.0|Portrait en colère", CustomMinimumSize = new Vector2(0, 90) };
        tab.AddChild(_portraitVariants);
        _portraitPath.TextChanged += _ => RefreshPortraitPreview();
    }

    private void BuildAppearanceTab(TabContainer tabs)
    {
        var tab = AddFormTab(tabs, "Apparence");
        _rig = Field(tab, "Rig compatible", "rig.humanoid");
        tab.AddChild(new Label { Text = "Parties du corps · slot=asset|layer|clipping|slots masqués" });
        _bodySlots = new TextEdit { PlaceholderText = "head=asset.hood|4|hide-covered|hair,ears\ntorso=asset.armor|2|allow-cloth|", CustomMinimumSize = new Vector2(0, 150) };
        tab.AddChild(_bodySlots);
        tab.AddChild(new Label { Text = "Couleur principale" });
        _primaryColor = new ColorPickerButton { Color = new Color("d68a3a"), CustomMinimumSize = new Vector2(0, 42) };
        _primaryColor.ColorChanged += _ => RebuildModelPreview();
        tab.AddChild(_primaryColor);
        _wings = Field(tab, "Ailes (asset optionnel)", "asset.wings.feathered");
        _tail = Field(tab, "Queue (asset optionnel)", "asset.tail.dragon");
        _wings.TextChanged += _ => RebuildModelPreview();
        _tail.TextChanged += _ => RebuildModelPreview();
    }

    private void BuildVoiceTab(TabContainer tabs)
    {
        var tab = AddFormTab(tabs, "Voix");
        _voiceBank = Field(tab, "Banque audio optionnelle", "audio.hero.core");
        _voiceLanguage = Field(tab, "Langue", "fr-FR");
        _voiceLicense = Field(tab, "Licence SPDX", "CC0-1.0");
        _voiceRights = Field(tab, "Provenance des droits", "author-recorded");
        _voiceEmotions = Field(tab, "Émotions (séparées par des virgules)", "calm, angry");
        _voiceVariants = Field(tab, "Variantes (séparées par des virgules)", "a, b");
        _lipSync = Field(tab, "Profil lipsync optionnel", "lipsync.fr-v1");
        tab.AddChild(new Label { Text = "Plusieurs banques audio peuvent être séparées par des virgules." });
    }

    private Control BuildPreviewViewport()
    {
        var container = new SubViewportContainer { Stretch = true, CustomMinimumSize = new Vector2(330, 300) };
        var viewport = new SubViewport { Size = new Vector2I(660, 600), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        container.AddChild(viewport);
        _previewRoot = new Node3D { Name = "CreaturePreview" }; viewport.AddChild(_previewRoot);
        var camera = new Camera3D { Position = new Vector3(0, 1.1f, 5.2f), Current = true };
        camera.RotationDegrees = new Vector3(-5, 0, 0); viewport.AddChild(camera);
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, -25, 0), LightEnergy = 1.25f });
        var environment = new WorldEnvironment();
        environment.Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("0b0812"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("806e9c"), AmbientLightEnergy = 0.7f };
        viewport.AddChild(environment);
        return container;
    }

    private void OnCreatureSelected(long index)
    {
        var state = _load?.Invoke();
        if (state is null || _creatures is null) return;
        var id = BlueprintId.Parse(_creatures.GetItemMetadata((int)index).AsString());
        var blueprint = state.Project.Blueprints.Single(item => item.Id == id);
        LoadBlueprint(blueprint);
    }

    private void LoadBlueprint(BlueprintDefinition blueprint)
    {
        _selectedBlueprintId = blueprint.Id;
        var profile = blueprint.CreaturePresentation ?? CreaturePresentationProfile.CreateDefault(Guid.NewGuid(), "author:toolset-creature-presentation");
        _draftPresentationId = profile.Id;
        _gender!.Text = profile.GenderId;
        _bodyType!.Text = profile.BodyTypeId;
        _pronounSubject!.Text = profile.Pronouns.Subject;
        _pronounObject!.Text = profile.Pronouns.Object;
        _pronounPossessive!.Text = profile.Pronouns.Possessive;
        _pronounReflexive!.Text = profile.Pronouns.Reflexive;
        var portrait = profile.Portraits.FirstOrDefault(item => item.Expression == "neutral") ?? profile.Portraits.FirstOrDefault();
        _portraitExpression!.Text = portrait?.Expression ?? "neutral";
        _portraitPath!.Text = portrait?.AssetPath ?? string.Empty;
        _portraitLicense!.Text = portrait?.LicenseSpdx ?? "CC0-1.0";
        _portraitHash!.Text = portrait?.ContentSha256 ?? string.Empty;
        _portraitAlt!.Text = portrait?.AltText ?? $"Portrait de {blueprint.DisplayName}";
        _portraitVariants!.Text = string.Join("\n", profile.Portraits.Where(item => item != portrait).Select(item =>
            $"{item.Expression}|{item.AssetPath}|{item.LicenseSpdx}|{item.AltText}"));
        _rig!.Text = profile.RigId;
        _bodySlots!.Text = string.Join("\n", profile.BodySlots.Select(item =>
            $"{item.SlotId}={item.AssetId}|{item.Layer}|{item.ClippingRule}|{string.Join(',', item.HiddenSlots)}"));
        var color = profile.MaterialColors.FirstOrDefault()?.HexColor;
        _primaryColor!.Color = string.IsNullOrWhiteSpace(color) ? new Color("d68a3a") : Color.FromString(color, new Color("d68a3a"));
        _wings!.Text = profile.Attachments.FirstOrDefault(item => item.Kind == "wings")?.AssetId ?? string.Empty;
        _tail!.Text = profile.Attachments.FirstOrDefault(item => item.Kind == "tail")?.AssetId ?? string.Empty;
        _voiceBank!.Text = profile.Voice?.AudioBankIds.FirstOrDefault() ?? string.Empty;
        _voiceLanguage!.Text = profile.Voice?.Language ?? "fr-FR";
        _voiceLicense!.Text = profile.Voice?.LicenseSpdx ?? "CC0-1.0";
        _voiceRights!.Text = profile.Voice?.RightsProvenance ?? "author-recorded";
        _voiceEmotions!.Text = string.Join(", ", profile.Voice?.EmotionIds ?? []);
        _voiceVariants!.Text = string.Join(", ", profile.Voice?.VariantIds ?? []);
        _lipSync!.Text = profile.Voice?.LipSyncProfileId ?? string.Empty;
        _status!.Text = $"{blueprint.DisplayName} · UUID {profile.Id:D} · schéma v{profile.SchemaVersion} · {profile.Provenance}";
        _feedback!.Text = blueprint.CreaturePresentation is null
            ? "[color=#b8a8ce]État vide : modèle 3D de secours actif. Configurez puis prévisualisez.[/color]"
            : "[color=#8ce0bd]Présentation canonique chargée.[/color]";
        SetEnabled(true);
        RefreshPortraitPreview();
        RebuildModelPreview();
    }

    private void Mutate(bool dryRun)
    {
        _lastCommandSucceeded = false;
        LastCommandId = Guid.Empty;
        LastPatchSummary = string.Empty;
        if (_selectedBlueprintId is null || _mutate is null || _feedback is null) return;
        try
        {
            var profile = BuildProfile();
            var result = _mutate(_selectedBlueprintId, profile, dryRun);
            LastCommandId = result.Audit.CommandId.Value;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            if (!result.Succeeded)
            {
                _feedback.Text = $"[color=#ff7777]Configuration refusée[/color]\n{string.Join("\n", result.Diagnostics.Select(item => $"{item.Code} · {item.Message}"))}";
                return;
            }

            _lastCommandSucceeded = true;
            _feedback.Text = dryRun
                ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                : $"[color=#8ce0bd]APPLIQUÉ AU PROJET[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision} · utilisez Sauvegarder pour écrire sur disque";
            if (!dryRun) Refresh();
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException)
        {
            _feedback.Text = $"[color=#ff7777]Erreur de formulaire[/color]\n{exception.Message}";
        }
    }

    private CreaturePresentationProfile BuildProfile()
    {
        var rig = _rig!.Text.Trim();
        var bodySlots = new List<CreaturePresentationProfile.BodySlot>();
        var layer = 0;
        foreach (var raw in _bodySlots!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var options = raw.Split('|', StringSplitOptions.TrimEntries);
            var parts = options[0].Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
                throw new FormatException($"Partie du corps invalide : '{raw}'. Format attendu slot=asset.");
            var explicitLayer = options.Length > 1 && int.TryParse(options[1], out var parsedLayer) ? parsedLayer : layer;
            var clipping = options.Length > 2 && !string.IsNullOrWhiteSpace(options[2]) ? options[2] : "hide-covered";
            var hidden = options.Length > 3
                ? options[3].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [];
            bodySlots.Add(new(parts[0], parts[1], rig, explicitLayer, hidden, clipping));
            layer++;
        }

        var portraits = new List<CreaturePresentationProfile.PortraitVariant>();
        if (!string.IsNullOrWhiteSpace(_portraitPath!.Text))
        {
            var (width, height, hash) = ResolvePortraitMetadata(_portraitPath.Text.Trim(), _portraitHash!.Text.Trim());
            portraits.Add(new(_portraitExpression!.Text.Trim(), _portraitPath.Text.Trim().Replace('\\', '/'), width, height,
                _portraitLicense!.Text.Trim(), hash, _portraitAlt!.Text.Trim()));
        }
        foreach (var raw in _portraitVariants!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split('|', 4, StringSplitOptions.TrimEntries);
            if (parts.Length != 4 || parts.Any(string.IsNullOrWhiteSpace))
                throw new FormatException($"Portrait invalide : '{raw}'. Format attendu expression|chemin|licence|texte alternatif.");
            var (width, height, hash) = ResolvePortraitMetadata(parts[1], string.Empty);
            portraits.Add(new(parts[0], parts[1].Replace('\\', '/'), width, height, parts[2], hash, parts[3]));
        }

        var attachments = new List<CreaturePresentationProfile.Attachment>();
        if (!string.IsNullOrWhiteSpace(_wings!.Text)) attachments.Add(new("back", "wings", _wings.Text.Trim(), rig));
        if (!string.IsNullOrWhiteSpace(_tail!.Text)) attachments.Add(new("hips", "tail", _tail.Text.Trim(), rig));
        CreaturePresentationProfile.VoiceProfile? voice = null;
        if (!string.IsNullOrWhiteSpace(_voiceBank!.Text))
        {
            voice = new("voice.creature", _voiceLanguage!.Text.Trim(), SplitList(_voiceBank.Text),
                SplitList(_voiceEmotions!.Text), string.IsNullOrWhiteSpace(_lipSync!.Text) ? null : _lipSync.Text,
                SplitList(_voiceVariants!.Text), _voiceLicense!.Text.Trim(), _voiceRights!.Text.Trim());
        }

        return new CreaturePresentationProfile(
            _draftPresentationId, 1, "author:toolset-creature-presentation", _gender!.Text, _bodyType!.Text,
            new(_pronounSubject!.Text, _pronounObject!.Text, _pronounPossessive!.Text, _pronounReflexive!.Text),
            portraits, portraits.Count == 0, rig, bodySlots,
            [new("primary", $"#{_primaryColor!.Color.ToHtml(false).ToUpperInvariant()}")], attachments, voice);

        static string[] SplitList(string value) =>
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private (int Width, int Height, string Hash) ResolvePortraitMetadata(string relativePath, string enteredHash)
    {
        var safePath = relativePath.Replace('\\', '/');
        if (Path.IsPathRooted(safePath) || safePath.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new FormatException("Le portrait doit utiliser un chemin relatif sûr dans le module.");
        var root = _workspaceRoot?.Invoke();
        var absolute = string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, safePath.Replace('/', Path.DirectorySeparatorChar));
        if (absolute is not null && File.Exists(absolute))
        {
            var image = Image.LoadFromFile(absolute);
            return (image.GetWidth(), image.GetHeight(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolute))).ToLowerInvariant());
        }

        return (512, 512, enteredHash);
    }

    private void RefreshPortraitPreview()
    {
        if (_portraitPreview is null || _portraitPath is null) return;
        _portraitPreview.Texture = null;
        var root = _workspaceRoot?.Invoke();
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(_portraitPath.Text)) return;
        var path = Path.Combine(root, _portraitPath.Text.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return;
        try { _portraitPreview.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(path)); }
        catch (Exception exception) when (exception is IOException or ArgumentException) { GD.PrintErr($"OPENRPG_FORGE_CREATURE_PORTRAIT_PREVIEW_FAILED reason={exception.Message}"); }
    }

    private void RebuildModelPreview()
    {
        if (_previewRoot is null || !GodotObject.IsInstanceValid(_previewRoot)) return;
        foreach (var child in _previewRoot.GetChildren()) { _previewRoot.RemoveChild(child); child.Free(); }
        var color = _primaryColor?.Color ?? new Color("d68a3a");
        AddMesh(new CapsuleMesh { Radius = 0.58f, Height = 2.2f }, new Vector3(0, 0.2f, 0), Vector3.Zero, color);
        AddMesh(new SphereMesh { Radius = 0.42f, Height = 0.84f }, new Vector3(0, 1.55f, 0), Vector3.Zero, color.Lightened(0.12f));
        if (!string.IsNullOrWhiteSpace(_wings?.Text))
        {
            AddMesh(new BoxMesh { Size = new Vector3(1.35f, 0.75f, 0.12f) }, new Vector3(-0.75f, 0.75f, 0.25f), new Vector3(0, 0, 28), color.Lightened(0.2f));
            AddMesh(new BoxMesh { Size = new Vector3(1.35f, 0.75f, 0.12f) }, new Vector3(0.75f, 0.75f, 0.25f), new Vector3(0, 0, -28), color.Lightened(0.2f));
        }
        if (!string.IsNullOrWhiteSpace(_tail?.Text))
            AddMesh(new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.18f, Height = 1.35f }, new Vector3(0, 0.2f, 0.55f), new Vector3(65, 0, 0), color.Darkened(0.15f));

        void AddMesh(PrimitiveMesh mesh, Vector3 position, Vector3 rotation, Color meshColor)
        {
            var instance = new MeshInstance3D
            {
                Mesh = mesh,
                Position = position,
                RotationDegrees = rotation,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = meshColor,
                    Metallic = 0.08f,
                    Roughness = 0.58f,
                },
            };
            _previewRoot.AddChild(instance);
        }
    }

    private void SetEmpty(string message)
    {
        _selectedBlueprintId = null;
        if (_status is not null) _status.Text = message;
        if (_feedback is not null) _feedback.Text = $"[color=#b8a8ce]{message}[/color]";
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
    {
        if (_previewButton is not null) _previewButton.Disabled = !enabled;
        if (_applyButton is not null) _applyButton.Disabled = !enabled;
    }

    private static VBoxContainer AddFormTab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer { Name = name, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7); scroll.AddChild(form);
        tabs.AddChild(scroll);
        return form;
    }

    private static LineEdit Field(Container owner, string label, string placeholder)
    {
        owner.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder };
        owner.AddChild(field);
        return field;
    }

    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 9); panel.AddChild(box);
        var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new Color("ffad70")); box.AddChild(label);
        return panel;
    }

    private static Button AddButton(Container owner, string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(120, 38) };
        if (primary) button.AddThemeColorOverride("font_color", new Color("fff4ec"));
        button.Pressed += action; owner.AddChild(button); return button;
    }
}
#endif
