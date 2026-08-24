using System.Text.RegularExpressions;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioTriggerPortalDestinationEditor : VBoxContainer
{
    private sealed record DestinationEntry(Guid Id, string Label, bool IsProtected);

    private static readonly Regex GuidPattern = new(
        @"(?<![0-9a-fA-F])(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})(?![0-9a-fA-F])",
        RegexOptions.CultureInvariant);

    private readonly ItemList _items;
    private readonly OptionButton _choice;
    private readonly Button _add;
    private readonly Button _remove;
    private readonly List<StudioTriggerPortalDestinationChoice> _choices = [];
    private readonly List<DestinationEntry> _values = [];
    private int _selectedValue = -1;
    private int _selectedChoice = -1;
    private bool _editingEnabled;

    internal event Action? Changed;

    internal IReadOnlyList<Guid> Values => _values.Select(item => item.Id).ToArray();

    internal int ProtectedValueCount => _values.Count(item => item.IsProtected);

    internal bool ShowsOnlyHumanLabels =>
        _values.Count > 0 &&
        _choices.Count > 0 &&
        _values.All(item => !GuidPattern.IsMatch(item.Label)) &&
        _choices.All(item => !GuidPattern.IsMatch(item.Label));

    internal StudioTriggerPortalDestinationEditor()
    {
        AddThemeConstantOverride("separation", 5);
        _items = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 82),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            AutoWidth = false,
            TooltipText = "Destinations admises par le contexte de validation.",
        };
        _items.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        _items.ItemSelected += index =>
        {
            _selectedValue = (int)index;
            UpdateButtons();
        };
        AddChild(_items);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        AddChild(row);
        _choice = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FitToLongestItem = false,
        };
        StudioStyle.ApplyOptionButton(_choice);
        _choice.ItemSelected += index =>
        {
            _selectedChoice = (int)index - 1;
            UpdateButtons();
        };
        row.AddChild(_choice);
        _add = StudioStyle.Button("Ajouter", compact: true);
        _add.Pressed += AddChoice;
        row.AddChild(_add);
        _remove = StudioStyle.Button("Retirer", compact: true);
        _remove.Pressed += RemoveValue;
        row.AddChild(_remove);
        RefreshChoiceMenu();
        RefreshValues();
        SetEditingEnabled(false);
    }

    internal void SetChoices(IEnumerable<StudioTriggerPortalDestinationChoice> choices)
    {
        var values = Values;
        _choices.Clear();
        _choices.AddRange(choices
            .Where(item => item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Label))
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase));
        SetValues(values);
        RefreshChoiceMenu();
    }

    internal void SetValues(IEnumerable<Guid> values)
    {
        _values.Clear();
        var protectedIndex = 0;
        foreach (var id in values)
        {
            var choice = _choices.FirstOrDefault(item => item.Id == id);
            if (choice is not null)
            {
                _values.Add(new DestinationEntry(id, choice.Label, false));
                continue;
            }
            protectedIndex++;
            _values.Add(new DestinationEntry(
                id,
                $"Destination héritée protégée {protectedIndex} · référence interne masquée",
                true));
        }
        RefreshValues();
        UpdateButtons();
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        _choice.Disabled = !enabled;
        _items.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        UpdateButtons();
    }

    private void AddChoice()
    {
        if (!_editingEnabled || _selectedChoice < 0 || _selectedChoice >= _choices.Count)
        {
            return;
        }
        var choice = _choices[_selectedChoice];
        if (_values.Any(item => item.Id == choice.Id))
        {
            return;
        }
        _values.Add(new DestinationEntry(choice.Id, choice.Label, false));
        RefreshValues();
        RefreshChoiceMenu();
        Changed?.Invoke();
    }

    private void RemoveValue()
    {
        if (!_editingEnabled || _selectedValue < 0 || _selectedValue >= _values.Count ||
            _values[_selectedValue].IsProtected)
        {
            return;
        }
        _values.RemoveAt(_selectedValue);
        RefreshValues();
        RefreshChoiceMenu();
        Changed?.Invoke();
    }

    private void RefreshValues()
    {
        _selectedValue = -1;
        _items.Clear();
        foreach (var value in _values)
        {
            _items.AddItem(value.Label);
        }
        UpdateButtons();
    }

    private void RefreshChoiceMenu()
    {
        _selectedChoice = -1;
        _choice.Clear();
        _choice.AddItem("Choisir une destination du projet…");
        foreach (var choice in _choices)
        {
            _choice.AddItem(choice.Label);
        }
        _choice.Select(0);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var canAdd = _editingEnabled && _selectedChoice >= 0 && _selectedChoice < _choices.Count &&
                     _values.All(item => item.Id != _choices[_selectedChoice].Id);
        _add.Disabled = !canAdd;
        var selected = _selectedValue >= 0 && _selectedValue < _values.Count
            ? _values[_selectedValue]
            : null;
        _remove.Disabled = !_editingEnabled || selected is null || selected.IsProtected;
        _remove.TooltipText = selected?.IsProtected == true
            ? "Cette destination héritée reste protégée tant qu’elle n’est pas résolue dans le projet."
            : "Retirer la destination sélectionnée du contexte connu.";
    }
}

