using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioGameplayProductionWorkbenchUx6E : ColorRect
{
    private const string ProtectedValue = "Identité héritée protégée";

    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6EEvidenceSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _profile;
    private readonly Label _selection;
    private readonly TabContainer _tabs;

    private readonly CheckBox _zeroGravityEnabled;
    private readonly SpinBox _thrusterAcceleration;
    private readonly SpinBox _maximumSpeed;
    private readonly SpinBox _linearDrag;
    private readonly CheckBox _orientationAssist;
    private readonly CheckBox _preserveMomentum;

    private readonly CheckBox _vehiclesEnabled;
    private readonly SpinBox _maximumSeats;
    private readonly OptionButton _motionMode;
    private readonly SpinBox _maximumIntegrity;
    private readonly CheckBox _allowPossession;
    private readonly CheckBox _replicateAuthoritatively;

    private readonly CheckBox _mountsEnabled;
    private readonly CheckBox _allowCombat;
    private readonly CheckBox _allowInventory;
    private readonly CheckBox _persistRiderState;
    private readonly OptionButton _dismountPolicy;

    private readonly OptionButton _achievementChoice;
    private readonly Button _addAchievement;
    private readonly Button _removeAchievement;
    private readonly LineEdit _achievementKey;
    private readonly LineEdit _achievementEvent;
    private readonly SpinBox _achievementThreshold;
    private readonly LineEdit _achievementPlatform;
    private readonly CheckBox _achievementTelemetry;

    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _runtime;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx6EProfileChoice> _choices = [];
    private StudioUx6EDraft? _draft;
    private List<StudioUx6EAchievementDraft> _achievements = [];
    private int _achievementIndex = -1;
    private bool _updating;
    private bool _previewReady;
    private string? _previewFingerprint;
    private StudioUx6EPreview? _previewResult;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? GameplayProductionChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _draft is not null && _profile.Selected > 0;
    internal bool PreviewReady => _previewReady && _previewResult is
    { Succeeded: true, CanonicalDryRun: true, RuntimeClaimsBounded: true };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6E();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsFourWorkflows => _tabs.GetTabCount() == 4 &&
        Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle).SequenceEqual(
            new[] { "Apesanteur", "Véhicules", "Montures", "Succès" });
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6E(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Production gameplay", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   text.Contains("Apesanteur", StringComparison.Ordinal) &&
                   text.Contains("Véhicules", StringComparison.Ordinal) &&
                   text.Contains("Montures", StringComparison.Ordinal) &&
                   text.Contains("Succès", StringComparison.Ordinal) && !ContainsUuidUx6E(text);
        }
    }

    internal StudioGameplayProductionWorkbenchUx6E(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .975f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6EGameplayProductionDialog", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("Production gameplay", 18));
        headingText.AddChild(StudioStyle.Label(
            "Apesanteur, véhicules, montures et succès · aperçu sec canonique · aucune exécution moteur.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _profile = StudioUx2EnvBControls.Options();
        _profile.ItemSelected += SelectProfileUx6E;
        root.AddChild(FieldUx6E("Module · sélection explicite", _profile, 620));
        _selection = StudioStyle.Label("Choisissez explicitement le profil gameplay du module.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 300),
            CustomMaximumSize = new Vector2(-1, 580),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var zeroGravityTab = ScrollTabUx6E("Apesanteur");
        zeroGravityTab.AddChild(StudioStyle.Label(
            "Politique de déplacement en apesanteur · valeurs exactes persistées, sans simulation physique.",
            10, StudioStyle.Muted, wrap: true));
        var zeroFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _zeroGravityEnabled = StudioUx2EnvBControls.Check("Apesanteur activée");
        _orientationAssist = StudioUx2EnvBControls.Check("Assistance d’orientation");
        _preserveMomentum = StudioUx2EnvBControls.Check("Conserver l’élan");
        foreach (var item in new[] { _zeroGravityEnabled, _orientationAssist, _preserveMomentum })
            zeroFlags.AddChild(item);
        zeroGravityTab.AddChild(zeroFlags);
        var zeroGrid = GridUx6E(4);
        _thrusterAcceleration = NumberUx6E(.1, 500, .1);
        _maximumSpeed = NumberUx6E(.1, 2_000, .1);
        _linearDrag = NumberUx6E(0, 20, .01);
        zeroGrid.AddChild(FieldUx6E("Accélération propulseur (m/s²)", _thrusterAcceleration));
        zeroGrid.AddChild(FieldUx6E("Vitesse maximale (m/s)", _maximumSpeed));
        zeroGrid.AddChild(FieldUx6E("Traînée linéaire", _linearDrag));
        zeroGravityTab.AddChild(zeroGrid);

        var vehicleTab = ScrollTabUx6E("Véhicules");
        vehicleTab.AddChild(StudioStyle.Label(
            "Capacité d’authoring véhicule · aucune conduite, possession ou réplication n’est lancée ici.",
            10, StudioStyle.Muted, wrap: true));
        var vehicleFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _vehiclesEnabled = StudioUx2EnvBControls.Check("Véhicules activés");
        _allowPossession = StudioUx2EnvBControls.Check("Possession autorisée");
        _replicateAuthoritatively = StudioUx2EnvBControls.Check("Réplication autoritaire demandée");
        foreach (var item in new[] { _vehiclesEnabled, _allowPossession, _replicateAuthoritatively })
            vehicleFlags.AddChild(item);
        vehicleTab.AddChild(vehicleFlags);
        var vehicleGrid = GridUx6E(4);
        _maximumSeats = NumberUx6E(1, 64, 1);
        _motionMode = StudioUx2EnvBControls.Options("Cinématique", "Physique");
        _maximumIntegrity = NumberUx6E(1, 1_000_000, 1);
        vehicleGrid.AddChild(FieldUx6E("Places maximales", _maximumSeats));
        vehicleGrid.AddChild(FieldUx6E("Mode de mouvement", _motionMode));
        vehicleGrid.AddChild(FieldUx6E("Intégrité maximale", _maximumIntegrity));
        vehicleTab.AddChild(vehicleGrid);

        var mountTab = ScrollTabUx6E("Montures");
        mountTab.AddChild(StudioStyle.Label(
            "Capacité de monture et politique de descente · aucun acteur ou inventaire n’est modifié.",
            10, StudioStyle.Muted, wrap: true));
        var mountFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _mountsEnabled = StudioUx2EnvBControls.Check("Montures activées");
        _allowCombat = StudioUx2EnvBControls.Check("Combat à dos de monture");
        _allowInventory = StudioUx2EnvBControls.Check("Inventaire autorisé");
        _persistRiderState = StudioUx2EnvBControls.Check("État du cavalier persistant");
        foreach (var item in new[] { _mountsEnabled, _allowCombat, _allowInventory, _persistRiderState })
            mountFlags.AddChild(item);
        mountTab.AddChild(mountFlags);
        _dismountPolicy = StudioUx2EnvBControls.Options(
            "Sol sûr", "Position valide la plus proche", "Action explicite uniquement");
        mountTab.AddChild(FieldUx6E("Politique de descente", _dismountPolicy, 420));

        var achievementTab = ScrollTabUx6E("Succès");
        achievementTab.AddChild(StudioStyle.Label(
            "Jalons ordonnés par clé · identités stables protégées · plateforme et télémétrie restent des politiques.",
            10, StudioStyle.Muted, wrap: true));
        var achievementHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _achievementChoice = StudioUx2EnvBControls.Options();
        _achievementChoice.ItemSelected += SelectAchievementUx6E;
        achievementHeader.AddChild(FieldUx6E("Succès sélectionné", _achievementChoice, 520));
        _addAchievement = StudioStyle.Button("Ajouter un succès", compact: true);
        _addAchievement.Pressed += AddAchievementUx6E;
        achievementHeader.AddChild(_addAchievement);
        _removeAchievement = StudioStyle.Button("Retirer", compact: true);
        _removeAchievement.Pressed += RemoveAchievementUx6E;
        achievementHeader.AddChild(_removeAchievement);
        achievementTab.AddChild(achievementHeader);
        var achievementGrid = GridUx6E(4);
        _achievementKey = StudioUx2EnvBControls.Text("premier-pas");
        _achievementEvent = StudioUx2EnvBControls.Text("module.started");
        _achievementThreshold = NumberUx6E(1, 1_000_000, 1);
        _achievementPlatform = StudioUx2EnvBControls.Text("optionnel");
        achievementGrid.AddChild(FieldUx6E("Clé humaine", _achievementKey));
        achievementGrid.AddChild(FieldUx6E("Événement typé", _achievementEvent));
        achievementGrid.AddChild(FieldUx6E("Seuil", _achievementThreshold));
        achievementGrid.AddChild(FieldUx6E("Clé plateforme (optionnelle)", _achievementPlatform));
        achievementTab.AddChild(achievementGrid);
        _achievementTelemetry = StudioUx2EnvBControls.Check("Télémétrie respectueuse de la vie privée");
        achievementTab.AddChild(_achievementTelemetry);

        WireInvalidationUx6E();

        var reviewHost = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 150),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _diff = ReviewColumnUx6E(reviewHost, "Diff sémantique");
        _runtime = ReviewColumnUx6E(reviewHost, "Résolveurs déterministes");
        _diagnostics = ReviewColumnUx6E(reviewHost, "Diagnostics");
        root.AddChild(reviewHost);
        _summary = StudioStyle.Label("Aucun aperçu sec frais.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_summary);
        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        root.AddChild(StudioStyle.Label(
            "Aperçu en mémoire uniquement · aucune gravité moteur, plateforme, télémétrie, physique, acteur, réseau ou ForgeScript exécuté.",
            10, StudioStyle.Muted, wrap: true));
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = StudioStyle.Button("Aperçu sec", compact: true); _preview.Pressed += PreviewUx6E;
        _apply = StudioStyle.Button("Appliquer", compact: true); _apply.Pressed += ApplyUx6E;
        _undo = StudioStyle.Button("Annuler", compact: true); _undo.Pressed += UndoUx6E;
        _redo = StudioStyle.Button("Rétablir", compact: true); _redo.Pressed += RedoUx6E;
        _save = StudioStyle.Button("Enregistrer", compact: true); _save.Pressed += SaveUx6E;
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(118, 34);
            actions.AddChild(button);
        }
        root.AddChild(actions);

        RenderPreviewUx6E(null);
        UpdateButtonsUx6E();
    }

    internal void Open() => OpenBoundUx6E(_sourceSession, false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx6E(_sourceSession, false, cancelApproval: false);

    private void OpenBoundUx6E(StudioSessionController session, bool evidence,
        bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6EApproval();
        _choices = _session.GameplayProductionChoicesUx6E;
        ResetControlsUx6E();
        _profile.Clear();
        _profile.AddItem("Choisir explicitement un profil…");
        foreach (var choice in _choices) _profile.AddItem(choice.Label);
        _profile.Select(0);
        _selection.Text = evidence
            ? "Copie temporaire isolée · la source reste strictement inchangée."
            : "Choisissez explicitement le profil gameplay du module.";
        Visible = true;
        MoveToFront();
        UpdateBoundsUx6E();
        UpdateButtonsUx6E();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null) return;
        if (_evidenceSession is not null || _evidenceRoot is not null)
            ReleaseEvidenceUx6E();
        else
        {
            _session.CancelUx6EApproval();
            _session = _sourceSession;
        }
        Visible = false;
        ResetControlsUx6E();
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        ReleaseEvidenceUx6E();
        Visible = false;
        ResetControlsUx6E();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        CloseEvidence();
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        _sourceSnapshot = CaptureSourceUx6E();
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux6-e-{Guid.NewGuid():N}");
        _evidenceSession = new StudioSessionController();
        _evidenceReleased = false;
        var created = ProjectSession.Create(_evidenceRoot, _sourceSession.Project);
        if (!created.Succeeded || !_evidenceSession.Open(_evidenceRoot).Succeeded)
        {
            ReleaseEvidenceUx6E();
            return false;
        }
        OpenBoundUx6E(_evidenceSession, true);
        if (_choices.Count == 0)
        {
            CloseEvidence();
            return false;
        }
        _profile.Select(1);
        SelectProfileUx6E(1);
        ConfigureEvidenceUx6E();
        PreviewUx6E();
        var ready = PreviewReady && UsesIsolatedEvidence && HasExplicitSelection &&
                    ShowsFourWorkflows && ShowsHumanSurface && SourceMatchesUx6E();
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        var source = CaptureSourceUx6E();
        var prepared = false;
        try
        {
            prepared = PrepareEvidence("ux6-e-gameplay-production");
            if (!prepared || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady) return false;
            var before = _evidenceSession.Project!;
            var history = _evidenceSession.History!;
            var journalCount = history.Journal.Count;
            ApplyUx6E();
            var applied = history.Journal.Count == journalCount + 1 &&
                          history.Journal.Last().CommandName == "set_runtime_extensions" &&
                          !ReferenceEquals(before, _evidenceSession.Project);
            var after = _evidenceSession.Project!;
            UndoUx6E();
            var undone = ReferenceEquals(before, _evidenceSession.Project);
            RedoUx6E();
            var redone = ReferenceEquals(after, _evidenceSession.Project);
            var json = ProjectJsonSerializer.Serialize(after);
            SaveUx6E();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded && reopened.Project is not null &&
                            string.Equals(json, ProjectJsonSerializer.Serialize(reopened.Project),
                                StringComparison.Ordinal);
            reopened.Close();
            var sourceMatches = SourceMatchesUx6E(source);
            if (!(applied && undone && redone && persisted && sourceMatches))
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_E_UI_TRANSACTION_FAILED " +
                            $"applied={applied.ToString().ToLowerInvariant()} " +
                            $"undone={undone.ToString().ToLowerInvariant()} " +
                            $"redone={redone.ToString().ToLowerInvariant()} " +
                            $"persisted={persisted.ToString().ToLowerInvariant()} " +
                            $"source={sourceMatches.ToString().ToLowerInvariant()}");
            return applied && undone && redone && persisted && sourceMatches;
        }
        finally
        {
            if (prepared || _evidenceSession is not null || _evidenceRoot is not null) CloseEvidence();
            _sourceSession.RestoreGameplayProductionApprovalUx6E(source.Approval);
        }
    }

    private void SelectProfileUx6E(long selected)
    {
        if (_updating) return;
        _session.CancelUx6EApproval();
        _previewReady = false;
        _previewFingerprint = null;
        _previewResult = null;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null;
            ResetControlsUx6E();
            _selection.Text = "Sélection explicite requise.";
            return;
        }
        _draft = _session.CreateGameplayProductionDraftUx6E(true);
        if (_draft is null)
        {
            ResetControlsUx6E();
            _selection.Text = "Le profil gameplay étendu n’est pas disponible.";
            return;
        }
        PopulateUx6E(_draft);
        _selection.Text = $"Profil chargé · {_choices[(int)selected - 1].Label} · identités internes protégées.";
        SetStatusUx6E("Profil chargé. Modifiez puis lancez l’aperçu sec.", false);
    }

    private void PopulateUx6E(StudioUx6EDraft draft)
    {
        _updating = true;
        _zeroGravityEnabled.ButtonPressed = draft.ZeroGravity.Enabled;
        _thrusterAcceleration.Value = DisplayUx6E(draft.ZeroGravity.ThrusterAcceleration);
        _maximumSpeed.Value = DisplayUx6E(draft.ZeroGravity.MaximumSpeed);
        _linearDrag.Value = DisplayUx6E(draft.ZeroGravity.LinearDrag);
        _orientationAssist.ButtonPressed = draft.ZeroGravity.OrientationAssist;
        _preserveMomentum.ButtonPressed = draft.ZeroGravity.PreserveMomentum;
        _vehiclesEnabled.ButtonPressed = draft.Vehicles.Enabled;
        _maximumSeats.Value = draft.Vehicles.MaximumSeats;
        _motionMode.Select(draft.Vehicles.MotionMode == "physics" ? 1 : 0);
        _maximumIntegrity.Value = draft.Vehicles.MaximumIntegrity;
        _allowPossession.ButtonPressed = draft.Vehicles.AllowPossession;
        _replicateAuthoritatively.ButtonPressed = draft.Vehicles.ReplicateAuthoritatively;
        _mountsEnabled.ButtonPressed = draft.Mounts.Enabled;
        _allowCombat.ButtonPressed = draft.Mounts.AllowCombat;
        _allowInventory.ButtonPressed = draft.Mounts.AllowInventory;
        _persistRiderState.ButtonPressed = draft.Mounts.PersistRiderState;
        _dismountPolicy.Select(DismountIndexUx6E(draft.Mounts.DismountPolicy));
        _achievements = draft.Achievements.ToList();
        _achievementIndex = _achievements.Count > 0 ? 0 : -1;
        RebuildAchievementChoicesUx6E();
        PopulateAchievementUx6E();
        _updating = false;
        InvalidatePreviewUx6E();
    }

    private StudioUx6EDraft? ReadDraftUx6E()
    {
        if (_draft is null) return null;
        FlushAchievementUx6E();
        return _draft with
        {
            ZeroGravity = new(_zeroGravityEnabled.ButtonPressed,
                (float)_thrusterAcceleration.Value, (float)_maximumSpeed.Value,
                (float)_linearDrag.Value, _orientationAssist.ButtonPressed,
                _preserveMomentum.ButtonPressed),
            Vehicles = new(_vehiclesEnabled.ButtonPressed, (int)_maximumSeats.Value,
                _motionMode.Selected == 1 ? "physics" : "kinematic",
                (int)_maximumIntegrity.Value, _allowPossession.ButtonPressed,
                _replicateAuthoritatively.ButtonPressed),
            Mounts = new(_mountsEnabled.ButtonPressed, _allowCombat.ButtonPressed,
                _allowInventory.ButtonPressed, _persistRiderState.ButtonPressed,
                DismountValueUx6E(_dismountPolicy.Selected)),
            Achievements = _achievements.ToArray(),
        };
    }

    private void PreviewUx6E()
    {
        var draft = ReadDraftUx6E();
        if (draft is null)
        {
            SetStatusUx6E("Choisissez explicitement un profil.", true);
            return;
        }
        _draft = draft;
        _previewResult = _session.PreviewGameplayProductionUx6E(draft);
        _previewReady = _previewResult.Succeeded;
        _previewFingerprint = _session.GameplayProductionDraftFingerprintUx6E(draft);
        RenderPreviewUx6E(_previewResult);
        SetStatusUx6E(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtonsUx6E();
    }

    private void ApplyUx6E()
    {
        var current = ReadDraftUx6E();
        if (current is null || !_previewReady ||
            !string.Equals(_previewFingerprint,
                _session.GameplayProductionDraftFingerprintUx6E(current), StringComparison.Ordinal))
        {
            _session.CancelUx6EApproval();
            _previewReady = false;
            SetStatusUx6E("Le brouillon a changé. Relancez l’aperçu sec.", true);
            UpdateButtonsUx6E();
            return;
        }
        var result = _session.ApplyApprovedGameplayProductionUx6E();
        SetStatusUx6E(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            GameplayProductionChanged?.Invoke();
            ReloadSelectedUx6E();
        }
        UpdateButtonsUx6E();
    }

    private void UndoUx6E()
    {
        var result = _session.Undo();
        SetStatusUx6E(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            GameplayProductionChanged?.Invoke();
            ReloadSelectedUx6E();
        }
    }

    private void RedoUx6E()
    {
        var result = _session.Redo();
        SetStatusUx6E(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            GameplayProductionChanged?.Invoke();
            ReloadSelectedUx6E();
        }
    }

    private void SaveUx6E()
    {
        var result = _session.Save();
        SetStatusUx6E(result.Message, !result.Succeeded);
        UpdateButtonsUx6E();
    }

    private void ReloadSelectedUx6E()
    {
        var selected = _profile.Selected;
        _session.CancelUx6EApproval();
        _previewReady = false;
        _previewFingerprint = null;
        if (selected > 0) SelectProfileUx6E(selected);
    }

    private void ConfigureEvidenceUx6E()
    {
        var current = ReadDraftUx6E();
        if (current is null) return;
        _draft = StudioSessionController.ChangedDraftUx6E(current);
        PopulateUx6E(_draft);
        _tabs.CurrentTab = 3;
    }

    private void SelectAchievementUx6E(long selected)
    {
        if (_updating) return;
        FlushAchievementUx6E();
        _achievementIndex = selected >= 0 && selected < _achievements.Count ? (int)selected : -1;
        PopulateAchievementUx6E();
        InvalidatePreviewUx6E();
    }

    private void AddAchievementUx6E()
    {
        FlushAchievementUx6E();
        var suffix = _achievements.Count + 1;
        _achievements.Add(new(Guid.NewGuid(), $"jalon-studio-{suffix}", "module.started", 1,
            null, false));
        _achievementIndex = _achievements.Count - 1;
        RebuildAchievementChoicesUx6E();
        PopulateAchievementUx6E();
        InvalidatePreviewUx6E();
    }

    private void RemoveAchievementUx6E()
    {
        if (_achievementIndex < 0 || _achievementIndex >= _achievements.Count) return;
        _achievements.RemoveAt(_achievementIndex);
        _achievementIndex = _achievements.Count == 0 ? -1 : Math.Min(_achievementIndex,
            _achievements.Count - 1);
        RebuildAchievementChoicesUx6E();
        PopulateAchievementUx6E();
        InvalidatePreviewUx6E();
    }

    private void RebuildAchievementChoicesUx6E()
    {
        var previous = _updating;
        _updating = true;
        _achievementChoice.Clear();
        foreach (var item in _achievements)
            _achievementChoice.AddItem(HumanValueUx6E(item.Key));
        if (_achievementIndex >= 0 && _achievementIndex < _achievements.Count)
            _achievementChoice.Select(_achievementIndex);
        _updating = previous;
    }

    private void PopulateAchievementUx6E()
    {
        var previous = _updating;
        _updating = true;
        var selected = _achievementIndex >= 0 && _achievementIndex < _achievements.Count;
        var item = selected ? _achievements[_achievementIndex] : null;
        _achievementKey.Text = item is null ? string.Empty : DisplayProtectedUx6E(item.Key);
        _achievementEvent.Text = item is null ? string.Empty : DisplayProtectedUx6E(item.EventName);
        _achievementThreshold.Value = item?.Threshold ?? 1;
        _achievementPlatform.Text = item?.PlatformKey is null
            ? string.Empty
            : DisplayProtectedUx6E(item.PlatformKey);
        _achievementTelemetry.ButtonPressed = item?.PrivacySafeTelemetry == true;
        foreach (var control in new Control[]
                 {
                     _achievementKey, _achievementEvent, _achievementThreshold,
                     _achievementPlatform, _achievementTelemetry,
                 })
            control.MouseFilter = selected ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _achievementKey.Editable = selected;
        _achievementEvent.Editable = selected;
        _achievementThreshold.Editable = selected;
        _achievementPlatform.Editable = selected;
        _achievementTelemetry.Disabled = !selected;
        _removeAchievement.Disabled = !selected;
        _updating = previous;
        UpdateButtonsUx6E();
    }

    private void FlushAchievementUx6E()
    {
        if (_updating || _achievementIndex < 0 || _achievementIndex >= _achievements.Count) return;
        var current = _achievements[_achievementIndex];
        _achievements[_achievementIndex] = current with
        {
            Key = ReadProtectedUx6E(current.Key, _achievementKey.Text)!,
            EventName = ReadProtectedUx6E(current.EventName, _achievementEvent.Text)!,
            Threshold = (int)_achievementThreshold.Value,
            PlatformKey = string.IsNullOrWhiteSpace(_achievementPlatform.Text)
                ? null
                : ReadProtectedUx6E(current.PlatformKey, _achievementPlatform.Text),
            PrivacySafeTelemetry = _achievementTelemetry.ButtonPressed,
        };
        var previous = _updating;
        _updating = true;
        _achievementChoice.SetItemText(_achievementIndex,
            HumanValueUx6E(_achievements[_achievementIndex].Key));
        _updating = previous;
    }

    private void InvalidatePreviewUx6E()
    {
        if (_updating) return;
        _previewReady = false;
        _previewFingerprint = null;
        _session.CancelUx6EApproval();
        UpdateButtonsUx6E();
    }

    private void ResetControlsUx6E()
    {
        _updating = true;
        _draft = null;
        _achievements = [];
        _achievementIndex = -1;
        _achievementChoice.Clear();
        _previewReady = false;
        _previewFingerprint = null;
        _previewResult = null;
        RenderPreviewUx6E(null);
        _updating = false;
        PopulateAchievementUx6E();
        UpdateButtonsUx6E();
    }

    private void UpdateButtonsUx6E()
    {
        var selected = _draft is not null;
        _preview.Disabled = !selected;
        _apply.Disabled = !PreviewReady || _previewResult is not { CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
        _addAchievement.Disabled = !selected;
        _removeAchievement.Disabled = _achievementIndex < 0;
    }

    private void RenderPreviewUx6E(StudioUx6EPreview? preview)
    {
        ClearChildrenUx6E(_diff);
        ClearChildrenUx6E(_runtime);
        ClearChildrenUx6E(_diagnostics);
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label("Le diff sémantique apparaîtra après l’aperçu sec.",
                10, StudioStyle.Muted, wrap: true));
            _runtime.AddChild(StudioStyle.Label("Les décisions pures de mobilité et de succès apparaîtront ici.",
                10, StudioStyle.Muted, wrap: true));
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic frais.", 10, StudioStyle.Muted));
            _summary.Text = "Aucun aperçu sec frais.";
            return;
        }
        if (preview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted));
        foreach (var item in preview.Diff)
            _diff.AddChild(StudioStyle.Label("• " + item, 10, StudioStyle.Text, wrap: true));
        foreach (var item in preview.RuntimePreview)
            _runtime.AddChild(StudioStyle.Label("• " + item, 10, StudioStyle.Text, wrap: true));
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Conforme · dry-run canonique", 10, StudioStyle.Success));
        foreach (var item in preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label("• " + item.Message, 10,
                item.Blocking ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
        _summary.Text = preview.Message + "\n" + preview.PatchSummary;
        _summary.AddThemeColorOverride("font_color",
            preview.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
    }

    private StudioUx6EEvidenceSourceSnapshot CaptureSourceUx6E()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureGameplayProductionApprovalUx6E());
    }

    private bool SourceMatchesUx6E(StudioUx6EEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureGameplayProductionApprovalUx6E() == snapshot.Approval;
    }

    private bool ReleaseEvidenceUx6E()
    {
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var sessionReleased = true;
        try
        {
            isolated?.Close();
        }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_E_RELEASE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx6ETemporaryWorkspace(root);
        _evidenceSession = null;
        _evidenceRoot = rootReleased ? null : root;
        _sourceSnapshot = null;
        _session = _sourceSession;
        _evidenceReleased = sessionReleased && rootReleased;
        return _evidenceReleased;
    }

    private void WireInvalidationUx6E()
    {
        foreach (var item in new[]
                 {
                     _zeroGravityEnabled, _orientationAssist, _preserveMomentum, _vehiclesEnabled,
                     _allowPossession, _replicateAuthoritatively, _mountsEnabled, _allowCombat,
                     _allowInventory, _persistRiderState, _achievementTelemetry,
                 })
            item.Toggled += _ => InvalidatePreviewUx6E();
        foreach (var item in new[]
                 {
                     _thrusterAcceleration, _maximumSpeed, _linearDrag, _maximumSeats,
                     _maximumIntegrity, _achievementThreshold,
                 })
            item.ValueChanged += _ => InvalidatePreviewUx6E();
        _motionMode.ItemSelected += _ => InvalidatePreviewUx6E();
        _dismountPolicy.ItemSelected += _ => InvalidatePreviewUx6E();
        _achievementKey.TextChanged += _ => InvalidatePreviewUx6E();
        _achievementEvent.TextChanged += _ => InvalidatePreviewUx6E();
        _achievementPlatform.TextChanged += _ => InvalidatePreviewUx6E();
    }

    private void UpdateBoundsUx6E()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.68f : viewport.X >= 2_300 ? 1.22f : 1f;
        var width = Math.Clamp((viewport.X - 48f) / scale, 1_180f, 1_650f);
        var height = Math.Clamp((viewport.Y - 38f) / scale, 760f, 1_060f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(16f, (viewport.Y - height * scale) * .46f));
    }

    private VBoxContainer ScrollTabUx6E(string title)
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
            CustomMinimumSize = new Vector2(1_060, 0),
        };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);
        _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        return content;
    }

    private static GridContainer GridUx6E(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 9);
        grid.AddThemeConstantOverride("v_separation", 7);
        return grid;
    }

    private static SpinBox NumberUx6E(double min, double max, double step) =>
        StudioUx2EnvBControls.Number(min, max, step);

    private static VBoxContainer FieldUx6E(string label, Control control, int minimum = 118) =>
        StudioUx2EnvBControls.Field(label, control, minimum);

    private static VBoxContainer ReviewColumnUx6E(HBoxContainer host, string title)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(340, 0),
        };
        column.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(body);
        column.AddChild(scroll);
        host.AddChild(column);
        return body;
    }

    private static int DismountIndexUx6E(string value) => value switch
    {
        "nearest-valid" => 1,
        "explicit-only" => 2,
        _ => 0,
    };

    private static string DismountValueUx6E(int index) => index switch
    {
        1 => "nearest-valid",
        2 => "explicit-only",
        _ => "safe-ground",
    };

    private static string DisplayProtectedUx6E(string value) =>
        Guid.TryParse(value, out _) ? ProtectedValue : value;

    private static string? ReadProtectedUx6E(string? source, string displayed)
    {
        if (source is not null && Guid.TryParse(source, out _) &&
            string.Equals(displayed, ProtectedValue, StringComparison.Ordinal)) return source;
        return displayed;
    }

    private static string HumanValueUx6E(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Sans nom";
        if (Guid.TryParse(value, out _)) return ProtectedValue;
        return string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static double DisplayUx6E(float value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static void ClearChildrenUx6E(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private void SetStatusUx6E(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private static IEnumerable<string> DescendantTextUx6E(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is CheckBox { Visible: true } check) yield return check.Text;
            if (child is LineEdit { Visible: true } line) yield return line.Text;
            if (child is OptionButton { Visible: true } option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var nested in DescendantTextUx6E(child)) yield return nested;
        }
    }

    private static bool ContainsUuidUx6E(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx6E();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
