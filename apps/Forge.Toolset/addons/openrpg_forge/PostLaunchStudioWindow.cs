#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.RuntimeExtensions;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class PostLaunchStudioWindow : Window
{
    private readonly Dictionary<string, CheckBox> _checks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpinBox> _numbers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LineEdit> _text = new(StringComparer.Ordinal);
    private Func<ProjectState?>? _load;
    private Func<ModuleRuntimeExtensionProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private bool _lastSucceeded;

    public int SectionCount => 6;
    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;

    public void Initialize(Func<ProjectState?> load,
        Func<ModuleRuntimeExtensionProfile, bool, CommandResult> mutate, Action undo, Action redo,
        Action<Window> applyIcon)
    {
        _load = load;
        _mutate = mutate;
        _undo = undo;
        _redo = redo;
        _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Studio production et expérience · OpenRPG Forge";
        MinSize = new(1180, 760);
        Size = new(1500, 900);
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

    public bool ConfigureForSmokeTest()
    {
        foreach (var check in _checks.Values) check.ButtonPressed = true;
        _numbers["gameplay.seats"].Value = 6;
        _numbers["network.players"].Value = 64;
        _numbers["creator.dynamic"].Value = 8;
        _numbers["production.duration"].Value = 120;
        _numbers["production.players"].Value = 64;
        _text["gameplay.channel"].Text = "stable";
        _text["director.query"].Text = "tag:guardian";
        _text["network.platforms"].Text = "windows,linux,macos";
        _text["production.provider"].Text = "local";
        RebuildSummary();
        return _load?.Invoke() is not null;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return _lastSucceeded; }
    public bool ApplyForSmokeTest() { Mutate(false); return _lastSucceeded; }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "PRODUCTION & EXPÉRIENCE" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Mobilité, Live Director, réseau, plateforme, distribution, génération média et endurance — profils explicites, locaux et annulables.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var split = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SplitOffsets = [960] };
        root.AddChild(split);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        split.AddChild(tabs);
        BuildGameplay(Tab(tabs, "MOBILITÉ"));
        BuildContent(Tab(tabs, "CONTENU"));
        BuildDirector(Tab(tabs, "LIVE DIRECTOR"));
        BuildNetwork(Tab(tabs, "RÉSEAU"));
        BuildCreator(Tab(tabs, "PLATEFORME"));
        BuildProduction(Tab(tabs, "PRODUCTION"));
        var preview = new VBoxContainer { CustomMinimumSize = new(390, 600) };
        split.AddChild(preview);
        var heading = new Label { Text = "APERÇU SÉMANTIQUE" };
        heading.AddThemeColorOverride("font_color", new("ffad73"));
        preview.AddChild(heading);
        _summary = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        preview.AddChild(_summary);
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(360, 120) };
        preview.AddChild(_feedback);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        actions.AddChild(Button("Actualiser", RebuildSummary));
        actions.AddChild(Button("Prévisualiser", () => Mutate(true)));
        actions.AddChild(Button("Appliquer", () => Mutate(false), true));
        actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Fermer", Hide));
    }

    private void BuildGameplay(VBoxContainer form)
    {
        Check(form, "gameplay.zero", "Apesanteur à six axes", "Inertie, propulseurs, aide d’orientation et transitions de volume.");
        Check(form, "gameplay.vehicle", "Véhicules autoritaires", "Sièges, possession, dégâts et réplication.");
        Number(form, "gameplay.seats", "Nombre maximal de sièges", 1, 64, 4);
        Check(form, "gameplay.mount", "Montures persistantes", "Rider, restrictions de combat, inventaire et sauvegarde.");
    }

    private void BuildContent(VBoxContainer form)
    {
        Check(form, "content.achievement", "Accomplissements", "Événements et seuils, sans télémétrie obligatoire.");
        Check(form, "content.curated", "Packs curatés signés", "Licences autorisées, canal et fallback hors ligne.");
        Text(form, "gameplay.channel", "Canal de distribution", "stable");
    }

    private void BuildDirector(VBoxContainer form)
    {
        Check(form, "director.rest", "Repos forcé", "Raison, override explicite et événement de fin.");
        Check(form, "director.level", "Migration de niveau", "Rebuild et snapshot de rollback obligatoires.");
        Check(form, "director.appearance", "Apparence temporaire", "Échelle bornée et validation d’asset.");
        Check(form, "director.record", "Enregistrement de session", "Commandes, événements, bookmarks et confidentialité.");
        Text(form, "director.query", "Groupe raccourci 1", "tag:guardian");
    }

    private void BuildNetwork(VBoxContainer form)
    {
        Check(form, "network.voice", "Voix positionnelle", "Opus, push-to-talk et modération.");
        Check(form, "network.shard", "Zones serveur", "Affectation de zones et capacité déclarée.");
        Number(form, "network.players", "Capacité du shard", 1, 100000, 64);
        Check(form, "network.reload", "Hot reload atomique", "Drain, swap et rollback.");
        Check(form, "network.extensions", "Extensions serveur signées", "Isolation et capacités autorisées.");
        Text(form, "network.platforms", "Plateformes protocole", "windows,linux,macos");
    }

    private void BuildCreator(VBoxContainer form)
    {
        Check(form, "creator.device", "Requêtes périphériques consenties", "Allowlist et identifiants stables masqués.");
        Check(form, "creator.dynamic", "Zones dynamiques autoritaires", "Dépendances, quota et durée de vie.");
        Number(form, "creator.dynamic", "Instances de zones maximales", 1, 1024, 8);
        Check(form, "creator.party", "Contrôle de groupe", "Switch direct et commandes de formation.");
        Check(form, "creator.observe", "Profilage Tracy/OpenTelemetry", "Capture bornée incluant scripts et réseau.");
        Check(form, "creator.graphics", "Qualité haute", "Eau améliorée, MSAA 8× et anisotropie 16× avec fallback.");
    }

    private void BuildProduction(VBoxContainer form)
    {
        Check(form, "production.media", "Génération média gouvernée", "VFX, audio et images en staging avec droits et revue humaine.");
        Text(form, "production.provider", "Fournisseur optionnel", "local");
        Check(form, "production.soak", "Soak test serveur", "Mémoire, handles, base, latence et population.");
        Number(form, "production.duration", "Durée cible (minutes)", 1, 10080, 120);
        Number(form, "production.players", "Joueurs virtuels", 1, 100000, 64);
    }

    public void Refresh()
    {
        var state = _load?.Invoke();
        if (state is null || _checks.Count == 0) return;
        var profile = state.Project.Module.RuntimePolicies.Extensions.PostLaunch;
        Set("gameplay.zero", profile.Gameplay.ZeroGravity.Enabled);
        Set("gameplay.vehicle", profile.Gameplay.Vehicles.Enabled);
        Set("gameplay.mount", profile.Gameplay.Mounts.Enabled);
        Set("content.achievement", profile.Gameplay.Achievements.Count > 0);
        Set("content.curated", profile.Gameplay.CuratedPacks.RequireTrustedSignature);
        Set("director.rest", profile.LiveDirector.ForcedRest.Enabled);
        Set("director.level", profile.LiveDirector.LevelAdjustment.Enabled);
        Set("director.appearance", profile.LiveDirector.AppearanceOverrides.Enabled);
        Set("director.record", profile.LiveDirector.SessionRecording.Enabled);
        Set("network.voice", profile.Network.Voice.Enabled);
        Set("network.shard", profile.Network.Shards.Count > 0);
        Set("network.reload", profile.Network.HotReload.Enabled);
        Set("network.extensions", profile.Network.ServerExtensions.Enabled);
        Set("creator.device", profile.CreatorExperience.DeviceQueries.Enabled);
        Set("creator.dynamic", profile.CreatorExperience.DynamicAreas.Enabled);
        Set("creator.party", profile.CreatorExperience.PartyControl.Enabled);
        Set("creator.observe", profile.CreatorExperience.Observability.Enabled);
        Set("creator.graphics", profile.CreatorExperience.Graphics.AntiAliasing != "disabled");
        Set("production.media", profile.Production.MediaGeneration.Enabled);
        Set("production.soak", profile.Production.SoakTests.Enabled);
        _numbers["gameplay.seats"].Value = profile.Gameplay.Vehicles.MaximumSeats;
        _numbers["network.players"].Value = profile.Network.Shards.FirstOrDefault()?.MaximumPlayers ?? 64;
        _numbers["creator.dynamic"].Value = profile.CreatorExperience.DynamicAreas.MaximumInstances;
        _numbers["production.duration"].Value = profile.Production.SoakTests.DurationMinutes;
        _numbers["production.players"].Value = profile.Production.SoakTests.VirtualPlayers;
        _text["gameplay.channel"].Text = profile.Gameplay.CuratedPacks.Channel;
        _text["director.query"].Text = profile.LiveDirector.SavedGroups.FirstOrDefault()?.Query ?? "tag:guardian";
        _text["network.platforms"].Text = string.Join(',', profile.Network.Protocol.Platforms);
        _text["production.provider"].Text = profile.Production.MediaGeneration.Providers.FirstOrDefault() ?? "local";
        RebuildSummary();
    }

    private ModuleRuntimeExtensionProfile BuildProfile()
    {
        var state = _load?.Invoke() ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var template = CryptPostLaunchAuthoringService.Create(state.Project, "forge-ui:g13-ac-al");
        var source = template.PostLaunch;
        var achievements = _checks["content.achievement"].ButtonPressed ? source.Gameplay.Achievements : [];
        var gameplay = new GameplayProductionProfile(source.Gameplay.Id, 1,
            source.Gameplay.ZeroGravity with { Enabled = Enabled("gameplay.zero") },
            source.Gameplay.Vehicles with { Enabled = Enabled("gameplay.vehicle"), MaximumSeats = Int("gameplay.seats") },
            source.Gameplay.Mounts with { Enabled = Enabled("gameplay.mount") }, achievements,
            source.Gameplay.CuratedPacks with
            {
                RequireTrustedSignature = Enabled("content.curated"),
                Channel = _text["gameplay.channel"].Text,
            }, "forge-ui:g13-ac-al");
        var groups = source.LiveDirector.SavedGroups.Select(item => item with { Query = _text["director.query"].Text }).ToArray();
        var director = new LiveDirectorProductionProfile(source.LiveDirector.Id, 1,
            source.LiveDirector.ForcedRest with { Enabled = Enabled("director.rest") },
            source.LiveDirector.LevelAdjustment with { Enabled = Enabled("director.level") }, groups,
            source.LiveDirector.AppearanceOverrides with { Enabled = Enabled("director.appearance") },
            source.LiveDirector.SessionRecording with { Enabled = Enabled("director.record") }, "forge-ui:g13-ac-al");
        var shards = Enabled("network.shard")
            ? source.Network.Shards.Select(item => item with { MaximumPlayers = Int("network.players") }).ToArray()
            : [];
        var platforms = _text["network.platforms"].Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var network = new NetworkProductionProfile(source.Network.Id, 1,
            source.Network.Voice with { Enabled = Enabled("network.voice") }, shards,
            source.Network.HotReload with { Enabled = Enabled("network.reload") },
            source.Network.ServerExtensions with { Enabled = Enabled("network.extensions") },
            source.Network.Protocol with { Platforms = platforms }, "forge-ui:g13-ac-al");
        var creator = new CreatorExperienceProductionProfile(source.CreatorExperience.Id, 1,
            source.CreatorExperience.Water with { FallbackQuality = Enabled("creator.graphics") ? "high" : "low" },
            source.CreatorExperience.DeviceQueries with { Enabled = Enabled("creator.device") },
            source.CreatorExperience.ResourceCommands,
            source.CreatorExperience.DynamicAreas with { Enabled = Enabled("creator.dynamic"), MaximumInstances = Int("creator.dynamic") },
            source.CreatorExperience.PartyControl with { Enabled = Enabled("creator.party") },
            source.CreatorExperience.ProgressionTracks, source.CreatorExperience.RepositoryMetadata,
            source.CreatorExperience.Observability with { Enabled = Enabled("creator.observe") },
            source.CreatorExperience.Graphics with
            {
                AntiAliasing = Enabled("creator.graphics") ? "msaa8" : "disabled",
                AnisotropicLevel = Enabled("creator.graphics") ? 16 : 1,
            }, "forge-ui:g13-ac-al");
        var provider = _text["production.provider"].Text.Trim();
        var production = new ProductionAutomationProfile(source.Production.Id, 1,
            source.Production.MediaGeneration with
            {
                Enabled = Enabled("production.media"),
                Providers = string.IsNullOrWhiteSpace(provider) ? [] : [provider],
            }, source.Production.SoakTests with
            {
                Enabled = Enabled("production.soak"),
                DurationMinutes = Int("production.duration"),
                VirtualPlayers = Int("production.players"),
            }, "forge-ui:g13-ac-al");
        var post = new PostLaunchCapabilityProfile(source.Id, 1, gameplay, director, network, creator,
            production, "forge-ui:g13-ac-al");
        return new ModuleRuntimeExtensionProfile(template.Id, 1, template.RuleSchemas, template.UiDocuments,
            template.JsonDocuments, template.SnapshotPolicy, "forge-ui:g13-ac-al", template.ModernTooling,
            template.OperationalReadiness, post);
    }

    private void Mutate(bool dryRun)
    {
        try
        {
            var result = _mutate?.Invoke(BuildProfile(), dryRun);
            _lastSucceeded = result?.Succeeded == true;
            LastCommandId = result?.Audit?.CommandId.Value ?? Guid.Empty;
            LastPatchSummary = result?.Patch?.Summary ?? string.Empty;
            _feedback!.Text = _lastSucceeded
                ? $"[color=#86e0b1]✓ {(dryRun ? "Aperçu validé" : "Configuration appliquée")}[/color]\n{LastPatchSummary}"
                : $"[color=#ff8e8e]✕ {string.Join("\n", result?.Diagnostics.Select(item => $"{item.Code} · {item.Message}") ?? [])}[/color]";
            if (!dryRun && _lastSucceeded) Refresh();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            _lastSucceeded = false;
            _feedback!.Text = $"[color=#ff8e8e]✕ {exception.Message}[/color]";
        }
    }

    private void RebuildSummary()
    {
        if (_summary is null) return;
        _summary.Text = $"[b]10 tranches · 27 capacités[/b]\n\n" +
            $"Mobilité · zero-g={Enabled("gameplay.zero")} · véhicules={Enabled("gameplay.vehicle")} · montures={Enabled("gameplay.mount")}\n" +
            $"Contenu · accomplissements={Enabled("content.achievement")} · packs signés={Enabled("content.curated")}\n" +
            $"Live Director · repos={Enabled("director.rest")} · niveaux={Enabled("director.level")} · apparence={Enabled("director.appearance")} · replay={Enabled("director.record")}\n" +
            $"Réseau · voix={Enabled("network.voice")} · shards={Enabled("network.shard")} · reload={Enabled("network.reload")} · extensions={Enabled("network.extensions")}\n" +
            $"Plateforme · périphériques={Enabled("creator.device")} · zones={Enabled("creator.dynamic")} · groupe={Enabled("creator.party")} · profilage={Enabled("creator.observe")}\n" +
            $"Production · média={Enabled("production.media")} · soak={Enabled("production.soak")}";
    }

    private static VBoxContainer Tab(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer { Name = name, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        tabs.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(form);
        return form;
    }

    private void Check(Control parent, string key, string label, string hint)
    {
        var check = new CheckBox { Text = label, TooltipText = hint };
        check.Toggled += _ => RebuildSummary();
        parent.AddChild(check);
        parent.AddChild(new Label { Text = hint, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _checks[key] = check;
    }

    private void Number(Control parent, string key, string label, double min, double max, double value)
    {
        parent.AddChild(new Label { Text = label });
        var number = new SpinBox { MinValue = min, MaxValue = max, Value = value, CustomMinimumSize = new(280, 36) };
        parent.AddChild(number);
        _numbers[key] = number;
    }

    private void Text(Control parent, string key, string label, string value)
    {
        parent.AddChild(new Label { Text = label });
        var text = new LineEdit { Text = value, CustomMinimumSize = new(360, 36) };
        parent.AddChild(text);
        _text[key] = text;
    }

    private static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(primary ? 170 : 120, 40) };
        button.Pressed += action;
        if (primary) button.AddThemeColorOverride("font_color", new("ffffff"));
        return button;
    }

    private bool Enabled(string key) => _checks.TryGetValue(key, out var check) && check.ButtonPressed;
    private int Int(string key) => (int)_numbers[key].Value;
    private void Set(string key, bool value) => _checks[key].ButtonPressed = value;
}
#endif
