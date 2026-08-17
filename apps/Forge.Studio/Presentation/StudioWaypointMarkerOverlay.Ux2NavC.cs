using Forge.Domain.Navigation;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal static class StudioUx2NavCControls
{
    internal static LineEdit Text(string placeholder = "")
    {
        var control = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(control);
        return control;
    }

    internal static SpinBox Number(double minimum, double maximum, double step = .1)
    {
        var control = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = 0,
            CustomArrowStep = step,
            CustomArrowRound = false,
            AllowGreater = true,
            AllowLesser = true,
            UpdateOnTextChanged = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(control);
        return control;
    }

    internal static OptionButton Options(params string[] labels)
    {
        var control = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FitToLongestItem = false,
        };
        StudioStyle.ApplyOptionButton(control);
        foreach (var label in labels)
        {
            control.AddItem(label);
        }
        return control;
    }

    internal static CheckBox Check(string label)
    {
        var control = new CheckBox { Text = label };
        control.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        control.AddThemeColorOverride("font_color", StudioStyle.Text);
        control.AddThemeColorOverride("font_hover_color", StudioStyle.Text);
        control.AddThemeColorOverride("font_pressed_color", StudioStyle.Text);
        return control;
    }

    internal static VBoxContainer Field(string label, Control control, int minimumWidth = 110)
    {
        var field = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(minimumWidth, 0),
        };
        field.AddThemeConstantOverride("separation", 3);
        field.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        field.AddChild(control);
        return field;
    }

    internal static PanelContainer Card(string name, string title, string help, Control content)
    {
        var panel = StudioStyle.Panel(name, StudioStyle.Surface, 8, 1);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 5);
        root.AddChild(StudioStyle.Label(title, 12));
        root.AddChild(StudioStyle.Label(help, 9, StudioStyle.Muted, wrap: true));
        root.AddChild(content);
        margin.AddChild(root);
        return panel;
    }
}

internal sealed partial class StudioWaypointMarkerOverlayUx2NavC : Control
{
    private static readonly MarkerVisibilityPolicy[] VisibilityPolicies =
        Enum.GetValues<MarkerVisibilityPolicy>();
    private static readonly MarkerVisibilityScope[] VisibilityScopes =
        Enum.GetValues<MarkerVisibilityScope>();

