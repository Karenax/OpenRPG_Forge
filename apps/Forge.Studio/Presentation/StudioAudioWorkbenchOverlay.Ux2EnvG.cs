using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAudioWorkbenchOverlayUx2EnvG : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvGEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceUsesCanonicalFallback;
    private bool _evidenceReleaseSucceeded = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly TabContainer _tabs;

    private readonly LineEdit _search;
    private readonly LineEdit _tags;
    private readonly LineEdit _locale;
    private readonly LineEdit _license;
    private readonly ItemList _resources;
    private readonly Label _resourceDetails;
    private readonly Label _waveform;
    private readonly Label _audition;

    private readonly ItemList _playlist;
    private readonly LineEdit _playlistWeight;
    private readonly Button _playlistAdd;
    private readonly Button _playlistRemove;
    private readonly Button _playlistUp;
    private readonly Button _playlistDown;
    private readonly OptionButton _order;
    private readonly LineEdit _selectionSeed;
    private readonly LineEdit _noRepeat;

    private readonly LineEdit _interval;
    private readonly LineEdit _variation;
    private readonly LineEdit _minimumInterval;
    private readonly LineEdit _maximumInterval;
    private readonly LineEdit _minimumPitch;
    private readonly LineEdit _maximumPitch;
    private readonly LineEdit _minimumVolume;
    private readonly LineEdit _maximumVolume;
    private readonly OptionButton _distribution;
    private readonly TextEdit _curve;
    private readonly LineEdit _modulationSeed;
    private readonly CheckBox _tempoSync;
    private readonly LineEdit _tempo;
    private readonly LineEdit _beatDivision;

    private readonly LineEdit _relativeVolume;
    private readonly LineEdit _targetLoudness;
    private readonly LineEdit _mixBus;
    private readonly LineEdit _ducking;
    private readonly LineEdit _distanceScale;
    private readonly LineEdit _accessibilityCap;
    private readonly OptionButton _meter;
    private readonly OptionButton _scope;
    private readonly LineEdit _ambientBus;
    private readonly OptionButton _listenerPolicy;
    private readonly ItemList _listenerGains;
    private readonly LineEdit _listenerGain;
    private readonly LineEdit _newListener;
    private readonly Button _listenerAdd;
    private readonly Button _listenerRemove;
    private readonly Label _governance;

    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly VBoxContainer _sequence;
    private readonly VBoxContainer _mixPlans;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2EnvGAreaChoice> _choices = [];
    private IReadOnlyList<StudioUx2EnvGResourceResult> _resourceResults = [];
    private readonly List<StudioUx2EnvGPlaylistEntry> _playlistEntries = [];
    private readonly List<StudioUx2EnvGListenerGainDraft> _listenerEntries = [];
    private IReadOnlyList<StudioUx2EnvGScopeChoice> _scopeChoices = [];
    private StudioUx2EnvGDraft? _draft;
    private StudioUx2EnvGPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AudioWorkbenchChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
    {
        Succeeded: true,
        CanonicalDryRun: true,
        StatisticsComputed: true,
        PlayerInstantiationSkipped: true,
        ResourceLoadingSkipped: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsCompleteCollections => _draft is not null && _playlistEntries.Count > 0 &&
                                                _resourceResults.Count > 0 &&
                                                _lastPreview is { Sequence.Count: > 0, Mixes.Count: 3 };
    internal bool ShowsSearchAndWaveform => _resourceResults.Count > 0 &&
                                            _waveform.Text.Any(character => "▂▃▄▅▆▇█".Contains(character)) &&
                                            _resourceDetails.Text.Contains("Licence", StringComparison.Ordinal) &&
                                            _resourceDetails.Text.Contains("Provenance", StringComparison.Ordinal);
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups.Count == 4;
    internal bool ShowsBoundedRuntimeClaim => _lastPreview is
    {
        PlayerInstantiationSkipped: true,
        ResourceLoadingSkipped: true,
        RuntimeClaimsBounded: true,
    } && _lastPreview.Summary.Contains("runtime Godot", StringComparison.Ordinal) &&
         _audition.Text.Contains("plan", StringComparison.CurrentCultureIgnoreCase) &&
         _audition.Text.Contains("aucun lecteur Studio", StringComparison.Ordinal);
    internal bool ShowsHumanSurface
    {
        get
        {
            var titles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantVisibleTextUx2EnvG(this).Concat(titles));
            return text.Contains("Ressources & aperçu", StringComparison.Ordinal) &&
                   text.Contains("Ordre & sélection", StringComparison.Ordinal) &&
                   text.Contains("Intervalle & modulation", StringComparison.Ordinal) &&
                   text.Contains("Volume & auditeurs", StringComparison.Ordinal) &&
                   text.Contains("Aperçu & validation", StringComparison.Ordinal) &&
                   text.Contains("Waveform", StringComparison.Ordinal) &&
                   !ContainsUuidUx2EnvG(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                            !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatchesUx2EnvG();
    internal bool EvidenceResourcesReleased => _evidenceReleaseSucceeded &&
                                                _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioAudioWorkbenchOverlayUx2EnvG(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.014f, .019f, .026f, .97f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux2EnvGAudioWorkbenchDialog", StudioStyle.Surface, 12, 1);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);
        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Atelier audio de zone", 18));
        headingText.AddChild(StudioStyle.Label(
            "Recherche gouvernée, playlist ordonnée, modulation, LUFS et auditeurs · simulation sans playback Studio.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        _area = StudioUx2EnvBControls.Options();
        _area.ItemSelected += SelectArea;
        root.AddChild(StudioUx2EnvBControls.Field("Zone à modifier · sélection explicite", _area, 460));
        _selection = StudioStyle.Label("Aucune zone sélectionnée.", 10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(_tabs);

        var resourcesTab = ScrollTabUx2EnvG("Ressources & aperçu");
        var searchGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        searchGrid.AddThemeConstantOverride("h_separation", 8);
        searchGrid.AddThemeConstantOverride("v_separation", 5);
        _search = TextUx2EnvG("Nom, chemin, tag ou transcript");
        _tags = TextUx2EnvG("eau, forêt");
        _locale = TextUx2EnvG("fr");
        _license = TextUx2EnvG("CC0-1.0");
        searchGrid.AddChild(StudioUx2EnvBControls.Field("Recherche", _search));
        searchGrid.AddChild(StudioUx2EnvBControls.Field("Tags requis", _tags));
        searchGrid.AddChild(StudioUx2EnvBControls.Field("Locale", _locale));
        searchGrid.AddChild(StudioUx2EnvBControls.Field("Licence SPDX", _license));
        resourcesTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGSearch", "Facettes gouvernées",
            "Le navigateur lit les clips ENV-F, leurs métadonnées, licence, transcript et waveform persistée.",
            searchGrid));
        var resourceActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var searchButton = StudioStyle.Button("Rechercher", compact: true);
        var auditionOne = StudioStyle.Button("Plan d’écoute unique", compact: true);
        var auditionBatch = StudioStyle.Button("Plan d’écoute par lot", compact: true);
        _playlistAdd = StudioStyle.Button("Ajouter à la playlist", compact: true);
        foreach (var button in new[] { searchButton, auditionOne, auditionBatch, _playlistAdd })
        {
            button.CustomMinimumSize = new Vector2(170, 34);
            resourceActions.AddChild(button);
        }
        searchButton.Pressed += RefreshResourceSearchUx2EnvG;
        auditionOne.Pressed += () => PlanAuditionUx2EnvG(false);
        auditionBatch.Pressed += () => PlanAuditionUx2EnvG(true);
        _playlistAdd.Pressed += AddSelectedResourceUx2EnvG;
        resourcesTab.AddChild(resourceActions);
        _resources = ListUx2EnvG(132);
        _resources.ItemSelected += _ => ShowSelectedResourceUx2EnvG();
        resourcesTab.AddChild(_resources);
        _resourceDetails = StudioStyle.Label("Aucune ressource sélectionnée.", 9, StudioStyle.Muted, wrap: true);
        _waveform = StudioStyle.Label("Waveform  ▁▁▁▁▁▁▁▁", 13, StudioStyle.Accent, wrap: true);
        _audition = StudioStyle.Label(
            "Aucun plan d’écoute · aucun lecteur Studio et aucun chargement de ressource.",
            9, StudioStyle.Muted, wrap: true);
        resourcesTab.AddChild(_resourceDetails);
        resourcesTab.AddChild(_waveform);
        resourcesTab.AddChild(_audition);

        var selectionTab = ScrollTabUx2EnvG("Ordre & sélection");
        _playlist = ListUx2EnvG(172);
        _playlist.ItemSelected += _ => SelectPlaylistEntryUx2EnvG();
        selectionTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGPlaylist", "Playlist complète et ordonnée",
            "Ajoutez depuis le navigateur, retirez ou réordonnez; le chemin canonique reste interne.", _playlist));
        var playlistActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _playlistRemove = StudioStyle.Button("Retirer", compact: true);
        _playlistUp = StudioStyle.Button("Monter", compact: true);
        _playlistDown = StudioStyle.Button("Descendre", compact: true);
        foreach (var button in new[] { _playlistRemove, _playlistUp, _playlistDown })
        {
            button.CustomMinimumSize = new Vector2(120, 32);
            playlistActions.AddChild(button);
        }
        _playlistRemove.Pressed += RemovePlaylistEntryUx2EnvG;
        _playlistUp.Pressed += () => MovePlaylistEntryUx2EnvG(-1);
        _playlistDown.Pressed += () => MovePlaylistEntryUx2EnvG(1);
        selectionTab.AddChild(playlistActions);
        _playlistWeight = TextUx2EnvG("vide = poids du clip gouverné");
        _playlistWeight.TextChanged += UpdatePlaylistWeightUx2EnvG;
        selectionTab.AddChild(StudioUx2EnvBControls.Field("Poids explicite de la cue sélectionnée", _playlistWeight));
        _order = StudioUx2EnvBControls.Options(
            "Séquentiel", "Aléatoire déterministe", "Sac mélangé", "Pondéré");
        _selectionSeed = TextUx2EnvG("2026");
        _noRepeat = TextUx2EnvG("1");
        var selectionGrid = ThreeColumnsUx2EnvG();
        selectionGrid.AddChild(StudioUx2EnvBControls.Field("Ordre", _order));
        selectionGrid.AddChild(StudioUx2EnvBControls.Field("Graine stable", _selectionSeed));
        selectionGrid.AddChild(StudioUx2EnvBControls.Field("Fenêtre sans répétition", _noRepeat));
        selectionTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGSelection", "Politique de sélection",
            "Les modes aléatoires sont déterministes; le sac et les poids conservent une graine stable.", selectionGrid));

        var modulationTab = ScrollTabUx2EnvG("Intervalle & modulation");
        _interval = TextUx2EnvG("centre compatible");
        _variation = TextUx2EnvG("variation compatible");
        var compatibility = TwoColumnsUx2EnvG();
        compatibility.AddChild(StudioUx2EnvBControls.Field("Intervalle compatible (s)", _interval));
        compatibility.AddChild(StudioUx2EnvBControls.Field("Variation compatible (s)", _variation));
        modulationTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGCompatibility", "Compatibilité du profil de zone",
            "Ces deux valeurs restent explicites et ne sont jamais recalculées silencieusement depuis la plage avancée.",
            compatibility));
        _minimumInterval = TextUx2EnvG("minimum");
        _maximumInterval = TextUx2EnvG("maximum");
        _minimumPitch = TextUx2EnvG("minimum");
        _maximumPitch = TextUx2EnvG("maximum");
        _minimumVolume = TextUx2EnvG("minimum");
        _maximumVolume = TextUx2EnvG("maximum");
        var ranges = TwoColumnsUx2EnvG();
        ranges.AddChild(StudioUx2EnvBControls.Field("Intervalle min (s)", _minimumInterval));
        ranges.AddChild(StudioUx2EnvBControls.Field("Intervalle max (s)", _maximumInterval));
        ranges.AddChild(StudioUx2EnvBControls.Field("Pitch min (demi-tons)", _minimumPitch));
        ranges.AddChild(StudioUx2EnvBControls.Field("Pitch max (demi-tons)", _maximumPitch));
        ranges.AddChild(StudioUx2EnvBControls.Field("Volume min", _minimumVolume));
        ranges.AddChild(StudioUx2EnvBControls.Field("Volume max", _maximumVolume));
        modulationTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGRanges", "Plages déterministes",
            "Intervalle, pitch et volume sont échantillonnés par le planificateur canonique.", ranges));
        _distribution = StudioUx2EnvBControls.Options("Uniforme", "Triangulaire", "Gaussienne");
        _curve = StudioUx2EnvBControls.Lines("0:0\n0.5:0.35\n1:1", 86);
        _modulationSeed = TextUx2EnvG("2026");
        _tempoSync = StudioUx2EnvBControls.Check("Synchroniser les délais au tempo");
        _tempo = TextUx2EnvG("120");
        _beatDivision = TextUx2EnvG("1");
        var shaping = ThreeColumnsUx2EnvG();
        shaping.AddChild(StudioUx2EnvBControls.Field("Distribution", _distribution));
        shaping.AddChild(StudioUx2EnvBControls.Field("Graine de modulation", _modulationSeed));
        shaping.AddChild(_tempoSync);
        shaping.AddChild(StudioUx2EnvBControls.Field("Tempo BPM", _tempo));
        shaping.AddChild(StudioUx2EnvBControls.Field("Division de temps", _beatDivision));
        modulationTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGShaping", "Distribution, courbe et tempo",
            "La courbe contient des points position:valeur ordonnés de 0 à 1.", shaping));
        modulationTab.AddChild(StudioUx2EnvBControls.Field("Courbe de modulation", _curve));

        var mixTab = ScrollTabUx2EnvG("Volume & auditeurs");
        _relativeVolume = TextUx2EnvG("1");
        _targetLoudness = TextUx2EnvG("-18");
        _mixBus = TextUx2EnvG("Ambience");
        _ducking = TextUx2EnvG("0");
        _distanceScale = TextUx2EnvG("1");
        _accessibilityCap = TextUx2EnvG("1");
        _meter = StudioUx2EnvBControls.Options("Pic", "RMS", "Loudness");
        var mixGrid = ThreeColumnsUx2EnvG();
        mixGrid.AddChild(StudioUx2EnvBControls.Field("Volume relatif", _relativeVolume));
        mixGrid.AddChild(StudioUx2EnvBControls.Field("Cible LUFS", _targetLoudness));
        mixGrid.AddChild(StudioUx2EnvBControls.Field("Bus de mix", _mixBus));
        mixGrid.AddChild(StudioUx2EnvBControls.Field("Ducking (dB)", _ducking));
        mixGrid.AddChild(StudioUx2EnvBControls.Field("Échelle de distance", _distanceScale));
        mixGrid.AddChild(StudioUx2EnvBControls.Field("Plafond accessibilité", _accessibilityCap));
        mixGrid.AddChild(StudioUx2EnvBControls.Field("Mesure", _meter));
        mixTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGMix", "Normalisation et mix",
            "Le plan calcule le gain LUFS, le ducking, la distance et le plafond; aucun vumètre live n’est revendiqué.",
            mixGrid));
        _scope = StudioUx2EnvBControls.Options();
        _ambientBus = TextUx2EnvG("Ambience");
        _listenerPolicy = StudioUx2EnvBControls.Options(
            "Tous les auditeurs", "Joueur local", "Groupe local", "Personnalisé");
        var scopeGrid = ThreeColumnsUx2EnvG();
        scopeGrid.AddChild(StudioUx2EnvBControls.Field("Portée de zone", _scope));
        scopeGrid.AddChild(StudioUx2EnvBControls.Field("Bus d’ambiance", _ambientBus));
        scopeGrid.AddChild(StudioUx2EnvBControls.Field("Politique d’auditeur", _listenerPolicy));
        mixTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGScope", "Son partout et auditeurs",
            "Portée globale ou zone choisie, puis politique tous/local/groupe/personnalisée.", scopeGrid));
        _listenerGains = ListUx2EnvG(112);
        _listenerGains.ItemSelected += _ => SelectListenerGainUx2EnvG();
        mixTab.AddChild(_listenerGains);
        _listenerGain = TextUx2EnvG("gain 0 à 4");
        _listenerGain.TextChanged += UpdateListenerGainUx2EnvG;
        _newListener = TextUx2EnvG("Nom humain du nouvel auditeur");
        var listenerActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _listenerAdd = StudioStyle.Button("Ajouter un auditeur", compact: true);
        _listenerRemove = StudioStyle.Button("Retirer l’auditeur", compact: true);
        listenerActions.AddChild(StudioUx2EnvBControls.Field("Gain sélectionné", _listenerGain));
        listenerActions.AddChild(StudioUx2EnvBControls.Field("Nouveau profil", _newListener));
        listenerActions.AddChild(_listenerAdd);
        listenerActions.AddChild(_listenerRemove);
        _listenerAdd.Pressed += AddListenerGainUx2EnvG;
        _listenerRemove.Pressed += RemoveListenerGainUx2EnvG;
        mixTab.AddChild(listenerActions);
        _governance = StudioStyle.Label("Gouvernance audio en attente.", 9, StudioStyle.Muted, wrap: true);
        mixTab.AddChild(_governance);

        var reviewTab = ScrollTabUx2EnvG("Aperçu & validation");
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sequence = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _mixPlans = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGDiff", "Modifications proposées",
            "Le dry-run canonique ne change ni projet, ni historique.", _diff));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGDiagnostics", "Validation structurée",
            "Ressources, sélection, modulation et auditeurs restent séparés.", _diagnostics));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGSequence", "Séquence simulée",
            "Échantillons déterministes seulement; aucun flux audio n’est ouvert.", _sequence));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvGMixPlans", "Auditeurs simulés",
            "Joueur local, membre du groupe et auditeur externe évaluent la politique canonique.", _mixPlans));

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis prévisualisez · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 56);
        root.AddChild(_summary);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        root.AddChild(actions);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(126, 34);
            actions.AddChild(button);
        }
        _preview.Pressed += PreviewUx2EnvG;
        _apply.Pressed += ApplyUx2EnvG;
        _undo.Pressed += UndoUx2EnvG;
        _redo.Pressed += RedoUx2EnvG;
        _save.Pressed += SaveUx2EnvG;
        var note = StudioStyle.Label(
            "Studio calcule facettes, waveform, séquence et mix. Seul le runtime Godot charge les clips et instancie AudioStreamPlayer.",
            9, StudioStyle.Muted, wrap: true);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        note.CustomMinimumSize = new Vector2(0, 30);
        root.AddChild(note);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        WireDraftChangesUx2EnvG();
        SetEditingEnabledUx2EnvG(false);
        ResetReviewUx2EnvG();
        UpdateActionStateUx2EnvG();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
            ReleaseIsolatedEvidenceSessionUx2EnvG();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvG();
        RefreshChoicesUx2EnvG();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession))
            _session.CancelAudioWorkbenchApprovalUx2EnvG();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        ReleaseIsolatedEvidenceSessionUx2EnvG();
        _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureAudioWorkbenchApprovalUx2EnvG();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvG();
        RefreshChoicesUx2EnvG();
        _sourceSession.RestoreAudioWorkbenchApprovalUx2EnvG(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureAudioWorkbenchApprovalUx2EnvG();
        Close();
        _sourceSession.RestoreAudioWorkbenchApprovalUx2EnvG(sourceApproval);
        _evidenceReleaseSucceeded = true;
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || sourceProject.Areas.Count == 0) return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshotUx2EnvG();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux2-env-g-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var realArea = sourceProject.Areas.FirstOrDefault(area =>
            {
                var audio = area.Environment.Audio;
                return audio.Playlist.Count > 0 &&
                       audio.Emitters.Definitions.Any(definition => definition.ClipSet.Clips.Count > 0) &&
                       Forge.Domain.Audio.AudioAuthoringProfileValidator.Validate(
                           audio.Authoring, audio.Playlist, audio.Emitters).Count == 0;
            });
            _evidenceUsesRealModuleData = realArea is not null;
            _evidenceUsesCanonicalFallback = realArea is null;
            var evidenceProject = realArea is null
                ? StudioSessionController.CreateUx2EnvGContractProject(sourceProject)
                : sourceProject;
            var evidenceAreaId = realArea?.Id ?? evidenceProject.Areas[0].Id;
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot).Succeeded)
            {
                evidenceSession.Close();
                var released = StudioSessionController.DeleteUx2EnvGTemporaryWorkspace(tempRoot);
                ResetEvidenceStateUx2EnvG();
                _evidenceReleaseSucceeded = released;
                if (!released) _evidenceTempRoot = tempRoot;
                return false;
            }
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            Visible = true;
            UpdateDialogBoundsUx2EnvG();
            RefreshChoicesUx2EnvG();
            var choiceIndex = _choices.ToList().FindIndex(choice => choice.AreaId == evidenceAreaId);
            if (choiceIndex < 0)
            {
                Close();
                return false;
            }
            _area.Select(choiceIndex + 1);
            LoadChoiceUx2EnvG(choiceIndex);
            if (!ConfigureEvidenceDraftUx2EnvG())
            {
                Close();
                return false;
            }
            RefreshResourceSearchUx2EnvG();
            if (_resourceResults.Count > 0)
            {
                _resources.Select(0);
                ShowSelectedResourceUx2EnvG();
            }
            PlanAuditionUx2EnvG(true);
            PreviewUx2EnvG();
            _tabs.CurrentTab = _tabs.GetTabCount() - 1;
            var ready = CanonicalPreviewReady && HasExplicitSelection && ShowsCompleteCollections &&
                        ShowsSearchAndWaveform && ShowsStructuredDiagnostics && ShowsBoundedRuntimeClaim &&
                        ShowsHumanSurface && UsesIsolatedEvidence &&
                        (UsesRealModuleSource ^ UsesCanonicalFallbackEvidence) && EvidenceSourceUntouched;
            if (!ready)
            {
                Close();
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_G_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            var sessionReleased = true;
            try
            {
                evidenceSession.Close();
            }
            catch (Exception releaseException)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_G_EVIDENCE_RELEASE_FAILED " +
                            $"type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            var released = StudioSessionController.DeleteUx2EnvGTemporaryWorkspace(tempRoot);
            ResetEvidenceStateUx2EnvG();
            _evidenceReleaseSucceeded = sessionReleased && released;
            if (!released) _evidenceTempRoot = tempRoot;
            _session = _sourceSession;
            Visible = false;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshotUx2EnvG();
        if (!PrepareEvidence("ux2-env-g-audio-workbench") || !CanonicalPreviewReady || !UsesIsolatedEvidence)
        {
            Close();
            return false;
        }
        var isolated = _session;
        if (_lastPreview is not { Succeeded: true, CanApply: true })
        {
            Close();
            return false;
        }
        var isolatedProjectBefore = isolated.Project;
        ApplyUx2EnvG();
        var applied = _applied && isolated.Project is not null;
        var appliedJson = applied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        UndoUx2EnvG();
        var undone = !_applied && ReferenceEquals(isolatedProjectBefore, isolated.Project) &&
                     isolated.History?.CanRedo == true;
        RedoUx2EnvG();
        var redone = _applied && isolated.Project is not null &&
                     string.Equals(appliedJson, ProjectJsonSerializer.Serialize(isolated.Project!),
                         StringComparison.Ordinal);
        SaveUx2EnvG();
        var reopen = new StudioSessionController();
        var roundTrip = false;
        try
        {
            var opened = _evidenceTempRoot is not null && reopen.Open(_evidenceTempRoot).Succeeded;
            roundTrip = opened && reopen.Project is not null &&
                        string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopen.Project!),
                            StringComparison.Ordinal);
        }
        finally
        {
            reopen.Close();
        }
        Close();
        return applied && undone && redone && roundTrip && SourceSnapshotMatchesUx2EnvG(sourceSnapshot) &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private VBoxContainer ScrollTabUx2EnvG(string title)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1080, 0),
        };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);
        _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        return content;
    }

    private void RefreshChoicesUx2EnvG()
    {
        _updating = true;
        _choices = _session.AudioWorkbenchChoicesUx2EnvG;
        _area.Clear();
        _area.AddItem("Choisir explicitement une zone…");
        foreach (var choice in _choices) _area.AddItem(choice.Label);
        _area.Select(0);
        _updating = false;
        ClearSelectionUx2EnvG();
    }

    private void SelectArea(long index)
    {
        if (_updating) return;
        var choiceIndex = (int)index - 1;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count) ClearSelectionUx2EnvG();
        else LoadChoiceUx2EnvG(choiceIndex);
    }

    private void ClearSelectionUx2EnvG()
    {
        _session.CancelAudioWorkbenchApprovalUx2EnvG();
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _playlistEntries.Clear();
        _listenerEntries.Clear();
        _resourceResults = [];
        RefreshPlaylistListUx2EnvG();
        RefreshListenerListUx2EnvG();
        _resources.Clear();
        _selection.Text = "Aucune zone sélectionnée.";
        _selection.Modulate = StudioStyle.Warning;
        SetEditingEnabledUx2EnvG(false);
        ResetReviewUx2EnvG();
        UpdateActionStateUx2EnvG();
    }

    private void LoadChoiceUx2EnvG(int choiceIndex)
    {
        var choice = _choices[choiceIndex];
        _session.CancelAudioWorkbenchApprovalUx2EnvG();
        var draft = _session.CreateAudioWorkbenchDraftUx2EnvG(choice.AreaId);
        if (draft is null)
        {
            ClearSelectionUx2EnvG();
            SetStatusUx2EnvG("La zone choisie n’est plus disponible.", true);
            return;
        }
        _draft = draft;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        PopulateUx2EnvG(draft);
        _selection.Text = $"Zone sélectionnée : {HumanLabelUx2EnvG(draft.AreaDisplayName)} · " +
                          $"{draft.Playlist.Count} cue(s), {choice.GovernedResourceCount} ressource(s) gouvernée(s).";
        _selection.Modulate = StudioStyle.Accent;
        SetEditingEnabledUx2EnvG(true);
        ResetReviewUx2EnvG();
        UpdateActionStateUx2EnvG();
        RefreshResourceSearchUx2EnvG();
        SetStatusUx2EnvG(draft.Playlist.Count == 0
            ? "Playlist vide · consultez le catalogue puis ajoutez explicitement une ressource."
            : "Zone chargée · lancez l’aperçu sec avant toute application.");
    }

    private void PopulateUx2EnvG(StudioUx2EnvGDraft draft)
    {
        _updating = true;
        _playlistEntries.Clear();
        _playlistEntries.AddRange(draft.Playlist);
        RefreshPlaylistListUx2EnvG();
        _order.Selected = (int)draft.OrderMode;
        _interval.Text = draft.IntervalSeconds;
        _variation.Text = draft.IntervalVariationSeconds;
        _relativeVolume.Text = draft.RelativeVolume;
        _selectionSeed.Text = draft.SelectionSeed;
        _noRepeat.Text = draft.NoRepeatWindow;
        _minimumInterval.Text = draft.MinimumIntervalSeconds;
        _maximumInterval.Text = draft.MaximumIntervalSeconds;
        _minimumPitch.Text = draft.MinimumPitchSemitones;
        _maximumPitch.Text = draft.MaximumPitchSemitones;
        _minimumVolume.Text = draft.MinimumVolume;
        _maximumVolume.Text = draft.MaximumVolume;
        _distribution.Selected = (int)draft.Distribution;
        _curve.Text = draft.Curve;
        _modulationSeed.Text = draft.ModulationSeed;
        _tempoSync.ButtonPressed = draft.SyncToTempo;
        _tempo.Text = draft.TempoBpm;
        _beatDivision.Text = draft.BeatDivision;
        _targetLoudness.Text = draft.TargetLoudnessLufs;
        _mixBus.Text = draft.MixBus;
        _ducking.Text = draft.DuckingDb;
        _distanceScale.Text = draft.DistanceScale;
        _accessibilityCap.Text = draft.AccessibilityVolumeCap;
        _meter.Selected = (int)draft.MeterMode;
        _scopeChoices = draft.ScopeChoices;
        _scope.Clear();
        foreach (var choice in _scopeChoices) _scope.AddItem(choice.Label);
        var scopeIndex = _scopeChoices.ToList().FindIndex(item =>
            item.Value.Equals(draft.AreaScope, StringComparison.OrdinalIgnoreCase));
        _scope.Selected = Math.Max(0, scopeIndex);
        _ambientBus.Text = draft.AmbientBus;
        _listenerPolicy.Selected = (int)draft.ListenerPolicy;
        _listenerEntries.Clear();
        _listenerEntries.AddRange(draft.ListenerGains);
        RefreshListenerListUx2EnvG();
        _governance.Text = draft.GovernanceSummary +
                           " · identifiant et empreinte restent internes, inchangés par ce lot.";
        _updating = false;
    }

    private StudioUx2EnvGDraft? CaptureDraftUx2EnvG()
    {
        if (_draft is null) return null;
        var scopeIndex = Math.Clamp(_scope.Selected, 0, Math.Max(0, _scopeChoices.Count - 1));
        var scopeValue = _scopeChoices.Count == 0 ? string.Empty : _scopeChoices[scopeIndex].Value;
        _draft = _draft with
        {
            Playlist = _playlistEntries.ToArray(),
            OrderMode = (Forge.Domain.Areas.AudioOrderMode)_order.Selected,
            IntervalSeconds = _interval.Text,
            IntervalVariationSeconds = _variation.Text,
            RelativeVolume = _relativeVolume.Text,
            SelectionSeed = _selectionSeed.Text,
            NoRepeatWindow = _noRepeat.Text,
            MinimumIntervalSeconds = _minimumInterval.Text,
            MaximumIntervalSeconds = _maximumInterval.Text,
            MinimumPitchSemitones = _minimumPitch.Text,
            MaximumPitchSemitones = _maximumPitch.Text,
            MinimumVolume = _minimumVolume.Text,
            MaximumVolume = _maximumVolume.Text,
            Distribution = (Forge.Domain.Audio.AudioModulationDistribution)_distribution.Selected,
            Curve = _curve.Text,
            ModulationSeed = _modulationSeed.Text,
            SyncToTempo = _tempoSync.ButtonPressed,
            TempoBpm = _tempo.Text,
            BeatDivision = _beatDivision.Text,
            TargetLoudnessLufs = _targetLoudness.Text,
            MixBus = _mixBus.Text,
            DuckingDb = _ducking.Text,
            DistanceScale = _distanceScale.Text,
            AccessibilityVolumeCap = _accessibilityCap.Text,
            MeterMode = (Forge.Domain.Audio.AudioMeterMode)_meter.Selected,
            AreaScope = scopeValue,
            AmbientBus = _ambientBus.Text,
            ListenerPolicy = (Forge.Domain.Audio.AudioListenerPolicy)_listenerPolicy.Selected,
            ListenerGains = _listenerEntries.ToArray(),
        };
        return _draft;
    }

    private void RefreshResourceSearchUx2EnvG()
    {
        if (_draft is null) return;
        _resourceResults = _session.BrowseAudioResourcesUx2EnvG(
            _draft.AreaId, _search.Text, _tags.Text, _locale.Text, _license.Text);
        _resources.Clear();
        foreach (var item in _resourceResults)
            _resources.AddItem($"{item.Label} · {item.Loudness} · {item.Duration}");
        if (_resourceResults.Count == 0)
        {
            _resourceDetails.Text = "État vide · aucune ressource ne correspond aux facettes.";
            _waveform.Text = "Waveform  ▁▁▁▁▁▁▁▁";
        }
        else
        {
            _resources.Select(0);
            ShowSelectedResourceUx2EnvG();
        }
        UpdateActionStateUx2EnvG();
    }

    private void ShowSelectedResourceUx2EnvG()
    {
        var index = SelectedIndexUx2EnvG(_resources);
        if (index < 0 || index >= _resourceResults.Count) return;
        var item = _resourceResults[index];
        _resourceDetails.Text = $"{item.Label}\nTags : {item.Tags} · locale {item.Locale}\n" +
                                $"Licence : {item.License} · Provenance : {item.Provenance}\n" +
                                $"Transcript : {(string.IsNullOrWhiteSpace(item.Transcript) ? "non renseigné" : item.Transcript)}\n" +
                                $"Distribution : {(item.DistributionReady ? "prête" : "à corriger")}";
        _waveform.Text = $"Waveform  {item.Waveform}";
        UpdateActionStateUx2EnvG();
    }

    private void PlanAuditionUx2EnvG(bool batch)
    {
        var draft = CaptureDraftUx2EnvG();
        if (draft is null)
        {
            SetStatusUx2EnvG("Sélectionnez explicitement une zone.", true);
            return;
        }
        var plan = _session.PlanAudioAuditionUx2EnvG(
            draft, _search.Text, _tags.Text, _locale.Text, _license.Text, batch);
        _audition.Text = plan.Succeeded
            ? $"{plan.Message}\n{plan.Resources.Count} ressource(s) · bus {plan.Bus} · gain normalisé {plan.NormalizedGain} · " +
              string.Join(", ", plan.Resources)
            : plan.Message;
        _audition.Modulate = plan.Succeeded ? StudioStyle.Success : StudioStyle.Warning;
        SetStatusUx2EnvG(plan.Message, !plan.Succeeded);
    }

    private void AddSelectedResourceUx2EnvG()
    {
        var index = SelectedIndexUx2EnvG(_resources);
        if (index < 0 || index >= _resourceResults.Count) return;
        var result = _resourceResults[index];
        if (_playlistEntries.Any(item => item.Resource.Equals(result.Resource, StringComparison.Ordinal)))
        {
            SetStatusUx2EnvG("Cette ressource appartient déjà à la playlist.", true);
            return;
        }
        _playlistEntries.Add(new(result.Resource, result.Label, string.Empty));
        RefreshPlaylistListUx2EnvG();
        _playlist.Select(_playlistEntries.Count - 1);
        SelectPlaylistEntryUx2EnvG();
        InvalidatePreviewUx2EnvG();
    }

    private void RemovePlaylistEntryUx2EnvG()
    {
        var index = SelectedIndexUx2EnvG(_playlist);
        if (index < 0 || index >= _playlistEntries.Count) return;
        _playlistEntries.RemoveAt(index);
        RefreshPlaylistListUx2EnvG();
        if (_playlistEntries.Count > 0)
        {
            _playlist.Select(Math.Min(index, _playlistEntries.Count - 1));
            SelectPlaylistEntryUx2EnvG();
        }
        InvalidatePreviewUx2EnvG();
    }

    private void MovePlaylistEntryUx2EnvG(int offset)
    {
        var index = SelectedIndexUx2EnvG(_playlist);
        var destination = index + offset;
        if (index < 0 || destination < 0 || destination >= _playlistEntries.Count) return;
        (_playlistEntries[index], _playlistEntries[destination]) =
            (_playlistEntries[destination], _playlistEntries[index]);
        RefreshPlaylistListUx2EnvG();
        _playlist.Select(destination);
        SelectPlaylistEntryUx2EnvG();
        InvalidatePreviewUx2EnvG();
    }

    private void RefreshPlaylistListUx2EnvG()
    {
        _playlist.Clear();
        foreach (var (item, index) in _playlistEntries.Select((value, index) => (value, index)))
            _playlist.AddItem($"{index + 1}. {item.Label} · poids " +
                              (string.IsNullOrWhiteSpace(item.Weight) ? "hérité du clip" : item.Weight));
        _playlistWeight.Text = string.Empty;
    }

    private void SelectPlaylistEntryUx2EnvG()
    {
        var index = SelectedIndexUx2EnvG(_playlist);
        _updating = true;
        _playlistWeight.Text = index >= 0 && index < _playlistEntries.Count
            ? _playlistEntries[index].Weight
            : string.Empty;
        _updating = false;
        UpdateActionStateUx2EnvG();
    }

    private void UpdatePlaylistWeightUx2EnvG(string value)
    {
        if (_updating) return;
        var index = SelectedIndexUx2EnvG(_playlist);
        if (index < 0 || index >= _playlistEntries.Count) return;
        _playlistEntries[index] = _playlistEntries[index] with { Weight = value };
        InvalidatePreviewUx2EnvG();
    }

    private void RefreshListenerListUx2EnvG()
    {
        _listenerGains.Clear();
        foreach (var item in _listenerEntries)
            _listenerGains.AddItem($"{item.Label} · gain {item.Gain}");
        _listenerGain.Text = string.Empty;
    }

    private void SelectListenerGainUx2EnvG()
    {
        var index = SelectedIndexUx2EnvG(_listenerGains);
        _updating = true;
        _listenerGain.Text = index >= 0 && index < _listenerEntries.Count
            ? _listenerEntries[index].Gain
            : string.Empty;
        _updating = false;
        UpdateActionStateUx2EnvG();
    }

    private void UpdateListenerGainUx2EnvG(string value)
    {
        if (_updating) return;
        var index = SelectedIndexUx2EnvG(_listenerGains);
        if (index < 0 || index >= _listenerEntries.Count) return;
        _listenerEntries[index] = _listenerEntries[index] with { Gain = value };
        InvalidatePreviewUx2EnvG();
    }

    private void AddListenerGainUx2EnvG()
    {
        var key = _newListener.Text.Trim();
        if (string.IsNullOrWhiteSpace(key) || ContainsUuidUx2EnvG(key) ||
            _listenerEntries.Any(item => item.Key.Equals(key, StringComparison.Ordinal)))
        {
            SetStatusUx2EnvG("Le nouvel auditeur exige un nom humain unique.", true);
            return;
        }
        _listenerEntries.Add(new(key, HumanLabelUx2EnvG(key), "1"));
        _newListener.Text = string.Empty;
        RefreshListenerListUx2EnvG();
        _listenerGains.Select(_listenerEntries.Count - 1);
        SelectListenerGainUx2EnvG();
        InvalidatePreviewUx2EnvG();
    }

    private void RemoveListenerGainUx2EnvG()
    {
        var index = SelectedIndexUx2EnvG(_listenerGains);
        if (index < 0 || index >= _listenerEntries.Count) return;
        _listenerEntries.RemoveAt(index);
        RefreshListenerListUx2EnvG();
        if (_listenerEntries.Count > 0)
        {
            _listenerGains.Select(Math.Min(index, _listenerEntries.Count - 1));
            SelectListenerGainUx2EnvG();
        }
        InvalidatePreviewUx2EnvG();
    }

    private void InvalidatePreviewUx2EnvG()
    {
        if (_updating || _draft is null) return;
        _session.CancelAudioWorkbenchApprovalUx2EnvG();
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _summary.Text = "La fiche a changé · relancez l’aperçu sec.";
        _summary.Modulate = StudioStyle.Warning;
        UpdateActionStateUx2EnvG();
    }

    private void PreviewUx2EnvG()
    {
        var draft = CaptureDraftUx2EnvG();
        if (draft is null)
        {
            SetStatusUx2EnvG("Sélectionnez explicitement une zone.", true);
            return;
        }
        var result = _session.PreviewAudioWorkbenchUx2EnvG(draft);
        _lastPreview = result;
        _canonicalPreviewReady = result.Succeeded && result.CanonicalDryRun;
        RenderPreviewUx2EnvG(result);
        UpdateActionStateUx2EnvG();
        SetStatusUx2EnvG(result.Message, !result.Succeeded);
    }

    private void ApplyUx2EnvG()
    {
        if (_lastPreview is not { Succeeded: true, CanApply: true })
        {
            SetStatusUx2EnvG("Un aperçu sec valide et encore frais est requis.", true);
            return;
        }
        var result = _session.ApplyApprovedAudioWorkbenchUx2EnvG();
        _applied = result.Succeeded;
        if (result.Succeeded)
        {
            _draft = _session.CreateAudioWorkbenchDraftUx2EnvG(_draft?.AreaId);
            if (_draft is not null) PopulateUx2EnvG(_draft);
            _lastPreview = null;
            _canonicalPreviewReady = false;
            _summary.Text = "Atelier audio appliqué · enregistrer reste explicite.";
            _summary.Modulate = StudioStyle.Success;
            AudioWorkbenchChanged?.Invoke();
        }
        UpdateActionStateUx2EnvG();
        SetStatusUx2EnvG(result.Message, !result.Succeeded);
    }

    private void UndoUx2EnvG()
    {
        var result = _session.Undo();
        _applied = false;
        ReloadAfterHistoryUx2EnvG();
        SetStatusUx2EnvG(result.Message, !result.Succeeded);
    }

    private void RedoUx2EnvG()
    {
        var result = _session.Redo();
        _applied = result.Succeeded;
        ReloadAfterHistoryUx2EnvG();
        SetStatusUx2EnvG(result.Message, !result.Succeeded);
    }

    private void SaveUx2EnvG()
    {
        var result = _session.Save();
        UpdateActionStateUx2EnvG();
        SetStatusUx2EnvG(result.Message, !result.Succeeded);
    }

    private void ReloadAfterHistoryUx2EnvG()
    {
        if (_draft is not null)
        {
            _draft = _session.CreateAudioWorkbenchDraftUx2EnvG(_draft.AreaId);
            if (_draft is not null)
            {
                PopulateUx2EnvG(_draft);
                RefreshResourceSearchUx2EnvG();
            }
        }
        _lastPreview = null;
        _canonicalPreviewReady = false;
        ResetReviewUx2EnvG();
        UpdateActionStateUx2EnvG();
        AudioWorkbenchChanged?.Invoke();
    }

    private void RenderPreviewUx2EnvG(StudioUx2EnvGPreview preview)
    {
        ClearChildrenUx2EnvG(_diff);
        ClearChildrenUx2EnvG(_diagnostics);
        ClearChildrenUx2EnvG(_sequence);
        ClearChildrenUx2EnvG(_mixPlans);
        if (preview.Diff.Entries.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucune différence.", 9, StudioStyle.Muted, wrap: true));
        else
            foreach (var entry in preview.Diff.Entries)
                _diff.AddChild(StudioStyle.Label($"• {entry}", 9, StudioStyle.Text, wrap: true));
        foreach (var group in preview.Groups)
        {
            var color = group.State == "conforme" ? StudioStyle.Success : StudioStyle.Warning;
            _diagnostics.AddChild(StudioStyle.Label($"{group.Title} · {group.State}", 10, color));
            foreach (var item in group.Diagnostics)
                _diagnostics.AddChild(StudioStyle.Label($"  • {item.Message}", 9, StudioStyle.Warning, wrap: true));
        }
        if (preview.Sequence.Count == 0)
            _sequence.AddChild(StudioStyle.Label("Playlist vide · aucun cue planifié.", 9, StudioStyle.Muted));
        else
            foreach (var cue in preview.Sequence)
                _sequence.AddChild(StudioStyle.Label(
                    $"• {cue.Resource} · délai {cue.Delay} · volume {cue.Volume} · {cue.Pitch} · bus {cue.Bus}",
                    9, StudioStyle.Text, wrap: true));
        foreach (var mix in preview.Mixes)
            _mixPlans.AddChild(StudioStyle.Label(
                $"• {mix.Listener} · {mix.Scope} · {(mix.Allowed ? "autorisé" : "bloqué")} · " +
                $"bus {mix.Bus} · gain {mix.Gain} · {mix.Meter}" +
                (mix.Personalized ? " · personnalisé" : string.Empty) +
                (mix.AccessibilityCapped ? " · plafonné" : string.Empty),
                9, mix.Allowed ? StudioStyle.Text : StudioStyle.Warning, wrap: true));
        _summary.Text = preview.Summary.Length > 0 ? preview.Summary : preview.Message;
        _summary.Modulate = preview.Succeeded ? StudioStyle.Text : StudioStyle.Warning;
    }

    private void ResetReviewUx2EnvG()
    {
        ClearChildrenUx2EnvG(_diff);
        ClearChildrenUx2EnvG(_diagnostics);
        ClearChildrenUx2EnvG(_sequence);
        ClearChildrenUx2EnvG(_mixPlans);
        _diff.AddChild(StudioStyle.Label("Aucun aperçu sec.", 9, StudioStyle.Muted));
        _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 9, StudioStyle.Muted));
        _sequence.AddChild(StudioStyle.Label("Aucune séquence calculée.", 9, StudioStyle.Muted));
        _mixPlans.AddChild(StudioStyle.Label("Aucun auditeur simulé.", 9, StudioStyle.Muted));
        _summary.Text = _draft is null
            ? "Sélectionnez une zone puis prévisualisez · aucun changement appliqué."
            : "Fiche chargée · aucun changement appliqué.";
        _summary.Modulate = StudioStyle.Muted;
    }

    private void UpdateActionStateUx2EnvG()
    {
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview is not { Succeeded: true, CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
        var resourceSelected = SelectedIndexUx2EnvG(_resources) >= 0;
        _playlistAdd.Disabled = _draft is null || !resourceSelected;
        var playlistIndex = SelectedIndexUx2EnvG(_playlist);
        _playlistRemove.Disabled = playlistIndex < 0;
        _playlistUp.Disabled = playlistIndex <= 0;
        _playlistDown.Disabled = playlistIndex < 0 || playlistIndex >= _playlistEntries.Count - 1;
        _playlistWeight.Editable = _draft is not null && playlistIndex >= 0;
        var listenerIndex = SelectedIndexUx2EnvG(_listenerGains);
        _listenerRemove.Disabled = listenerIndex < 0;
        _listenerGain.Editable = _draft is not null && listenerIndex >= 0;
    }

    private void SetEditingEnabledUx2EnvG(bool enabled)
    {
        foreach (var edit in DraftLineEditsUx2EnvG()) edit.Editable = enabled;
        foreach (var option in new[] { _order, _distribution, _meter, _scope, _listenerPolicy })
            option.Disabled = !enabled;
        _curve.Editable = enabled;
        _tempoSync.Disabled = !enabled;
        _playlistRemove.Disabled = !enabled;
        _playlistUp.Disabled = !enabled;
        _playlistDown.Disabled = !enabled;
        _listenerAdd.Disabled = !enabled;
        _listenerRemove.Disabled = !enabled;
    }

    private bool ConfigureEvidenceDraftUx2EnvG()
    {
        var draft = CaptureDraftUx2EnvG();
        if (draft is null || draft.Playlist.Count == 0) return false;
        var gains = draft.ListenerGains.Count > 0
            ? draft.ListenerGains.Select((item, index) => index == 0
                ? item with { Gain = "0.82" }
                : item).ToArray()
            : [new StudioUx2EnvGListenerGainDraft("auditeur-local", "Auditeur local", "0.82")];
        draft = draft with
        {
            OrderMode = draft.Playlist.Count > 1
                ? Forge.Domain.Areas.AudioOrderMode.Weighted
                : Forge.Domain.Areas.AudioOrderMode.Sequential,
            IntervalSeconds = "11",
            IntervalVariationSeconds = "3",
            RelativeVolume = "0.84",
            SelectionSeed = "8801",
            NoRepeatWindow = draft.Playlist.Count > 1 ? "1" : "0",
            MinimumIntervalSeconds = "8",
            MaximumIntervalSeconds = "14",
            MinimumPitchSemitones = "-0.75",
            MaximumPitchSemitones = "1.25",
            MinimumVolume = "0.75",
            MaximumVolume = "1.15",
            Distribution = Forge.Domain.Audio.AudioModulationDistribution.Triangular,
            Curve = "0:0\n0.5:0.35\n1:1",
            ModulationSeed = "8802",
            SyncToTempo = true,
            TempoBpm = "96",
            BeatDivision = "2",
            TargetLoudnessLufs = "-19",
            MixBus = "Ambience",
            DuckingDb = "-7",
            DistanceScale = "1.1",
            AccessibilityVolumeCap = "0.85",
            MeterMode = Forge.Domain.Audio.AudioMeterMode.Loudness,
            AreaScope = string.Empty,
            AmbientBus = "Ambience",
            ListenerPolicy = Forge.Domain.Audio.AudioListenerPolicy.Personalized,
            ListenerGains = gains,
        };
        _draft = draft;
        PopulateUx2EnvG(draft);
        _search.Text = string.Empty;
        _tags.Text = string.Empty;
        _locale.Text = string.Empty;
        _license.Text = string.Empty;
        return true;
    }

    private StudioUx2EnvGEvidenceSourceSnapshot CaptureSourceSnapshotUx2EnvG()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("Source project is missing.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("Source history is missing.");
        return new StudioUx2EnvGEvidenceSourceSnapshot(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            _sourceSession.CaptureAudioWorkbenchApprovalUx2EnvG());
    }

    private bool SourceSnapshotMatchesUx2EnvG(StudioUx2EnvGEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _evidenceSourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAudioWorkbenchApprovalUx2EnvG() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSessionUx2EnvG()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        if (evidenceSession is null && tempRoot is null)
        {
            _session = _sourceSession;
            return true;
        }
        var released = true;
        try
        {
            evidenceSession?.Close();
        }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_G_RELEASE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx2EnvGTemporaryWorkspace(tempRoot);
        ResetEvidenceStateUx2EnvG();
        if (!rootReleased) _evidenceTempRoot = tempRoot;
        _session = _sourceSession;
        _evidenceReleaseSucceeded = released && rootReleased;
        return _evidenceReleaseSucceeded;
    }

    private void ResetEvidenceStateUx2EnvG()
    {
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceSourceSnapshot = null;
        _evidenceUsesRealModuleData = false;
        _evidenceUsesCanonicalFallback = false;
    }

    private void WireDraftChangesUx2EnvG()
    {
        foreach (var edit in DraftLineEditsUx2EnvG()) edit.TextChanged += _ => InvalidatePreviewUx2EnvG();
        foreach (var option in new[] { _order, _distribution, _meter, _scope, _listenerPolicy })
            option.ItemSelected += _ => InvalidatePreviewUx2EnvG();
        _curve.TextChanged += InvalidatePreviewUx2EnvG;
        _tempoSync.Toggled += _ => InvalidatePreviewUx2EnvG();
    }

    private IReadOnlyList<LineEdit> DraftLineEditsUx2EnvG() =>
    [
        _selectionSeed, _noRepeat, _interval, _variation, _minimumInterval, _maximumInterval,
        _minimumPitch, _maximumPitch, _minimumVolume, _maximumVolume, _modulationSeed,
        _tempo, _beatDivision, _relativeVolume, _targetLoudness, _mixBus, _ducking,
        _distanceScale, _accessibilityCap, _ambientBus,
    ];

    private void SetStatusUx2EnvG(string text, bool error = false)
    {
        _status.Text = text;
        _status.Modulate = error ? StudioStyle.Warning : StudioStyle.Muted;
        StatusChanged?.Invoke(text, error);
    }

    private void UpdateDialogBoundsUx2EnvG()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.7f : viewport.X >= 2_300 ? 1.23f : 1f;
        var width = Math.Clamp((viewport.X - 52f) / scale, 1_120f, 1_680f);
        var height = Math.Clamp((viewport.Y - 42f) / scale, 760f, 1_060f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(18f, (viewport.Y - height * scale) * .45f));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateDialogBoundsUx2EnvG();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null) Close();
    }

    private static LineEdit TextUx2EnvG(string placeholder) => StudioUx2EnvBControls.Text(placeholder);

    private static ItemList ListUx2EnvG(float height)
    {
        var list = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        list.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 5));
        list.AddThemeColorOverride("font_color", StudioStyle.Text);
        list.AddThemeColorOverride("font_selected_color", StudioStyle.Text);
        return list;
    }

    private static GridContainer TwoColumnsUx2EnvG() => GridUx2EnvG(2);
    private static GridContainer ThreeColumnsUx2EnvG() => GridUx2EnvG(3);

    private static GridContainer GridUx2EnvG(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 5);
        return grid;
    }

    private static int SelectedIndexUx2EnvG(ItemList list) =>
        list.GetSelectedItems().FirstOrDefault(-1);

    private static string HumanLabelUx2EnvG(string? value)
    {
        var text = (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsUuidUx2EnvG(text)
            ? "élément sans nom lisible"
            : text;
    }

    private static void ClearChildrenUx2EnvG(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static IEnumerable<string> DescendantVisibleTextUx2EnvG(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label:
                    yield return label.Text;
                    break;
                case LineEdit line:
                    yield return line.Text;
                    break;
                case TextEdit text:
                    yield return text.Text;
                    break;
                case OptionButton option:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case Button button:
                    yield return button.Text;
                    break;
                case ItemList list:
                    for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
                    break;
            }
            foreach (var descendant in DescendantVisibleTextUx2EnvG(child)) yield return descendant;
        }
    }

    private static bool ContainsUuidUx2EnvG(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text,
        @"\b(?:[0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
