#if TOOLS
using System.Globalization;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AreaExperienceWindow : Window
{
    private Func<ProjectState?>? _state;
    private Func<AreaId?>? _activeArea;
    private Func<AreaId, AreaExperienceProfile, bool>? _apply;
    private Action<Window>? _brand;
    private AreaId? _areaId;
    private AreaExperienceProfile? _profile;
    private TextEdit? _cues;
    private TextEdit? _scatter;
    private TextEdit? _voices;
    private TextEdit? _patrols;
    private LineEdit? _loadingTitle;
    private LineEdit? _loadingImage;
    private LineEdit? _loadingVideo;
    private TextEdit? _loadingTips;
    private LineEdit? _loadingFallback;
    private CheckBox? _showProgress;
    private CheckBox? _prefetch;
    private SpinBox? _minimumVisible;
    private Label? _summary;
    private Label? _status;

    internal string Summary => _profile is null ? "unavailable" :
        $"cues={_profile.AudioCues.Count};scatter={_profile.ScatterEmitters.Count};" +
        $"voices={_profile.Voices.Count};patrols={_profile.PatrolRoutes.Count};loading={_profile.Loading.Title}";

    public void Initialize(Func<ProjectState?> state, Func<AreaId?> activeArea,
        Func<AreaId, AreaExperienceProfile, bool> apply, Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _activeArea = activeArea ?? throw new ArgumentNullException(nameof(activeArea));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Audio, patrouilles & chargement · OpenRPG Forge";
        MinSize = new Vector2I(1220, 790); Size = new Vector2I(1320, 850); Transient = true; Visible = false;
        Build();
    }

    public void Open() { Refresh(); PopupCentered(Size); _brand?.Invoke(this); }
    internal bool ConfigureForSmokeTest(AreaId areaId, AreaExperienceProfile profile)
    { _areaId = areaId; _profile = profile; Populate(); return true; }
    internal bool ApplyForSmokeTest() => Save();

    private void Build()
    {
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); margin.AddChild(root);
        var title = new Label { Text = "AUDIO SPATIAL, VOIX, PATROUILLES & CHARGEMENT", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Préparez l’expérience de transition de zone : sons dispersés, voix localisées, reprise des patrouilles et écran de chargement accessible.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _summary = new Label(); _summary.AddThemeColorOverride("font_color", new Color("c7b3dc")); root.AddChild(_summary);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(scroll);
        var columns = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12); scroll.AddChild(columns);

        var audio = Card(columns, "CUES & SCATTER SPATIAL");
        audio.AddChild(Hint("Cue : clé | ressource | bus | volume | boucle | licence | SHA-256"));
        _cues = Edit(340, 135); audio.AddChild(_cues);
        audio.AddChild(Hint("Scatter : cue | centre xyz | taille xyz | surface | seed | min | max | volume | nav | biomes | exclusions"));
        _scatter = Edit(340, 165); audio.AddChild(_scatter);

        var actors = Card(columns, "VOIX & PATROUILLES");
        actors.AddChild(Hint("Voix : blueprint | locale | ressource | sous-titre | phonèmes | licence | SHA-256 | TTS | autorisé | consentement | attribution"));
        _voices = Edit(380, 165); actors.AddChild(_voices);
        actors.AddChild(Hint("Patrouille : propriétaire | zone source | zone cible | portail | reprise | précharger | autorité | boucle"));
        _patrols = Edit(380, 150); actors.AddChild(_patrols);

        var loading = Card(columns, "PRÉSENTATION DE CHARGEMENT");
        _loadingTitle = new LineEdit(); loading.AddChild(Row("Titre", _loadingTitle));
        _loadingImage = new LineEdit(); loading.AddChild(Row("Image", _loadingImage));
        _loadingVideo = new LineEdit(); loading.AddChild(Row("Vidéo", _loadingVideo));
        loading.AddChild(Hint("Astuces : locale | texte")); _loadingTips = Edit(330, 120); loading.AddChild(_loadingTips);
        _loadingFallback = new LineEdit(); loading.AddChild(Row("Repli accessible", _loadingFallback));
        _showProgress = new CheckBox { Text = "Afficher la progression", ButtonPressed = true };
        loading.AddChild(_showProgress);
        _prefetch = new CheckBox { Text = "Précharger les ressources requises", ButtonPressed = true };
        loading.AddChild(_prefetch);
        _minimumVisible = new SpinBox { MinValue = 0, MaxValue = 30000, Step = 50, Value = 250 };
        loading.AddChild(Row("Durée minimale (ms)", _minimumVisible));

        var footer = new HBoxContainer();
        _status = new Label { Text = "Prêt · aucune mutation.", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd")); footer.AddChild(_status);
        var apply = new Button { Text = "Prévisualiser et appliquer", CustomMinimumSize = new Vector2(220, 40) };
        apply.Pressed += () => Save(); footer.AddChild(apply);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide; footer.AddChild(close); root.AddChild(footer);
    }

    private void Refresh()
    {
        var state = _state?.Invoke(); _areaId = _activeArea?.Invoke();
        if (state is null || _areaId is null) { _status!.Text = "Aucun projet ou zone active."; return; }
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == _areaId);
        if (area is null) { _status!.Text = "Zone active introuvable."; return; }
        _profile = area.RuntimePolicies.Experience; Populate();
    }

    private void Populate()
    {
        if (_profile is null || _areaId is null) return;
        _cues!.Text = string.Join("\n", _profile.AudioCues.Select(item => string.Join(" | ", item.CueId,
            item.Resource, item.Bus, F(item.Volume), item.Loop ? "oui" : "non", item.LicenseSpdx,
            item.ContentSha256)));
        _scatter!.Text = string.Join("\n", _profile.ScatterEmitters.Select(item => string.Join(" | ", item.CueId,
            F(item.Center.X), F(item.Center.Y), F(item.Center.Z), F(item.Size.X), F(item.Size.Y), F(item.Size.Z),
            item.SurfaceOnly ? "oui" : "non", item.DeterministicSeed, F(item.MinimumIntervalSeconds),
            F(item.MaximumIntervalSeconds), F(item.Volume), item.RequireNavigable ? "oui" : "non",
            string.Join(',', item.BiomeTags), string.Join(',', item.ExclusionTags))));
        _voices!.Text = string.Join("\n", _profile.Voices.SelectMany(voice => voice.Takes.Select(take =>
            string.Join(" | ", voice.CreatureBlueprintId, take.Locale, take.Resource, take.Subtitle,
                take.PhonemeResource, take.LicenseSpdx, take.ContentSha256, voice.TtsPluginId,
                voice.Consent.TtsUseAllowed ? "oui" : "non", voice.Consent.ConsentReference,
                voice.Consent.SpeakerAttribution))));
        _patrols!.Text = string.Join("\n", _profile.PatrolRoutes.SelectMany(route => route.Segments.Select(segment =>
            string.Join(" | ", route.OwnerId, segment.FromAreaId, segment.ToAreaId, segment.PortalId,
                segment.ResumeWaypointId, segment.PrefetchDestination ? "oui" : "non", segment.Authority,
                route.Loop ? "oui" : "non"))));
        var loading = _profile.Loading; _loadingTitle!.Text = loading.Title; _loadingImage!.Text = loading.ImageResource;
        _loadingVideo!.Text = loading.VideoResource;
        _loadingTips!.Text = string.Join("\n", loading.LocalizedTips.SelectMany(pair => pair.Value.Select(tip =>
            $"{pair.Key} | {tip}")));
        _loadingFallback!.Text = loading.AccessibleFallback; _showProgress!.ButtonPressed = loading.ShowProgress;
        _prefetch!.ButtonPressed = loading.PrefetchRequiredResources;
        _minimumVisible!.Value = loading.MinimumVisibleMilliseconds;
        _summary!.Text = $"Schéma v{_profile.SchemaVersion} · zone {_areaId} · {Summary}";
        _status!.Text = "Prêt · profil canonique chargé.";
    }

    private bool Save()
    {
        if (_profile is null || _areaId is null) return false;
        try
        {
            var cues = ParseCues();
            var loading = new AreaLoadingPresentation(_profile.Loading.Id, _loadingTitle!.Text,
                _loadingImage!.Text, _loadingVideo!.Text, ParseTips(), _showProgress!.ButtonPressed,
                _loadingFallback!.Text, _prefetch!.ButtonPressed, (int)_minimumVisible!.Value, "forge-ui");
            var profile = new AreaExperienceProfile(_profile.Id, 1, cues, ParseScatter(cues), ParseVoices(),
                ParsePatrols(), loading, "forge-ui");
            var succeeded = _apply?.Invoke(_areaId, profile) == true;
            _status!.Text = succeeded ? "✓ Audio, patrouilles et chargement validés, persistants et annulables." :
                "✕ Application refusée · consultez les diagnostics.";
            if (succeeded) { _profile = profile; Populate(); }
            return succeeded;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        { _status!.Text = $"✕ {exception.Message}"; return false; }
    }

    private IReadOnlyList<AudioCueDefinition> ParseCues()
    {
        var ids = _profile!.AudioCues.ToDictionary(item => item.CueId, item => item.Id, StringComparer.Ordinal);
        return Lines(_cues!).Select(raw =>
        {
            var p = Parts(raw, 7); return new AudioCueDefinition(ids.GetValueOrDefault(p[0], Guid.NewGuid()), p[0],
                p[1], p[2], Float(p[3]), Yes(p[4]), p[5], p[6], "1.0", "forge-ui");
        }).ToArray();
    }

    private IReadOnlyList<SpatialScatterEmitter> ParseScatter(IReadOnlyList<AudioCueDefinition> cues)
    {
        var existing = _profile!.ScatterEmitters;
        return Lines(_scatter!).Select((raw, index) =>
        {
            var p = Parts(raw, 15);
            var id = index < existing.Count && existing[index].CueId == p[0] ? existing[index].Id : Guid.NewGuid();
            return new SpatialScatterEmitter(id,
                p[0], new Vector3Value(Float(p[1]), Float(p[2]), Float(p[3])),
                new Vector3Value(Float(p[4]), Float(p[5]), Float(p[6])), Yes(p[7]), int.Parse(p[8],
                    CultureInfo.InvariantCulture), Float(p[9]), Float(p[10]), Float(p[11]), Yes(p[12]),
                Csv(p[13]), Csv(p[14]), "forge-ui");
        }).ToArray();
    }

    private IReadOnlyList<CreatureVoiceProfile> ParseVoices()
    {
        var ids = _profile!.Voices.ToDictionary(item => item.CreatureBlueprintId.ToString(), item => item.Id,
            StringComparer.Ordinal);
        return Lines(_voices!).Select(raw => Parts(raw, 11)).GroupBy(p => p[0], StringComparer.Ordinal).Select(group =>
        {
            var rows = group.ToArray();
            if (rows.Any(row => row[7] != rows[0][7] || Yes(row[8]) != Yes(rows[0][8]) ||
                    row[9] != rows[0][9] || row[10] != rows[0][10]))
                throw new FormatException($"Les métadonnées TTS doivent être identiques pour {group.Key}.");
            var takes = rows.Select(p => new LocalizedVoiceTake(p[1], p[2], p[3], p[4], p[5], p[6])).ToArray();
            return new CreatureVoiceProfile(ids.GetValueOrDefault(group.Key, Guid.NewGuid()),
                BlueprintId.Parse(group.Key), takes[0].Locale, takes, rows[0][7],
                new VoiceConsentMetadata(takes.Any(take => string.IsNullOrWhiteSpace(take.Resource)),
                    Yes(rows[0][8]), rows[0][9], rows[0][10]), "forge-ui");
        }).ToArray();
    }

    private IReadOnlyList<CrossAreaPatrolRoute> ParsePatrols()
    {
        var routes = _profile!.PatrolRoutes.ToDictionary(item => item.OwnerId, StringComparer.Ordinal);
        return Lines(_patrols!).Select(raw => Parts(raw, 8)).GroupBy(p => p[0], StringComparer.Ordinal).Select(group =>
        {
            var rows = group.ToArray();
            if (rows.Any(row => Yes(row[7]) != Yes(rows[0][7])))
                throw new FormatException($"La boucle doit être identique pour toute la route {group.Key}.");
            var existing = routes.GetValueOrDefault(group.Key);
            var segments = rows.Select((p, index) => new CrossAreaPatrolSegment(
                existing is not null && index < existing.Segments.Count ? existing.Segments[index].Id : Guid.NewGuid(),
                AreaId.Parse(p[1]), AreaId.Parse(p[2]), PortalId.Parse(p[3]), Guid.Parse(p[4]), Yes(p[5]),
                Enum.Parse<PatrolOwnershipAuthority>(p[6], true))).ToArray();
            return new CrossAreaPatrolRoute(existing?.Id ?? Guid.NewGuid(), group.Key, segments, Yes(rows[0][7]),
                "forge-ui");
        }).ToArray();
    }

    private IReadOnlyDictionary<string, IReadOnlyList<string>> ParseTips() => Lines(_loadingTips!)
        .Select(raw => Parts(raw, 2)).GroupBy(p => p[0], StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key.ToLowerInvariant(), group => (IReadOnlyList<string>)group.Select(p => p[1]).ToArray(),
            StringComparer.Ordinal);

    private static IEnumerable<string> Lines(TextEdit edit) =>
        edit.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string[] Parts(string raw, int count)
    {
        var values = raw.Split('|').Select(item => item.Trim()).ToArray();
        if (values.Length != count) throw new FormatException($"Ligne invalide ({count} champs attendus) : {raw}");
        return values;
    }
    private static float Float(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static bool Yes(string value) => value.Trim().ToLowerInvariant() is "oui" or "yes" or "true" or "on";
    private static IReadOnlyList<string> Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries |
        StringSplitOptions.TrimEntries).Select(item => item.ToLowerInvariant()).ToArray();
    private static TextEdit Edit(float width, float height) => new() { CustomMinimumSize = new Vector2(width, height) };
    private static Label Hint(string text) => new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private static HBoxContainer Row(string label, Control control)
    { var row = new HBoxContainer(); row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(135, 0) }); control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(control); return row; }
    private static VBoxContainer Card(Container parent, string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat { BgColor = new Color("171221"), BorderColor = new Color("513a6d") };
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(10); style.ContentMarginLeft = style.ContentMarginRight = 14;
        style.ContentMarginTop = style.ContentMarginBottom = 12; panel.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(panel); var box = new VBoxContainer { CustomMinimumSize = new Vector2(350, 0) };
        box.AddThemeConstantOverride("separation", 7); panel.AddChild(box); var heading = new Label { Text = title };
        heading.AddThemeColorOverride("font_color", new Color("ffb06f")); box.AddChild(heading); return box;
    }
}
#endif
