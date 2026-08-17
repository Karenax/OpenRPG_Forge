using System.Globalization;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioDoorAccessEventsOverlay : Control
{
    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _door;
    private readonly Button _lockTab;
    private readonly Button _rejectionTrapTab;
    private readonly Button _eventsTab;
    private readonly VBoxContainer _lockPanel;
    private readonly VBoxContainer _rejectionTrapPanel;
    private readonly VBoxContainer _eventsPanel;
    private readonly CheckBox _accessRequired;
    private readonly OptionButton _lockState;
    private readonly LineEdit _credentials;
    private readonly LineEdit _skill;
    private readonly LineEdit _difficulty;
    private readonly CheckBox _hackable;
    private readonly CheckBox _relockable;
    private readonly CheckBox _canJam;
    private readonly CheckBox _consumeCredential;
    private readonly LineEdit _permissions;
    private readonly LineEdit _rejectionApiVersion;
    private readonly LineEdit _rejectionFrench;
    private readonly LineEdit _rejectionEnglish;
    private readonly LineEdit _rejectionSound;
    private readonly LineEdit _rejectionAnimation;
    private readonly LineEdit _rejectionScript;
    private readonly LineEdit _trapTrigger;
    private readonly LineEdit _trapPayload;
    private readonly LineEdit _trapDetection;
    private readonly LineEdit _trapDisarm;
    private readonly LineEdit _trapReset;
    private readonly LineEdit _trapOwner;
    private readonly LineEdit _trapVisualEffect;
    private readonly LineEdit _trapFriendlyPolicy;
    private readonly CheckBox _trapArmed;
    private readonly CheckBox _trapOneShot;
    private readonly LineEdit _trapSides;
    private readonly LineEdit _trapEvents;
    private readonly LineEdit _stateApiVersion;
    private readonly CheckBox _lockEventsEnabled;
    private readonly CheckBox _lockEventsRequired;
    private readonly LineEdit _lockEventsApiVersion;
    private readonly Label _previewSummary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;

    private IReadOnlyList<StudioDoorAccessEventsChoice> _choices = [];
    private StudioDoorAccessEventsDraft? _draft;
    private StudioDoorAccessEventsDraft? _approvedDraft;
    private bool _updating;
    private bool _operationApplied;
    private bool _editingEnabled;
    private bool _selectionRequiredAtOpen;
    private int _section;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? DoorChanged;

    internal bool IsOpen => Visible;
    internal int SectionCount => 3;
    internal bool PreviewReady => Visible && _approvedDraft is not null && !_apply.Disabled;
    internal bool UiTransactionSourceUntouched { get; private set; }
    internal bool ExplicitSelectionProven =>
        _selectionRequiredAtOpen && _door.Selected > 0 && _draft is not null;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(
                ' ',
                _lockTab.Text,
                _rejectionTrapTab.Text,
                _eventsTab.Text,
                _previewSummary.Text,
                _status.Text);
            return SectionCount == 3 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("Portal", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("Navigation", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioDoorAccessEventsOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioDoorAccessEventsOverlay";
        Visible = false;
        ZIndex = 457;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .78f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("DoorAccessEventsDialog", StudioStyle.Chrome, 12, 2);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("PORTE · ACCÈS ET ÉVÉNEMENTS", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Verrou, refus, piège et transitions terminales", 20));
        heading.AddChild(StudioStyle.Label(
            "Choisissez une porte · simulez sans écriture · appliquez par commande annulable",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le changement en attente · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var sourceRow = new HBoxContainer();
        sourceRow.AddThemeConstantOverride("separation", 10);
        root.AddChild(sourceRow);
        sourceRow.AddChild(StudioStyle.Label("Porte à configurer", 10, StudioStyle.Muted));
        _door = Option();
        _door.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _door.TooltipText = "Sélection explicite de la porte à modifier.";
        _door.ItemSelected += _ => LoadSelectedDoor();
        sourceRow.AddChild(_door);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _lockTab = TabButton("1 · Verrou & clé", "Configurer accès, compétence et reverrouillage.");
        _lockTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_lockTab);
        _rejectionTrapTab = TabButton("2 · Refus & piège", "Configurer le retour d’échec et le piège de porte.");
        _rejectionTrapTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_rejectionTrapTab);
        _eventsTab = TabButton("3 · Événements", "Configurer les événements terminaux d’état et de verrou.");
        _eventsTab.Pressed += () => ShowSection(2);
        tabs.AddChild(_eventsTab);

        var body = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _lockPanel = PanelColumn();
        panels.AddChild(_lockPanel);
        _lockPanel.AddChild(StudioStyle.Label("Contrôler l’accès", 17));
        _lockPanel.AddChild(StudioStyle.Label(
            "Les clés, badges, permissions et tests de compétence partagent le même verrou canonique.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var lockGrid = Grid();
        _lockPanel.AddChild(lockGrid);
        _accessRequired = AddField(lockGrid, "Présence au runtime", Check("Exiger cette configuration"));
        _lockState = AddField(
            lockGrid,
            "État du verrou",
            Option("Déverrouillé", "Verrouillé", "Bloqué", "Brisé"));
        _credentials = AddField(lockGrid, "Clés ou credentials", Text("Ex. key:crypt, badge:watch"));
        _skill = AddField(lockGrid, "Compétence", Text("Ex. mechanics"));
        _difficulty = AddField(lockGrid, "Difficulté", NumericText("Ex. 12"));
        _hackable = AddField(lockGrid, "Crochetage", Check("Autoriser le piratage ou crochetage"));
        _relockable = AddField(lockGrid, "Reverrouillage", Check("Autoriser après ouverture"));
        _canJam = AddField(lockGrid, "Blocage", Check("Autoriser un verrou bloqué"));
        _consumeCredential = AddField(lockGrid, "Consommation", Check("Retirer la clé utilisée"));
        _permissions = AddField(lockGrid, "Permissions", Text("Ex. door.unlock, door.override"));

        _rejectionTrapPanel = PanelColumn();
        panels.AddChild(_rejectionTrapPanel);
        _rejectionTrapPanel.AddChild(StudioStyle.Label("Expliquer le refus", 17));
        var rejectionGrid = Grid();
        _rejectionTrapPanel.AddChild(rejectionGrid);
        _rejectionApiVersion = AddField(rejectionGrid, "Version de script du refus", Text("Ex. 1.0.0"));
        _rejectionFrench = AddField(rejectionGrid, "Message français", Text("Ex. La porte est verrouillée."));
        _rejectionEnglish = AddField(rejectionGrid, "Message anglais", Text("Ex. The door is locked."));
        _rejectionSound = AddField(rejectionGrid, "Son", Text("Ex. audio:door-locked"));
        _rejectionAnimation = AddField(rejectionGrid, "Animation", Text("Ex. animation:door-rattle"));
        _rejectionScript = AddField(rejectionGrid, "Script", Text("Ex. scripts/door_open_rejected"));
        _rejectionTrapPanel.AddChild(StudioStyle.SectionLabel("PIÈGE DE PORTE"));
        _rejectionTrapPanel.AddChild(StudioStyle.Label(
            "Le piège filtre les côtés, les événements et les alliés avant de déclencher son effet.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var trapGrid = Grid();
        _rejectionTrapPanel.AddChild(trapGrid);
        _trapTrigger = AddField(trapGrid, "Déclencheur", Text("Ex. door:interact"));
        _trapPayload = AddField(trapGrid, "Effet", Text("Ex. damage:fire-trap"));
        _trapDetection = AddField(trapGrid, "Difficulté de détection", NumericText("Ex. 10"));
        _trapDisarm = AddField(trapGrid, "Difficulté de désamorçage", NumericText("Ex. 12"));
        _trapReset = AddField(trapGrid, "Réarmement en secondes", NumericText("Ex. 30"));
        _trapOwner = AddField(trapGrid, "Propriétaire", Text("Ex. ash-watch"));
        _trapVisualEffect = AddField(trapGrid, "Effet visuel", Text("Ex. vfx:embers"));
        _trapFriendlyPolicy = AddField(trapGrid, "Alliés ignorés", Text("Ex. ash-watch"));
        _trapArmed = AddField(trapGrid, "État du piège", Check("Piège armé"));
        _trapOneShot = AddField(trapGrid, "Déclenchement", Check("Usage unique"));
        _trapSides = AddField(trapGrid, "Côtés", Text("Ex. front, back"));
        _trapEvents = AddField(trapGrid, "Événements", Text("Ex. interact, open-attempt"));

        _eventsPanel = PanelColumn();
        panels.AddChild(_eventsPanel);
        _eventsPanel.AddChild(StudioStyle.Label("Réagir après une transition terminée", 17));
        _eventsPanel.AddChild(StudioStyle.Label(
            "Ouverture et fermeture sont toujours liées à leur fin d’animation ; verrouillage et déverrouillage peuvent être rendus obligatoires.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var eventsGrid = Grid();
        _eventsPanel.AddChild(eventsGrid);
        AddField(eventsGrid, "Ouverture", StaticValue("Porte ouverte · après transition terminale"));
        AddField(eventsGrid, "Fermeture", StaticValue("Porte fermée · après transition terminale"));
        _stateApiVersion = AddField(eventsGrid, "Version de scripts d’état", Text("Ex. 1.0.0"));
        _lockEventsEnabled = AddField(
            eventsGrid,
            "Verrouillage et déverrouillage",
            Check("Émettre les deux événements terminaux"));
        _lockEventsRequired = AddField(
            eventsGrid,
            "Présence au runtime",
            Check("Exiger les événements de verrou"));
        _lockEventsApiVersion = AddField(eventsGrid, "Version de scripts de verrou", Text("Ex. 1.0.0"));
        AddField(eventsGrid, "Verrouillé", StaticValue("Après transition vers verrouillé"));
        AddField(eventsGrid, "Déverrouillé", StaticValue("Après transition vers déverrouillé"));

        var previewCard = StudioStyle.Panel("DoorAccessEventsPreview", StudioStyle.SurfaceRaised, 8);
        previewCard.CustomMinimumSize = new Vector2(0, 150);
        root.AddChild(previewCard);
        var previewMargin = StudioStyle.Margin(14, 11, 14, 11);
        previewCard.AddChild(previewMargin);
        var previewColumn = new VBoxContainer();
        previewColumn.AddThemeConstantOverride("separation", 7);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.SectionLabel("APERÇU SANS ÉCRITURE"));
        _previewSummary = StudioStyle.Label(
            "Choisissez une porte puis simulez son verrou, son refus, son piège et ses événements.",
            11,
            StudioStyle.Muted,
            wrap: true);
        previewColumn.AddChild(_previewSummary);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _status = StudioStyle.Label("Prêt · aucun changement appliqué", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
        ShowSection(0);
    }

    internal void Open()
    {
        _session.CancelDoorAccessEventsPreviewUx1AG();
        _choices = _session.DoorAccessEventsChoicesUx1AG;
        _updating = true;
        _door.Clear();
        _door.AddItem("Choisir une porte…");
        foreach (var choice in _choices)
        {
            _door.AddItem($"{choice.Label} · {(choice.Configured ? "configurée" : "à configurer")}");
        }
        _door.Selected = 0;
        _updating = false;
        _draft = null;
        _approvedDraft = null;
        _operationApplied = false;
        _selectionRequiredAtOpen = true;
        Visible = true;
        SetEditingEnabled(false);
        _door.Disabled = _choices.Count == 0;
        _preview.Disabled = true;
        _apply.Disabled = true;
        _undo.Disabled = true;
        _redo.Disabled = true;
        ShowSection(0);
        _previewSummary.Text = "Choisissez explicitement une porte avant de préparer un aperçu.";
        _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetStatus(
            _choices.Count == 0
                ? "Ajoutez d’abord un blueprint de porte au module."
                : "Choisissez une porte · aucun brouillon n’est ouvert automatiquement.",
            _choices.Count == 0);
    }

    internal void Close()
    {
        _session.CancelDoorAccessEventsPreviewUx1AG();
        _draft = null;
        _approvedDraft = null;
        _selectionRequiredAtOpen = false;
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        if (!Visible || !SelectDoorExplicitlyForEvidence() || _draft is null)
        {
            return false;
        }
        SetEvidenceValues("preuve-ag");
        ShowSection(mode.EndsWith("refusal-trap", StringComparison.OrdinalIgnoreCase)
            ? 1
            : mode.EndsWith("events", StringComparison.OrdinalIgnoreCase)
                ? 2
                : 0);
        Preview();
        return PreviewReady && ShowsHumanSurface && ExplicitSelectionProven;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalHistory = _session.History;
        UiTransactionSourceUntouched = false;
        if (originalProject is null || originalHistory is null)
        {
            return false;
        }

        var originalDirty = _session.IsDirty;
        var originalRevision = originalHistory.State.Revision;
        var originalJournal = originalHistory.Journal.ToArray();
        var originalCanUndo = originalHistory.CanUndo;
        var originalCanRedo = originalHistory.CanRedo;
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ag-ui-" + Guid.NewGuid().ToString("N"));
        var isolatedSession = new StudioSessionController();
        StudioDoorAccessEventsOverlay? isolatedOverlay = null;
        try
        {
            var created = ProjectSession.Create(tempRoot, originalProject);
            if (!created.Succeeded ||
                !isolatedSession.Open(tempRoot, _session.ActiveAreaId?.ToString()).Succeeded)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_STUDIO_UX1AG_UI_TRANSACTION_FAILED stage=isolated-open created={created.Succeeded.ToString().ToLowerInvariant()}");
                return false;
            }

            isolatedOverlay = new StudioDoorAccessEventsOverlay(isolatedSession);
            AddChild(isolatedOverlay);
            var isolatedEvidence = isolatedOverlay.RunBoundUiTransactionEvidence();
            var currentHistory = _session.History;
            UiTransactionSourceUntouched = ReferenceEquals(originalProject, _session.Project) &&
                                           _session.IsDirty == originalDirty &&
                                           currentHistory is not null &&
                                           ReferenceEquals(originalHistory, currentHistory) &&
                                           currentHistory.State.Revision == originalRevision &&
                                           currentHistory.CanUndo == originalCanUndo &&
                                           currentHistory.CanRedo == originalCanRedo &&
                                           currentHistory.Journal.SequenceEqual(originalJournal);
            var valid = isolatedEvidence && UiTransactionSourceUntouched;
            if (!valid)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_STUDIO_UX1AG_UI_TRANSACTION_FAILED stage=isolated-transaction " +
                    $"isolated={isolatedEvidence.ToString().ToLowerInvariant()} " +
                    $"source_untouched={UiTransactionSourceUntouched.ToString().ToLowerInvariant()}");
            }
            return valid;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AG_UI_TRANSACTION_FAILED stage=exception " +
                $"type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            if (isolatedOverlay is not null && GodotObject.IsInstanceValid(isolatedOverlay))
            {
                isolatedOverlay.Close();
                isolatedOverlay.GetParent()?.RemoveChild(isolatedOverlay);
                isolatedOverlay.Free();
            }
            isolatedSession.Close();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private bool RunBoundUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        Open();
        var selectionRequired = Visible && originalProject is not null &&
                                _door.Selected == 0 && _draft is null &&
                                _preview.Disabled && _apply.Disabled &&
                                ReferenceEquals(originalProject, _session.Project);
        if (!selectionRequired || !SelectDoorExplicitlyForEvidence() || _draft is not { } selectedDraft ||
            originalProject is null)
        {
            Close();
            return false;
        }

        var blueprintId = selectedDraft.BlueprintId;
        var explicitSelection = ExplicitSelectionProven;
        SetEvidenceValues("transaction-ag");
        Preview();
        var previewed = PreviewReady && ShowsHumanSurface;
        Apply();
        var appliedProfile = _session.Project?.Module.WorldFeatures.Doors.SingleOrDefault(profile =>
            profile.BlueprintId == blueprintId);
        var applied = appliedProfile?.AccessEvents is
        {
            Lock:
            {
                State: DoorAccessLockState.Locked,
                Difficulty: 16,
                ConsumeCredential: true,
            },
            Rejection:
            {
                SoundAssetId: "audio:transaction-ag-locked",
                ScriptId: "scripts/transaction-ag_rejected",
            },
            Trap:
            {
                PayloadId: "damage:transaction-ag-trap",
                Armed: true,
            },
            StateEvents:
            {
                OpenedBinding: "DoorOpened",
                ClosedBinding: "DoorClosed",
            },
            LockStateEvents:
            {
                LockedBinding: "DoorLocked",
                UnlockedBinding: "DoorUnlocked",
            },
        } && !_undo.Disabled;
        Undo();
        var undone = ReferenceEquals(originalProject, _session.Project) && _session.IsDirty == originalDirty &&
                     !_redo.Disabled;
        Redo();
        var redone = _session.Project?.Module.WorldFeatures.Doors.SingleOrDefault(profile =>
            profile.BlueprintId == blueprintId)?.AccessEvents?.Trap.PayloadId == "damage:transaction-ag-trap";
        Undo();
        var restored = ReferenceEquals(originalProject, _session.Project) && _session.IsDirty == originalDirty;
        Close();
        return selectionRequired && explicitSelection && previewed && applied && undone && redone && restored;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            return;
        }
        Close();
        GetViewport().SetInputAsHandled();
    }

    private void LoadSelectedDoor()
    {
        if (_updating || _choices.Count == 0)
        {
            return;
        }
        _session.CancelDoorAccessEventsPreviewUx1AG();
        _approvedDraft = null;
        if (_door.Selected <= 0)
        {
            _draft = null;
            SetEditingEnabled(false);
            _door.Disabled = false;
            _preview.Disabled = true;
            _apply.Disabled = true;
            SetStatus("Choisissez une porte · aucun brouillon n’est ouvert automatiquement.", false);
            return;
        }

        var index = Math.Clamp(_door.Selected - 1, 0, _choices.Count - 1);
        if (!_session.TryCreateDoorAccessEventsDraftUx1AG(
                _choices[index].BlueprintId,
                out _draft,
                out var message) || _draft is null)
        {
            SetStatus(message, true);
            SetEditingEnabled(false);
            _door.Disabled = false;
            _preview.Disabled = true;
            _apply.Disabled = true;
            return;
        }
        LoadDraft();
        SetEditingEnabled(true);
        _preview.Disabled = false;
        SetStatus("Porte sélectionnée · prévisualisation obligatoire avant application.", false);
    }

    private bool SelectDoorExplicitlyForEvidence()
    {
        if (_choices.Count == 0 || _door.Selected != 0 || _draft is not null)
        {
            return false;
        }
        var configuredIndex = _choices.ToList().FindIndex(choice => choice.Configured);
        _door.Selected = (configuredIndex >= 0 ? configuredIndex : 0) + 1;
        LoadSelectedDoor();
        return ExplicitSelectionProven;
    }

    private void LoadDraft()
    {
        var draft = _draft!;
        _updating = true;
        _accessRequired.ButtonPressed = draft.AccessRequiredAtRuntime;
        _lockState.Selected = (int)draft.LockState;
        _credentials.Text = JoinList(draft.CredentialIds);
        _skill.Text = draft.SkillId;
        _difficulty.Text = FormatNumber(draft.Difficulty);
        _hackable.ButtonPressed = draft.Hackable;
        _relockable.ButtonPressed = draft.Relockable;
        _canJam.ButtonPressed = draft.CanJam;
        _consumeCredential.ButtonPressed = draft.ConsumeCredential;
        _permissions.Text = JoinList(draft.Permissions);
        _rejectionApiVersion.Text = draft.RejectionForgeScriptApiVersion;
        _rejectionFrench.Text = draft.RejectionFrenchText;
        _rejectionEnglish.Text = draft.RejectionEnglishText;
        _rejectionSound.Text = draft.RejectionSoundAssetId;
        _rejectionAnimation.Text = draft.RejectionAnimationId;
        _rejectionScript.Text = draft.RejectionScriptId;
        _trapTrigger.Text = draft.TrapTriggerId;
        _trapPayload.Text = draft.TrapPayloadId;
        _trapDetection.Text = FormatNumber(draft.TrapDetectionDifficulty);
        _trapDisarm.Text = FormatNumber(draft.TrapDisarmDifficulty);
        _trapReset.Text = FormatNumber(draft.TrapResetDelaySeconds);
        _trapOwner.Text = draft.TrapOwnerId;
        _trapVisualEffect.Text = draft.TrapVisualEffectId;
        _trapFriendlyPolicy.Text = draft.TrapFriendlyPolicy;
        _trapArmed.ButtonPressed = draft.TrapArmed;
        _trapOneShot.ButtonPressed = draft.TrapOneShot;
        _trapSides.Text = JoinList(draft.TrapSides);
        _trapEvents.Text = JoinList(draft.TrapEvents);
        _stateApiVersion.Text = draft.StateForgeScriptApiVersion;
        _lockEventsEnabled.ButtonPressed = draft.LockStateEventsEnabled;
        _lockEventsRequired.ButtonPressed = draft.LockStateEventsRequiredAtRuntime;
        _lockEventsApiVersion.Text = draft.LockStateForgeScriptApiVersion;
        _updating = false;
        _operationApplied = false;
        _undo.Disabled = true;
        _redo.Disabled = true;
        InvalidatePreview();
    }

    private StudioDoorAccessEventsDraft ReadDraft() => _draft! with
    {
        AccessRequiredAtRuntime = _accessRequired.ButtonPressed,
        LockState = (DoorAccessLockState)Math.Clamp(
            _lockState.Selected,
            0,
            Enum.GetValues<DoorAccessLockState>().Length - 1),
        CredentialIds = SplitList(_credentials.Text),
        SkillId = _skill.Text,
        Difficulty = ParseNumber(_difficulty.Text),
        Hackable = _hackable.ButtonPressed,
        Relockable = _relockable.ButtonPressed,
        CanJam = _canJam.ButtonPressed,
        ConsumeCredential = _consumeCredential.ButtonPressed,
        Permissions = SplitList(_permissions.Text),
        RejectionForgeScriptApiVersion = _rejectionApiVersion.Text,
        RejectionFrenchText = _rejectionFrench.Text,
        RejectionEnglishText = _rejectionEnglish.Text,
        RejectionSoundAssetId = _rejectionSound.Text,
        RejectionAnimationId = _rejectionAnimation.Text,
        RejectionScriptId = _rejectionScript.Text,
        TrapTriggerId = _trapTrigger.Text,
        TrapPayloadId = _trapPayload.Text,
        TrapDetectionDifficulty = ParseNumber(_trapDetection.Text),
        TrapDisarmDifficulty = ParseNumber(_trapDisarm.Text),
        TrapResetDelaySeconds = ParseNumber(_trapReset.Text),
        TrapOwnerId = _trapOwner.Text,
        TrapVisualEffectId = _trapVisualEffect.Text,
        TrapFriendlyPolicy = _trapFriendlyPolicy.Text,
        TrapArmed = _trapArmed.ButtonPressed,
        TrapOneShot = _trapOneShot.ButtonPressed,
        TrapSides = SplitList(_trapSides.Text),
        TrapEvents = SplitList(_trapEvents.Text),
        StateForgeScriptApiVersion = _stateApiVersion.Text,
        LockStateEventsEnabled = _lockEventsEnabled.ButtonPressed,
        LockStateEventsRequiredAtRuntime = _lockEventsRequired.ButtonPressed,
        LockStateForgeScriptApiVersion = _lockEventsApiVersion.Text,
    };

    private void Preview()
    {
        if (_draft is null)
        {
            SetStatus("Sélectionnez une porte avant de lancer l’aperçu.", true);
            return;
        }
        var candidate = ReadDraft();
        var result = _session.PreviewDoorAccessEventsUx1AG(candidate);
        _previewSummary.Text = result.Succeeded
            ? result.Summary
            : "Aucun aperçu validé · corrigez les champs signalés puis recommencez.";
        _previewSummary.AddThemeColorOverride(
            "font_color",
            result.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _approvedDraft = result.CanApply ? candidate : null;
        _apply.Disabled = _approvedDraft is null;
        SetStatus(result.Message, !result.Succeeded);
    }

    private void Apply()
    {
        if (_approvedDraft is null)
        {
            SetStatus("Relancez l’aperçu avant d’appliquer.", true);
            return;
        }
        var result = _session.ApplyApprovedDoorAccessEventsUx1AG();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _approvedDraft = null;
            _apply.Disabled = true;
            return;
        }
        _operationApplied = true;
        SetEditingEnabled(false);
        _preview.Disabled = true;
        _apply.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        DoorChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(
            result.Succeeded ? "Modification annulée · la porte précédente est restaurée." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = true;
            _redo.Disabled = false;
            DoorChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(
            result.Succeeded ? "Modification rétablie · enregistrez pour écrire le module." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            DoorChanged?.Invoke();
        }
    }

    private void SetEvidenceValues(string stem)
    {
        _updating = true;
        _accessRequired.ButtonPressed = true;
        _lockState.Selected = (int)DoorAccessLockState.Locked;
        _credentials.Text = $"key:{stem}, badge:{stem}";
        _skill.Text = "mechanics";
        _difficulty.Text = "16";
        _hackable.ButtonPressed = true;
        _relockable.ButtonPressed = true;
        _canJam.ButtonPressed = true;
        _consumeCredential.ButtonPressed = true;
        _permissions.Text = $"door.unlock.{stem}";
        _rejectionApiVersion.Text = "1.1.0";
        _rejectionFrench.Text = $"La serrure {stem} résiste.";
        _rejectionEnglish.Text = $"The {stem} lock resists.";
        _rejectionSound.Text = $"audio:{stem}-locked";
        _rejectionAnimation.Text = $"animation:{stem}-rattle";
        _rejectionScript.Text = $"scripts/{stem}_rejected";
        _trapTrigger.Text = $"door:{stem}-interact";
        _trapPayload.Text = $"damage:{stem}-trap";
        _trapDetection.Text = "14";
        _trapDisarm.Text = "18";
        _trapReset.Text = "40";
        _trapOwner.Text = $"owner:{stem}";
        _trapVisualEffect.Text = $"vfx:{stem}-trap";
        _trapFriendlyPolicy.Text = $"allies:{stem}";
        _trapArmed.ButtonPressed = true;
        _trapOneShot.ButtonPressed = true;
        _trapSides.Text = "front, back";
        _trapEvents.Text = "interact, open-attempt";
        _stateApiVersion.Text = "1.1.0";
        _lockEventsEnabled.ButtonPressed = true;
        _lockEventsRequired.ButtonPressed = true;
        _lockEventsApiVersion.Text = "1.1.0";
        _updating = false;
        UpdateFieldStates();
        InvalidatePreview();
    }

    private void ConnectInvalidation()
    {
        foreach (var check in new[]
                 {
                     _accessRequired,
                     _hackable,
                     _relockable,
                     _canJam,
                     _consumeCredential,
                     _trapArmed,
                     _trapOneShot,
                     _lockEventsEnabled,
                     _lockEventsRequired,
                 })
        {
            check.Toggled += _ =>
            {
                if (_updating) return;
                UpdateFieldStates();
                InvalidatePreview();
            };
        }
        foreach (var edit in AllEdits())
        {
            edit.TextChanged += _ => InvalidatePreview();
        }
        _lockState.ItemSelected += _ => InvalidatePreview();
    }

    private IEnumerable<LineEdit> AllEdits()
    {
        yield return _credentials;
        yield return _skill;
        yield return _difficulty;
        yield return _permissions;
        yield return _rejectionApiVersion;
        yield return _rejectionFrench;
        yield return _rejectionEnglish;
        yield return _rejectionSound;
        yield return _rejectionAnimation;
        yield return _rejectionScript;
        yield return _trapTrigger;
        yield return _trapPayload;
        yield return _trapDetection;
        yield return _trapDisarm;
        yield return _trapReset;
        yield return _trapOwner;
        yield return _trapVisualEffect;
        yield return _trapFriendlyPolicy;
        yield return _trapSides;
        yield return _trapEvents;
        yield return _stateApiVersion;
        yield return _lockEventsApiVersion;
    }

    private void InvalidatePreview()
    {
        if (_updating || _operationApplied || _draft is null)
        {
            return;
        }
        _approvedDraft = null;
        _session.CancelDoorAccessEventsPreviewUx1AG();
        _apply.Disabled = true;
        _preview.Disabled = false;
        _previewSummary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        _door.Disabled = !enabled;
        _lockTab.Disabled = !enabled;
        _rejectionTrapTab.Disabled = !enabled;
        _eventsTab.Disabled = !enabled;
        UpdateFieldStates();
    }

    private void UpdateFieldStates()
    {
        foreach (var check in new[]
                 {
                     _accessRequired,
                     _hackable,
                     _relockable,
                     _canJam,
                     _consumeCredential,
                     _trapArmed,
                     _trapOneShot,
                     _lockEventsEnabled,
                 })
        {
            check.Disabled = !_editingEnabled;
        }
        _lockState.Disabled = !_editingEnabled;
        foreach (var edit in AllEdits())
        {
            edit.Editable = _editingEnabled;
        }
        var lockEvents = _editingEnabled && _lockEventsEnabled.ButtonPressed;
        _lockEventsRequired.Disabled = !_editingEnabled;
        _lockEventsApiVersion.Editable = lockEvents;
    }

    private void ShowSection(int section)
    {
        _section = Math.Clamp(section, 0, 2);
        _lockPanel.Visible = _section == 0;
        _rejectionTrapPanel.Visible = _section == 1;
        _eventsPanel.Visible = _section == 2;
        ApplyTabStyle(_lockTab, _section == 0);
        ApplyTabStyle(_rejectionTrapTab, _section == 1);
        ApplyTabStyle(_eventsTab, _section == 2);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.72f : Size.X >= 2300 ? 1.24f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 940, 1200);
        var height = Math.Clamp((Size.Y - 72) / scale, 720, 900);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static IReadOnlyList<string> SplitList(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToArray();

    private static string JoinList(IEnumerable<string> values) => string.Join(", ", values);

    private static string FormatNumber(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static double ParseNumber(string value)
    {
        var text = value.Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var current))
        {
            return current;
        }
        if (double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture,
                out var invariant))
        {
            return invariant;
        }
        return double.NaN;
    }

    private static VBoxContainer PanelColumn()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 9);
        return panel;
    }

    private static Button TabButton(string text, string tooltip)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.TooltipText = tooltip;
        return button;
    }

    private static void ApplyTabStyle(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            StudioStyle.Box(
                selected ? StudioStyle.Selection : StudioStyle.SurfaceRaised,
                selected ? StudioStyle.Success : StudioStyle.Border,
                6,
                selected ? 2 : 1));
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(260, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.AddChild(title);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static CheckBox Check(string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        return check;
    }

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton();
        foreach (var label in labels) option.AddItem(label);
        StudioStyle.ApplyOptionButton(option);
        return option;
    }

    private static LineEdit Text(string placeholder)
    {
        var edit = new LineEdit
        {
            PlaceholderText = placeholder,
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private static LineEdit NumericText(string placeholder)
    {
        var edit = Text(placeholder);
        edit.TooltipText = "Saisissez un nombre positif ou nul.";
        return edit;
    }

    private static Label StaticValue(string text) =>
        StudioStyle.Label(text, 11, StudioStyle.Success, wrap: true);
}
