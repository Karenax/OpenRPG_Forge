using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed class StudioDoorDestinationListEditor : VBoxContainer
{
    private readonly Label _empty;
    private readonly VBoxContainer _rows;
    private readonly List<Guid> _values = [];
    private IReadOnlyDictionary<Guid, StudioDoorDestinationSuggestion> _suggestions =
        new Dictionary<Guid, StudioDoorDestinationSuggestion>();
    private bool _editingEnabled;

    internal event Action? Changed;

    internal IReadOnlyList<Guid> Values => _values.ToArray();

    internal bool HasProtectedValue => _values.Any(id =>
        _suggestions.TryGetValue(id, out var suggestion) && suggestion.Protected);

    internal StudioDoorDestinationListEditor()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 4);
        _empty = StudioStyle.Label(
            "Aucune destination connue. Ajoutez une suggestion réelle ci-dessous.",
            9,
            StudioStyle.Muted,
            wrap: true);
        AddChild(_empty);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 3);
        AddChild(_rows);
    }

    internal void SetSuggestions(IReadOnlyList<StudioDoorDestinationSuggestion> suggestions)
    {
        _suggestions = suggestions
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());
        Render();
    }

    internal void SetValues(IEnumerable<Guid> values)
    {
        _values.Clear();
        _values.AddRange(values.Where(item => item != Guid.Empty).Distinct());
        Render();
    }

    internal bool AddSuggestion(StudioDoorDestinationSuggestion suggestion)
    {
        if (_values.Contains(suggestion.Id))
        {
            return false;
        }
        _values.Add(suggestion.Id);
        Render();
        Changed?.Invoke();
        return true;
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        Render();
    }

    private void Render()
    {
        foreach (var child in _rows.GetChildren())
        {
            child.QueueFree();
        }
        _empty.Visible = _values.Count == 0;
        foreach (var id in _values)
        {
            var suggestion = _suggestions.TryGetValue(id, out var known)
                ? known
                : new StudioDoorDestinationSuggestion(
                    id,
                    "Destination qualifiée existante",
                    "Politique enregistrée",
                    false);
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(StudioStyle.Label(
                $"{suggestion.Label} · source : {suggestion.Provenance}",
                10,
                suggestion.Protected ? StudioStyle.Accent : StudioStyle.Text,
                wrap: true));
            var remove = StudioStyle.Button(suggestion.Protected ? "Héritée" : "Retirer", compact: true);
            remove.Disabled = !_editingEnabled || suggestion.Protected;
            remove.TooltipText = suggestion.Protected
                ? "Destination héritée de la fondation : elle reste protégée tant qu’elle figure dans la politique."
                : "Retirer cette destination du brouillon.";
            remove.Pressed += () =>
            {
                if (!_editingEnabled || suggestion.Protected || !_values.Remove(id))
                {
                    return;
                }
                Render();
                Changed?.Invoke();
            };
            row.AddChild(remove);
            _rows.AddChild(row);
        }
    }
}

internal sealed class StudioDoorSuggestionPicker : VBoxContainer
{
    private readonly OptionButton _options;
    private readonly Button _add;
    private IReadOnlyList<StudioDoorValidationSuggestion> _suggestions = [];

    internal event Action<StudioDoorValidationSuggestion>? SuggestionChosen;

    internal bool HasSuggestions => _suggestions.Count > 0;

    internal StudioDoorSuggestionPicker(string emptyLabel)
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 4);
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 5);
        AddChild(row);
        _options = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_options);
        _options.AddItem(emptyLabel);
        row.AddChild(_options);
        _add = StudioStyle.Button("Ajouter", compact: true);
        _add.Disabled = true;
        _add.Pressed += AddSelected;
        row.AddChild(_add);
    }

    internal void SetSuggestions(IReadOnlyList<StudioDoorValidationSuggestion> suggestions)
    {
        _suggestions = suggestions;
        _options.Clear();
        _options.AddItem(_suggestions.Count == 0
            ? "Aucune source réelle disponible"
            : "Choisissez une source réelle…");
        foreach (var suggestion in _suggestions)
        {
            _options.AddItem($"{suggestion.Label} · source : {suggestion.Provenance}");
        }
        _options.Select(0);
        _add.Disabled = _suggestions.Count == 0;
    }

    internal void SetEditingEnabled(bool enabled) =>
        _add.Disabled = !enabled || _suggestions.Count == 0;

    internal bool AddFirstForEvidence()
    {
        if (_suggestions.Count == 0)
        {
            return false;
        }
        SuggestionChosen?.Invoke(_suggestions[0]);
        return true;
    }

    private void AddSelected()
    {
        var index = _options.Selected - 1;
        if (index < 0 || index >= _suggestions.Count)
        {
            return;
        }
        SuggestionChosen?.Invoke(_suggestions[index]);
    }
}

