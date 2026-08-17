using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioTriggerLivingWorldOverlayUx2LwE : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwEEvidenceSourceSnapshot? _evidenceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _trigger;
    private readonly Label _selection;
    private readonly CheckBox _restEnabled;
    private readonly CheckBox _restSafe;
    private readonly LineEdit _dangerQuery;
    private readonly CheckBox _wholeParty;
    private readonly CheckBox _campSetup;
    private readonly LineEdit _allowedMessage;
    private readonly LineEdit _deniedMessage;
    private readonly CheckBox _clueEnabled;
    private readonly LineEdit _skill;
    private readonly SpinBox _difficulty;
    private readonly LineEdit _trace;
    private readonly SpinBox _direction;
    private readonly SpinBox _age;
    private readonly LineEdit _falseTrace;
    private readonly OptionButton _quest;
    private readonly CheckBox _narrativeEnabled;
    private readonly ItemList _participants;
    private readonly SpinBox _priority;
    private readonly CheckBox _once;
    private readonly SpinBox _cooldown;
    private readonly LineEdit _multiplayerPolicy;
    private readonly OptionButton _dialogue;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _restSummary;
    private readonly Label _clueSummary;
    private readonly Label _narrativeSummary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwETriggerChoice> _triggerChoices = [];
    private IReadOnlyList<StudioUx2LwEReferenceChoice<BlueprintId>> _participantChoices = [];
    private IReadOnlyList<StudioUx2LwEReferenceChoice<Guid>> _questChoices = [];
    private IReadOnlyList<StudioUx2LwEReferenceChoice<Guid>> _dialogueChoices = [];
    private readonly List<Guid?> _triggerIds = [];
    private readonly List<Guid?> _questIds = [];
    private readonly List<Guid?> _dialogueIds = [];
    private StudioUx2LwEDraft? _draft;
    private StudioUx2LwEPreview? _lastPreview;
    private bool _updating;
    private bool _canonicalPreviewReady;
    private bool _lastEvidenceSourceUntouched;
    private bool _lastEvidenceResourcesReleased;
    private bool _lastSourceRestored = true;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? LivingWorldTriggerChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _triggerChoices.Count;
    internal bool HasExplicitSelection => _draft is not null && _trigger.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview?.Succeeded == true;
    internal bool ShowsThreePolicies => _lastPreview is not null &&
                                        !_restSummary.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase) &&
                                        !_clueSummary.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase) &&
                                        !_narrativeSummary.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsNoRuntimeExecutionBoundary => _lastPreview?.Message.Contains(
        "aucun document, script, moteur ou réseau n’a été exécuté", StringComparison.OrdinalIgnoreCase) == true;
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesRealReferences => _participantChoices.Count > 0 && _dialogueChoices.Count > 0 &&
                                        _participantChoices.All(item => item.Provenance.StartsWith(
                                            "Blueprint ", StringComparison.Ordinal)) &&
                                        _dialogueChoices.All(item => item.Provenance.StartsWith(
                                            "Dialogue ", StringComparison.Ordinal));
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _evidenceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceTempRoot is null &&
                                               _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabels(_dialog).Select(item => item.Text));
            return text.Contains("Repos sûr", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Piste", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Interjection", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("politique", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsTechnicalIdentifier(text);
        }
    }

    internal StudioTriggerLivingWorldOverlayUx2LwE(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioTriggerLivingWorldOverlayUx2LwE";
        Visible = false;
        ZIndex = 470;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(0, 0, 0, .82f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("TriggerLivingWorldDialog", StudioStyle.Chrome, 12, 2);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("MONDE VIVANT · DÉCLENCHEURS", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Repos sûr, pistes et interjections", 20));
        heading.AddChild(StudioStyle.Label(
            "TRG-014/015/016 · politiques canoniques sur un volume existant · aperçu sec sans exécuter " +
            "le moteur, le dialogue, un script ou un vote réseau",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);

        _trigger = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_trigger);
        _trigger.ItemSelected += index => SelectTrigger((int)index);
        root.AddChild(_trigger);
        _selection = StudioStyle.Label(
            "Choisissez explicitement un déclencheur doté d’un volume de base ; aucune fiche n’est ouverte automatiquement.",
            11, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 560),
            CustomMaximumSize = new Vector2(-1, 810),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);

        _restEnabled = Check("Configurer le repos sûr");
        _restSafe = Check("Ce volume autorise le repos");
        _dangerQuery = Line("danger.nearby");
        _wholeParty = Check("Exiger le groupe complet");
        _campSetup = Check("Autoriser l’installation du camp");
        _allowedMessage = Line("rest.allowed");
        _deniedMessage = Line("rest.denied");
        var restGrid = Grid();
        AddRow(restGrid, "Politique", _restEnabled);
        AddRow(restGrid, "Sécurité", _restSafe);
        AddRow(restGrid, "Requête de danger", _dangerQuery);
        AddRow(restGrid, "Présence", _wholeParty);
        AddRow(restGrid, "Camp", _campSetup);
        AddRow(restGrid, "Message autorisé", _allowedMessage);
        AddRow(restGrid, "Message refusé", _deniedMessage);
        content.AddChild(Card("LivingWorldRest", "Repos sûr",
            "Le runtime recoupe ce volume avec la politique de zone, le danger réellement observé et la présence du groupe.",
            restGrid));

        _clueEnabled = Check("Configurer une piste");
        _skill = Line("tracking");
        _difficulty = Spin(0, int.MaxValue, 1);
        _trace = Line("trail.authored");
        _direction = Spin(0, Math.BitDecrement(360d), .25);
        _age = Spin(0, double.MaxValue, .25);
        _falseTrace = Line("trail.decoy");
        _quest = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_quest);
        var clueGrid = Grid();
        AddRow(clueGrid, "Politique", _clueEnabled);
        AddRow(clueGrid, "Compétence", _skill);
        AddRow(clueGrid, "Difficulté", _difficulty);
        AddRow(clueGrid, "Trace révélée", _trace);
        AddRow(clueGrid, "Direction", _direction);
        AddRow(clueGrid, "Âge en heures", _age);
        AddRow(clueGrid, "Faux indice optionnel", _falseTrace);
        AddRow(clueGrid, "Quête / journal", _quest);
        content.AddChild(Card("LivingWorldClue", "Piste",
            "Le score de compétence vient du runtime ; la fiche conserve la trace, sa direction, son âge et un faux indice déterministe.",
            clueGrid));

        _narrativeEnabled = Check("Configurer une interjection");
        _participants = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Multi,
            CustomMinimumSize = new Vector2(0, 92),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _participants.AddThemeColorOverride("font_color", StudioStyle.Text);
        _priority = Spin(0, 1_000, 1);
        _once = Check("Une seule fois");
        _cooldown = Spin(0, double.MaxValue, 1);
        _multiplayerPolicy = Line("party-vote");
        _dialogue = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_dialogue);
        var narrativeGrid = Grid();
        AddRow(narrativeGrid, "Politique", _narrativeEnabled);
        AddRow(narrativeGrid, "Participants présents", _participants);
        AddRow(narrativeGrid, "Priorité", _priority);
        AddRow(narrativeGrid, "Répétition", _once);
        AddRow(narrativeGrid, "Délai en secondes", _cooldown);
        AddRow(narrativeGrid, "Politique multijoueur", _multiplayerPolicy);
        AddRow(narrativeGrid, "Dialogue canonique", _dialogue);
        content.AddChild(Card("LivingWorldNarrative", "Interjection",
            "Le serveur vérifie les participants, la priorité, le délai et la politique multijoueur avant de demander le dialogue existant. " +
            "La passerelle locale ne résout party-vote qu’avec un seul membre ; les autres cas restent en attente.",
            narrativeGrid));

        _diff = new VBoxContainer();
        _diff.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card("LivingWorldDiff", "Diff humain",
            "Avant / après des trois politiques ; les autres familles restent inchangées.", _diff));
        _diagnostics = new VBoxContainer();
        _diagnostics.AddThemeConstantOverride("separation", 4);
        content.AddChild(Card("LivingWorldDiagnostics", "Diagnostics localisés",
            "Chaque refus conserve son code et son chemin canonique sans exposer d’identifiant technique.",
            _diagnostics));
        _restSummary = StudioStyle.Label("Repos non évalué", 10, StudioStyle.Muted, wrap: true);
        _clueSummary = StudioStyle.Label("Piste non évaluée", 10, StudioStyle.Muted, wrap: true);
        _narrativeSummary = StudioStyle.Label("Interjection non évaluée", 10, StudioStyle.Muted, wrap: true);
        var runtime = new VBoxContainer();
        runtime.AddChild(_restSummary);
        runtime.AddChild(_clueSummary);
        runtime.AddChild(_narrativeSummary);
        content.AddChild(Card("LivingWorldRuntime", "Aperçu runtime honnête",
            "Résolution de politiques seulement : aucune simulation de collision, aucun script, dialogue ou vote n’est exécuté.",
            runtime));

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 7);
        root.AddChild(actions);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        actions.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        actions.AddChild(_apply);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += Undo;
        actions.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        actions.AddChild(_redo);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += Save;
        actions.AddChild(_save);
        _status = StudioStyle.Label("Aucune fiche sélectionnée.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        WireDraftInvalidation();
        Resized += FitDialog;
        VisibilityChanged += FitDialog;
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        _lastSourceRestored = true;
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
            ReleaseEvidence();
        else
            _session.CancelLivingWorldTriggerPreviewUx2LwE();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _apply.Disabled = true;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return false;
        if (_evidenceSession is not null) ReleaseEvidence();
        var project = _sourceSession.CreateUx2LwEEvidenceProject();
        if (project is null) return false;
        _evidenceSnapshot = CaptureSourceSnapshot();
        _lastEvidenceSourceUntouched = false;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        var evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux2-lw-e-evidence-{Guid.NewGuid():N}");
        _evidenceTempRoot = evidenceRoot;
        try
        {
            var created = ProjectSession.Create(evidenceRoot, project);
            if (!created.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX2_LW_E_EVIDENCE_CREATE_FAILED " +
                    string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                ReleaseEvidence();
                return false;
            }
            var evidence = new StudioSessionController();
            _evidenceSession = evidence;
            var opened = evidence.Open(evidenceRoot);
            if (!opened.Succeeded)
            {
                Godot.GD.PrintErr(
                    "OPENRPG_FORGE_STUDIO_UX2_LW_E_EVIDENCE_OPEN_FAILED " +
                    string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                ReleaseEvidence();
                return false;
            }
            evidence.SelectArea(evidence.Project!.Areas.First(item =>
                evidence.Project.Module.WorldFeatures.Triggers.Any(trigger =>
                    trigger.AreaId == item.Id && trigger.Foundation is not null)).Id);
            _session = evidence;
            Visible = true;
            Reload(clearSelection: true);
            var eligible = _triggerChoices.Select((item, position) => (item, position))
                .FirstOrDefault(pair => pair.item.HasFoundation);
            if (eligible.item is null)
            {
                ReleaseEvidence();
                return false;
            }
            var index = eligible.position + 1;
            _trigger.Select(index);
            SelectTrigger(index);
            if (_draft is null)
            {
                ReleaseEvidence();
                return false;
            }
            _draft = ConfigureEvidenceDraft(_draft);
            Populate(_draft);
            var preview = _session.PreviewLivingWorldTriggerUx2LwE(_draft);
            if (preview.Succeeded && !preview.CanApply)
            {
                _draft = _draft with { AllowCampSetup = !_draft.AllowCampSetup };
                Populate(_draft);
                preview = _session.PreviewLivingWorldTriggerUx2LwE(_draft);
            }
            RenderPreview(preview);
            _lastEvidenceSourceUntouched = SourceMatchesSnapshot(_evidenceSnapshot!);
            FitDialog();
            return preview.Succeeded && preview.CanApply && _lastEvidenceSourceUntouched;
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_E_EVIDENCE_FAILED {exception.Message}");
            ReleaseEvidence();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux2-lw-e-ui-transaction") || _draft is null || _evidenceTempRoot is null)
                return false;
            var draft = _draft with
            {
                AllowCampSetup = !_draft.AllowCampSetup,
                Difficulty = Math.Min(1_000, _draft.Difficulty + 1),
                Priority = Math.Min(1_000, _draft.Priority + 1),
            };
            Populate(draft);
            _draft = draft;
            var preview = _session.PreviewLivingWorldTriggerUx2LwE(draft);
            var apply = _session.ApplyApprovedLivingWorldTriggerUx2LwE();
            var undo = _session.Undo();
            var redo = _session.Redo();
            var saved = _session.Save();
            reopened = new StudioSessionController();
            var opened = saved.Succeeded && reopened.Open(_evidenceTempRoot).Succeeded;
            var persisted = opened && reopened.Project!.Module.WorldFeatures.Triggers.Any(item =>
                item.Id == draft.ProfileId && item.Rest?.AllowCampSetup == draft.AllowCampSetup &&
                item.Clue?.Difficulty == draft.Difficulty && item.Narrative?.Priority == draft.Priority);
            return preview.Succeeded && preview.CanApply && apply.Succeeded && undo.Succeeded &&
                   redo.Succeeded && persisted && SourceMatchesSnapshot(_evidenceSnapshot!);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_E_UI_TRANSACTION_FAILED {exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            Close();
        }
    }

    private void Reload(bool clearSelection)
    {
        _updating = true;
        try
        {
            _triggerChoices = _session.LivingWorldTriggerChoicesUx2LwE;
            _participantChoices = _session.LivingWorldParticipantChoicesUx2LwE();
            _questChoices = _session.LivingWorldQuestChoicesUx2LwE();
            _dialogueChoices = _session.LivingWorldDialogueChoicesUx2LwE();
            _trigger.Clear();
            _triggerIds.Clear();
            _trigger.AddItem("Choisir un déclencheur…");
            _triggerIds.Add(null);
            foreach (var choice in _triggerChoices)
            {
                _trigger.AddItem(choice.Label);
                _triggerIds.Add(choice.ProfileId);
            }
            _participants.Clear();
            foreach (var choice in _participantChoices)
            {
                _participants.AddItem($"{choice.Label} · {choice.Provenance}");
            }
            _quest.Clear();
            _questIds.Clear();
            _quest.AddItem("Aucune quête liée");
            _questIds.Add(null);
            foreach (var choice in _questChoices)
            {
                _quest.AddItem($"{choice.Label} · {choice.Provenance}");
                _questIds.Add(choice.Id);
            }
            _dialogue.Clear();
            _dialogueIds.Clear();
            _dialogue.AddItem("Choisir un dialogue…");
            _dialogueIds.Add(null);
            foreach (var choice in _dialogueChoices)
            {
                _dialogue.AddItem($"{choice.Label} · {choice.Provenance}");
                _dialogueIds.Add(choice.Id);
            }
            if (clearSelection)
            {
                _trigger.Select(0);
                _draft = null;
                ResetPreview("Aucune fiche sélectionnée.");
                _selection.Text = _triggerChoices.Count == 0
                    ? "Aucun profil de déclencheur n’est disponible dans cette zone. L’outil ne crée pas de volume implicitement."
                    : "Choisissez explicitement un déclencheur doté d’un volume de base.";
            }
        }
        finally
        {
            _updating = false;
        }
        UpdateActions();
    }

    private void SelectTrigger(int index)
    {
        if (_updating) return;
        _session.CancelLivingWorldTriggerPreviewUx2LwE();
        _canonicalPreviewReady = false;
        _apply.Disabled = true;
        var profileId = index >= 0 && index < _triggerIds.Count ? _triggerIds[index] : null;
        _draft = _session.CreateLivingWorldTriggerDraftUx2LwE(profileId);
        if (_draft is null)
        {
            var choice = index > 0 && index - 1 < _triggerChoices.Count ? _triggerChoices[index - 1] : null;
            _selection.Text = choice is { HasFoundation: false }
                ? "Ce déclencheur n’a pas encore de volume de base. Configurez-le d’abord avec l’outil Déclencheur."
                : "Choisissez explicitement un déclencheur.";
            ResetPreview(_selection.Text);
            UpdateActions();
            return;
        }
        Populate(_draft);
        _selection.Text = _triggerChoices.Single(item => item.ProfileId == _draft.ProfileId).Label;
        ResetPreview("Fiche chargée · prévisualisez avant toute application.");
        UpdateActions();
    }

    private void Populate(StudioUx2LwEDraft draft)
    {
        _updating = true;
        try
        {
            _draft = draft;
            _restEnabled.ButtonPressed = draft.RestEnabled;
            _restSafe.ButtonPressed = draft.RestSafe;
            _dangerQuery.Text = draft.DangerQuery;
            _wholeParty.ButtonPressed = draft.WholePartyRequired;
            _campSetup.ButtonPressed = draft.AllowCampSetup;
            _allowedMessage.Text = draft.AllowedMessageKey;
            _deniedMessage.Text = draft.DeniedMessageKey;
            _clueEnabled.ButtonPressed = draft.ClueEnabled;
            _skill.Text = draft.SkillId;
            _difficulty.Value = draft.Difficulty;
            _trace.Text = draft.TraceId;
            _direction.Value = draft.DirectionDegrees;
            _age.Value = draft.AgeHours;
            _falseTrace.Text = draft.FalseClueId ?? string.Empty;
            SelectId(_quest, _questIds, draft.QuestId);
            _narrativeEnabled.ButtonPressed = draft.NarrativeEnabled;
            _participants.DeselectAll();
            for (var index = 0; index < _participantChoices.Count; index++)
                if (draft.Participants.Contains(_participantChoices[index].Id))
                    _participants.Select(index, false);
            _priority.Value = draft.Priority;
            _once.ButtonPressed = draft.Once;
            _cooldown.Value = draft.CooldownSeconds;
            _multiplayerPolicy.Text = draft.MultiplayerPolicy;
            SelectId(_dialogue, _dialogueIds, draft.DialogueId);
        }
        finally
        {
            _updating = false;
        }
    }

    private StudioUx2LwEDraft ReadDraft()
    {
        var source = _draft ?? throw new InvalidOperationException("Aucune fiche n’est sélectionnée.");
        var participants = _participants.GetSelectedItems()
            .Where(index => index >= 0 && index < _participantChoices.Count)
            .Select(index => _participantChoices[index].Id).ToArray();
        return source with
        {
            RestEnabled = _restEnabled.ButtonPressed,
            RestSafe = _restSafe.ButtonPressed,
            DangerQuery = _dangerQuery.Text,
            WholePartyRequired = _wholeParty.ButtonPressed,
            AllowCampSetup = _campSetup.ButtonPressed,
            AllowedMessageKey = _allowedMessage.Text,
            DeniedMessageKey = _deniedMessage.Text,
            ClueEnabled = _clueEnabled.ButtonPressed,
            SkillId = _skill.Text,
            Difficulty = (int)_difficulty.Value,
            TraceId = _trace.Text,
            DirectionDegrees = _direction.Value,
            AgeHours = _age.Value,
            FalseClueId = string.IsNullOrWhiteSpace(_falseTrace.Text) ? null : _falseTrace.Text,
            QuestId = SelectedId(_quest, _questIds),
            NarrativeEnabled = _narrativeEnabled.ButtonPressed,
            Participants = participants,
            Priority = (int)_priority.Value,
            Once = _once.ButtonPressed,
            CooldownSeconds = _cooldown.Value,
            MultiplayerPolicy = _multiplayerPolicy.Text,
            DialogueId = SelectedId(_dialogue, _dialogueIds),
        };
    }

    private void Preview()
    {
        if (_draft is null) return;
        _draft = ReadDraft();
        RenderPreview(_session.PreviewLivingWorldTriggerUx2LwE(_draft));
    }

    private void WireDraftInvalidation()
    {
        _restEnabled.Toggled += _ => InvalidatePreview();
        _restSafe.Toggled += _ => InvalidatePreview();
        _dangerQuery.TextChanged += _ => InvalidatePreview();
        _wholeParty.Toggled += _ => InvalidatePreview();
        _campSetup.Toggled += _ => InvalidatePreview();
        _allowedMessage.TextChanged += _ => InvalidatePreview();
        _deniedMessage.TextChanged += _ => InvalidatePreview();
        _clueEnabled.Toggled += _ => InvalidatePreview();
        _skill.TextChanged += _ => InvalidatePreview();
        _difficulty.ValueChanged += _ => InvalidatePreview();
        _trace.TextChanged += _ => InvalidatePreview();
        _direction.ValueChanged += _ => InvalidatePreview();
        _age.ValueChanged += _ => InvalidatePreview();
        _falseTrace.TextChanged += _ => InvalidatePreview();
        _quest.ItemSelected += _ => InvalidatePreview();
        _narrativeEnabled.Toggled += _ => InvalidatePreview();
        _participants.MultiSelected += (_, _) => InvalidatePreview();
        _priority.ValueChanged += _ => InvalidatePreview();
        _once.Toggled += _ => InvalidatePreview();
        _cooldown.ValueChanged += _ => InvalidatePreview();
        _multiplayerPolicy.TextChanged += _ => InvalidatePreview();
        _dialogue.ItemSelected += _ => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null) return;
        _session.CancelLivingWorldTriggerPreviewUx2LwE();
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _apply.Disabled = true;
        Clear(_diff);
        _diff.AddChild(StudioStyle.Label(
            "Brouillon modifié · relancez l’aperçu sec pour afficher le diff.",
            10, StudioStyle.Warning, wrap: true));
        Clear(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label("Diagnostics à recalculer.", 10, StudioStyle.Muted));
        _restSummary.Text = "Repos à réévaluer";
        _clueSummary.Text = "Piste à réévaluer";
        _narrativeSummary.Text = "Interjection à réévaluer";
        SetStatus("Aperçu précédent effacé · aucun changement appliqué", false);
        UpdateActions();
    }

    private void RenderPreview(StudioUx2LwEPreview preview)
    {
        _lastPreview = preview;
        _canonicalPreviewReady = preview.Succeeded;
        _apply.Disabled = !(preview.Succeeded && preview.CanApply);
        Clear(_diff);
        foreach (var item in preview.Diff)
        {
            var color = item.Changed ? StudioStyle.Warning : StudioStyle.Muted;
            _diff.AddChild(StudioStyle.Label(
                $"{item.Section} · {(item.Changed ? "modifié" : "inchangé")}\n" +
                $"Avant : {item.Before}\nAprès : {item.After}", 10, color, wrap: true));
        }
        if (preview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun diff disponible.", 10, StudioStyle.Muted));
        Clear(_diagnostics);
        foreach (var diagnostic in preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label(
                $"{diagnostic.Message}\nCode {diagnostic.Code} · emplacement {HumanPath(diagnostic.Path)}",
                10, StudioStyle.Danger, wrap: true));
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success));
        _restSummary.Text = $"Repos · {preview.RestSummary}";
        _clueSummary.Text = $"Piste · {preview.ClueSummary}";
        _narrativeSummary.Text = $"Interjection · {preview.NarrativeSummary}";
        SetStatus(preview.Message, !preview.Succeeded);
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedLivingWorldTriggerUx2LwE();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _canonicalPreviewReady = false;
        _apply.Disabled = true;
        LivingWorldTriggerChanged?.Invoke();
        ReloadKeepingSelection();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            LivingWorldTriggerChanged?.Invoke();
            ReloadKeepingSelection();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            LivingWorldTriggerChanged?.Invoke();
            ReloadKeepingSelection();
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void ReloadKeepingSelection()
    {
        var id = _draft?.ProfileId;
        Reload(clearSelection: true);
        if (id is null) return;
        var index = _triggerIds.FindIndex(item => item == id);
        if (index <= 0) return;
        _trigger.Select(index);
        SelectTrigger(index);
    }

    private void ResetPreview(string message)
    {
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _apply.Disabled = true;
        Clear(_diff);
        _diff.AddChild(StudioStyle.Label("Prévisualisez la fiche pour afficher le diff.", 10, StudioStyle.Muted));
        Clear(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic affiché.", 10, StudioStyle.Muted));
        _restSummary.Text = "Repos non évalué";
        _clueSummary.Text = "Piste non évaluée";
        _narrativeSummary.Text = "Interjection non évaluée";
        SetStatus(message, false);
    }

    private void UpdateActions()
    {
        var hasDraft = _draft is not null;
        _preview.Disabled = !hasDraft;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private StudioUx2LwEDraft ConfigureEvidenceDraft(StudioUx2LwEDraft draft)
    {
        var participant = _participantChoices.FirstOrDefault()?.Id;
        var dialogue = _dialogueChoices.FirstOrDefault()?.Id;
        if (participant is null || dialogue is null) return draft;
        return draft with
        {
            RestEnabled = true,
            RestSafe = true,
            DangerQuery = "danger.nearby",
            WholePartyRequired = true,
            AllowCampSetup = true,
            AllowedMessageKey = "rest.allowed",
            DeniedMessageKey = "rest.denied",
            ClueEnabled = true,
            SkillId = "tracking",
            Difficulty = 12,
            TraceId = "trail.evidence",
            DirectionDegrees = 45,
            AgeHours = 2,
            FalseClueId = "trail.decoy",
            QuestId = _questChoices.FirstOrDefault()?.Id,
            NarrativeEnabled = true,
            Participants = [participant],
            Priority = 8,
            Once = true,
            CooldownSeconds = 60,
            MultiplayerPolicy = "party-vote",
            DialogueId = dialogue,
        };
    }

    private StudioUx2LwEEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("Historique source absent.");
        return new(_sourceSession.Project ?? throw new InvalidOperationException("Projet source absent."),
            history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureLivingWorldTriggerApprovalUx2LwE());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwEEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureLivingWorldTriggerApprovalUx2LwE() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        var snapshot = _evidenceSnapshot;
        _evidenceSession?.CancelLivingWorldTriggerPreviewUx2LwE();
        _evidenceSession?.Close();
        _evidenceSession = null;
        var root = _evidenceTempRoot;
        _evidenceTempRoot = null;
        _session = _sourceSession;
        _lastSourceRestored = true;
        _lastEvidenceSourceUntouched = snapshot is not null && SourceMatchesSnapshot(snapshot);
        _evidenceSnapshot = null;
        var released = true;
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                released = false;
            }
            catch (UnauthorizedAccessException)
            {
                released = false;
            }
        }
        _lastEvidenceResourcesReleased = released && (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root));
    }

    private void FitDialog()
    {
        if (!Visible) return;
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 48, 820, 1320);
        var height = Mathf.Clamp(viewport.Y - 36, 700, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new Vector2(width, height);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private static CheckBox Check(string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        return check;
    }

    private static LineEdit Line(string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(line);
        return line;
    }

    private static SpinBox Spin(double minimum, double maximum, double step)
    {
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }

    private static GridContainer Grid()
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    private static void AddRow(GridContainer grid, string label, Control control)
    {
        var caption = StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true);
        caption.CustomMinimumSize = new Vector2(180, 0);
        caption.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        grid.AddChild(caption);
        grid.AddChild(control);
    }

    private static PanelContainer Card(string name, string title, string help, Control body)
    {
        var card = StudioStyle.Panel(name, StudioStyle.Surface, 8);
        var margin = StudioStyle.Margin(12, 10, 12, 10);
        card.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 5);
        margin.AddChild(root);
        root.AddChild(StudioStyle.Label(title, 13));
        root.AddChild(StudioStyle.Label(help, 10, StudioStyle.Muted, wrap: true));
        root.AddChild(body);
        return card;
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren()) child.Free();
    }

    private static void SelectId(OptionButton option, IReadOnlyList<Guid?> ids, Guid? id)
    {
        var index = ids.Select((value, position) => (value, position))
            .FirstOrDefault(pair => pair.value == id).position;
        option.Select(index);
    }

    private static Guid? SelectedId(OptionButton option, IReadOnlyList<Guid?> ids) =>
        option.Selected >= 0 && option.Selected < ids.Count ? ids[option.Selected] : null;

    private static string HumanPath(string path)
    {
        if (path.EndsWith("/rest", StringComparison.Ordinal)) return "fiche Repos sûr";
        if (path.EndsWith("/clue", StringComparison.Ordinal)) return "fiche Piste";
        if (path.Contains("/narrative/participants", StringComparison.Ordinal)) return "participants de l’interjection";
        if (path.EndsWith("/narrative/dialogueId", StringComparison.Ordinal)) return "dialogue de l’interjection";
        if (path.EndsWith("/narrative", StringComparison.Ordinal)) return "fiche Interjection";
        return "déclencheur sélectionné";
    }

    private static IEnumerable<Label> DescendantLabels(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) yield return label;
            foreach (var nested in DescendantLabels(child)) yield return nested;
        }
    }

    private static bool ContainsTechnicalIdentifier(string text) =>
        text.Split([' ', '·', '/', ':', ',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));
}
