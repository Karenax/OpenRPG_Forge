using System.Text.RegularExpressions;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioLiveDirectorAuthorityWorkbenchUx9H : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9H? _sourceSnapshot;
    private StudioUx9HAuthorityDraft? _draft;
    private StudioUx9HAuthorityPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private OptionButton _focusChoice = null!;
    private readonly List<Control> _focusPages = [];
    private SpinBox _maximumProtectionMinutes = null!;
    private SpinBox _maximumCurrencyAdjustment = null!;
    private SpinBox _maximumWallet = null!;
    private SpinBox _maximumExperienceAdjustment = null!;
    private CheckBox _requiredAtRuntime = null!;
    private OptionButton _impersonationMode = null!;
    private OptionButton _disclosure = null!;
    private SpinBox _protectionMinutes = null!;
    private OptionButton _protectionScope = null!;
    private LineEdit _protectionIndicator = null!;
    private SpinBox _currencyAmount = null!;
    private LineEdit _currencyReason = null!;
    private SpinBox _experienceAmount = null!;
    private LineEdit _experienceReason = null!;
    private OptionButton _doorState = null!;
    private LineEdit _doorPortal = null!;
    private CheckBox _approval = null!;
    private CheckBox _probeConfirmation = null!;
    private Label _previewSummary = null!;
    private Label _auditSummary = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Button _probeButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true, CanApply: true, CanonicalDryRun: true,
        ImpersonationRuntime: true, ProtectionRuntime: true, CurrencyRuntime: true,
        ExperienceRuntime: true, DoorRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9H();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9H(this));
            return Visible && text.Contains("ORF-DM-013", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-014", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-016", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-017", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-019", StringComparison.Ordinal) &&
                   text.Contains("divulgation", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("indicateur visible", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("solde non négatif", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("projection de niveau", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("collision et portail", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun joueur ni porte réelle", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx9H(text);
        }
    }

    internal StudioLiveDirectorAuthorityWorkbenchUx9H(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9H();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9H();
    }

    internal void Close()
    {
        if (_evidenceSession is not null)
        {
            CloseEvidence();
            return;
        }
        if (!Visible) return;
        Visible = false;
        _session.CancelLiveAuthorityApprovalUx9H();
        ResetUx9H();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9H(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-h-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = LiveDirectorAuthorityProfile.CreateDefault("evidence:ux9-h-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceLiveDirectorAuthorityStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9H();
            RestoreSourceSessionUx9H();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9H();
        _moduleChoice.Select(1);
        SelectModuleUx9H(1);
        ConfigureEvidenceUx9H(mode);
        PreviewCurrentUx9H();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_H_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                 $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                 $"source_untouched={EvidenceSourceUntouched.ToString().ToLowerInvariant()}");
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiEvidence()
    {
        if (!UsesIsolatedEvidence || !PreviewReady || _evidenceRoot is null) return false;
        var draft = ReadDraftUx9H();
        var options = ReadOptionsUx9H();
        if (draft is null || options is null) return false;
        var preview = _session.PreviewLiveAuthorityUx9H(draft, options);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedLiveAuthorityUx9H(draft).Succeeded;
        var undo = applied && _session.UndoLiveAuthorityUx9H().Succeeded;
        var redo = undo && _session.RedoLiveAuthorityUx9H().Succeeded;
        var save = redo && _session.SaveLiveAuthorityUx9H().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalLiveAuthorityUx9H(true, NetworkRole.Administrator,
                         options).Succeeded;
        var expected = _session.CurrentLiveAuthorityUx9H;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentLiveAuthorityUx9H is { } actual &&
                         new LiveDirectorAuthorityStore().Serialize(expected) ==
                         new LiveDirectorAuthorityStore().Serialize(actual);
        reopened.Close();
        RefreshAuditUx9H();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.LiveAuthorityProbeUx9H.ResourcesReleased &&
               EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        Visible = false;
        ResetUx9H();
        DeleteEvidenceUx9H();
        RestoreSourceSessionUx9H();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9H();
        _moduleChoice.Select(1);
        SelectModuleUx9H(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9H();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9H;
        UpdateBoundsUx9H();
    }

    public override void _ExitTree()
    {
        CloseEvidence();
    }

    private void BuildUiUx9H()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9HLiveAuthority", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9HRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Live Director · autorité des joueurs et du monde",
            20, StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Prévisualiser, autoriser, auditer puis compenser chaque intervention locale.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("h_separation", 7);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9H("ORF-DM-013", "Incarner",
            "Chat, dialogue, voix ou animation avec divulgation explicite."));
        routes.AddChild(RouteCardUx9H("ORF-DM-014", "Protéger",
            "Durée bornée, portée, indicateur visible et adaptateur de dégâts."));
        routes.AddChild(RouteCardUx9H("ORF-DM-016", "Ajuster l’or",
            "Plafond, raison, solde non négatif et portefeuille persistant."));
        routes.AddChild(RouteCardUx9H("ORF-DM-017", "Ajuster l’expérience",
            "Plafond, raison, projection de niveau et progression persistante."));
        routes.AddChild(RouteCardUx9H("ORF-DM-019", "Commander une porte",
            "Verrou, ouverture, réparation, collision et portail relié."));

        _moduleChoice = ChoiceUx9H(null, "Sélection du module", "Sélectionner un module");
        _moduleChoice.ItemSelected += index => SelectModuleUx9H((int)index);
        root.AddChild(LabeledRowUx9H("Module explicitement ciblé", _moduleChoice));
        _focusChoice = ChoiceUx9H(null, "Vue", "Incarner et protéger",
            "Or et expérience", "Portes et portails");
        _focusChoice.ItemSelected += index => SelectFocusUx9H((int)index);
        root.AddChild(LabeledRowUx9H("Workflow direct", _focusChoice));

        var editorScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(0, 260)
        };
        root.AddChild(editorScroll);
        var editor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editor.AddThemeConstantOverride("separation", 7);
        editorScroll.AddChild(editor);
        BuildControlPageUx9H(editor);
        BuildEconomyPageUx9H(editor);
        BuildDoorPageUx9H(editor);

        var decision = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        decision.AddThemeConstantOverride("separation", 18);
        _approval = CheckUx9H(decision,
            "J’approuve ce brouillon et cette révision après l’aperçu sec");
        _probeConfirmation = CheckUx9H(decision,
            "Je confirme la sonde locale restaurée en fin d’exécution");
        root.AddChild(decision);

        _previewSummary = StudioStyle.Label("Aucun aperçu sec.", 10, StudioStyle.Text,
            wrap: true);
        _previewSummary.CustomMinimumSize = new Vector2(0, 52);
        root.AddChild(_previewSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_auditSummary);
        _status = StudioStyle.Label(
            "Sélectionnez explicitement le module avant de modifier l’autorité.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Sandbox isolée : aucun joueur ni porte réelle, aucun serveur et aucun portefeuille externe ne sont modifiés.",
            10, StudioStyle.Warning, wrap: true));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9H(actions, "Prévisualiser", PreviewCurrentUx9H, 122,
            primary: true);
        _applyButton = ActionButtonUx9H(actions, "Appliquer", ApplyCurrentUx9H, 104);
        _undoButton = ActionButtonUx9H(actions, "Annuler", UndoUx9H, 92);
        _redoButton = ActionButtonUx9H(actions, "Rétablir", RedoUx9H, 92);
        _saveButton = ActionButtonUx9H(actions, "Enregistrer", SaveUx9H, 112);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _probeButton = ActionButtonUx9H(actions, "Tester localement", ProbeUx9H, 148);

        ConnectInvalidationUx9H();
        SelectFocusUx9H(0);
        UpdateActionsUx9H();
    }

    private void BuildControlPageUx9H(VBoxContainer parent)
    {
        var page = FocusPageUx9H(parent,
            "Incarner et protéger · restitution et expiration automatiques");
        var grid = GridUx9H(page);
        _maximumProtectionMinutes = SpinUx9H(grid, "Limite de protection (minutes)", 1,
            1_440, 1);
        _protectionMinutes = SpinUx9H(grid, "Durée de la sonde (minutes)", 1, 1_440, 1);
        _impersonationMode = ChoiceUx9H(grid, "Mode d’incarnation", "Chat", "Dialogue",
            "Voix", "Animation");
        _disclosure = ChoiceUx9H(grid, "Divulgation", "Visible",
            "Masquée uniquement par politique");
        _protectionScope = ChoiceUx9H(grid, "Portée de protection", "Dégâts", "Effets",
            "Tout");
        _protectionIndicator = LineUx9H(grid, "Indicateur visible", "Bouclier ambre");
        _requiredAtRuntime = CheckUx9H(page,
            "Exiger ce profil au démarrage du runtime Live Director");
        page.AddChild(StudioStyle.Label(
            "L’adaptateur de rendu correspond au canal choisi ; l’adaptateur de dégâts et l’état initial sont restaurés à l’expiration.",
            10, StudioStyle.Success, wrap: true));
    }

    private void BuildEconomyPageUx9H(VBoxContainer parent)
    {
        var page = FocusPageUx9H(parent,
            "Or et expérience · montants bornés, persistants et compensables");
        var grid = GridUx9H(page);
        _maximumCurrencyAdjustment = SpinUx9H(grid, "Plafond d’ajustement d’or", 1,
            10_000_000, 1);
        _maximumWallet = SpinUx9H(grid, "Plafond du portefeuille", 1, 100_000_000, 1);
        _currencyAmount = SpinUx9H(grid, "Ajustement d’or de la sonde", -10_000_000,
            10_000_000, 1);
        _currencyReason = LineUx9H(grid, "Raison de l’or", "Récompense de scène");
        _maximumExperienceAdjustment = SpinUx9H(grid,
            "Plafond d’ajustement d’expérience", 1, 10_000_000, 1);
        _experienceAmount = SpinUx9H(grid, "Expérience de la sonde", -10_000_000,
            10_000_000, 1);
        _experienceReason = LineUx9H(grid, "Raison de l’expérience", "Jalon narratif");
        page.AddChild(StudioStyle.Label(
            "Le solde non négatif, le plafond du portefeuille et la projection de niveau du ruleset sont vérifiés avant application.",
            10, StudioStyle.Success, wrap: true));
    }

    private void BuildDoorPageUx9H(VBoxContainer parent)
    {
        var page = FocusPageUx9H(parent,
            "Portes et portails · état sémantique et adaptateurs runtime");
        var grid = GridUx9H(page);
        _doorState = ChoiceUx9H(grid, "Action de porte", "Verrouiller", "Déverrouiller",
            "Ouvrir", "Réparer", "Relier à un portail");
        _doorPortal = LineUx9H(grid, "Destination humaine du portail", "Sortie surface");
        page.AddChild(StudioStyle.Label(
            "Chaque aperçu indique l’état de collision et portail ; la compensation rétablit l’état verrouillé initial.",
            10, StudioStyle.Success, wrap: true));
    }

    private VBoxContainer FocusPageUx9H(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9H()
    {
        Visible = true;
        ResetUx9H();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9H();
        UpdateBoundsUx9H();
        UpdateActionsUx9H();
    }

    private void SelectModuleUx9H(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelLiveAuthorityApprovalUx9H();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9H("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9H();
            return;
        }
        _draft = _session.CreateLiveAuthorityDraftUx9H(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9H(_session.LiveAuthorityLoadMessageUx9H ??
                          "Le profil d’autorité est indisponible.", true);
            UpdateActionsUx9H();
            return;
        }
        PopulateDraftUx9H(_draft);
        PopulateOptionsUx9H(StudioUx9HProbeOptions.CreateDefault());
        SetStatusUx9H(_session.LiveAuthorityFileMissingUx9H
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Autorité Live Director chargée · prévisualisez avant toute application.", false);
        UpdateActionsUx9H();
    }

    private void PopulateDraftUx9H(StudioUx9HAuthorityDraft draft)
    {
        _updating = true;
        _maximumProtectionMinutes.Value = draft.MaximumProtectionMinutes;
        _maximumCurrencyAdjustment.Value = draft.MaximumCurrencyAdjustment;
        _maximumWallet.Value = draft.MaximumWallet;
        _maximumExperienceAdjustment.Value = draft.MaximumExperienceAdjustment;
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _updating = false;
    }

    private void PopulateOptionsUx9H(StudioUx9HProbeOptions value)
    {
        _updating = true;
        _impersonationMode.Select(Array.IndexOf(ImpersonationValues, value.ImpersonationMode));
        _disclosure.Select(Array.IndexOf(DisclosureValues, value.Disclosure));
        _protectionMinutes.Value = value.ProtectionMinutes;
        _protectionScope.Select(Array.IndexOf(ProtectionValues, value.ProtectionScope));
        _protectionIndicator.Text = value.ProtectionIndicator;
        _currencyAmount.Value = value.CurrencyAmount;
        _currencyReason.Text = value.CurrencyReason;
        _experienceAmount.Value = value.ExperienceAmount;
        _experienceReason.Text = value.ExperienceReason;
        _doorState.Select(Array.IndexOf(DoorValues, value.DoorState));
        _doorPortal.Text = value.DoorPortal;
        _updating = false;
    }

    private StudioUx9HAuthorityDraft? ReadDraftUx9H() => _draft is null ? null : _draft with
    {
        MaximumProtectionMinutes = (int)_maximumProtectionMinutes.Value,
        MaximumCurrencyAdjustment = (long)_maximumCurrencyAdjustment.Value,
        MaximumWallet = (long)_maximumWallet.Value,
        MaximumExperienceAdjustment = (long)_maximumExperienceAdjustment.Value,
        RequiredAtRuntime = _requiredAtRuntime.ButtonPressed
    };

    private StudioUx9HProbeOptions? ReadOptionsUx9H()
    {
        if (!_explicitSelection) return null;
        return new(ImpersonationValues[_impersonationMode.Selected],
            DisclosureValues[_disclosure.Selected], (int)_protectionMinutes.Value,
            ProtectionValues[_protectionScope.Selected], _protectionIndicator.Text.Trim(),
            (long)_currencyAmount.Value, _currencyReason.Text.Trim(),
            (long)_experienceAmount.Value, _experienceReason.Text.Trim(),
            DoorValues[_doorState.Selected], _doorPortal.Text.Trim());
    }

    private void PreviewCurrentUx9H()
    {
        var draft = ReadDraftUx9H();
        var options = ReadOptionsUx9H();
        if (draft is null || options is null)
        {
            SetStatusUx9H("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewLiveAuthorityUx9H(draft, options);
        PopulatePreviewUx9H();
        SetStatusUx9H(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9H();
    }

    private void ApplyCurrentUx9H()
    {
        var draft = ReadDraftUx9H();
        if (draft is null) return;
        var focus = _focusChoice.Selected;
        var result = _session.ApplyApprovedLiveAuthorityUx9H(draft);
        SetStatusUx9H(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9H(1);
            SelectFocusUx9H(focus);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9H();
    }

    private void UndoUx9H()
    {
        var result = _session.UndoLiveAuthorityUx9H();
        SetStatusUx9H(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            SelectModuleUx9H(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9H();
    }

    private void RedoUx9H()
    {
        var result = _session.RedoLiveAuthorityUx9H();
        SetStatusUx9H(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9H(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9H();
    }

    private void SaveUx9H()
    {
        var result = _session.SaveLiveAuthorityUx9H();
        SetStatusUx9H(result.Message, !result.Succeeded);
        UpdateActionsUx9H();
    }

    private void ProbeUx9H()
    {
        var options = ReadOptionsUx9H();
        if (options is null) return;
        var result = _session.RunLocalLiveAuthorityUx9H(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator, options);
        SetStatusUx9H(result.Message, !result.Succeeded);
        RefreshAuditUx9H();
        PopulatePreviewUx9H();
        UpdateActionsUx9H();
    }

    private void PopulatePreviewUx9H()
    {
        var probe = _session.LiveAuthorityProbeUx9H;
        _previewSummary.Text = _preview is null
            ? $"Sandbox · {probe.Status}."
            : $"{_preview.Message} · {string.Join(" · ", _preview.SemanticDiff)}\n" +
              $"Incarnation {HumanCodeUx9H(probe.ImpersonationCode)}, protection {HumanCodeUx9H(probe.ProtectionCode)}, " +
              $"or {HumanCodeUx9H(probe.CurrencyCode)}, niveau projeté {probe.ProjectedLevel}, porte {HumanCodeUx9H(probe.DoorCode)}.";
    }

    private void RefreshAuditUx9H()
    {
        var audit = _session.LiveAuthorityAuditUx9H;
        var probe = _session.LiveAuthorityProbeUx9H;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrée(s) · portefeuille {(probe.WalletRoundTrip ? "persisté" : "à vérifier")} · " +
              $"progression {(probe.ProgressionRoundTrip ? "persistée" : "à vérifier")} · ressources {(probe.ResourcesReleased ? "restaurées" : "actives")}.";
    }

    private void ConfigureEvidenceUx9H(string mode)
    {
        _updating = true;
        _maximumProtectionMinutes.Value = 120;
        _maximumCurrencyAdjustment.Value = 50_000;
        _maximumWallet.Value = 1_000_000;
        _maximumExperienceAdjustment.Value = 250_000;
        _requiredAtRuntime.ButtonPressed = true;
        PopulateOptionsUx9H(StudioUx9HProbeOptions.CreateDefault());
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        _updating = false;
        var focus = mode.Contains("currency", StringComparison.OrdinalIgnoreCase) ||
                    mode.Contains("experience", StringComparison.OrdinalIgnoreCase) ||
                    mode.Contains("gold", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("door", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("portal", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9H(focus);
    }

    private void SelectFocusUx9H(int index)
    {
        for (var i = 0; i < _focusPages.Count; i++) _focusPages[i].Visible = i == index;
    }

    private void ConnectInvalidationUx9H()
    {
        foreach (var spin in new[] { _maximumProtectionMinutes, _maximumCurrencyAdjustment,
                     _maximumWallet, _maximumExperienceAdjustment, _protectionMinutes,
                     _currencyAmount, _experienceAmount })
            spin.ValueChanged += _ => InvalidateUx9H();
        foreach (var line in new[] { _protectionIndicator, _currencyReason,
                     _experienceReason, _doorPortal })
            line.TextChanged += _ => InvalidateUx9H();
        foreach (var option in new[] { _impersonationMode, _disclosure, _protectionScope,
                     _doorState })
            option.ItemSelected += _ => InvalidateUx9H();
        _requiredAtRuntime.Toggled += _ => InvalidateUx9H();
        _approval.Toggled += _ => UpdateActionsUx9H();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9H();
    }

    private void InvalidateUx9H()
    {
        if (_updating) return;
        _preview = null;
        _session.CancelLiveAuthorityApprovalUx9H();
        _approval.ButtonPressed = false;
        PopulatePreviewUx9H();
        UpdateActionsUx9H();
    }

    private void UpdateActionsUx9H()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || _preview is not { CanApply: true } ||
                                !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.LiveAuthorityHasUnsavedChangesUx9H;
        _probeButton.Disabled = !selected || !_probeConfirmation.ButtonPressed ||
                                _session.LiveAuthorityHasUnsavedChangesUx9H;
    }

    private void ResetUx9H()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        if (_previewSummary is not null) PopulatePreviewUx9H();
    }

    private SourceSnapshotUx9H CaptureSourceUx9H(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureLiveAuthorityApprovalUx9H());

    private bool SourceUnchangedUx9H()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        if (source is null || snapshot is null) return _evidenceSession is null;
        return ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.Dirty && source.ActiveAreaId == snapshot.Area &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.Selected &&
               source.CaptureLiveAuthorityApprovalUx9H() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9H()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9H()
    {
        var root = _evidenceRoot;
        _evidenceRoot = null;
        if (string.IsNullOrWhiteSpace(root))
        {
            _evidenceResourcesReleased = true;
            return;
        }
        try
        {
            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), temp,
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-h-ui-",
                    StringComparison.Ordinal))
            {
                _evidenceResourcesReleased = false;
                return;
            }
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            _evidenceResourcesReleased = !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _evidenceResourcesReleased = false;
            SetStatusUx9H($"La copie temporaire n’a pas pu être libérée : {exception.Message}",
                true);
        }
    }

    private void UpdateBoundsUx9H()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9H(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger :
            StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9H(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9H" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 6, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stack = new VBoxContainer();
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text));
        stack.AddChild(StudioStyle.Label(detail, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Control LabeledRowUx9H(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9H(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 7);
        parent.AddChild(grid);
        return grid;
    }

    private static SpinBox SpinUx9H(GridContainer parent, string label, double min, double max,
        double step)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static LineEdit LineUx9H(GridContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static OptionButton ChoiceUx9H(GridContainer? parent, string label,
        params string[] choices)
    {
        parent?.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260, 34)
        };
        foreach (var choice in choices) value.AddItem(choice);
        parent?.AddChild(value);
        return value;
    }

    private static CheckBox CheckUx9H(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9H(HBoxContainer parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9H(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9H(child))
                yield return value;
    }

    private static bool ContainsUuidUx9H(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private static string HumanCodeUx9H(string code) => code switch
    {
        "LIVE_CONTROL_APPLIED" => "appliquée puis restaurée",
        "LIVE_CONTROL_PREVIEW" => "prévisualisée",
        "LIVE_ECONOMY_APPLIED" => "appliqué puis compensé",
        "LIVE_ECONOMY_PREVIEW" => "prévisualisé",
        "LIVE_RULE_APPLIED" => "appliquée puis compensée",
        "LIVE_RULE_PREVIEW" => "prévisualisée",
        _ => code
    };

    private static readonly string[] ImpersonationValues =
        ["chat", "dialogue", "voice", "animation"];
    private static readonly string[] DisclosureValues = ["visible", "hidden-by-policy"];
    private static readonly string[] ProtectionValues = ["damage", "effects", "all"];
    private static readonly string[] DoorValues =
        ["locked", "unlocked", "open", "repaired", "relinked"];

    private sealed record SourceSnapshotUx9H(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9HApprovalSnapshot Approval);
}
