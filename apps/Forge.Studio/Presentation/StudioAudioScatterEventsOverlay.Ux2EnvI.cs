using Forge.Domain.Areas;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAudioScatterEventsOverlayUx2EnvI : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvIEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceUsesCanonicalFallback;
    private bool _evidenceReleaseSucceeded = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly ItemList _scatter;
    private readonly Button _addScatter;
    private readonly Button _removeScatter;
    private readonly OptionButton _cue;
    private readonly LineEdit _centerX;
    private readonly LineEdit _centerY;
    private readonly LineEdit _centerZ;
    private readonly LineEdit _sizeX;
    private readonly LineEdit _sizeY;
    private readonly LineEdit _sizeZ;
    private readonly CheckBox _surfaceOnly;
    private readonly LineEdit _seed;
    private readonly LineEdit _minimumInterval;
    private readonly LineEdit _maximumInterval;
    private readonly LineEdit _volume;
    private readonly CheckBox _requireNavigable;
    private readonly LineEdit _biomeTags;
    private readonly LineEdit _exclusionTags;
    private readonly VBoxContainer _samples;
    private readonly VBoxContainer _filterProbes;
    private readonly VBoxContainer _events;
    private readonly Label _scriptSnapshot;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2EnvIAreaChoice> _choices = [];
    private IReadOnlyList<StudioUx2EnvICueChoice> _cueChoices = [];
    private readonly List<StudioUx2EnvIScatterDraft> _scatterDrafts = [];
    private StudioUx2EnvIDraft? _draft;
    private StudioUx2EnvIPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? AudioScatterChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
    {
        Succeeded: true,
        CanonicalDryRun: true,
        DeterministicScatter: true,
        TypedEvents: true,
        PlayerInstantiationSkipped: true,
        ScriptDispatchSkipped: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsScatterPlan => _lastPreview is { Samples.Count: 5, FilterProbes.Count: 5 } &&
                                      _samples.GetChildCount() == 5 && _filterProbes.GetChildCount() == 5;
    internal bool ShowsTypedEventPlan => _lastPreview is
    {
        Events.Count: 6,
        ScriptSnapshot.CopiedValues: true,
        ScriptSnapshot.PersistentMutationExcluded: true,
    } && _events.GetChildCount() == 6 &&
         _scriptSnapshot.Text.Contains("copié", StringComparison.CurrentCultureIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups.Count == 4;
    internal bool ShowsBoundedRuntimeClaim => _lastPreview is
    {
        PlayerInstantiationSkipped: true,
        ScriptDispatchSkipped: true,
        RuntimeClaimsBounded: true,
    } && _lastPreview.Summary.Contains("aucun lecteur audio 3D", StringComparison.Ordinal) &&
         _lastPreview.Summary.Contains("aucun handler ForgeScript", StringComparison.Ordinal);
    internal bool ShowsHumanSurface
    {
        get
        {
            var titles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantVisibleTextUx2EnvI(this).Concat(titles));
            return text.Contains("Dispersions spatiales", StringComparison.Ordinal) &&
                   text.Contains("Simulation déterministe", StringComparison.Ordinal) &&
                   text.Contains("Événements typés", StringComparison.Ordinal) &&
                   text.Contains("Aperçu & validation", StringComparison.Ordinal) &&
                   text.Contains("Volume ou surface", StringComparison.Ordinal) &&
                   text.Contains("Prévisualiser", StringComparison.Ordinal) &&
                   !ContainsUuidUx2EnvI(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                             !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatchesUx2EnvI();
    internal bool EvidenceResourcesReleased => _evidenceReleaseSucceeded &&
                                                _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioAudioScatterEventsOverlayUx2EnvI(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.014f, .019f, .026f, .97f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux2EnvIAudioScatterEventsDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
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
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Dispersion & événements audio", 18));
        headingText.AddChild(StudioStyle.Label(
            "Positions déterministes par volume/surface et aperçu des événements typés · aucun playback live dans Studio.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _area = StudioUx2EnvBControls.Options();
        _area.ItemSelected += SelectAreaUx2EnvI;
        root.AddChild(StudioUx2EnvBControls.Field("Zone à modifier · sélection explicite", _area, 500));
        _selection = StudioStyle.Label("Aucune zone sélectionnée.", 10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 330),
            CustomMaximumSize = new Vector2(-1, 620),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var scatterTab = ScrollTabUx2EnvI("Dispersions spatiales");
        var collection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scatter = ListUx2EnvI(190);
        _scatter.ItemSelected += _ => SelectScatterUx2EnvI();
        collection.AddChild(_scatter);
        var collectionActions = new VBoxContainer { CustomMinimumSize = new Vector2(170, 0) };
        _addScatter = StudioStyle.Button("Ajouter", compact: true);
        _removeScatter = StudioStyle.Button("Retirer", compact: true);
        _addScatter.Pressed += AddScatterUx2EnvI;
        _removeScatter.Pressed += RemoveScatterUx2EnvI;
        collectionActions.AddChild(_addScatter);
        collectionActions.AddChild(_removeScatter);
        collectionActions.AddChild(StudioStyle.Label(
            "Les identités stables restent internes; aucun UUID n’est affiché.",
            9, StudioStyle.Muted, wrap: true));
        collection.AddChild(collectionActions);
        scatterTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvIScatterCollection", "Dispersions de la zone",
            "Chaque entrée référence un cue gouverné existant. La liste vide reste valide.", collection));

        _cue = StudioUx2EnvBControls.Options();
        _centerX = TextUx2EnvI("centre X");
        _centerY = TextUx2EnvI("centre Y");
        _centerZ = TextUx2EnvI("centre Z");
        _sizeX = TextUx2EnvI("largeur X");
        _sizeY = TextUx2EnvI("hauteur Y");
        _sizeZ = TextUx2EnvI("profondeur Z");
        _surfaceOnly = StudioUx2EnvBControls.Check("Surface seulement · hauteur Y fixe");
        var geometry = GridUx2EnvI(3);
        geometry.AddChild(StudioUx2EnvBControls.Field("Cue gouverné", _cue));
        geometry.AddChild(StudioUx2EnvBControls.Field("Centre X", _centerX));
        geometry.AddChild(StudioUx2EnvBControls.Field("Centre Y", _centerY));
        geometry.AddChild(StudioUx2EnvBControls.Field("Centre Z", _centerZ));
        geometry.AddChild(StudioUx2EnvBControls.Field("Taille X", _sizeX));
        geometry.AddChild(StudioUx2EnvBControls.Field("Taille Y", _sizeY));
        geometry.AddChild(StudioUx2EnvBControls.Field("Taille Z", _sizeZ));
        geometry.AddChild(_surfaceOnly);
        scatterTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvIGeometry", "Volume ou surface",
            "Les valeurs restent brutes jusqu’au dry-run; aucune valeur finie valide n’est arrondie ou plafonnée.",
            geometry));

        _seed = TextUx2EnvI("seed entier");
        _minimumInterval = TextUx2EnvI("minimum en secondes");
        _maximumInterval = TextUx2EnvI("maximum en secondes");
        _volume = TextUx2EnvI("0 à 4");
        _requireNavigable = StudioUx2EnvBControls.Check("Exiger une position navigable");
        _biomeTags = TextUx2EnvI("forest, ruins");
        _exclusionTags = TextUx2EnvI("combat, silence");
        var policy = GridUx2EnvI(3);
        policy.AddChild(StudioUx2EnvBControls.Field("Seed déterministe", _seed));
        policy.AddChild(StudioUx2EnvBControls.Field("Intervalle minimum (s)", _minimumInterval));
        policy.AddChild(StudioUx2EnvBControls.Field("Intervalle maximum (s)", _maximumInterval));
        policy.AddChild(StudioUx2EnvBControls.Field("Volume relatif", _volume));
        policy.AddChild(_requireNavigable);
        policy.AddChild(StudioUx2EnvBControls.Field("Biomes autorisés", _biomeTags));
        policy.AddChild(StudioUx2EnvBControls.Field("Tags d’exclusion", _exclusionTags));
        scatterTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvIPolicy", "Cadence et filtres",
            "Navigation, biome et exclusion sont évalués séparément dans l’aperçu canonique.", policy));

        var simulationTab = ScrollTabUx2EnvI("Simulation déterministe");
        _samples = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _filterProbes = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        simulationTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvISamples", "Positions et délais",
            "Cinq séquences sont calculées par ResolveScatter avec le seed auteur; aucun nœud 3D n’est instancié.",
            _samples));
        simulationTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvIFilters", "Scénarios de filtre",
            "Navigation disponible/absente, biome correspondant/absent et exclusion active sont distingués.",
            _filterProbes));

        var eventTab = ScrollTabUx2EnvI("Événements typés");
        _scriptSnapshot = StudioStyle.Label(
            "Aucun snapshot · prévisualisez une zone contenant au moins un cue gouverné.",
            10, StudioStyle.Muted, wrap: true);
        _events = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        eventTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvISnapshot", "Snapshot ForgeScript copié",
            "Le handle n’est pas affiché. L’instantané expose uniquement des valeurs copiées et ne modifie pas l’auteur.",
            _scriptSnapshot));
        eventTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvIEvents", "Cycle audio typé",
            "PlayCue, Complete, Interrupt, SetMusicState et PushSnapshot sont simulés en mémoire.", _events));
        eventTab.AddChild(StudioStyle.Label(
            "Cette vue ne déclenche ni ressource audio, ni callback, ni handler ForgeScript. Le runtime Godot reste l’autorité du playback.",
            9, StudioStyle.Muted, wrap: true));

        var reviewTab = ScrollTabUx2EnvI("Aperçu & validation");
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvIDiff", "Modifications proposées",
            "Le dry-run canonique ne change ni projet, ni historique.", _diff));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvIDiagnostics", "Validation structurée",
            "Sélection, géométrie, filtres et commande restent regroupés séparément.", _diagnostics));

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis prévisualisez · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 66);
        root.AddChild(_summary);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
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
        var actionNote = StudioStyle.Label(
            "Aperçu sec obligatoire · une seule commande canonique annulable est enregistrée.",
            9, StudioStyle.Muted, wrap: true);
        actionNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actionNote.CustomMinimumSize = new Vector2(300, 34);
        actions.AddChild(actionNote);
        root.AddChild(actions);
        _preview.Pressed += PreviewUx2EnvI;
        _apply.Pressed += ApplyUx2EnvI;
        _undo.Pressed += UndoUx2EnvI;
        _redo.Pressed += RedoUx2EnvI;
        _save.Pressed += SaveUx2EnvI;
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        WireDraftChangesUx2EnvI();
        SetEditingEnabledUx2EnvI(false);
        ResetReviewUx2EnvI();
        UpdateActionStateUx2EnvI();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
            ReleaseIsolatedEvidenceSessionUx2EnvI();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvI();
        RefreshChoicesUx2EnvI();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession))
            _session.CancelAudioScatterApprovalUx2EnvI();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _scatterDrafts.Clear();
        ReleaseIsolatedEvidenceSessionUx2EnvI();
        _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureAudioScatterApprovalUx2EnvI();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvI();
        RefreshChoicesUx2EnvI();
        _sourceSession.RestoreAudioScatterApprovalUx2EnvI(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureAudioScatterApprovalUx2EnvI();
        Close();
        _sourceSession.RestoreAudioScatterApprovalUx2EnvI(sourceApproval);
        _evidenceReleaseSucceeded = true;
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || sourceProject.Areas.Count == 0) return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshotUx2EnvI();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux2-env-i-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var realArea = sourceProject.Areas.FirstOrDefault(area =>
                area.RuntimePolicies.Experience.AudioCues.Count > 0 &&
                area.RuntimePolicies.Experience.ScatterEmitters.Count > 0);
            _evidenceUsesRealModuleData = realArea is not null;
            _evidenceUsesCanonicalFallback = realArea is null;
            var evidenceProject = realArea is null
                ? StudioSessionController.CreateUx2EnvIContractProject(sourceProject)
                : sourceProject;
            var evidenceAreaId = realArea?.Id ?? evidenceProject.Areas.First(area =>
                area.RuntimePolicies.Experience.ScatterEmitters.Count > 0).Id;
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot, evidenceAreaId.ToString()).Succeeded)
            {
                evidenceSession.Close();
                var released = StudioSessionController.DeleteUx2EnvITemporaryWorkspace(tempRoot);
                ResetEvidenceStateUx2EnvI();
                _evidenceReleaseSucceeded = released;
                if (!released) _evidenceTempRoot = tempRoot;
                return false;
            }
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            Visible = true;
            UpdateDialogBoundsUx2EnvI();
            RefreshChoicesUx2EnvI();
            var choiceIndex = _choices.ToList().FindIndex(choice => choice.AreaId == evidenceAreaId);
            if (choiceIndex < 0)
            {
                Close();
                return false;
            }
            _area.Select(choiceIndex + 1);
            LoadChoiceUx2EnvI(choiceIndex);
            if (!ConfigureEvidenceDraftUx2EnvI())
            {
                Close();
                return false;
            }
            PreviewUx2EnvI();
            _tabs.CurrentTab = 2;
            var ready = CanonicalPreviewReady && HasExplicitSelection && ShowsScatterPlan &&
                        ShowsTypedEventPlan && ShowsStructuredDiagnostics && ShowsBoundedRuntimeClaim &&
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
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_I_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            var sessionReleased = true;
            try
            {
                evidenceSession.Close();
            }
            catch (Exception releaseException)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_I_EVIDENCE_RELEASE_FAILED " +
                            $"type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            var released = StudioSessionController.DeleteUx2EnvITemporaryWorkspace(tempRoot);
            ResetEvidenceStateUx2EnvI();
            _evidenceReleaseSucceeded = sessionReleased && released;
            if (!released) _evidenceTempRoot = tempRoot;
            _session = _sourceSession;
            Visible = false;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshotUx2EnvI();
        if (!PrepareEvidence("ux2-env-i-audio-scatter-events") ||
            !CanonicalPreviewReady || !UsesIsolatedEvidence)
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
        ApplyUx2EnvI();
        var applied = _applied && isolated.Project is not null;
        var appliedJson = applied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        UndoUx2EnvI();
        var undone = !_applied && ReferenceEquals(isolatedProjectBefore, isolated.Project) &&
                     isolated.History?.CanRedo == true;
        RedoUx2EnvI();
        var redone = _applied && isolated.Project is not null &&
                     string.Equals(appliedJson, ProjectJsonSerializer.Serialize(isolated.Project!),
                         StringComparison.Ordinal);
        SaveUx2EnvI();
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
        return applied && undone && redone && roundTrip && SourceSnapshotMatchesUx2EnvI(sourceSnapshot) &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private VBoxContainer ScrollTabUx2EnvI(string title)
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

    private void RefreshChoicesUx2EnvI()
    {
        _updating = true;
        _choices = _session.AudioScatterChoicesUx2EnvI;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        foreach (var choice in _choices) _area.AddItem(choice.Label);
        _area.Select(0);
        _updating = false;
        ClearSelectionUx2EnvI();
    }

    private void SelectAreaUx2EnvI(long index)
    {
        if (_updating) return;
        if (index <= 0 || index > _choices.Count)
        {
            ClearSelectionUx2EnvI();
            return;
        }
        LoadChoiceUx2EnvI((int)index - 1);
    }

    private void ClearSelectionUx2EnvI()
    {
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _scatterDrafts.Clear();
        _cueChoices = [];
        _selection.Text = "Aucune zone sélectionnée · aucune donnée chargée.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditingEnabledUx2EnvI(false);
        RefreshScatterListUx2EnvI();
        ResetReviewUx2EnvI();
        _session.CancelAudioScatterApprovalUx2EnvI();
        UpdateActionStateUx2EnvI();
    }

    private void LoadChoiceUx2EnvI(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            ClearSelectionUx2EnvI();
            return;
        }
        _draft = _session.CreateAudioScatterDraftUx2EnvI(_choices[choiceIndex].AreaId);
        if (_draft is null)
        {
            ClearSelectionUx2EnvI();
            SetStatusUx2EnvI("La zone choisie n’est plus disponible.", error: true);
            return;
        }
        _cueChoices = _session.AudioScatterCueChoicesUx2EnvI(_draft.AreaId);
        _scatterDrafts.Clear();
        _scatterDrafts.AddRange(_draft.ScatterEmitters);
        _updating = true;
        _cue.Clear();
        foreach (var cue in _cueChoices) _cue.AddItem(cue.Label);
        _updating = false;
        RefreshScatterListUx2EnvI();
        if (_scatterDrafts.Count > 0)
        {
            _scatter.Select(0);
            LoadScatterEditorUx2EnvI(0);
        }
        else
        {
            ClearScatterEditorUx2EnvI();
        }
        _selection.Text = $"Zone sélectionnée · {HumanLabelUx2EnvI(_draft.AreaDisplayName)} · " +
                          $"{_choices[choiceIndex].CueCount} cue(s) · {_scatterDrafts.Count} dispersion(s)";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        SetEditingEnabledUx2EnvI(true);
        ResetReviewUx2EnvI();
        SetStatusUx2EnvI(_cueChoices.Count == 0
            ? "Zone chargée · aucun cue gouverné : ajout de dispersion bloqué honnêtement"
            : "Zone chargée · prévisualisation sèche obligatoire");
        UpdateActionStateUx2EnvI();
    }

    private void SelectScatterUx2EnvI()
    {
        if (_updating) return;
        var index = SelectedIndexUx2EnvI(_scatter);
        if (index >= 0 && index < _scatterDrafts.Count) LoadScatterEditorUx2EnvI(index);
    }

    private void LoadScatterEditorUx2EnvI(int index)
    {
        if (index < 0 || index >= _scatterDrafts.Count)
        {
            ClearScatterEditorUx2EnvI();
            return;
        }
        var item = _scatterDrafts[index];
        _updating = true;
        var cueIndex = _cueChoices.ToList().FindIndex(choice => choice.CueId == item.CueId);
        if (cueIndex >= 0) _cue.Select(cueIndex);
        _centerX.Text = item.CenterX;
        _centerY.Text = item.CenterY;
        _centerZ.Text = item.CenterZ;
        _sizeX.Text = item.SizeX;
        _sizeY.Text = item.SizeY;
        _sizeZ.Text = item.SizeZ;
        _surfaceOnly.ButtonPressed = item.SurfaceOnly;
        _seed.Text = item.DeterministicSeed;
        _minimumInterval.Text = item.MinimumIntervalSeconds;
        _maximumInterval.Text = item.MaximumIntervalSeconds;
        _volume.Text = item.Volume;
        _requireNavigable.ButtonPressed = item.RequireNavigable;
        _biomeTags.Text = item.BiomeTags;
        _exclusionTags.Text = item.ExclusionTags;
        _updating = false;
    }

    private void ClearScatterEditorUx2EnvI()
    {
        _updating = true;
        foreach (var edit in DraftLineEditsUx2EnvI()) edit.Text = string.Empty;
        _surfaceOnly.ButtonPressed = false;
        _requireNavigable.ButtonPressed = false;
        if (_cue.ItemCount > 0) _cue.Select(0);
        _updating = false;
    }

    private void UpdateSelectedScatterUx2EnvI()
    {
        if (_updating) return;
        var index = SelectedIndexUx2EnvI(_scatter);
        if (index < 0 || index >= _scatterDrafts.Count) return;
        var source = _scatterDrafts[index];
        var cueId = _cue.Selected >= 0 && _cue.Selected < _cueChoices.Count
            ? _cueChoices[_cue.Selected].CueId
            : source.CueId;
        _scatterDrafts[index] = source with
        {
            CueId = cueId,
            CenterX = _centerX.Text,
            CenterY = _centerY.Text,
            CenterZ = _centerZ.Text,
            SizeX = _sizeX.Text,
            SizeY = _sizeY.Text,
            SizeZ = _sizeZ.Text,
            SurfaceOnly = _surfaceOnly.ButtonPressed,
            DeterministicSeed = _seed.Text,
            MinimumIntervalSeconds = _minimumInterval.Text,
            MaximumIntervalSeconds = _maximumInterval.Text,
            Volume = _volume.Text,
            RequireNavigable = _requireNavigable.ButtonPressed,
            BiomeTags = _biomeTags.Text,
            ExclusionTags = _exclusionTags.Text,
        };
        RefreshScatterListUx2EnvI(index);
    }

    private void AddScatterUx2EnvI()
    {
        if (_draft is null || _cueChoices.Count == 0)
        {
            SetStatusUx2EnvI("Ajout impossible : la zone ne contient aucun cue gouverné.", error: true);
            return;
        }
        UpdateSelectedScatterUx2EnvI();
        _scatterDrafts.Add(new StudioUx2EnvIScatterDraft(
            Guid.NewGuid(), _cueChoices[0].CueId,
            "0", "1", "0", "16", "4", "16", true,
            "1", "5", "15", "1", true, string.Empty, string.Empty,
            "studio:ux2-env-i"));
        RefreshScatterListUx2EnvI(_scatterDrafts.Count - 1);
        _scatter.Select(_scatterDrafts.Count - 1);
        LoadScatterEditorUx2EnvI(_scatterDrafts.Count - 1);
        InvalidatePreviewUx2EnvI();
    }

    private void RemoveScatterUx2EnvI()
    {
        var index = SelectedIndexUx2EnvI(_scatter);
        if (index < 0 || index >= _scatterDrafts.Count) return;
        _scatterDrafts.RemoveAt(index);
        RefreshScatterListUx2EnvI();
        if (_scatterDrafts.Count > 0)
        {
            var next = Math.Min(index, _scatterDrafts.Count - 1);
            _scatter.Select(next);
            LoadScatterEditorUx2EnvI(next);
        }
        else
        {
            ClearScatterEditorUx2EnvI();
        }
        InvalidatePreviewUx2EnvI();
    }

    private void RefreshScatterListUx2EnvI(int selected = -1)
    {
        _updating = true;
        _scatter.Clear();
        for (var index = 0; index < _scatterDrafts.Count; index++)
        {
            var item = _scatterDrafts[index];
            _scatter.AddItem($"{index + 1} · {HumanLabelUx2EnvI(item.CueId)} · " +
                             $"{(item.SurfaceOnly ? "surface" : "volume")} · " +
                             $"{HumanLabelUx2EnvI(item.MinimumIntervalSeconds)}–" +
                             $"{HumanLabelUx2EnvI(item.MaximumIntervalSeconds)} s");
        }
        if (selected >= 0 && selected < _scatterDrafts.Count) _scatter.Select(selected);
        _updating = false;
    }

    private StudioUx2EnvIDraft? CaptureDraftUx2EnvI()
    {
        if (_draft is null) return null;
        UpdateSelectedScatterUx2EnvI();
        var index = SelectedIndexUx2EnvI(_scatter);
        return _draft with
        {
            SelectedEmitterId = index >= 0 && index < _scatterDrafts.Count ? _scatterDrafts[index].Id : null,
            ScatterEmitters = _scatterDrafts.ToArray(),
        };
    }

    private void InvalidatePreviewUx2EnvI()
    {
        if (_updating) return;
        UpdateSelectedScatterUx2EnvI();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = false;
        _session.CancelAudioScatterApprovalUx2EnvI();
        ResetReviewUx2EnvI();
        SetStatusUx2EnvI("Réglages modifiés · relancez la prévisualisation");
        UpdateActionStateUx2EnvI();
    }

    private void PreviewUx2EnvI()
    {
        var draft = CaptureDraftUx2EnvI();
        if (draft is null)
        {
            SetStatusUx2EnvI("Sélectionnez explicitement une zone.", error: true);
            return;
        }
        _draft = draft;
        _lastPreview = _session.PreviewAudioScatterUx2EnvI(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded && _lastPreview.CanonicalDryRun;
        _applied = false;
        RenderPreviewUx2EnvI(_lastPreview);
        SetStatusUx2EnvI(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActionStateUx2EnvI();
    }

    private void ApplyUx2EnvI()
    {
        if (!_canonicalPreviewReady)
        {
            SetStatusUx2EnvI("Relancez l’aperçu sec avant d’appliquer.", error: true);
            return;
        }
        var result = _session.ApplyApprovedAudioScatterUx2EnvI();
        _canonicalPreviewReady = false;
        _applied = result.Succeeded;
        if (result.Succeeded) AudioScatterChanged?.Invoke();
        SetStatusUx2EnvI(result.Message, !result.Succeeded);
        UpdateActionStateUx2EnvI();
    }

    private void UndoUx2EnvI()
    {
        var result = _session.Undo();
        _applied = false;
        if (result.Succeeded) ReloadAfterHistoryUx2EnvI();
        SetStatusUx2EnvI(result.Message, !result.Succeeded);
        UpdateActionStateUx2EnvI();
    }

    private void RedoUx2EnvI()
    {
        var result = _session.Redo();
        _applied = result.Succeeded;
        if (result.Succeeded) ReloadAfterHistoryUx2EnvI();
        SetStatusUx2EnvI(result.Message, !result.Succeeded);
        UpdateActionStateUx2EnvI();
    }

    private void SaveUx2EnvI()
    {
        var result = _session.Save();
        SetStatusUx2EnvI(result.Message, !result.Succeeded);
        UpdateActionStateUx2EnvI();
    }

    private void ReloadAfterHistoryUx2EnvI()
    {
        var applied = _applied;
        var areaId = _draft?.AreaId;
        if (areaId is not null)
        {
            var index = _choices.ToList().FindIndex(choice => choice.AreaId == areaId);
            if (index >= 0) LoadChoiceUx2EnvI(index);
        }
        _applied = applied;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        ResetReviewUx2EnvI();
    }

    private void RenderPreviewUx2EnvI(StudioUx2EnvIPreview preview)
    {
        ClearChildrenUx2EnvI(_samples);
        foreach (var sample in preview.Samples)
            _samples.AddChild(StudioStyle.Label(
                $"Séquence {sample.Sequence} · {sample.Position} · délai {sample.Delay} · volume {sample.Volume} · {sample.FilterState}",
                9, StudioStyle.Text, wrap: true));
        ClearChildrenUx2EnvI(_filterProbes);
        foreach (var probe in preview.FilterProbes)
            _filterProbes.AddChild(StudioStyle.Label(
                $"{probe.Scenario} · {probe.Result} · {probe.Explanation}",
                9, probe.Result == "Bloqué" ? StudioStyle.Warning : StudioStyle.Success, wrap: true));
        ClearChildrenUx2EnvI(_events);
        foreach (var step in preview.Events)
            _events.AddChild(StudioStyle.Label(
                $"{step.Operation} · {step.EventType} · {step.Cue} · {step.RuntimeState} · {step.Detail}",
                9, StudioStyle.Text, wrap: true));
        _scriptSnapshot.Text = preview.ScriptSnapshot is null
            ? "Aucun snapshot : le profil ne contient aucun cue."
            : $"Schéma {preview.ScriptSnapshot.SchemaVersion} · {preview.ScriptSnapshot.CueCount} cue(s) · " +
              $"{preview.ScriptSnapshot.ScatterCount} dispersion(s) · {preview.ScriptSnapshot.VoiceCount} voix · " +
              $"{preview.ScriptSnapshot.PatrolCount} patrouille(s) · valeurs copiées · mutation persistante exclue";
        ClearChildrenUx2EnvI(_diff);
        if (preview.Diff.Entries.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucune différence persistante.", 9, StudioStyle.Muted));
        else
            foreach (var entry in preview.Diff.Entries)
                _diff.AddChild(StudioStyle.Label($"• {entry}", 9, StudioStyle.Text, wrap: true));
        ClearChildrenUx2EnvI(_diagnostics);
        foreach (var group in preview.Groups)
        {
            _diagnostics.AddChild(StudioStyle.Label(
                $"{group.Title} · {group.State}", 10,
                group.Diagnostics.Count == 0 ? StudioStyle.Success : StudioStyle.Warning));
            foreach (var diagnostic in group.Diagnostics)
                _diagnostics.AddChild(StudioStyle.Label(
                    $"  {diagnostic.Message} · champ {HumanLabelUx2EnvI(diagnostic.Path)}",
                    9, StudioStyle.Warning, wrap: true));
        }
        _summary.Text = preview.Summary;
        _summary.AddThemeColorOverride("font_color", preview.Succeeded ? StudioStyle.Text : StudioStyle.Warning);
    }

    private void ResetReviewUx2EnvI()
    {
        ClearChildrenUx2EnvI(_samples);
        ClearChildrenUx2EnvI(_filterProbes);
        ClearChildrenUx2EnvI(_events);
        ClearChildrenUx2EnvI(_diff);
        ClearChildrenUx2EnvI(_diagnostics);
        _samples.AddChild(StudioStyle.Label("Prévisualisez pour calculer cinq positions.", 9, StudioStyle.Muted));
        _filterProbes.AddChild(StudioStyle.Label("Aucun scénario de filtre calculé.", 9, StudioStyle.Muted));
        _events.AddChild(StudioStyle.Label("Aucun événement simulé.", 9, StudioStyle.Muted));
        _scriptSnapshot.Text = "Aucun snapshot · prévisualisez une zone contenant un cue gouverné.";
        _diff.AddChild(StudioStyle.Label("Aucune prévisualisation.", 9, StudioStyle.Muted));
        _diagnostics.AddChild(StudioStyle.Label("Aucune validation exécutée.", 9, StudioStyle.Muted));
        _summary.Text = "Sélectionnez une zone puis prévisualisez · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void UpdateActionStateUx2EnvI()
    {
        _apply.Disabled = _lastPreview is not { Succeeded: true, CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = _session.Project is null;
        _preview.Disabled = _draft is null;
        _addScatter.Disabled = _draft is null || _cueChoices.Count == 0;
        _removeScatter.Disabled = _draft is null || SelectedIndexUx2EnvI(_scatter) < 0;
    }

    private void SetEditingEnabledUx2EnvI(bool enabled)
    {
        foreach (var edit in DraftLineEditsUx2EnvI()) edit.Editable = enabled;
        _cue.Disabled = !enabled;
        _surfaceOnly.Disabled = !enabled;
        _requireNavigable.Disabled = !enabled;
        _scatter.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    private bool ConfigureEvidenceDraftUx2EnvI()
    {
        if (_draft is null || _scatterDrafts.Count == 0 || _cueChoices.Count == 0) return false;
        var source = _scatterDrafts[0];
        _scatterDrafts[0] = source with
        {
            CenterX = source.CenterX == "6.375" ? "6.5" : "6.375",
            CenterY = "1.75",
            CenterZ = "-3.25",
            SizeX = "26.5",
            SizeY = "7.25",
            SizeZ = "19.5",
            SurfaceOnly = true,
            DeterministicSeed = "2117",
            MinimumIntervalSeconds = "3.75",
            MaximumIntervalSeconds = "12.5",
            Volume = "0.87",
            RequireNavigable = true,
            BiomeTags = "forest, ruins",
            ExclusionTags = "combat, silence",
        };
        _draft = _draft with
        {
            SelectedEmitterId = source.Id,
            ScatterEmitters = _scatterDrafts.ToArray(),
        };
        RefreshScatterListUx2EnvI(0);
        _scatter.Select(0);
        LoadScatterEditorUx2EnvI(0);
        return true;
    }

    private StudioUx2EnvIEvidenceSourceSnapshot CaptureSourceSnapshotUx2EnvI()
    {
        var project = _sourceSession.Project!;
        var history = _sourceSession.History!;
        return new StudioUx2EnvIEvidenceSourceSnapshot(
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
            _sourceSession.CaptureAudioScatterApprovalUx2EnvI());
    }

    private bool SourceSnapshotMatchesUx2EnvI(StudioUx2EnvIEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _evidenceSourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAudioScatterApprovalUx2EnvI() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSessionUx2EnvI()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        var sessionReleased = true;
        try
        {
            evidenceSession?.Close();
        }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_I_EVIDENCE_RELEASE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
        }
        var directoryReleased = StudioSessionController.DeleteUx2EnvITemporaryWorkspace(tempRoot);
        ResetEvidenceStateUx2EnvI();
        _session = _sourceSession;
        _evidenceReleaseSucceeded = sessionReleased && directoryReleased;
        if (!directoryReleased) _evidenceTempRoot = tempRoot;
        return _evidenceReleaseSucceeded;
    }

    private void ResetEvidenceStateUx2EnvI()
    {
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceUsesRealModuleData = false;
        _evidenceUsesCanonicalFallback = false;
        _evidenceSourceSnapshot = null;
    }

    private void WireDraftChangesUx2EnvI()
    {
        foreach (var edit in DraftLineEditsUx2EnvI()) edit.TextChanged += _ => InvalidatePreviewUx2EnvI();
        _cue.ItemSelected += _ => InvalidatePreviewUx2EnvI();
        _surfaceOnly.Toggled += _ => InvalidatePreviewUx2EnvI();
        _requireNavigable.Toggled += _ => InvalidatePreviewUx2EnvI();
    }

    private IReadOnlyList<LineEdit> DraftLineEditsUx2EnvI() =>
    [
        _centerX, _centerY, _centerZ, _sizeX, _sizeY, _sizeZ, _seed,
        _minimumInterval, _maximumInterval, _volume, _biomeTags, _exclusionTags,
    ];

    private void SetStatusUx2EnvI(string text, bool error = false)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Warning : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private void UpdateDialogBoundsUx2EnvI()
    {
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 48, 1180, 1740);
        var height = Mathf.Clamp(viewport.Y - 48, 720, 1030);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateDialogBoundsUx2EnvI();
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

    private static LineEdit TextUx2EnvI(string placeholder) => StudioUx2EnvBControls.Text(placeholder);

    private static ItemList ListUx2EnvI(float height) => new()
    {
        CustomMinimumSize = new Vector2(0, height),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SelectMode = ItemList.SelectModeEnum.Single,
    };

    private static GridContainer GridUx2EnvI(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 8);
        return grid;
    }

    private static int SelectedIndexUx2EnvI(ItemList list) =>
        list.GetSelectedItems().FirstOrDefault(-1);

    private static string HumanLabelUx2EnvI(string? value)
    {
        var text = (value ?? string.Empty).Replace('-', ' ').Replace('_', ' ').Replace('/', ' ').Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsUuidUx2EnvI(text)
            ? "élément sans nom lisible"
            : text;
    }

    private static void ClearChildrenUx2EnvI(Node node)
    {
        foreach (var child in node.GetChildren()) child.Free();
    }

    private static IEnumerable<string> DescendantVisibleTextUx2EnvI(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is CheckBox { Visible: true } check) yield return check.Text;
            if (child is OptionButton { Visible: true } option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            if (child is ItemList { Visible: true } list)
                for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
            foreach (var nested in DescendantVisibleTextUx2EnvI(child)) yield return nested;
        }
    }

    private static bool ContainsUuidUx2EnvI(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text, "(?i)(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})");
}