internal sealed partial class StudioTriggerValidationOverlay : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx1AIEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _trigger;
    private readonly Label _selection;
    private readonly CheckBox _validationEnabled;
    private readonly CheckBox _requiredAtRuntime;
    private readonly StudioTriggerStringListEditor _targets;
    private readonly StudioTriggerStringListEditor _scripts;
    private readonly StudioTriggerPortalDestinationEditor _portalDestinations;
    private readonly StudioTriggerStringListEditor _layers;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;

    private IReadOnlyList<StudioTriggerValidationChoice> _choices = [];
    private StudioTriggerValidationDraft? _draft;
    private IReadOnlyList<StudioTriggerValidationDiagnosticGroup> _lastGroups = [];
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? TriggerChanged;

    internal bool IsOpen => Visible;

    internal int ChoiceCount => _choices.Count;

    internal bool HasExplicitSelection => _draft is not null && _trigger.Selected > 0;

    internal bool CanonicalPreviewReady => _canonicalPreviewReady;

    internal bool ShowsGroupedDiagnostics => _lastGroups.Count == 7 &&
                                               _lastGroups.Select(item => item.Key).Distinct().Count() == 7;

    internal bool ShowsHumanPortalDestinations => _portalDestinations.ShowsOnlyHumanLabels;

    internal bool PreservesProtectedPortalDestinations =>
        _draft is not null &&
        _portalDestinations.ProtectedValueCount > 0 &&
        _portalDestinations.Values.SequenceEqual(_draft.KnownPortalDestinationIds);

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
                _validationEnabled.Text,
                _requiredAtRuntime.Text,
                "Cibles Scripts Portails Couches Validation canonique");
            return HasExplicitSelection &&
                   ShowsHumanPortalDestinations &&
                   text.Contains("Validation canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Cibles", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Scripts", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Portails", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Couches", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioTriggerValidationOverlay(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioTriggerValidationOverlay";
        Visible = false;
        ZIndex = 450;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .8f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("TriggerValidationDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("Contrôler un déclencheur avant publication", 20));
        heading.AddChild(StudioStyle.Label(
            "ORF-TRG-020 · même verdict que le catalogue et la commande projet · aperçu obligatoire",
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
        selector.AddChild(StudioStyle.Label("Déclencheur à valider", 10, StudioStyle.Muted));
        _trigger = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_trigger);
        _trigger.ItemSelected += SelectTrigger;
        selector.AddChild(_trigger);
        _selection = StudioStyle.Label(
            "Choisissez explicitement un déclencheur doté d’un volume de base.",
            11,
            StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        var policy = new HBoxContainer();
        policy.AddThemeConstantOverride("separation", 14);
        root.AddChild(policy);
        _validationEnabled = Check("Activer le contexte canonique");
        _validationEnabled.TooltipText =
            "Déclare les références connues utilisées par le validateur canonique du déclencheur.";
        policy.AddChild(_validationEnabled);
        _requiredAtRuntime = Check("Exiger ce contexte au runtime");
        _requiredAtRuntime.TooltipText =
            "Si activé, l’absence de contexte devient une erreur bloquante au chargement.";
        policy.AddChild(_requiredAtRuntime);
        policy.AddChild(StudioStyle.Label(
            "Désactivé et optionnel = fallback explicite, jamais une validation simulée.",
            10,
            StudioStyle.Muted,
            wrap: true));

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 390),
            CustomMaximumSize = new Vector2(-1, 620),
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
        _targets = new StudioTriggerStringListEditor(
            "Aucune cible connue.",
            "Ex. encounter:ambush");
        lists.AddChild(ListCard(
            "KnownTargetsCard",
            "Cibles connues",
            "Identifiants métier autorisés pour les actions du volume.",
            _targets));
        _scripts = new StudioTriggerStringListEditor(
            "Aucun script connu.",
            "Ex. script:gate-enter");
        lists.AddChild(ListCard(
            "KnownScriptsCard",
            "Scripts connus",
            "Scripts autorisés pour actions, interactions et événements.",
            _scripts));
        _portalDestinations = new StudioTriggerPortalDestinationEditor();
        lists.AddChild(ListCard(
            "KnownPortalsCard",
            "Portails connus",
            "Destinations autorisées, choisies par leur nom. Les références héritées non résolues restent masquées et protégées.",
            _portalDestinations));
        _layers = new StudioTriggerStringListEditor(
            "Aucune couche accessible.",
            "Ex. characters");
        lists.AddChild(ListCard(
            "AccessibleLayersCard",
            "Couches accessibles",
            "Couches depuis lesquelles le volume doit rester atteignable.",
            _layers));

        var diagnosticsCard = StudioStyle.Panel("TriggerValidationDiagnostics", StudioStyle.Surface, 8, 1);
        var diagnosticsMargin = StudioStyle.Margin(12, 10, 12, 10);
        diagnosticsCard.AddChild(diagnosticsMargin);
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics.AddThemeConstantOverride("separation", 5);
        diagnosticsMargin.AddChild(_diagnostics);
        content.AddChild(diagnosticsCard);

        _summary = StudioStyle.Label(
            "Sélectionnez un déclencheur puis prévisualisez · aucun changement appliqué.",
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
        _preview.TooltipText = "Valider sans modifier le projet.";
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
            "Seuls le contexte de validation et son caractère requis sont modifiés.",
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
        _session.CancelTriggerValidationUx1AI();
        _choices = _session.TriggerValidationChoicesUx1AI;
        _trigger.Clear();
        _trigger.AddItem("Choisissez explicitement un déclencheur…");
        foreach (var choice in _choices)
        {
            _trigger.AddItem(choice.Label);
        }
        _trigger.Select(0);
        _draft = null;
        _lastGroups = [];
        _applied = false;
        _canonicalPreviewReady = false;
        ResetDraftControls();
        _selection.Text = _choices.Count == 0
            ? "Aucun profil de déclencheur n’est disponible dans la zone active."
            : "Choisissez explicitement un déclencheur doté d’un volume de base.";
        _selection.AddThemeColorOverride(
            "font_color",
            _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez un déclencheur puis prévisualisez · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        RenderDiagnosticGroups([]);
        _status.Text = "Prêt · sélection explicite requise";
        _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(false);
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        MoveToFront();
        UpdateDialogBounds();
        _trigger.GrabFocus();
    }

    internal void Close()
    {
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _session.CancelTriggerValidationUx1AI();
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
            item.IsEvidence && item.HasFoundation && item.HasValidation);
        if (index < 0)
        {
            Close();
            return false;
        }
        _trigger.Select(index + 1);
        LoadChoice(index);
        if (_draft is null)
        {
            Close();
            return false;
        }
        _targets.SetValues(_targets.Values.Append("encounter:ux1-ai-human-review"));
        Preview();
        _selection.Text += " · session de preuve isolée";
        _summary.Text = "PREUVE ISOLÉE · le projet source reste strictement inchangé.\n" + _summary.Text;
        SetStatus("Preuve isolée prête · fermeture = suppression de la session temporaire", error: false);
        return !_apply.Disabled && HasExplicitSelection && CanonicalPreviewReady && ShowsGroupedDiagnostics &&
               ShowsHumanSurface && ShowsHumanPortalDestinations && PreservesProtectedPortalDestinations &&
               UsesIsolatedEvidence && EvidenceSourceUntouched;
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
            var ready = PrepareEvidence("ux1-ai-trigger-validation");
            var transactionSession = _session;
            var transactionHistory = transactionSession.History;
            var originalProject = transactionSession.Project;
            var originalRevision = transactionHistory?.State.Revision;
            var originalJournal = transactionHistory?.Journal.ToArray() ?? [];
            var originalCanUndo = transactionHistory?.CanUndo;
            var originalCanRedo = transactionHistory?.CanRedo;
            var originalDirty = transactionSession.IsDirty;
            var targetId = _draft?.TriggerProfileId;
            var expectedDraft = ReadDraft();
            var dryRun = ready && targetId.HasValue && expectedDraft is not null && originalProject is not null &&
                         ReferenceEquals(originalProject, transactionSession.Project) &&
                         ReferenceEquals(transactionHistory, transactionSession.History) &&
                         transactionHistory?.State.Revision == originalRevision &&
                         (transactionHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                         transactionHistory?.CanUndo == originalCanUndo &&
                         transactionHistory?.CanRedo == originalCanRedo &&
                         transactionSession.IsDirty == originalDirty &&
                         UsesIsolatedEvidence && SourceMatchesSnapshot(sourceSnapshot);
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
            if (saved && transactionSession.Session is { } session && targetId is { } profileId &&
                expectedDraft is not null)
            {
                reopened = new StudioSessionController();
                if (reopened.Open(session.RootDirectory, transactionSession.ActiveAreaId?.ToString()).Succeeded)
                {
                    var profile = reopened.Project?.Module.WorldFeatures.Triggers
                        .SingleOrDefault(item => item.Id == profileId);
                    persisted = profile?.Foundation is { Validation: { } validation } foundation &&
                                SameValidation(expectedDraft, validation) &&
                                foundation.ValidationRequiredAtRuntime ==
                                expectedDraft.ValidationRequiredAtRuntime;
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
                $"OPENRPG_FORGE_STUDIO_UX1AI_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
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
        var evidenceProject = _sourceSession.CreateUx1AIEvidenceProject();
        if (snapshot is null || evidenceProject is null)
        {
            return false;
        }

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ai-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded ||
                !evidenceSession.Open(tempRoot, snapshot.ActiveAreaId?.ToString()).Succeeded)
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
            var ready = _choices.Any(item => item.IsEvidence && item.HasFoundation && item.HasValidation) &&
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
                $"OPENRPG_FORGE_STUDIO_UX1AI_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
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

    private StudioUx1AIEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new StudioUx1AIEvidenceSourceSnapshot(
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
            _sourceSession.CaptureTriggerValidationApprovalUx1AI());
    }

    private bool SourceMatchesSnapshot(StudioUx1AIEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history is not null &&
               history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureTriggerValidationApprovalUx1AI() == snapshot.Approval;
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
                $"OPENRPG_FORGE_STUDIO_UX1AI_EVIDENCE_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
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
                !leaf.StartsWith("openrpg-forge-studio-ux1ai-evidence-", StringComparison.Ordinal))
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
                $"OPENRPG_FORGE_STUDIO_UX1AI_EVIDENCE_DELETE_FAILED type={exception.GetType().Name} message={exception.Message}");
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

    private void SelectTrigger(long index)
    {
        if (_updating)
        {
            return;
        }
        var choiceIndex = (int)index - 1;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            _draft = null;
            _lastGroups = [];
            _applied = false;
            _canonicalPreviewReady = false;
            _session.CancelTriggerValidationUx1AI();
            ResetDraftControls();
            _selection.Text = "Choisissez explicitement un déclencheur doté d’un volume de base.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            _summary.Text = "Sélectionnez un déclencheur puis prévisualisez · aucun changement appliqué.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
            _status.Text = "Prêt · sélection explicite requise";
            _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
            RenderDiagnosticGroups([]);
            SetEditingEnabled(false);
            _undo.Disabled = true;
            _redo.Disabled = true;
            return;
        }
        LoadChoice(choiceIndex);
    }

    private void LoadChoice(int choiceIndex)
    {
        var choice = _choices[choiceIndex];
        _session.CancelTriggerValidationUx1AI();
        _draft = _session.CreateTriggerValidationDraftUx1AI(choice.ProfileId);
        _lastGroups = [];
        _applied = false;
        _canonicalPreviewReady = false;
        _undo.Disabled = true;
        _redo.Disabled = true;
        if (_draft is null)
        {
            _selection.Text = choice.HasFoundation
                ? "Le déclencheur a changé. Rechargez la fiche."
                : "Volume de base requis · ouvrez d’abord l’outil Déclencheur.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            _summary.Text = "La validation canonique ne peut pas être configurée sans Foundation.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            ResetDraftControls();
            RenderDiagnosticGroups([]);
            SetEditingEnabled(false);
            SetStatus("Volume de base requis · aucune validation appliquable", error: true);
            return;
        }

        Populate(_draft);
        _selection.Text = choice.HasValidation
            ? choice.ValidationRequiredAtRuntime
                ? "Contexte configuré et requis · prévisualisez avant toute modification."
                : "Contexte configuré et optionnel · prévisualisez avant toute modification."
            : "Aucun contexte configuré · fallback optionnel tant qu’il n’est pas déclaré requis.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        _summary.Text = "Brouillon chargé · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        RenderDiagnosticGroups([]);
        SetEditingEnabled(true);
        SetStatus("Brouillon prêt · aperçu canonique requis", error: false);
    }

    private void Populate(StudioTriggerValidationDraft draft)
    {
        _updating = true;
        _validationEnabled.ButtonPressed = draft.ValidationEnabled;
        _requiredAtRuntime.ButtonPressed = draft.ValidationRequiredAtRuntime;
        _portalDestinations.SetChoices(_session.TriggerValidationPortalDestinationChoicesUx1AI());
        _portalDestinations.SetValues(draft.KnownPortalDestinationIds);
        _targets.SetValues(draft.KnownTargetIds);
        _scripts.SetValues(draft.KnownScriptIds);
        _layers.SetValues(draft.AccessibleLayers);
        _updating = false;
        UpdateValidationControls();
    }

    private void ResetDraftControls()
    {
        _updating = true;
        _validationEnabled.ButtonPressed = false;
        _requiredAtRuntime.ButtonPressed = false;
        _portalDestinations.SetChoices([]);
        _portalDestinations.SetValues([]);
        _targets.SetValues([]);
        _scripts.SetValues([]);
        _layers.SetValues([]);
        _updating = false;
        UpdateValidationControls();
    }

    private StudioTriggerValidationDraft? ReadDraft()
    {
        if (_draft is null)
        {
            return null;
        }
        return _draft with
        {
            ValidationEnabled = _validationEnabled.ButtonPressed,
            ValidationRequiredAtRuntime = _requiredAtRuntime.ButtonPressed,
            KnownPortalDestinationIds = _portalDestinations.Values,
            KnownTargetIds = _targets.Values,
            KnownScriptIds = _scripts.Values,
            AccessibleLayers = _layers.Values,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            return;
        }
        var result = _session.PreviewTriggerValidationUx1AI(draft);
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
        var result = _session.ApplyApprovedTriggerValidationUx1AI();
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
            "Géométrie, filtres, actions, portail, événements et profils voisins sont préservés.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        TriggerChanged?.Invoke();
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
            TriggerChanged?.Invoke();
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
            TriggerChanged?.Invoke();
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
        foreach (var editor in new[] { _targets, _scripts, _layers })
        {
            editor.Changed += InvalidatePreview;
        }
        _portalDestinations.Changed += InvalidatePreview;
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null || _applied)
        {
            return;
        }
        _session.CancelTriggerValidationUx1AI();
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
        foreach (var editor in new[] { _targets, _scripts, _layers })
        {
            editor.SetEditingEnabled(enabled);
        }
        _portalDestinations.SetEditingEnabled(enabled);
    }

    private void RenderDiagnosticGroups(IReadOnlyList<StudioTriggerValidationDiagnosticGroup> groups)
    {
        foreach (var child in _diagnostics.GetChildren())
        {
            child.QueueFree();
        }
        if (groups.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label(
                "Prévisualisez pour obtenir les sept contrôles canoniques regroupés.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }
        foreach (var group in groups)
        {
            var color = group.Blocking ? StudioStyle.Danger :
                group.State.Contains("fallback", StringComparison.OrdinalIgnoreCase) ||
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
        var width = Math.Clamp((Size.X - 56) / scale, 960, 1220);
        var height = Math.Clamp((Size.Y - 72) / scale, 720, 900);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static PanelContainer ListCard(
        string name,
        string title,
        string help,
        Control editor)
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
        StudioTriggerValidationDraft expected,
        TriggerWorldProfile.RuntimeValidationPolicy actual) =>
        expected.KnownPortalDestinationIds.SequenceEqual(actual.KnownPortalDestinationIds) &&
        expected.KnownTargetIds.SequenceEqual(actual.KnownTargetIds) &&
        expected.KnownScriptIds.SequenceEqual(actual.KnownScriptIds) &&
        expected.AccessibleLayers.SequenceEqual(actual.AccessibleLayers);
}