    private readonly StudioSessionController _sourceSession;
    private readonly StudioWorldViewport _worldViewport;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2NavCEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastUiTransaction;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _waypoint;
    private readonly Button _addWaypoint;
    private readonly Label _selection;
    private readonly VBoxContainer _editor;
    private readonly LineEdit _name;
    private readonly SpinBox _positionX;
    private readonly SpinBox _positionY;
    private readonly SpinBox _positionZ;
    private readonly LineEdit _appearanceIcon;
    private readonly LineEdit _appearanceColor;
    private readonly LineEdit _appearanceLayer;
    private readonly SpinBox _appearanceSize;
    private readonly CheckBox _appearanceVisible;
    private readonly CheckBox _markerEnabled;
    private readonly VBoxContainer _markerFields;
    private readonly LineEdit _markerText;
    private readonly LineEdit _markerIcon;
    private readonly SpinBox _markerFloor;
    private readonly LineEdit _markerCategory;
    private readonly SpinBox _markerPrecision;
    private readonly OptionButton _markerPolicy;
    private readonly OptionButton _markerScope;
    private readonly CheckBox _markerTracked;
    private readonly CheckBox _markerRevealed;
    private readonly CheckBox _markerPersistent;
    private readonly LineEdit _markerRevealEvent;
    private readonly LineEdit _markerRevealAnimation;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _revealPreview;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2NavCAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2NavCWaypointChoice> _waypointChoices = [];
    private StudioUx2NavCDraft? _draft;
    private StudioUx2NavCPreview? _lastPreview;
    private bool _updating;
    private bool _explicitSelection;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? NavigationChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _areaChoices.Count;
    internal bool HasExplicitSelection => _draft is not null && _explicitSelection && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady;
    internal bool ShowsViewportOverlay => _lastPreview is { Succeeded: true } &&
                                          _worldViewport.WaypointAppearanceMarkerCountUx2NavC > 0;
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceTempRoot is null &&
                                               _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;
    internal bool LastUiTransaction => _lastUiTransaction;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabelsUx2NavC(_dialog).Select(item => item.Text));
            return HasExplicitSelection && text.Contains("Apparence", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Note cartographique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Révélation", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx2NavC(text);
        }
    }

    internal StudioWaypointMarkerOverlayUx2NavC(StudioSessionController session,
        StudioWorldViewport worldViewport)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        _worldViewport = worldViewport ?? throw new ArgumentNullException(nameof(worldViewport));
        Name = "StudioWaypointMarkerOverlayUx2NavC";
        Visible = false;
        ZIndex = 456;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("WaypointMarkerDialogUx2NavC", StudioStyle.Chrome, 12, 2);
        _dialog.MouseFilter = MouseFilterEnum.Stop;
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 7);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · REPÈRES CARTOGRAPHIQUES", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Apparence et révélation des waypoints", 19));
        heading.AddChild(StudioStyle.Label(
            "WPT-002 / 003 / 004 · gizmo iconique, note de carte et politique de visibilité. " +
            "L’aperçu évalue le contrat canonique sans déclencher d’événement runtime.", 9,
            StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selector.AddThemeConstantOverride("separation", 7);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Zone", 10, StudioStyle.Muted));
        _area = StudioUx2NavCControls.Options();
        _area.ItemSelected += SelectArea;
        selector.AddChild(_area);
        selector.AddChild(StudioStyle.Label("Repère", 10, StudioStyle.Muted));
        _waypoint = StudioUx2NavCControls.Options();
        _waypoint.ItemSelected += SelectWaypoint;
        selector.AddChild(_waypoint);
        _addWaypoint = StudioStyle.Button("Nouveau repère", compact: true);
        _addWaypoint.TooltipText = "Créer explicitement un repère au centre de la zone";
        _addWaypoint.Pressed += AddWaypoint;
        selector.AddChild(_addWaypoint);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone puis un repère ; aucune fiche n’est chargée automatiquement.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 430),
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(scroll);
        _editor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _editor.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_editor);

        var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddThemeConstantOverride("separation", 5);
        _name = StudioUx2NavCControls.Text("Nom humain du repère");
        identity.AddChild(StudioUx2NavCControls.Field("Nom", _name));
        var position = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        position.AddThemeConstantOverride("separation", 5);
        _positionX = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        _positionY = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        _positionZ = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        position.AddChild(StudioUx2NavCControls.Field("Position X", _positionX));
        position.AddChild(StudioUx2NavCControls.Field("Altitude Y", _positionY));
        position.AddChild(StudioUx2NavCControls.Field("Position Z", _positionZ));
        identity.AddChild(position);
        identity.AddChild(StudioStyle.Label(
            "Ancrage, arrivée, exigence runtime et API ForgeScript restent protégés et inchangés.",
            9, StudioStyle.Muted, wrap: true));
        _editor.AddChild(StudioUx2NavCControls.Card("WaypointIdentityUx2NavC", "Repère sélectionné",
            "Le nom et la position servent à identifier le gizmo ; les propriétés avancées voisines ne sont pas éditées.",
            identity));

        var appearance = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        appearance.AddThemeConstantOverride("separation", 5);
        var appearanceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        appearanceRow.AddThemeConstantOverride("separation", 5);
        _appearanceIcon = StudioUx2NavCControls.Text("flag");
        _appearanceColor = StudioUx2NavCControls.Text("#F2B84B");
        _appearanceLayer = StudioUx2NavCControls.Text("Repères");
        _appearanceSize = StudioUx2NavCControls.Number(8, 96, 1);
        appearanceRow.AddChild(StudioUx2NavCControls.Field("Icône", _appearanceIcon));
        appearanceRow.AddChild(StudioUx2NavCControls.Field("Couleur", _appearanceColor));
        appearanceRow.AddChild(StudioUx2NavCControls.Field("Couche", _appearanceLayer));
        appearanceRow.AddChild(StudioUx2NavCControls.Field("Taille écran (px)", _appearanceSize));
        appearance.AddChild(appearanceRow);
        _appearanceVisible = StudioUx2NavCControls.Check("Afficher le gizmo dans l’éditeur");
        appearance.AddChild(_appearanceVisible);
        _editor.AddChild(StudioUx2NavCControls.Card("WaypointAppearanceUx2NavC", "Apparence d’éditeur",
            "Icône, couleur et couche restent lisibles à taille écran constante ; le viewport montre le repère sélectionné.",
            appearance));

        var marker = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        marker.AddThemeConstantOverride("separation", 5);
        _markerEnabled = StudioUx2NavCControls.Check("Créer une note cartographique");
        marker.AddChild(_markerEnabled);
        _markerFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _markerFields.AddThemeConstantOverride("separation", 5);
        marker.AddChild(_markerFields);
        var markerIdentity = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        markerIdentity.AddThemeConstantOverride("separation", 5);
        _markerText = StudioUx2NavCControls.Text("Texte lisible sur la carte");
        _markerIcon = StudioUx2NavCControls.Text("pin");
        _markerCategory = StudioUx2NavCControls.Text("exploration");
        _markerFloor = StudioUx2NavCControls.Number(-64, 64, 1);
        markerIdentity.AddChild(StudioUx2NavCControls.Field("Texte", _markerText, 180));
        markerIdentity.AddChild(StudioUx2NavCControls.Field("Icône carte", _markerIcon));
        markerIdentity.AddChild(StudioUx2NavCControls.Field("Catégorie", _markerCategory));
        markerIdentity.AddChild(StudioUx2NavCControls.Field("Étage", _markerFloor, 75));
        _markerFields.AddChild(markerIdentity);
        var policyRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        policyRow.AddThemeConstantOverride("separation", 5);
        _markerPolicy = StudioUx2NavCControls.Options(VisibilityPolicies.Select(HumanPolicy).ToArray());
        _markerScope = StudioUx2NavCControls.Options(VisibilityScopes.Select(HumanScope).ToArray());
        _markerPrecision = StudioUx2NavCControls.Number(0, 100, .5);
        policyRow.AddChild(StudioUx2NavCControls.Field("Visibilité", _markerPolicy));
        policyRow.AddChild(StudioUx2NavCControls.Field("Portée", _markerScope));
        policyRow.AddChild(StudioUx2NavCControls.Field("Précision (m)", _markerPrecision));
        _markerFields.AddChild(policyRow);
        var flags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        flags.AddThemeConstantOverride("separation", 10);
        _markerTracked = StudioUx2NavCControls.Check("Suivie");
        _markerRevealed = StudioUx2NavCControls.Check("Déjà révélée");
        _markerPersistent = StudioUx2NavCControls.Check("Révélation persistante");
        flags.AddChild(_markerTracked);
        flags.AddChild(_markerRevealed);
        flags.AddChild(_markerPersistent);
        _markerFields.AddChild(flags);
        var reveal = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reveal.AddThemeConstantOverride("separation", 5);
        _markerRevealEvent = StudioUx2NavCControls.Text("quest.location.discovered");
        _markerRevealAnimation = StudioUx2NavCControls.Text("pulse");
        reveal.AddChild(StudioUx2NavCControls.Field("Événement de révélation", _markerRevealEvent));
        reveal.AddChild(StudioUx2NavCControls.Field("Animation de révélation", _markerRevealAnimation));
        _markerFields.AddChild(reveal);
        _editor.AddChild(StudioUx2NavCControls.Card("WaypointMarkerUx2NavC", "Note cartographique et révélation",
            "La portée joueur, groupe ou monde est évaluée sans exécuter de script ni modifier l’état de jeu.",
            marker));

        var result = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        result.AddThemeConstantOverride("separation", 8);
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        result.AddChild(StudioUx2NavCControls.Card("WaypointDiffUx2NavC", "Diff sémantique",
            "Ce qui sera remplacé par la commande atomique.", _diff));
        result.AddChild(StudioUx2NavCControls.Card("WaypointDiagnosticsUx2NavC", "Diagnostics",
            "Codes stables et chemins humains avant application.", _diagnostics));
        _editor.AddChild(result);
        _summary = StudioStyle.Label("Aucun aperçu calculé.", 10, StudioStyle.Muted, wrap: true);
        _editor.AddChild(_summary);
        _revealPreview = StudioStyle.Label(
            "Révélation : l’aperçu n’émet aucun événement runtime.", 10, StudioStyle.Muted, wrap: true);
        _editor.AddChild(_revealPreview);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 6);
        root.AddChild(footer);
        _status = StudioStyle.Label("Sélection explicite requise.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        _preview = StudioStyle.Button("Aperçu sec", compact: true);
        _preview.TooltipText = "Valider et calculer le diff sans mutation";
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Sauvegarder", compact: true);
        _save.Pressed += Save;
        footer.AddChild(_save);

        WireDraftSignals();
        SetEditorEnabled(false);
        UpdateActionState();
        SetProcessUnhandledKeyInput(true);
        Resized += UpdateDialogBounds;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }
        if (key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    internal void Open()
    {
        ReleaseEvidenceWorkspace();
        _session = _sourceSession;
        _sourceSession.CancelWaypointUx2NavC();
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        PopulateAreas();
        ClearSelection();
        UpdateDialogBounds();
        SetStatus("Choisissez explicitement une zone puis un repère.");
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null)
        {
            return;
        }
        var evidenceOpen = _evidenceSession is not null;
        if (evidenceOpen)
        {
            _evidenceSession!.CancelWaypointUx2NavC();
        }
        else
        {
            _sourceSession.CancelWaypointUx2NavC();
        }
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _worldViewport.ClearWaypointAppearanceOverlayUx2NavC();
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _explicitSelection = false;
        ReleaseEvidenceWorkspace();
        RestoreSourceViewport();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!string.Equals(mode, "ux2-nav-c", StringComparison.OrdinalIgnoreCase) ||
            _sourceSession.Project is null || _sourceSession.History is null)
        {
            return false;
        }
        ReleaseEvidenceWorkspace();
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        _lastEvidenceSourceUntouched = true;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        var project = _sourceSession.CreateUx2NavCEvidenceProject();
        if (project is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-c-evidence-" + Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                StudioSessionController.DeleteUx2NavCTemporaryWorkspace(tempRoot);
                return false;
            }
            var evidence = new StudioSessionController();
            if (!evidence.Open(tempRoot).Succeeded)
            {
                evidence.Close();
                StudioSessionController.DeleteUx2NavCTemporaryWorkspace(tempRoot);
                return false;
            }
            _evidenceTempRoot = tempRoot;
            _evidenceSession = evidence;
            _session = evidence;
            Visible = true;
            MouseFilter = MouseFilterEnum.Stop;
            PopulateAreas();
            var areaIndex = _areaChoices.ToList().FindIndex(item => item.IsEvidence);
            if (areaIndex < 0)
            {
                Close();
                return false;
            }
            _area.Selected = areaIndex + 1;
            SelectArea(areaIndex + 1);
            var waypointIndex = _waypointChoices.ToList().FindIndex(item =>
                item.Label.StartsWith("Autel des braises", StringComparison.Ordinal));
            if (waypointIndex < 0)
            {
                Close();
                return false;
            }
            _waypoint.Selected = waypointIndex + 1;
            SelectWaypoint(waypointIndex + 1);
            _appearanceSize.Value = 34;
            _markerText.Text = "Autel révélé";
            _markerRevealEvent.Text = "quest.ashes.revealed";
            DraftChanged();
            Preview();
            UpdateDialogBounds();
            return HasExplicitSelection && _canonicalPreviewReady && ShowsViewportOverlay &&
                   UsesIsolatedEvidence && EvidenceSourceUntouched && ShowsHumanSurface;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_C_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            Close();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        _lastUiTransaction = false;
        try
        {
            if (!PrepareEvidence("ux2-nav-c"))
            {
                Close();
                return false;
            }
            _appearanceColor.Text = "#E66A3C";
            _appearanceLayer.Text = "Quêtes principales";
            _markerCategory.Text = "quête principale";
            _markerPrecision.Value = 1.5;
            DraftChanged();
            Preview();
            var dryRun = _canonicalPreviewReady && _apply.Disabled == false;
            var projectBeforeApply = _session.Project;
            Apply();
            var appliedProject = _session.Project;
            var applied = appliedProject is not null && !ReferenceEquals(projectBeforeApply, appliedProject);
            Undo();
            var undone = ReferenceEquals(projectBeforeApply, _session.Project);
            Redo();
            var redone = ReferenceEquals(appliedProject, _session.Project);
            Save();
            var saved = !_session.IsDirty;
            _lastUiTransaction = dryRun && applied && undone && redone && saved;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_C_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            _lastUiTransaction = false;
        }
        finally
        {
            Close();
        }
        return _lastUiTransaction && EvidenceResourcesReleased && SourceSessionRestored &&
               _lastEvidenceSourceUntouched;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureWaypointApprovalUx2NavC();
        Open();
        _sourceSession.RestoreWaypointApprovalUx2NavC(approval);
    }

    private void PopulateAreas()
    {
        _updating = true;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        _areaChoices = _session.WaypointAreaChoicesUx2NavC;
        foreach (var choice in _areaChoices)
        {
            _area.AddItem(choice.Label);
        }
        _area.Selected = 0;
        _updating = false;
    }

    private void SelectArea(long index)
    {
        if (_updating)
        {
            return;
        }
        _session.CancelWaypointUx2NavC();
        _worldViewport.ClearWaypointAppearanceOverlayUx2NavC();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _draft = null;
        _explicitSelection = false;
        _updating = true;
        _waypoint.Clear();
        _waypoint.AddItem("Choisir un repère…");
        _waypointChoices = [];
        if (index > 0 && index <= _areaChoices.Count)
        {
            var areaChoice = _areaChoices[(int)index - 1];
            _waypointChoices = _session.WaypointChoicesUx2NavC(areaChoice.AreaId);
            foreach (var choice in _waypointChoices)
            {
                _waypoint.AddItem(choice.Label);
            }
            BindViewportToArea(_session, areaChoice.AreaId);
            _selection.Text = _waypointChoices.Count == 0
                ? $"{areaChoice.Label} · aucun repère · utilisez « Nouveau repère »."
                : $"{areaChoice.Label} · choisissez explicitement un repère ou créez-en un.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        }
        else
        {
            _selection.Text = "Choisissez explicitement une zone ; aucune fiche n’est chargée automatiquement.";
        }
        _waypoint.Selected = 0;
        _updating = false;
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void SelectWaypoint(long index)
    {
        if (_updating || _area.Selected <= 0 || _area.Selected > _areaChoices.Count)
        {
            return;
        }
        _session.CancelWaypointUx2NavC();
        _worldViewport.ClearWaypointAppearanceOverlayUx2NavC();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        if (index <= 0 || index > _waypointChoices.Count)
        {
            _draft = null;
            _explicitSelection = false;
            SetEditorEnabled(false);
            RenderPreview(null);
            UpdateActionState();
            return;
        }
        var areaChoice = _areaChoices[_area.Selected - 1];
        var choice = _waypointChoices[(int)index - 1];
        _draft = _session.CreateWaypointDraftUx2NavC(areaChoice.AreaId, choice.WaypointId);
        _explicitSelection = _draft is not null;
        if (_draft is null)
        {
            SetStatus("Le repère choisi n’existe plus. Rechargez la page.", true);
            SetEditorEnabled(false);
            return;
        }
        LoadDraft(_draft);
        _selection.Text = $"{areaChoice.Label} · {choice.Label} · sélection explicite";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        SetEditorEnabled(true);
        SetStatus("Repère chargé · prévisualisez avant toute application.");
        UpdateActionState();
    }

    private void AddWaypoint()
    {
        if (_area.Selected <= 0 || _area.Selected > _areaChoices.Count)
        {
            SetStatus("Choisissez d’abord une zone.", true);
            return;
        }
        var areaChoice = _areaChoices[_area.Selected - 1];
        _draft = _session.CreateNewWaypointDraftUx2NavC(areaChoice.AreaId);
        if (_draft is null)
        {
            SetStatus("Le nouveau repère ne peut pas être préparé.", true);
            return;
        }
        _waypoint.Selected = 0;
        _explicitSelection = true;
        LoadDraft(_draft);
        SetEditorEnabled(true);
        _selection.Text = $"{areaChoice.Label} · nouveau repère explicite · non appliqué";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        SetStatus("Nouveau repère préparé au centre de la zone · configurez puis prévisualisez.");
        UpdateActionState();
    }

    private void LoadDraft(StudioUx2NavCDraft draft)
    {
        _updating = true;
        _name.Text = draft.Name;
        _positionX.Value = draft.Position.X;
        _positionY.Value = draft.Position.Y;
        _positionZ.Value = draft.Position.Z;
        _appearanceIcon.Text = draft.AppearanceIcon;
        _appearanceColor.Text = draft.AppearanceColor;
        _appearanceLayer.Text = draft.AppearanceLayer;
        _appearanceSize.Value = draft.AppearanceScreenSize;
        _appearanceVisible.ButtonPressed = draft.AppearanceVisible;
        _markerEnabled.ButtonPressed = draft.HasMarker;
        _markerFields.Visible = draft.HasMarker;
        _markerText.Text = draft.MarkerText;
        _markerIcon.Text = draft.MarkerIcon;
        _markerFloor.Value = draft.MarkerFloor;
        _markerCategory.Text = draft.MarkerCategory;
        _markerPrecision.Value = draft.MarkerPrecision;
        _markerPolicy.Selected = Array.IndexOf(VisibilityPolicies, draft.MarkerVisibility);
        _markerScope.Selected = Array.IndexOf(VisibilityScopes, draft.MarkerScope);
        _markerTracked.ButtonPressed = draft.MarkerTracked;
        _markerRevealed.ButtonPressed = draft.MarkerRevealed;
        _markerPersistent.ButtonPressed = draft.MarkerPersistent;
        _markerRevealEvent.Text = draft.MarkerRevealEvent;
        _markerRevealAnimation.Text = draft.MarkerRevealAnimation;
        _updating = false;
        RenderPreview(null);
    }

    private StudioUx2NavCDraft? CollectDraft()
    {
        if (_draft is null)
        {
            return null;
        }
        if (_markerEnabled.ButtonPressed && (!double.IsFinite(_markerFloor.Value) ||
            Math.Abs(_markerFloor.Value - Math.Round(_markerFloor.Value)) > .000001 ||
            _markerFloor.Value is < int.MinValue or > int.MaxValue))
        {
            SetStatus("L’étage doit être un nombre entier ; aucune valeur n’a été arrondie.", true);
            return null;
        }
        var policyIndex = Math.Clamp(_markerPolicy.Selected, 0, VisibilityPolicies.Length - 1);
        var scopeIndex = Math.Clamp(_markerScope.Selected, 0, VisibilityScopes.Length - 1);
        return _draft with
        {
            Name = _name.Text.Trim(),
            Position = new((float)_positionX.Value, (float)_positionY.Value, (float)_positionZ.Value),
            AppearanceIcon = _appearanceIcon.Text.Trim(),
            AppearanceColor = _appearanceColor.Text.Trim(),
            AppearanceLayer = _appearanceLayer.Text.Trim(),
            AppearanceVisible = _appearanceVisible.ButtonPressed,
            AppearanceScreenSize = _appearanceSize.Value,
            HasMarker = _markerEnabled.ButtonPressed,
            MarkerText = _markerText.Text.Trim(),
            MarkerIcon = _markerIcon.Text.Trim(),
            MarkerFloor = (int)_markerFloor.Value,
            MarkerCategory = _markerCategory.Text.Trim(),
            MarkerPrecision = _markerPrecision.Value,
            MarkerVisibility = VisibilityPolicies[policyIndex],
            MarkerScope = VisibilityScopes[scopeIndex],
            MarkerTracked = _markerTracked.ButtonPressed,
            MarkerRevealed = _markerRevealed.ButtonPressed,
            MarkerRevealEvent = _markerRevealEvent.Text.Trim(),
            MarkerRevealAnimation = _markerRevealAnimation.Text.Trim(),
            MarkerPersistent = _markerPersistent.ButtonPressed,
        };
    }

    private void Preview()
    {
        var draft = CollectDraft();
        if (draft is null)
        {
            SetStatus("Choisissez explicitement un repère avant l’aperçu.", true);
            return;
        }
        _draft = draft;
        _lastPreview = _session.PreviewWaypointUx2NavC(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderPreview(_lastPreview);
        if (_lastPreview.Succeeded)
        {
            _worldViewport.ShowWaypointAppearanceOverlayUx2NavC(_lastPreview.AppearanceMarkers);
        }
        else
        {
            _worldViewport.ClearWaypointAppearanceOverlayUx2NavC();
        }
        _apply.Disabled = !_lastPreview.CanApply;
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActionState();
    }

    private void Apply()
    {
        var selectedId = _draft?.WaypointId;
        var areaId = _draft?.AreaId;
        var result = _session.ApplyApprovedWaypointUx2NavC();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            _canonicalPreviewReady = false;
            return;
        }
        NavigationChanged?.Invoke();
        if (areaId is not null && selectedId is not null)
        {
            RefreshAfterMutation(areaId, selectedId.Value);
        }
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Succeeded ? "Modification annulée." : result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            NavigationChanged?.Invoke();
            RefreshCurrentAreaAfterHistory();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Succeeded ? "Modification rétablie." : result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            NavigationChanged?.Invoke();
            RefreshCurrentAreaAfterHistory();
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Succeeded ? "Module sauvegardé avec ses repères." : result.Message,
            !result.Succeeded);
        UpdateActionState();
    }

    private void RefreshAfterMutation(Forge.Domain.Areas.AreaId areaId, Guid waypointId)
    {
        var areaIndex = _areaChoices.ToList().FindIndex(item => item.AreaId == areaId);
        PopulateAreas();
        if (areaIndex < 0)
        {
            ClearSelection();
            return;
        }
        _area.Selected = areaIndex + 1;
        SelectArea(areaIndex + 1);
        var waypointIndex = _waypointChoices.ToList().FindIndex(item => item.WaypointId == waypointId);
        if (waypointIndex >= 0)
        {
            _waypoint.Selected = waypointIndex + 1;
            SelectWaypoint(waypointIndex + 1);
        }
    }

    private void RefreshCurrentAreaAfterHistory()
    {
        var areaId = _draft?.AreaId;
        var waypointId = _draft?.WaypointId;
        if (areaId is not null && waypointId is not null)
        {
            RefreshAfterMutation(areaId, waypointId.Value);
        }
        else
        {
            PopulateAreas();
            ClearSelection();
        }
    }

    private void ClearSelection()
    {
        _updating = true;
        _waypoint.Clear();
        _waypoint.AddItem("Choisir un repère…");
        _waypoint.Selected = 0;
        _waypointChoices = [];
        _draft = null;
        _lastPreview = null;
        _explicitSelection = false;
        _canonicalPreviewReady = false;
        _updating = false;
        _selection.Text =
            "Choisissez explicitement une zone puis un repère ; aucune fiche n’est chargée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        _session.CancelWaypointUx2NavC();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _apply.Disabled = true;
        _markerFields.Visible = _markerEnabled.ButtonPressed;
        _worldViewport.ClearWaypointAppearanceOverlayUx2NavC();
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec.";
        _revealPreview.Text = "Révélation : aucune politique n’est simulée avant validation.";
        ClearChildren(_diff);
        ClearChildren(_diagnostics);
    }

    private void RenderPreview(StudioUx2NavCPreview? preview)
    {
        ClearChildren(_diff);
        ClearChildren(_diagnostics);
        if (preview is null)
        {
            _summary.Text = "Aucun aperçu calculé.";
            _revealPreview.Text = "Révélation : l’aperçu n’émet aucun événement runtime.";
            return;
        }
        foreach (var item in preview.Diff)
        {
            _diff.AddChild(StudioStyle.Label($"• {item}", 9, StudioStyle.Text, wrap: true));
        }
        if (preview.Diff.Count == 0)
        {
            _diff.AddChild(StudioStyle.Label("Aucune différence.", 9, StudioStyle.Muted));
        }
        foreach (var item in preview.Diagnostics)
        {
            _diagnostics.AddChild(StudioStyle.Label(
                $"{item.Code} · {item.Path}\n{item.Message}", 9, StudioStyle.Danger, wrap: true));
        }
        if (preview.Diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 9, StudioStyle.Success));
        }
        _summary.Text = preview.Summary;
        _summary.AddThemeColorOverride("font_color", preview.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _revealPreview.Text = preview.MarkerVisibleBeforeReveal == preview.MarkerVisibleAfterReveal
            ? $"Visibilité canonique : {(preview.MarkerVisibleBeforeReveal ? "visible" : "masquée")} · aucun événement émis."
            : "Visibilité canonique : masquée avant l’événement, visible après révélation · simulation pure, état non persisté.";
        _revealPreview.AddThemeColorOverride("font_color", StudioStyle.Accent);
    }

    private void WireDraftSignals()
    {
        foreach (var lineEdit in new[]
                 {
                     _name, _appearanceIcon, _appearanceColor, _appearanceLayer, _markerText,
                     _markerIcon, _markerCategory, _markerRevealEvent, _markerRevealAnimation,
                 })
        {
            lineEdit.TextChanged += _ => DraftChanged();
        }
        foreach (var spin in new[]
                 {
                     _positionX, _positionY, _positionZ, _appearanceSize, _markerFloor, _markerPrecision,
                 })
        {
            spin.ValueChanged += _ => DraftChanged();
        }
        _appearanceVisible.Toggled += _ => DraftChanged();
        _markerEnabled.Toggled += enabled =>
        {
            _markerFields.Visible = enabled;
            DraftChanged();
        };
        _markerPolicy.ItemSelected += _ => DraftChanged();
        _markerScope.ItemSelected += _ => DraftChanged();
        _markerTracked.Toggled += _ => DraftChanged();
        _markerRevealed.Toggled += _ => DraftChanged();
        _markerPersistent.Toggled += _ => DraftChanged();
    }

    private void SetEditorEnabled(bool enabled)
    {
        _editor.Visible = enabled;
        _preview.Disabled = !enabled;
    }

    private void UpdateActionState()
    {
        _addWaypoint.Disabled = _area.Selected <= 0 || _area.Selected > _areaChoices.Count;
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null || !_session.IsDirty;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private StudioUx2NavCEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History!;
        return new StudioUx2NavCEvidenceSourceSnapshot(_sourceSession.Project!, history,
            history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo,
            history.CanRedo, _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureWaypointApprovalUx2NavC());
    }

    private bool SourceMatchesSnapshot(StudioUx2NavCEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureWaypointApprovalUx2NavC() == snapshot.Approval;
    }

    private void ReleaseEvidenceWorkspace()
    {
        if (_evidenceSession is null && _evidenceTempRoot is null)
        {
            _session = _sourceSession;
            _lastSourceRestored = true;
            return;
        }
        try
        {
            _evidenceSession?.Close();
        }
        finally
        {
            _evidenceSession = null;
            var root = _evidenceTempRoot;
            _evidenceTempRoot = null;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased = StudioSessionController.DeleteUx2NavCTemporaryWorkspace(root);
            _lastEvidenceSourceUntouched = _evidenceSourceSnapshot is null ||
                                           SourceMatchesSnapshot(_evidenceSourceSnapshot);
            _lastSourceRestored = true;
        }
    }

    private void BindViewportToArea(StudioSessionController session, Forge.Domain.Areas.AreaId areaId)
    {
        try
        {
            _worldViewport.RebuildArea(session, areaId, readOnly: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_C_VIEWPORT_FALLBACK {exception.Message}");
        }
    }

    private void RestoreSourceViewport()
    {
        try
        {
            if (_sourceSession.ActiveAreaId is { } activeArea)
            {
                _worldViewport.RebuildArea(_sourceSession, activeArea, readOnly: false, frameArea: false);
                _worldViewport.ApplyCameraProfile(_sourceSession.CameraProfile);
            }
            else
            {
                _worldViewport.ShowNoArea(_sourceSession);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_C_VIEWPORT_RESTORE_FAILED {exception.Message}");
        }
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0)
        {
            return;
        }
        var scale = viewport.X >= 3_200 ? 1.5f : viewport.X >= 2_300 ? 1.15f : 1f;
        var width = Math.Clamp((viewport.X * .45f) / scale, 720f, 920f);
        var height = Math.Clamp((viewport.Y - 30f) / scale, 700f, 1_180f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(viewport.X - width * scale - 15f,
            Math.Max(15f, (viewport.Y - height * scale) / 2f));
    }

    private static string HumanPolicy(MarkerVisibilityPolicy policy) => policy switch
    {
        MarkerVisibilityPolicy.Always => "Toujours visible",
        MarkerVisibilityPolicy.HiddenUntilRevealed => "Masquée jusqu’à révélation",
        MarkerVisibilityPolicy.WhileTracked => "Visible pendant le suivi",
        _ => "Héritée",
    };

    private static string HumanScope(MarkerVisibilityScope scope) => scope switch
    {
        MarkerVisibilityScope.Player => "Joueur",
        MarkerVisibilityScope.Party => "Groupe",
        MarkerVisibilityScope.World => "Monde",
        _ => "Héritée",
    };

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.Free();
        }
    }

    private static IEnumerable<Label> DescendantLabelsUx2NavC(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label)
            {
                yield return label;
            }
            foreach (var descendant in DescendantLabelsUx2NavC(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool ContainsUuidUx2NavC(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}

internal sealed partial class StudioWorldViewport
{
    private Node3D? _ux2NavCWaypointAppearanceOverlay;

    internal int WaypointAppearanceMarkerCountUx2NavC { get; private set; }

    internal void ShowWaypointAppearanceOverlayUx2NavC(
        IReadOnlyList<StudioUx2NavCAppearanceMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        ClearWaypointAppearanceOverlayUx2NavC();
        var visible = markers.Where(item => item.Visible).ToArray();
        if (visible.Length == 0)
        {
            return;
        }
        var root = new Node3D { Name = "Ux2NavCWaypointAppearanceOverlay" };
        foreach (var marker in visible)
        {
            var color = new Color(marker.Color.TrimStart('#'));
            root.AddChild(new Label3D
            {
                Name = $"WaypointAppearance{WaypointAppearanceMarkerCountUx2NavC + 1}",
                Text = $"{IconGlyphUx2NavC(marker.Icon)}  {marker.HumanLabel}\n{marker.Layer}",
                Position = new Vector3((float)marker.Position.X, (float)marker.Position.Y + 1.2f,
                    (float)marker.Position.Z),
                Modulate = color,
                OutlineModulate = new Color("101410"),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FixedSize = true,
                NoDepthTest = true,
                FontSize = (int)Math.Round(Math.Clamp(marker.ScreenSize, 8, 96)),
                OutlineSize = marker.Selected ? 10 : 6,
            });
            WaypointAppearanceMarkerCountUx2NavC++;
        }
        _world.AddChild(root);
        _ux2NavCWaypointAppearanceOverlay = root;
    }

    internal void ClearWaypointAppearanceOverlayUx2NavC()
    {
        WaypointAppearanceMarkerCountUx2NavC = 0;
        ClearNavigationOverlayUx2NavB();
        if (_ux2NavCWaypointAppearanceOverlay is null ||
            !GodotObject.IsInstanceValid(_ux2NavCWaypointAppearanceOverlay))
        {
            _ux2NavCWaypointAppearanceOverlay = null;
            return;
        }
        _ux2NavCWaypointAppearanceOverlay.GetParent()?.RemoveChild(_ux2NavCWaypointAppearanceOverlay);
        _ux2NavCWaypointAppearanceOverlay.Free();
        _ux2NavCWaypointAppearanceOverlay = null;
    }

    private static string IconGlyphUx2NavC(string icon) => icon.Trim().ToLowerInvariant() switch
    {
        "flag" => "⚑",
        "flame" or "ember" => "✦",
        "arrow" or "path" => "➜",
        "beacon" => "✧",
        "pin" or "marker" => "◆",
        _ => "◇",
    };
}