internal sealed partial class StudioDoorValidationOverlay : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx1AJEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _door;
    private readonly Label _selection;
    private readonly CheckBox _validationEnabled;
    private readonly CheckBox _requiredAtRuntime;
    private readonly StudioDoorDestinationListEditor _destinations;
    private readonly OptionButton _destinationSuggestions;
    private readonly Button _addDestination;
    private readonly StudioTriggerStringListEditor _animations;
    private readonly StudioTriggerStringListEditor _collisions;
    private readonly StudioTriggerStringListEditor _credentials;
    private readonly StudioTriggerStringListEditor _scripts;
    private readonly StudioDoorSuggestionPicker _animationSuggestions;
    private readonly StudioDoorSuggestionPicker _collisionSuggestions;
    private readonly StudioDoorSuggestionPicker _credentialSuggestions;
    private readonly StudioDoorSuggestionPicker _scriptSuggestions;
    private readonly Label _authoredCollision;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;

    private IReadOnlyList<StudioDoorValidationChoice> _choices = [];
    private StudioDoorValidationDraft? _draft;
    private StudioDoorValidationSuggestions _suggestions = new([], [], [], [], [], null);
    private IReadOnlyList<StudioDoorValidationDiagnosticGroup> _lastGroups = [];
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? DoorChanged;

    internal bool IsOpen => Visible;

    internal int ChoiceCount => _choices.Count;

    internal bool HasExplicitSelection => _draft is not null && _door.Selected > 0;

    internal bool CanonicalPreviewReady => _canonicalPreviewReady;

    internal bool ShowsGroupedDiagnostics => _lastGroups.Count == 7 &&
                                              _lastGroups.Select(item => item.Key).Distinct().Count() == 7;

    internal bool SuggestionsReady => _suggestions.Destinations.Count > 0 &&
                                      _animationSuggestions.HasSuggestions &&
                                      _collisionSuggestions.HasSuggestions &&
                                      _credentialSuggestions.HasSuggestions &&
                                      _scriptSuggestions.HasSuggestions &&
                                      _suggestions.AuthoredCollision is not null;

    internal bool ProtectedDestinations => _destinations.HasProtectedValue;

    internal bool UsesIsolatedEvidence =>
        _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);

    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;

    internal bool EvidenceResourcesReleased =>
        _evidenceSession is null && _evidenceTempRoot is null && _lastEvidenceResourcesReleased;

    internal bool SourceSessionRestored =>
        ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ',
                _selection.Text,
                _summary.Text,
                _status.Text,
                _authoredCollision.Text,
                "Destinations Animations Collision Identifiants d’accès Scripts Validation canonique");
            return HasExplicitSelection &&
                   text.Contains("Validation canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Destinations", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Animations", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Collision", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Scripts", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("module/worldFeatures", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioDoorValidationOverlay(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioDoorValidationOverlay";
        Visible = false;
        ZIndex = 455;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .8f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("DoorValidationDialog", StudioStyle.Chrome, 12, 2);
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
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("VALIDATION CANONIQUE", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Qualifier le contexte runtime d’une porte", 20));
        heading.AddChild(StudioStyle.Label(
            "ORF-DOOR-021 · verdict canonique et commande projet · aucune simulation moteur, réseau ou navigation",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer();
        selector.AddThemeConstantOverride("separation", 8);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Porte à qualifier", 10, StudioStyle.Muted));
        _door = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_door);
        _door.ItemSelected += SelectDoor;
        selector.AddChild(_door);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une porte dotée d’une fondation et d’une navigation.",
            11,
            StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        var policy = new HBoxContainer();
        policy.AddThemeConstantOverride("separation", 14);
        root.AddChild(policy);
        _validationEnabled = Check("Activer le contexte canonique");
        policy.AddChild(_validationEnabled);
        _requiredAtRuntime = Check("Exiger ce contexte au runtime");
        policy.AddChild(_requiredAtRuntime);
        policy.AddChild(StudioStyle.Label(
            "Absent et optionnel = Non évalué ; absent et requis = blocage canonique.",
            10,
            StudioStyle.Muted,
            wrap: true));

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 430),
            CustomMaximumSize = new Vector2(-1, 650),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var body = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(body);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        body.AddChild(content);

        var lists = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lists.AddThemeConstantOverride("h_separation", 10);
        lists.AddThemeConstantOverride("v_separation", 10);
        content.AddChild(lists);

        _destinations = new StudioDoorDestinationListEditor();
        _destinationSuggestions = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_destinationSuggestions);
        _addDestination = StudioStyle.Button("Ajouter", compact: true);
        _addDestination.Pressed += AddDestinationSuggestion;
        lists.AddChild(DestinationCard());

        _animations = new StudioTriggerStringListEditor("Aucune animation connue.", "Ex. animation:door-open");
        _animationSuggestions = new StudioDoorSuggestionPicker("Aucune animation suggérée");
        _animationSuggestions.SuggestionChosen += suggestion => AddStringSuggestion(_animations, suggestion);
        lists.AddChild(ListCard(
            "KnownDoorAnimationsCard",
            "Animations connues",
            "États et animation de refus réellement authorés.",
            _animations,
            _animationSuggestions));

        _collisions = new StudioTriggerStringListEditor("Aucun profil collision connu.", "Ex. collision:stone-gate");
        _collisionSuggestions = new StudioDoorSuggestionPicker("Aucune collision suggérée");
        _collisionSuggestions.SuggestionChosen += suggestion => AddStringSuggestion(_collisions, suggestion);
        _authoredCollision = StudioStyle.Label(
            "Collision authorée · Non disponible",
            9,
            StudioStyle.Warning,
            wrap: true);
        var collisionCard = ListCard(
            "KnownDoorCollisionsCard",
            "Collisions connues",
            "Catalogue connu distinct de la collision authorée par la fondation.",
            _collisions,
            _collisionSuggestions,
            _authoredCollision);
        lists.AddChild(collisionCard);

        _credentials = new StudioTriggerStringListEditor("Aucun identifiant d’accès connu.", "Ex. key:gate");
        _credentialSuggestions = new StudioDoorSuggestionPicker("Aucun accès suggéré");
        _credentialSuggestions.SuggestionChosen += suggestion => AddStringSuggestion(_credentials, suggestion);
        lists.AddChild(ListCard(
            "KnownDoorCredentialsCard",
            "Identifiants d’accès connus",
            "Identifiants provenant de politiques d’accès réellement authorées.",
            _credentials,
            _credentialSuggestions));

        _scripts = new StudioTriggerStringListEditor("Aucun script connu.", "Ex. scripts/door_open");
        _scriptSuggestions = new StudioDoorSuggestionPicker("Aucun script suggéré");
        _scriptSuggestions.SuggestionChosen += suggestion => AddStringSuggestion(_scripts, suggestion);
        lists.AddChild(ListCard(
            "KnownDoorScriptsCard",
            "Scripts connus",
            "Événements de porte, refus et routages existants.",
            _scripts,
            _scriptSuggestions));

        var diagnosticsCard = StudioStyle.Panel("DoorValidationDiagnostics", StudioStyle.Surface, 8, 1);
        var diagnosticsMargin = StudioStyle.Margin(12, 10, 12, 10);
        diagnosticsCard.AddChild(diagnosticsMargin);
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics.AddThemeConstantOverride("separation", 5);
        diagnosticsMargin.AddChild(_diagnostics);
        content.AddChild(diagnosticsCard);

        _summary = StudioStyle.Label(
            "Sélectionnez une porte puis prévisualisez · aucun changement appliqué.",
            11,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_summary);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("Prévisualiser", primary: true);
        _preview.TooltipText = "Interroger le validateur canonique sans modifier le projet.";
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", primary: true);
        _apply.TooltipText = "Disponible seulement après un aperçu canonique réussi.";
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        footer.AddChild(StudioStyle.Label(
            "Seuls la politique de validation et son caractère requis sont modifiés.",
            10,
            StudioStyle.Muted,
            wrap: true));

        ConnectInvalidation();
        SetEditingEnabled(false);
        Resized += UpdateDialogBounds;
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void OpenBoundSession()
    {
        _session.CancelDoorValidationUx1AJ();
        _choices = _session.DoorValidationChoicesUx1AJ;
        _door.Clear();
        _door.AddItem("Choisissez explicitement une porte…");
        foreach (var choice in _choices)
        {
            _door.AddItem(choice.Label);
        }
        _door.Select(0);
        _draft = null;
        _lastGroups = [];
        _applied = false;
        _canonicalPreviewReady = false;
        ResetDraftControls();
        _selection.Text = _choices.Count == 0
            ? "Aucun profil de porte n’est disponible dans ce module."
            : "Choisissez explicitement une porte dotée d’une fondation et d’une navigation.";
        _selection.AddThemeColorOverride(
            "font_color",
            _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez une porte puis prévisualisez · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _status.Text = "Prêt · sélection explicite requise";
        _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
        RenderDiagnosticGroups([]);
        SetEditingEnabled(false);
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        MoveToFront();
        UpdateDialogBounds();
        _door.GrabFocus();
    }

    internal void Close()
    {
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _session.CancelDoorValidationUx1AJ();
        Visible = false;
        _draft = null;
        _lastGroups = [];
        _applied = false;
        _canonicalPreviewReady = false;
        ResetDraftControls();
        var released = ReleaseIsolatedEvidenceSession();
        _lastEvidenceSourceUntouched = sourceUntouched;
        _lastEvidenceResourcesReleased = released;
        _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        if ((_evidenceSession is not null || _evidenceTempRoot is not null) &&
            !ReleaseIsolatedEvidenceSession())
        {
            return false;
        }
        if (!TryOpenIsolatedEvidenceSession())
        {
            return false;
        }
        var index = _choices.ToList().FindIndex(item =>
            item.IsEvidence && item.HasFoundation && item.HasNavigation);
        if (index < 0)
        {
            Close();
            return false;
        }
        _door.Select(index + 1);
        LoadChoice(index);
        if (_draft is null || !ConfigureEvidenceDraft())
        {
            Close();
            return false;
        }
        Preview();
        _selection.Text += " · session de preuve isolée";
        _summary.Text = "PREUVE ISOLÉE · le projet source reste strictement inchangé.\n" + _summary.Text;
        SetStatus("Preuve isolée prête · fermeture = suppression de la session temporaire", error: false);
        return !_apply.Disabled && HasExplicitSelection && CanonicalPreviewReady && ShowsGroupedDiagnostics &&
               ShowsHumanSurface && SuggestionsReady && ProtectedDestinations && UsesIsolatedEvidence &&
               EvidenceSourceUntouched;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        StudioSessionController? reopened = null;
        try
        {
            var ready = PrepareEvidence("ux1-aj-door-validation");
            var transactionSession = _session;
            var transactionHistory = transactionSession.History;
            var originalProject = transactionSession.Project;
            var originalRevision = transactionHistory?.State.Revision;
            var originalJournal = transactionHistory?.Journal.ToArray() ?? [];
            var originalCanUndo = transactionHistory?.CanUndo;
            var originalCanRedo = transactionHistory?.CanRedo;
            var originalDirty = transactionSession.IsDirty;
            var targetId = _draft?.DoorProfileId;
            var expectedDraft = ReadDraft();
            var dryRun = ready && targetId.HasValue && expectedDraft is not null && originalProject is not null &&
                         ReferenceEquals(originalProject, transactionSession.Project) &&
                         ReferenceEquals(transactionHistory, transactionSession.History) &&
                         transactionHistory?.State.Revision == originalRevision &&
                         (transactionHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                         transactionHistory?.CanUndo == originalCanUndo &&
                         transactionHistory?.CanRedo == originalCanRedo &&
                         transactionSession.IsDirty == originalDirty && UsesIsolatedEvidence &&
                         SourceMatchesSnapshot(sourceSnapshot);
            Apply();
            var appliedProject = transactionSession.Project;
            var applied = dryRun && _applied && !ReferenceEquals(originalProject, appliedProject);
            if (applied)
            {
                Undo();
            }
            var undone = applied && ReferenceEquals(originalProject, transactionSession.Project) &&
                         transactionSession.IsDirty == originalDirty;
            if (undone)
            {
                Redo();
            }
            var redone = undone && ReferenceEquals(appliedProject, transactionSession.Project);
            var saved = redone && transactionSession.Save().Succeeded;
            var persisted = false;
            if (saved && transactionSession.Session is { } workspace && targetId is { } profileId &&
                expectedDraft is not null)
            {
                reopened = new StudioSessionController();
                if (reopened.Open(workspace.RootDirectory).Succeeded)
                {
                    var profile = reopened.Project?.Module.WorldFeatures.Doors
                        .SingleOrDefault(item => item.Id == profileId);
                    persisted = profile?.Validation is { } validation &&
                                SameValidation(expectedDraft, validation) &&
                                profile.ValidationRequiredAtRuntime == expectedDraft.ValidationRequiredAtRuntime;
                }
            }
            var sourceUntouched = SourceMatchesSnapshot(sourceSnapshot);
            reopened?.Close();
            reopened = null;
            Close();
            return dryRun && applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AJ_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (Visible || _evidenceSession is not null || _evidenceTempRoot is not null)
            {
                Close();
            }
        }
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx1AJEvidenceProject();
        var evidenceManifest = _sourceSession.CreateUx1AJEvidenceManifest();
        if (snapshot is null || evidenceProject is null || evidenceManifest is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1aj-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            var manifestSaved = created.Succeeded &&
                                AssetPackageManifestStore.Save(tempRoot, [evidenceManifest]).Succeeded;
            if (!manifestSaved || !evidenceSession.Open(tempRoot).Succeeded)
            {
                evidenceSession.Close();
                _lastEvidenceResourcesReleased = DeleteEvidenceRoot(tempRoot);
                _evidenceTempRoot = _lastEvidenceResourcesReleased ? null : tempRoot;
                _session = _sourceSession;
                _lastSourceRestored = true;
                return false;
            }
            _evidenceSourceSnapshot = snapshot;
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            _lastEvidenceResourcesReleased = false;
            _lastSourceRestored = false;
            OpenBoundSession();
            var ready = _choices.Any(item => item.IsEvidence && item.HasFoundation && item.HasNavigation) &&
                        SourceMatchesSnapshot(snapshot);
            if (!ready)
            {
                Close();
            }
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AJ_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            evidenceSession.Close();
            var released = DeleteEvidenceRoot(tempRoot);
            _evidenceSourceSnapshot = null;
            _evidenceSession = null;
            _evidenceTempRoot = released ? null : tempRoot;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased = released;
            _lastSourceRestored = true;
            return false;
        }
    }

    private StudioUx1AJEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new(
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
            _sourceSession.CaptureDoorValidationApprovalUx1AJ());
    }

    private bool SourceMatchesSnapshot(StudioUx1AJEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureDoorValidationApprovalUx1AJ() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceSourceSnapshot = null;
        _session = _sourceSession;
        var released = true;
        try
        {
            evidenceSession?.Close();
        }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AJ_EVIDENCE_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = DeleteEvidenceRoot(tempRoot);
        if (!rootReleased)
        {
            _evidenceTempRoot = tempRoot;
        }
        return released && rootReleased;
    }

    private static bool DeleteEvidenceRoot(string? tempRoot)
    {
        if (tempRoot is null)
        {
            return true;
        }
        try
        {
            var fullRoot = Path.GetFullPath(tempRoot);
            var tempBase = Path.GetFullPath(Path.GetTempPath());
            var leaf = Path.GetFileName(fullRoot);
            if (!fullRoot.StartsWith(tempBase, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux1aj-evidence-", StringComparison.Ordinal))
            {
                return false;
            }
            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, recursive: true);
            }
            return !Directory.Exists(fullRoot);
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AJ_EVIDENCE_DELETE_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
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
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
    }

    private void SelectDoor(long index)
    {
        if (_updating)
        {
            return;
        }
        var choiceIndex = (int)index - 1;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            ClearSelection();
            return;
        }
        LoadChoice(choiceIndex);
    }

    private void ClearSelection()
    {
        _draft = null;
        _suggestions = new([], [], [], [], [], null);
        _lastGroups = [];
        _applied = false;
        _canonicalPreviewReady = false;
        _session.CancelDoorValidationUx1AJ();
        ResetDraftControls();
        _selection.Text = "Choisissez explicitement une porte dotée d’une fondation et d’une navigation.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _summary.Text = "Sélectionnez une porte puis prévisualisez · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _status.Text = "Prêt · sélection explicite requise";
        _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
        RenderDiagnosticGroups([]);
        SetEditingEnabled(false);
        _undo.Disabled = true;
        _redo.Disabled = true;
    }

    private void LoadChoice(int choiceIndex)
    {
        var choice = _choices[choiceIndex];
        _session.CancelDoorValidationUx1AJ();
        _draft = _session.CreateDoorValidationDraftUx1AJ(choice.ProfileId);
        _lastGroups = [];
        _applied = false;
        _canonicalPreviewReady = false;
        _undo.Disabled = true;
        _redo.Disabled = true;
        if (_draft is null)
        {
            _selection.Text = !choice.HasFoundation
                ? "Fondation requise · ouvrez d’abord l’outil Fondation de porte."
                : !choice.HasNavigation
                    ? "Navigation authorée requise · ouvrez d’abord l’outil Fondation de porte."
                    : "La porte a changé. Rechargez sa fiche.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            _summary.Text =
                "Cette fiche ne crée ni fondation ni navigation et ne promeut pas ORF-DOOR-019.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            ResetDraftControls();
            RenderDiagnosticGroups([]);
            SetEditingEnabled(false);
            SetStatus("Prérequis canoniques absents · aucune mutation disponible", error: true);
            return;
        }

        _suggestions = _session.GetDoorValidationSuggestionsUx1AJ(choice.ProfileId);
        Populate(_draft);
        PopulateSuggestions();
        _selection.Text = choice.HasValidation
            ? choice.ValidationRequiredAtRuntime
                ? "Contexte configuré et requis · prévisualisez avant toute modification."
                : "Contexte configuré et optionnel · prévisualisez avant toute modification."
            : "Contexte absent · Non évalué tant qu’il reste optionnel.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        _summary.Text = "Brouillon chargé · aucune suggestion n’a été ajoutée automatiquement.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        RenderDiagnosticGroups([]);
        SetEditingEnabled(true);
        SetStatus("Brouillon prêt · aperçu canonique requis", error: false);
    }

    private void Populate(StudioDoorValidationDraft draft)
    {
        _updating = true;
        _validationEnabled.ButtonPressed = draft.ValidationEnabled;
        _requiredAtRuntime.ButtonPressed = draft.ValidationRequiredAtRuntime;
        _destinations.SetValues(draft.KnownDestinationIds);
        _animations.SetValues(draft.KnownAnimationIds);
        _collisions.SetValues(draft.KnownCollisionProfileIds);
        _credentials.SetValues(draft.KnownCredentialIds);
        _scripts.SetValues(draft.KnownScriptIds);
        _updating = false;
        UpdateValidationControls();
    }

    private void PopulateSuggestions()
    {
        _destinations.SetSuggestions(_suggestions.Destinations);
        _destinationSuggestions.Clear();
        _destinationSuggestions.AddItem(_suggestions.Destinations.Count == 0
            ? "Aucune destination réelle disponible"
            : "Choisissez une destination réelle…");
        foreach (var suggestion in _suggestions.Destinations)
        {
            _destinationSuggestions.AddItem($"{suggestion.Label} · source : {suggestion.Provenance}");
        }
        _destinationSuggestions.Select(0);
        _animationSuggestions.SetSuggestions(_suggestions.Animations);
        _collisionSuggestions.SetSuggestions(_suggestions.CollisionProfiles);
        _credentialSuggestions.SetSuggestions(_suggestions.Credentials);
        _scriptSuggestions.SetSuggestions(_suggestions.Scripts);
        _authoredCollision.Text = _suggestions.AuthoredCollision is { } authored
            ? $"Collision authorée séparément · {authored.Label} · source : {authored.Provenance}"
            : $"Collision authorée séparément · override {DoorWorldProfile.CollisionProfileOverrideKey} absent";
        _authoredCollision.AddThemeColorOverride(
            "font_color",
            _suggestions.AuthoredCollision is null ? StudioStyle.Danger : StudioStyle.Accent);
    }

    private bool ConfigureEvidenceDraft()
    {
        var authoredCollision = _suggestions.AuthoredCollision;
        if (_draft is null || authoredCollision is null)
        {
            return false;
        }
        _updating = true;
        _validationEnabled.ButtonPressed = true;
        _requiredAtRuntime.ButtonPressed = true;
        _updating = false;
        var protectedDestination = _suggestions.Destinations.FirstOrDefault(item => item.Protected);
        if (protectedDestination is null)
        {
            return false;
        }
        _destinations.AddSuggestion(protectedDestination);
        _animations.SetValues(_suggestions.Animations.Select(item => item.Value));
        _collisions.SetValues([]);
        if (!_collisionSuggestions.AddFirstForEvidence() ||
            !_collisions.Values.Contains(authoredCollision.ProfileId, StringComparer.Ordinal))
        {
            return false;
        }
        _credentials.SetValues(_suggestions.Credentials.Select(item => item.Value));
        _scripts.SetValues(_suggestions.Scripts.Select(item => item.Value));
        UpdateValidationControls();
        InvalidatePreview();
        return true;
    }

    private void ResetDraftControls()
    {
        _updating = true;
        _validationEnabled.ButtonPressed = false;
        _requiredAtRuntime.ButtonPressed = false;
        _destinations.SetSuggestions([]);
        _destinations.SetValues([]);
        _animations.SetValues([]);
        _collisions.SetValues([]);
        _credentials.SetValues([]);
        _scripts.SetValues([]);
        _destinationSuggestions.Clear();
        _destinationSuggestions.AddItem("Aucune porte sélectionnée");
        _animationSuggestions.SetSuggestions([]);
        _collisionSuggestions.SetSuggestions([]);
        _credentialSuggestions.SetSuggestions([]);
        _scriptSuggestions.SetSuggestions([]);
        _authoredCollision.Text = "Collision authorée · Non disponible";
        _updating = false;
        UpdateValidationControls();
    }

    private StudioDoorValidationDraft? ReadDraft()
    {
        if (_draft is null)
        {
            return null;
        }
        return _draft with
        {
            ValidationEnabled = _validationEnabled.ButtonPressed,
            ValidationRequiredAtRuntime = _requiredAtRuntime.ButtonPressed,
            KnownDestinationIds = _destinations.Values,
            KnownAnimationIds = _animations.Values,
            KnownCollisionProfileIds = _collisions.Values,
            KnownCredentialIds = _credentials.Values,
            KnownScriptIds = _scripts.Values,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            return;
        }
        var result = _session.PreviewDoorValidationUx1AJ(draft);
        _canonicalPreviewReady = result.Succeeded;
        _apply.Disabled = !result.CanApply || !_canonicalPreviewReady;
        _lastGroups = result.Groups;
        RenderDiagnosticGroups(result.Groups);
        _summary.Text = string.IsNullOrWhiteSpace(result.Summary) ? result.Message : result.Summary;
        _summary.AddThemeColorOverride(
            "font_color",
            result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedDoorValidationUx1AJ();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            _canonicalPreviewReady = false;
            return;
        }
        _applied = true;
        _apply.Disabled = true;
        _preview.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        SetEditingEnabled(false);
        _summary.Text =
            "Appliqué · seuls le contexte de validation et son caractère requis ont changé. " +
            "Fondation, navigation, accès, événements, assemblage et profils voisins sont préservés.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        DoorChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _applied = false;
            _undo.Disabled = true;
            _redo.Disabled = false;
            DoorChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _applied = true;
            _undo.Disabled = false;
            _redo.Disabled = true;
            DoorChanged?.Invoke();
        }
    }

    private void AddDestinationSuggestion()
    {
        var index = _destinationSuggestions.Selected - 1;
        if (index < 0 || index >= _suggestions.Destinations.Count)
        {
            return;
        }
        _destinations.AddSuggestion(_suggestions.Destinations[index]);
    }

    private void AddStringSuggestion(
        StudioTriggerStringListEditor editor,
        StudioDoorValidationSuggestion suggestion)
    {
        if (!editor.Values.Contains(suggestion.Value, StringComparer.Ordinal))
        {
            editor.SetValues(editor.Values.Append(suggestion.Value));
            InvalidatePreview();
        }
    }

    private void ConnectInvalidation()
    {
        _validationEnabled.Toggled += _ =>
        {
            UpdateValidationControls();
            InvalidatePreview();
        };
        _requiredAtRuntime.Toggled += _ => InvalidatePreview();
        _destinations.Changed += InvalidatePreview;
        foreach (var editor in new[] { _animations, _collisions, _credentials, _scripts })
        {
            editor.Changed += InvalidatePreview;
        }
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null || _applied)
        {
            return;
        }
        _session.CancelDoorValidationUx1AJ();
        _canonicalPreviewReady = false;
        _lastGroups = [];
        _apply.Disabled = true;
        _preview.Disabled = false;
        RenderDiagnosticGroups([]);
        _summary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _status.Text = "Brouillon modifié · prévisualisation requise";
        _status.AddThemeColorOverride("font_color", StudioStyle.Warning);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _validationEnabled.Disabled = !enabled;
        _requiredAtRuntime.Disabled = !enabled;
        _preview.Disabled = !enabled;
        if (!enabled)
        {
            _apply.Disabled = true;
        }
        UpdateValidationControls();
    }

    private void UpdateValidationControls()
    {
        var enabled = _draft is not null && !_validationEnabled.Disabled && _validationEnabled.ButtonPressed;
        _destinations.SetEditingEnabled(enabled);
        _destinationSuggestions.Disabled = !enabled || _suggestions.Destinations.Count == 0;
        _addDestination.Disabled = !enabled || _suggestions.Destinations.Count == 0;
        foreach (var editor in new[] { _animations, _collisions, _credentials, _scripts })
        {
            editor.SetEditingEnabled(enabled);
        }
        _animationSuggestions.SetEditingEnabled(enabled);
        _collisionSuggestions.SetEditingEnabled(enabled);
        _credentialSuggestions.SetEditingEnabled(enabled);
        _scriptSuggestions.SetEditingEnabled(enabled);
    }

    private void RenderDiagnosticGroups(IReadOnlyList<StudioDoorValidationDiagnosticGroup> groups)
    {
        foreach (var child in _diagnostics.GetChildren())
        {
            child.QueueFree();
        }
        if (groups.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label(
                "Prévisualisez pour obtenir les sept verdicts canoniques regroupés.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }
        foreach (var group in groups)
        {
            var color = group.Blocking ? StudioStyle.Danger :
                group.State.Contains("Non évalué", StringComparison.OrdinalIgnoreCase)
                    ? StudioStyle.Warning
                    : StudioStyle.Success;
            _diagnostics.AddChild(StudioStyle.Label(
                $"{group.Title} · {group.State}",
                11,
                color,
                wrap: true));
            foreach (var detail in group.Details)
            {
                _diagnostics.AddChild(StudioStyle.Label($"  {detail}", 10, StudioStyle.Muted, wrap: true));
            }
        }
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
        var width = Math.Clamp((Size.X - 56) / scale, 1040, 1320);
        var height = Math.Clamp((Size.Y - 72) / scale, 760, 940);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private PanelContainer DestinationCard()
    {
        var panel = StudioStyle.Panel("KnownDoorDestinationsCard", StudioStyle.Surface, 8, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var margin = StudioStyle.Margin(11, 9, 11, 9);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 5);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label("Destinations connues", 13));
        column.AddChild(StudioStyle.Label(
            "Les destinations héritées de la fondation sont identifiées par leur nom et protégées une fois ajoutées.",
            9,
            StudioStyle.Muted,
            wrap: true));
        column.AddChild(_destinations);
        var suggestionRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        suggestionRow.AddThemeConstantOverride("separation", 5);
        suggestionRow.AddChild(_destinationSuggestions);
        suggestionRow.AddChild(_addDestination);
        column.AddChild(suggestionRow);
        return panel;
    }

    private static PanelContainer ListCard(
        string name,
        string title,
        string help,
        StudioTriggerStringListEditor editor,
        StudioDoorSuggestionPicker suggestions,
        Control? note = null)
    {
        var panel = StudioStyle.Panel(name, StudioStyle.Surface, 8, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var margin = StudioStyle.Margin(11, 9, 11, 9);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 5);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label(title, 13));
        column.AddChild(StudioStyle.Label(help, 9, StudioStyle.Muted, wrap: true));
        column.AddChild(editor);
        column.AddChild(suggestions);
        if (note is not null)
        {
            column.AddChild(note);
        }
        return panel;
    }

    private static CheckBox Check(string text)
    {
        var value = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        value.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        value.AddThemeColorOverride("font_color", StudioStyle.Text);
        return value;
    }

    private static bool SameValidation(
        StudioDoorValidationDraft expected,
        DoorWorldProfile.RuntimeValidationPolicy actual) =>
        expected.KnownDestinationIds.SequenceEqual(actual.KnownDestinationIds) &&
        expected.KnownAnimationIds.SequenceEqual(actual.KnownAnimationIds) &&
        expected.KnownCollisionProfileIds.SequenceEqual(actual.KnownCollisionProfileIds) &&
        expected.KnownCredentialIds.SequenceEqual(actual.KnownCredentialIds) &&
        expected.KnownScriptIds.SequenceEqual(actual.KnownScriptIds);
}
