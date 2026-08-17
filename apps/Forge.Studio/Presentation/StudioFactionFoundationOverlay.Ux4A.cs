using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioFactionFoundationOverlayUx4A : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioFactionEvidenceSourceSnapshotUx4A? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceSessionRestored = true;
    private bool _evidenceUsesRealCryptSource;
    private bool _evidenceUsesCanonicalFallback;
    private bool _lastEvidencePreviewDryRun;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _faction;
    private readonly Button _newFaction;
    private readonly OptionButton _operation;
    private readonly LineEdit _displayName;
    private readonly LineEdit _slug;
    private readonly CheckBox _archived;
    private readonly ItemList _parents;
    private readonly CheckBox _inheritTags;
    private readonly CheckBox _inheritPolicies;
    private readonly CheckBox _inheritRelationships;
    private readonly OptionButton _migration;
    private readonly Label _lifecycleHint;

    private readonly OptionButton _relationshipList;
    private readonly GridContainer _relationshipMatrix;
    private readonly OptionButton _relationshipSource;
    private readonly OptionButton _relationshipTarget;
    private readonly LineEdit _relationshipScore;
    private readonly LineEdit _relationshipPreset;
    private readonly LineEdit _relationshipExplanation;
    private readonly Button _saveRelationship;
    private readonly Button _removeRelationship;
    private readonly Label _directionalSummary;

    private readonly OptionButton _bandList;
    private readonly LineEdit _bandId;
    private readonly LineEdit _bandMinimum;
    private readonly LineEdit _bandMaximum;
    private readonly LineEdit _bandPriority;
    private readonly Button _saveBand;
    private readonly Button _removeBand;
    private readonly Label _bandSummary;

    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;

    private IReadOnlyList<StudioFactionChoiceUx4A> _choices = [];
    private IReadOnlyList<StudioFactionChoiceUx4A> _parentChoices = [];
    private IReadOnlyList<StudioFactionChoiceUx4A> _relationshipChoices = [];
    private IReadOnlyList<StudioFactionChoiceUx4A> _migrationChoices = [];
    private readonly List<StudioFactionRelationshipDraftUx4A> _relationships = [];
    private readonly List<StudioDispositionBandDraftUx4A> _bands = [];
    private StudioFactionFoundationDraftUx4A? _draft;
    private bool _newActionSelected;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;
    private bool _relationshipEditorDirty;
    private bool _bandEditorDirty;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? FactionChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && (_faction.Selected > 0 || _newActionSelected);
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && !_apply.Disabled;
    internal bool ShowsDirectionalMatrix => _relationships.Count >= 2 &&
                                             _relationshipMatrix.Columns == _relationshipChoices.Count + 1 &&
                                             _directionalSummary.Text.Contains("→", StringComparison.Ordinal);
    internal bool ShowsHierarchy => _draft is not null && _parents.ItemCount >= 1 &&
                                    (_inheritTags.ButtonPressed || _inheritPolicies.ButtonPressed ||
                                     _inheritRelationships.ButtonPressed);
    internal bool ShowsBands => _bands.Count >= 3 &&
                                _bandSummary.Text.Contains("priorité", StringComparison.OrdinalIgnoreCase);
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool UsesRealCryptEvidence => UsesIsolatedEvidence && _evidenceUsesRealCryptSource;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidencePreviewDryRun => UsesIsolatedEvidence && _lastEvidencePreviewDryRun;
    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceTempRoot is null &&
                                                _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) &&
                                           _lastSourceSessionRestored;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabels(_dialog).Select(item => item.Text));
            return text.Contains("Factions", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Parents", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Relations directionnelles", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Bandes", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("module/factions", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioFactionFoundationOverlayUx4A(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioFactionFoundationOverlayUx4A";
        Visible = false;
        ZIndex = 470;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .82f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("FactionFoundationDialogUx4A", StudioStyle.Chrome, 12, 2);
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
        header.AddThemeConstantOverride("separation", 10);
        root.AddChild(header);
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddChild(StudioStyle.Label("FACTIONS · FONDATIONS", 10, StudioStyle.Accent));
        title.AddChild(StudioStyle.Label("Définitions, parents, relations et bandes", 20));
        title.AddChild(StudioStyle.Label(
            "FAC-001 à FAC-005 · édition directe et commande canonique · réputation personnelle et extensions avancées préservées",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(title);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer();
        selector.AddThemeConstantOverride("separation", 8);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Faction", 10, StudioStyle.Muted));
        _faction = Option("Choisissez explicitement une faction…", 390);
        selector.AddChild(_faction);
        _newFaction = StudioStyle.Button("Nouvelle faction", compact: true);
        _newFaction.TooltipText = "Créer une définition vide sans toucher aux extensions voisines.";
        selector.AddChild(_newFaction);
        selector.AddChild(StudioStyle.Label("Opération", 10, StudioStyle.Muted));
        _operation = Option("Modifier", 190);
        _operation.AddItem("Archiver");
        _operation.AddItem("Supprimer avec contrôle des dépendances");
        selector.AddChild(_operation);

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(tabs);

        var definition = Tab(tabs, "1 · Définition et parents");
        definition.AddChild(StudioStyle.Label(
            "La fiche conserve l’identité interne, le schéma, les tags, les politiques et toutes les extensions non affichées.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var definitionGrid = Grid();
        definition.AddChild(definitionGrid);
        _displayName = Field(definitionGrid, "Nom affiché", "Nom lisible dans le Studio");
        _slug = Field(definitionGrid, "Nom interne", "identifiant-lisible");
        _archived = new CheckBox { Text = "Définition archivée" };
        definition.AddChild(_archived);
        definition.AddChild(StudioStyle.Label("Parents", 11, StudioStyle.Accent));
        definition.AddChild(StudioStyle.Label(
            "Sélection multiple autorisée. Les cycles sont refusés par le validateur canonique.",
            9,
            StudioStyle.Muted,
            wrap: true));
        _parents = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Multi,
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        definition.AddChild(_parents);
        var inheritance = new HBoxContainer();
        inheritance.AddThemeConstantOverride("separation", 16);
        definition.AddChild(inheritance);
        _inheritTags = new CheckBox { Text = "Hériter les tags" };
        _inheritPolicies = new CheckBox { Text = "Hériter les politiques" };
        _inheritRelationships = new CheckBox { Text = "Hériter les relations" };
        inheritance.AddChild(_inheritTags);
        inheritance.AddChild(_inheritPolicies);
        inheritance.AddChild(_inheritRelationships);
        var lifecycle = new HBoxContainer();
        lifecycle.AddThemeConstantOverride("separation", 8);
        definition.AddChild(lifecycle);
        lifecycle.AddChild(StudioStyle.Label("Destination de migration", 10, StudioStyle.Muted));
        _migration = Option("Aucune · suppression sans référence migrée", 360);
        lifecycle.AddChild(_migration);
        _lifecycleHint = StudioStyle.Label(
            "L’archivage conserve les références. La suppression est prévisualisée et toute dépendance externe bloque l’application.",
            10,
            StudioStyle.Warning,
            wrap: true);
        definition.AddChild(_lifecycleHint);

        var relations = Tab(tabs, "2 · Relations directionnelles");
        relations.AddChild(StudioStyle.Label(
            "Chaque direction est indépendante : A → B ne réécrit jamais B → A.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var matrixScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 158),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        relations.AddChild(matrixScroll);
        _relationshipMatrix = new GridContainer
        {
            Columns = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _relationshipMatrix.AddThemeConstantOverride("h_separation", 7);
        _relationshipMatrix.AddThemeConstantOverride("v_separation", 5);
        matrixScroll.AddChild(_relationshipMatrix);
        _relationshipList = Option("Nouvelle direction…", 560);
        relations.AddChild(_relationshipList);
        var relationGrid = Grid();
        relations.AddChild(relationGrid);
        _relationshipSource = Option("Source…", 260);
        AddControl(relationGrid, "Source", _relationshipSource);
        _relationshipTarget = Option("Cible…", 260);
        AddControl(relationGrid, "Cible", _relationshipTarget);
        _relationshipScore = Field(relationGrid, "Score −100 à 100", "ex. -75 ou 20.5");
        _relationshipPreset = Field(relationGrid, "Preset", "hostile, neutral, allied…");
        _relationshipExplanation = Field(relationGrid, "Explication", "Pourquoi cette direction existe");
        var relationActions = new HBoxContainer();
        relationActions.AddThemeConstantOverride("separation", 8);
        relations.AddChild(relationActions);
        _saveRelationship = StudioStyle.Button("Ajouter ou mettre à jour", compact: true);
        _removeRelationship = StudioStyle.Button("Retirer cette direction", compact: true);
        relationActions.AddChild(_saveRelationship);
        relationActions.AddChild(_removeRelationship);
        _directionalSummary = StudioStyle.Label("Aucune relation authorée.", 10, StudioStyle.Accent, wrap: true);
        relations.AddChild(_directionalSummary);

        var bands = Tab(tabs, "3 · Bandes de disposition");
        bands.AddChild(StudioStyle.Label(
            "Les bornes restent exactes : aucun arrondi ni plafonnement silencieux n’est appliqué.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _bandList = Option("Nouvelle bande…", 520);
        bands.AddChild(_bandList);
        var bandGrid = Grid();
        bands.AddChild(bandGrid);
        _bandId = Field(bandGrid, "Nom de l’état", "hostile, neutral, allied…");
        _bandMinimum = Field(bandGrid, "Minimum", "-100");
        _bandMaximum = Field(bandGrid, "Maximum", "100");
        _bandPriority = Field(bandGrid, "Priorité", "0 ou plus");
        var bandActions = new HBoxContainer();
        bandActions.AddThemeConstantOverride("separation", 8);
        bands.AddChild(bandActions);
        _saveBand = StudioStyle.Button("Ajouter ou mettre à jour", compact: true);
        _removeBand = StudioStyle.Button("Retirer cette bande", compact: true);
        bandActions.AddChild(_saveBand);
        bandActions.AddChild(_removeBand);
        _bandSummary = StudioStyle.Label("Aucune bande authorée.", 10, StudioStyle.Accent, wrap: true);
        bands.AddChild(_bandSummary);

        var review = StudioStyle.Panel("FactionReviewUx4A", StudioStyle.Surface, 8, 1);
        review.CustomMinimumSize = new Vector2(0, 142);
        root.AddChild(review);
        var reviewMargin = StudioStyle.Margin(10, 8, 10, 8);
        review.AddChild(reviewMargin);
        var reviewColumn = new VBoxContainer();
        reviewColumn.AddThemeConstantOverride("separation", 5);
        reviewMargin.AddChild(reviewColumn);
        _summary = StudioStyle.Label(
            "Sélectionnez une faction ou choisissez Nouvelle faction · aucune donnée modifiée.",
            10,
            StudioStyle.Muted,
            wrap: true);
        reviewColumn.AddChild(_summary);
        _diagnostics = new VBoxContainer();
        _diagnostics.AddThemeConstantOverride("separation", 2);
        reviewColumn.AddChild(_diagnostics);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 10, StudioStyle.Muted, wrap: true);
        reviewColumn.AddChild(_status);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", primary: true, compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        actions.AddChild(_preview);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(StudioStyle.Label(
            "Les détails techniques restent masqués ; le journal conserve la commande et sa révision.",
            9,
            StudioStyle.Muted,
            wrap: true));

        ConnectEvents();
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
        _session.CancelFactionFoundationUx4A();
        _choices = _session.FactionChoicesUx4A;
        _faction.Clear();
        _faction.AddItem("Choisissez explicitement une faction…");
        foreach (var choice in _choices)
        {
            _faction.AddItem(choice.Label);
        }
        _faction.Select(0);
        _draft = null;
        _newActionSelected = false;
        _applied = false;
        _canonicalPreviewReady = false;
        ResetControls();
        _summary.Text = _choices.Count == 0
            ? "Catalogue vide · utilisez Nouvelle faction pour commencer sans modifier de fichier à la main."
            : "Choisissez une faction · aucune donnée du module n’a été modifiée.";
        _summary.AddThemeColorOverride("font_color", _choices.Count == 0 ? StudioStyle.Warning : StudioStyle.Muted);
        SetStatus("Prêt · sélection explicite requise", error: false);
        RenderDiagnostics([]);
        SetEditingEnabled(false);
        Visible = true;
        MoveToFront();
        UpdateDialogBounds();
        _faction.GrabFocus();
    }

    internal void Close()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            CloseIsolatedEvidenceSession();
            return;
        }

        _sourceSession.CancelFactionFoundationUx4A();
        ResetClosedState();
        var released = ReleaseIsolatedEvidenceSession();
        _lastEvidenceSourceUntouched = true;
        _lastEvidenceResourcesReleased = released;
        _lastSourceSessionRestored = ReferenceEquals(_session, _sourceSession);
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            CloseIsolatedEvidenceSession();
        }
    }

    private void CloseIsolatedEvidenceSession()
    {
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _evidenceSession?.CancelFactionFoundationUx4A();
        ResetClosedState();
        var released = ReleaseIsolatedEvidenceSession();
        _lastEvidenceSourceUntouched = sourceUntouched;
        _lastEvidenceResourcesReleased = released;
        _lastSourceSessionRestored = ReferenceEquals(_session, _sourceSession);
    }

    private void ResetClosedState()
    {
        Visible = false;
        _draft = null;
        _relationships.Clear();
        _bands.Clear();
        _canonicalPreviewReady = false;
        _applied = false;
        _lastEvidencePreviewDryRun = false;
    }

    private void SelectFaction(long selected)
    {
        if (_updating)
        {
            return;
        }
        var index = (int)selected - 1;
        if (index < 0 || index >= _choices.Count)
        {
            ClearSelection();
            return;
        }
        LoadDraft(_session.CreateFactionDraftUx4A(_choices[index].FactionId), newAction: false);
    }

    private void StartNewFaction()
    {
        _updating = true;
        _faction.Select(0);
        _updating = false;
        LoadDraft(_session.CreateNewFactionDraftUx4A(), newAction: true);
    }

    private void ClearSelection()
    {
        _session.CancelFactionFoundationUx4A();
        _draft = null;
        _newActionSelected = false;
        _relationships.Clear();
        _bands.Clear();
        ResetControls();
        RenderDiagnostics([]);
        SetEditingEnabled(false);
        _summary.Text = "Sélection explicite requise · aucune donnée modifiée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetStatus("Prêt · sélection explicite requise", error: false);
    }

    private void LoadDraft(StudioFactionFoundationDraftUx4A? draft, bool newAction)
    {
        _session.CancelFactionFoundationUx4A();
        _draft = draft;
        _newActionSelected = newAction && draft is not null;
        _canonicalPreviewReady = false;
        _applied = false;
        if (draft is null)
        {
            ClearSelection();
            SetStatus("La faction a changé. Rechargez la page.", error: true);
            return;
        }
        Populate(draft);
        RenderDiagnostics([]);
        SetEditingEnabled(true);
        _summary.Text = draft.IsNew
            ? "Nouvelle définition prête · identité interne stable créée mais non appliquée."
            : "Brouillon chargé · parents, directions, bandes et dépendances sont préservés.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetStatus("Brouillon prêt · aperçu canonique requis", error: false);
    }

    private void Populate(StudioFactionFoundationDraftUx4A draft)
    {
        _updating = true;
        _displayName.Text = draft.DisplayName;
        _slug.Text = draft.Slug;
        _archived.ButtonPressed = draft.Archived;
        _inheritTags.ButtonPressed = draft.InheritTags;
        _inheritPolicies.ButtonPressed = draft.InheritPolicies;
        _inheritRelationships.ButtonPressed = draft.InheritRelationships;
        _operation.Select((int)draft.Operation);
        _relationships.Clear();
        _relationships.AddRange(draft.Relationships.Select(item => item with { }));
        _bands.Clear();
        _bands.AddRange(draft.Bands.Select(item => item with { }));
        RebuildParentChoices(draft);
        RebuildRelationshipChoices();
        RebuildRelationshipList();
        RebuildBandList();
        RebuildMigrationChoices(draft);
        _updating = false;
        UpdateOperationState();
        UpdateSummaries(draft.SourceCatalog);
    }

    private void RebuildParentChoices(StudioFactionFoundationDraftUx4A draft)
    {
        _parentChoices = _choices.Where(item => item.FactionId != draft.WorkingFactionId).ToArray();
        _parents.Clear();
        foreach (var choice in _parentChoices)
        {
            _parents.AddItem(choice.Label);
        }
        for (var index = 0; index < _parentChoices.Count; index++)
        {
            if (draft.ParentIds.Contains(_parentChoices[index].FactionId))
            {
                _parents.Select(index, single: false);
            }
        }
    }

    private void RebuildRelationshipChoices()
    {
        _relationshipChoices = _choices.ToArray();
        RebuildOption(_relationshipSource, "Source…", _relationshipChoices.Select(item => item.Label));
        RebuildOption(_relationshipTarget, "Cible…", _relationshipChoices.Select(item => item.Label));
    }

    private void RebuildRelationshipList()
    {
        _relationshipList.Clear();
        _relationshipList.AddItem("Nouvelle direction…");
        foreach (var relationship in _relationships)
        {
            _relationshipList.AddItem(
                $"{ChoiceName(relationship.SourceId)} → {ChoiceName(relationship.TargetId)} · " +
                $"{relationship.Score} · {relationship.Preset}");
        }
        _relationshipList.Select(0);
        ClearRelationshipEditor();
        UpdateDirectionalSummary();
    }

    private void SelectRelationship(long selected)
    {
        if (_updating)
        {
            return;
        }
        var index = (int)selected - 1;
        if (index < 0 || index >= _relationships.Count)
        {
            ClearRelationshipEditor();
            return;
        }
        var relationship = _relationships[index];
        var updating = _updating;
        _updating = true;
        SelectChoice(_relationshipSource, _relationshipChoices, relationship.SourceId);
        SelectChoice(_relationshipTarget, _relationshipChoices, relationship.TargetId);
        _relationshipScore.Text = relationship.Score;
        _relationshipPreset.Text = relationship.Preset;
        _relationshipExplanation.Text = relationship.Explanation;
        _removeRelationship.Disabled = _operation.Selected != (int)StudioFactionOperationUx4A.Edit;
        _relationshipEditorDirty = false;
        _updating = updating;
    }

    private void SaveRelationship()
    {
        var sourceIndex = _relationshipSource.Selected - 1;
        var targetIndex = _relationshipTarget.Selected - 1;
        if (sourceIndex < 0 || sourceIndex >= _relationshipChoices.Count ||
            targetIndex < 0 || targetIndex >= _relationshipChoices.Count)
        {
            SetStatus("Choisissez une source et une cible humaines avant d’ajouter la direction.", error: true);
            return;
        }
        var candidate = new StudioFactionRelationshipDraftUx4A(
            _relationshipChoices[sourceIndex].FactionId,
            _relationshipChoices[targetIndex].FactionId,
            _relationshipScore.Text,
            _relationshipPreset.Text,
            _relationshipExplanation.Text);
        var index = _relationships.FindIndex(item =>
            item.SourceId == candidate.SourceId && item.TargetId == candidate.TargetId);
        if (index < 0)
        {
            _relationships.Add(candidate);
        }
        else
        {
            _relationships[index] = candidate;
        }
        RebuildRelationshipList();
        InvalidatePreview();
    }

    private void RemoveRelationship()
    {
        var index = _relationshipList.Selected - 1;
        if (index < 0 || index >= _relationships.Count)
        {
            return;
        }
        _relationships.RemoveAt(index);
        RebuildRelationshipList();
        InvalidatePreview();
    }

    private void ClearRelationshipEditor()
    {
        var updating = _updating;
        _updating = true;
        _relationshipSource.Select(0);
        _relationshipTarget.Select(0);
        _relationshipScore.Text = "0";
        _relationshipPreset.Text = string.Empty;
        _relationshipExplanation.Text = string.Empty;
        _removeRelationship.Disabled = true;
        _relationshipEditorDirty = false;
        _updating = updating;
    }

    private void RebuildBandList()
    {
        _bandList.Clear();
        _bandList.AddItem("Nouvelle bande…");
        foreach (var band in _bands)
        {
            _bandList.AddItem($"{band.Id} · {band.Minimum}…{band.Maximum} · priorité {band.Priority}");
        }
        _bandList.Select(0);
        ClearBandEditor();
        UpdateBandSummary();
    }

    private void SelectBand(long selected)
    {
        if (_updating)
        {
            return;
        }
        var index = (int)selected - 1;
        if (index < 0 || index >= _bands.Count)
        {
            ClearBandEditor();
            return;
        }
        var band = _bands[index];
        var updating = _updating;
        _updating = true;
        _bandId.Text = band.Id;
        _bandMinimum.Text = band.Minimum;
        _bandMaximum.Text = band.Maximum;
        _bandPriority.Text = band.Priority;
        _removeBand.Disabled = _operation.Selected != (int)StudioFactionOperationUx4A.Edit;
        _bandEditorDirty = false;
        _updating = updating;
    }

    private void SaveBand()
    {
        var candidate = new StudioDispositionBandDraftUx4A(
            _bandId.Text,
            _bandMinimum.Text,
            _bandMaximum.Text,
            _bandPriority.Text);
        var index = _bands.FindIndex(item => string.Equals(item.Id, candidate.Id, StringComparison.Ordinal));
        if (index < 0)
        {
            _bands.Add(candidate);
        }
        else
        {
            _bands[index] = candidate;
        }
        RebuildBandList();
        InvalidatePreview();
    }

    private void RemoveBand()
    {
        var index = _bandList.Selected - 1;
        if (index < 0 || index >= _bands.Count)
        {
            return;
        }
        _bands.RemoveAt(index);
        RebuildBandList();
        InvalidatePreview();
    }

    private void ClearBandEditor()
    {
        var updating = _updating;
        _updating = true;
        _bandId.Text = string.Empty;
        _bandMinimum.Text = "-100";
        _bandMaximum.Text = "100";
        _bandPriority.Text = "1";
        _removeBand.Disabled = true;
        _bandEditorDirty = false;
        _updating = updating;
    }

    private void RebuildMigrationChoices(StudioFactionFoundationDraftUx4A draft)
    {
        _migrationChoices = _choices.Where(item => item.FactionId != draft.WorkingFactionId && !item.Archived).ToArray();
        RebuildOption(
            _migration,
            "Aucune · suppression sans référence migrée",
            _migrationChoices.Select(item => item.Label));
        if (draft.MigrationTargetId is { } target)
        {
            SelectChoice(_migration, _migrationChoices, target);
        }
    }

    private StudioFactionFoundationDraftUx4A? ReadDraft()
    {
        var source = _draft;
        if (source is null)
        {
            return null;
        }
        var selectedParents = Enumerable.Range(0, _parentChoices.Count)
            .Where(_parents.IsSelected)
            .Select(index => _parentChoices[index].FactionId)
            .ToArray();
        var selectedParentSet = selectedParents.ToHashSet();
        var parentIds = source.ParentIds.Where(selectedParentSet.Contains)
            .Concat(selectedParents.Where(item => !source.ParentIds.Contains(item)))
            .ToArray();
        var migrationIndex = _migration.Selected - 1;
        Guid? migration = migrationIndex >= 0 && migrationIndex < _migrationChoices.Count
            ? _migrationChoices[migrationIndex].FactionId
            : null;
        return source with
        {
            Operation = (StudioFactionOperationUx4A)_operation.Selected,
            Slug = _slug.Text,
            DisplayName = _displayName.Text,
            Archived = _archived.ButtonPressed,
            ParentIds = parentIds,
            InheritTags = _inheritTags.ButtonPressed,
            InheritPolicies = _inheritPolicies.ButtonPressed,
            InheritRelationships = _inheritRelationships.ButtonPressed,
            MigrationTargetId = migration,
            Relationships = _relationships.Select(item => item with { }).ToArray(),
            Bands = _bands.Select(item => item with { }).ToArray(),
        };
    }

    private void Preview()
    {
        if (_relationshipEditorDirty || _bandEditorDirty)
        {
            SetStatus(
                "Enregistrez ou retirez l’édition de relation ou de bande avant de prévisualiser.",
                error: true);
            return;
        }
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Choisissez explicitement une faction avant de prévisualiser.", error: true);
            return;
        }
        var result = _session.PreviewFactionFoundationUx4A(draft);
        _canonicalPreviewReady = result.Succeeded && result.CanApply;
        _apply.Disabled = !_canonicalPreviewReady;
        RenderDiagnostics(result.Diagnostics);
        _directionalSummary.Text = result.DirectionalSummary;
        _bandSummary.Text = result.BandSummary;
        var impactedMembers = result.Impact.LegacyMembers + result.Impact.AdvancedMemberships;
        var impactedReputation = result.Impact.PersonalReputation + result.Impact.ReputationTransactions +
                                 result.Impact.HeatmapRelationships;
        var impact = result.Impact.Total == 0
            ? "Aucune référence interne affectée."
            : $"Impact interne : {result.Impact.Total} référence(s) · " +
              $"parents {result.Impact.ChildParents}, relations {result.Impact.Relationships}, " +
              $"membres {impactedMembers}, héritage {result.Impact.InheritancePolicies}, " +
              $"témoins {result.Impact.WitnessPolicies}, factions dynamiques {result.Impact.DynamicFactions}, " +
              $"diplomatie {result.Impact.DiplomacyStates}, réputation {impactedReputation}.";
        _summary.Text = result.Succeeded
            ? $"APERÇU · aucune écriture\n{result.PatchSummary}\n{impact}\nInverse : {result.InverseSummary}"
            : $"APERÇU REFUSÉ · aucune écriture\n{impact}";
        _summary.AddThemeColorOverride(
            "font_color",
            result.Succeeded ? StudioStyle.Warning : StudioStyle.Danger);
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            return;
        }
        var selectedId = draft.WorkingFactionId;
        var removed = draft.Operation == StudioFactionOperationUx4A.Remove;
        var result = _session.ApplyApprovedFactionFoundationUx4A(draft);
        if (!result.Succeeded)
        {
            _canonicalPreviewReady = false;
            _apply.Disabled = true;
            SetStatus(result.Message, error: true);
            return;
        }
        FactionChanged?.Invoke();
        ReloadAfterMutation(removed ? null : selectedId);
        _applied = true;
        SetStatus(result.Message, error: false);
    }

    private void Undo()
    {
        var result = _session.UndoFactionFoundationUx4A();
        ReloadAfterMutation(_draft?.WorkingFactionId);
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            FactionChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.RedoFactionFoundationUx4A();
        ReloadAfterMutation(_draft?.WorkingFactionId);
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            FactionChanged?.Invoke();
        }
    }

    private void ReloadAfterMutation(Guid? preferredId)
    {
        _choices = _session.FactionChoicesUx4A;
        _updating = true;
        _faction.Clear();
        _faction.AddItem("Choisissez explicitement une faction…");
        foreach (var choice in _choices)
        {
            _faction.AddItem(choice.Label);
        }
        var index = preferredId is { } id ? _choices.ToList().FindIndex(item => item.FactionId == id) : -1;
        _faction.Select(index >= 0 ? index + 1 : 0);
        _updating = false;
        if (index >= 0)
        {
            LoadDraft(_session.CreateFactionDraftUx4A(_choices[index].FactionId), newAction: false);
        }
        else
        {
            ClearSelection();
        }
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
    }

    private void UpdateOperationState()
    {
        if (_draft is null)
        {
            return;
        }
        var editing = _operation.Selected == (int)StudioFactionOperationUx4A.Edit;
        var removing = _operation.Selected == (int)StudioFactionOperationUx4A.Remove;
        if (!editing && (_relationshipEditorDirty || _bandEditorDirty))
        {
            ClearRelationshipEditor();
            ClearBandEditor();
        }
        _displayName.Editable = editing;
        _slug.Editable = editing;
        _archived.Disabled = !editing;
        _parents.MouseFilter = editing ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _inheritTags.Disabled = !editing;
        _inheritPolicies.Disabled = !editing;
        _inheritRelationships.Disabled = !editing;
        _relationshipSource.Disabled = !editing;
        _relationshipTarget.Disabled = !editing;
        _relationshipScore.Editable = editing;
        _relationshipPreset.Editable = editing;
        _relationshipExplanation.Editable = editing;
        _saveRelationship.Disabled = !editing;
        _removeRelationship.Disabled = !editing || _relationshipList.Selected <= 0;
        _bandId.Editable = editing;
        _bandMinimum.Editable = editing;
        _bandMaximum.Editable = editing;
        _bandPriority.Editable = editing;
        _saveBand.Disabled = !editing;
        _removeBand.Disabled = !editing || _bandList.Selected <= 0;
        _migration.Disabled = !removing;
        _lifecycleHint.Text = removing
            ? "Suppression : les membres et références internes sont planifiés par le service canonique. Une créature, un placeable ou une autre dépendance externe bloque honnêtement l’application."
            : _operation.Selected == (int)StudioFactionOperationUx4A.Archive
                ? "Archivage : la définition reste référencée et toutes ses données voisines sont conservées."
                : "Modification : identité technique, tags, politiques, réputation personnelle et extensions voisines restent masqués et préservés.";
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        _session.CancelFactionFoundationUx4A();
        _canonicalPreviewReady = false;
        _applied = false;
        _apply.Disabled = true;
        RenderDiagnostics([]);
        UpdateDirectionalSummary();
        UpdateBandSummary();
        _summary.Text = "Brouillon modifié · prévisualisez à nouveau avant toute application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetStatus("Aperçu périmé · nouvelle prévisualisation requise", error: false);
    }

    private void MarkRelationshipEditorDirty()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        _relationshipEditorDirty = true;
        InvalidatePreview();
    }

    private void MarkBandEditorDirty()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        _bandEditorDirty = true;
        InvalidatePreview();
    }

    private void UpdateSummaries(Forge.Domain.Factions.FactionCatalog catalog)
    {
        _ = catalog;
        UpdateDirectionalSummary();
        UpdateBandSummary();
    }

    private void UpdateDirectionalSummary()
    {
        RebuildDirectionalMatrix();
        var directions = _relationships.Take(4).Select(item =>
            $"{ChoiceName(item.SourceId)} → {ChoiceName(item.TargetId)} : {item.Score} · {item.Preset}");
        _directionalSummary.Text = _relationships.Count == 0
            ? "Aucune relation authorée."
            : $"{_relationships.Count} direction(s) indépendante(s) · {string.Join(" | ", directions)}";
    }

    private void RebuildDirectionalMatrix()
    {
        foreach (var child in _relationshipMatrix.GetChildren())
        {
            _relationshipMatrix.RemoveChild(child);
            child.QueueFree();
        }
        _relationshipMatrix.Columns = Math.Max(1, _relationshipChoices.Count + 1);
        _relationshipMatrix.AddChild(MatrixCell("Source ↓ / Cible →", header: true));
        foreach (var target in _relationshipChoices)
        {
            _relationshipMatrix.AddChild(MatrixCell(target.Label, header: true));
        }
        foreach (var source in _relationshipChoices)
        {
            _relationshipMatrix.AddChild(MatrixCell(source.Label, header: true));
            foreach (var target in _relationshipChoices)
            {
                var relationship = _relationships.SingleOrDefault(item =>
                    item.SourceId == source.FactionId && item.TargetId == target.FactionId);
                var text = relationship is null
                    ? "—"
                    : $"{relationship.Score}\n{relationship.Preset}";
                _relationshipMatrix.AddChild(MatrixCell(text, header: false));
            }
        }
    }

    private static Label MatrixCell(string text, bool header)
    {
        var label = StudioStyle.Label(
            text,
            header ? 9 : 10,
            header ? StudioStyle.Muted : StudioStyle.Accent,
            wrap: true);
        label.CustomMinimumSize = new Vector2(138, 42);
        return label;
    }

    private void UpdateBandSummary()
    {
        _bandSummary.Text = _bands.Count == 0
            ? "Aucune bande authorée."
            : string.Join(" · ", _bands.Select(item =>
                $"{item.Id} {item.Minimum}…{item.Maximum} (priorité {item.Priority})"));
    }

    private void RenderDiagnostics(IReadOnlyList<StudioFactionDiagnosticUx4A> diagnostics)
    {
        foreach (var child in _diagnostics.GetChildren())
        {
            child.QueueFree();
        }
        foreach (var diagnostic in diagnostics.Take(5))
        {
            _diagnostics.AddChild(StudioStyle.Label(
                $"• {diagnostic.HumanMessage}",
                9,
                StudioStyle.Danger,
                wrap: true));
        }
    }

    private void SetEditingEnabled(bool enabled)
    {
        _operation.Disabled = !enabled || _draft?.IsNew == true;
        _preview.Disabled = !enabled;
        _apply.Disabled = !enabled || !_canonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        if (enabled)
        {
            UpdateOperationState();
        }
    }

    private void ResetControls()
    {
        _updating = true;
        _displayName.Text = string.Empty;
        _slug.Text = string.Empty;
        _archived.ButtonPressed = false;
        _parents.Clear();
        _inheritTags.ButtonPressed = false;
        _inheritPolicies.ButtonPressed = false;
        _inheritRelationships.ButtonPressed = false;
        _operation.Select(0);
        _migration.Clear();
        _migration.AddItem("Aucune · suppression sans référence migrée");
        _relationships.Clear();
        _bands.Clear();
        RebuildOption(_relationshipSource, "Source…", []);
        RebuildOption(_relationshipTarget, "Cible…", []);
        RebuildRelationshipList();
        RebuildBandList();
        _updating = false;
    }

    private void ConnectEvents()
    {
        _faction.ItemSelected += SelectFaction;
        _newFaction.Pressed += StartNewFaction;
        _operation.ItemSelected += _ =>
        {
            UpdateOperationState();
            InvalidatePreview();
        };
        _displayName.TextChanged += _ => InvalidatePreview();
        _slug.TextChanged += _ => InvalidatePreview();
        _archived.Toggled += _ => InvalidatePreview();
        _parents.MultiSelected += (_, _) => InvalidatePreview();
        _inheritTags.Toggled += _ => InvalidatePreview();
        _inheritPolicies.Toggled += _ => InvalidatePreview();
        _inheritRelationships.Toggled += _ => InvalidatePreview();
        _migration.ItemSelected += _ => InvalidatePreview();
        _relationshipList.ItemSelected += SelectRelationship;
        _relationshipSource.ItemSelected += _ => MarkRelationshipEditorDirty();
        _relationshipTarget.ItemSelected += _ => MarkRelationshipEditorDirty();
        _relationshipScore.TextChanged += _ => MarkRelationshipEditorDirty();
        _relationshipPreset.TextChanged += _ => MarkRelationshipEditorDirty();
        _relationshipExplanation.TextChanged += _ => MarkRelationshipEditorDirty();
        _saveRelationship.Pressed += SaveRelationship;
        _removeRelationship.Pressed += RemoveRelationship;
        _bandList.ItemSelected += SelectBand;
        _bandId.TextChanged += _ => MarkBandEditorDirty();
        _bandMinimum.TextChanged += _ => MarkBandEditorDirty();
        _bandMaximum.TextChanged += _ => MarkBandEditorDirty();
        _bandPriority.TextChanged += _ => MarkBandEditorDirty();
        _saveBand.Pressed += SaveBand;
        _removeBand.Pressed += RemoveBand;
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
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
        var index = _choices.ToList().FindIndex(item => item.Slug == "ash-watch");
        if (index < 0)
        {
            Close();
            return false;
        }
        _updating = true;
        _faction.Select(index + 1);
        _updating = false;
        LoadDraft(_session.CreateFactionDraftUx4A(_choices[index].FactionId), newAction: false);
        if (_draft is null || !ConfigureEvidenceDraft())
        {
            Close();
            return false;
        }
        var evidenceHistory = _session.History;
        var evidenceProject = _session.Project;
        if (evidenceHistory is null || evidenceProject is null)
        {
            Close();
            return false;
        }
        var evidenceRevision = evidenceHistory.State.Revision;
        var evidenceJournal = evidenceHistory.Journal.ToArray();
        var evidenceCanUndo = evidenceHistory.CanUndo;
        var evidenceCanRedo = evidenceHistory.CanRedo;
        var evidenceDirty = _session.IsDirty;
        Preview();
        _lastEvidencePreviewDryRun = ReferenceEquals(evidenceProject, _session.Project) &&
                                     ReferenceEquals(evidenceHistory, _session.History) &&
                                     evidenceHistory.State.Revision == evidenceRevision &&
                                     evidenceHistory.Journal.SequenceEqual(evidenceJournal) &&
                                     evidenceHistory.CanUndo == evidenceCanUndo &&
                                     evidenceHistory.CanRedo == evidenceCanRedo &&
                                     _session.IsDirty == evidenceDirty && EvidenceSourceUntouched;
        var evidenceOrigin = UsesRealCryptEvidence
            ? "CAPTURE RÉELLE CRYPTE · copie temporaire du projet Crypt sélectionné"
            : "FALLBACK CANONIQUE TEMPORAIRE · fixture factions isolée pour la qualification headless";
        _summary.Text = $"{evidenceOrigin} · source strictement inchangée.\n{_summary.Text}";
        var ready = _lastEvidencePreviewDryRun && HasExplicitSelection && CanonicalPreviewReady &&
                    ShowsHumanSurface && ShowsDirectionalMatrix && ShowsHierarchy && ShowsBands &&
                    UsesIsolatedEvidence && EvidenceSourceUntouched &&
                    (UsesRealCryptEvidence ^ UsesCanonicalFallbackEvidence);
        if (!ready)
        {
            SetStatus(
                CanonicalPreviewReady
                    ? "Preuve isolée incomplète ou non pure · session temporaire libérée sans application"
                    : "Preuve isolée créée, mais l’aperçu canonique a été refusé · session temporaire libérée",
                error: true);
            Close();
            return false;
        }
        SetStatus(
            UsesRealCryptEvidence
                ? "Preuve Crypt réelle isolée prête · fermeture = suppression de la session temporaire"
                : "Preuve fallback canonique isolée prête · le module source reste strictement inchangé",
            error: false);
        return true;
    }

    private bool ConfigureEvidenceDraft()
    {
        var covenant = _choices.SingleOrDefault(item => item.Slug == "crypt-covenant");
        var surface = _choices.SingleOrDefault(item => item.Slug == "surface-alliance");
        if (covenant is null || surface is null)
        {
            return false;
        }
        var index = _relationships.FindIndex(item =>
            item.SourceId == covenant.FactionId && item.TargetId == surface.FactionId);
        if (index < 0)
        {
            return false;
        }
        _relationships[index] = _relationships[index] with
        {
            Score = "-68",
            Preset = "hostile",
            Explanation = "intrusion visible dans la crypte",
        };
        RebuildRelationshipList();
        return true;
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
            var ready = PrepareEvidence("ux4-a-faction-foundation");
            if (!ready || _evidenceSession is null || ReferenceEquals(_session, _sourceSession) ||
                !ReferenceEquals(_session, _evidenceSession))
            {
                return false;
            }
            var transactionSession = _session;
            var transactionHistory = transactionSession.History;
            var originalProject = transactionSession.Project;
            var originalRevision = transactionHistory?.State.Revision;
            var originalJournal = transactionHistory?.Journal.ToArray() ?? [];
            var originalCanUndo = transactionHistory?.CanUndo;
            var originalCanRedo = transactionHistory?.CanRedo;
            var originalDirty = transactionSession.IsDirty;
            var draft = ReadDraft();
            var dryRun = draft is not null && originalProject is not null && transactionHistory is not null &&
                         ReferenceEquals(originalProject, transactionSession.Project) &&
                         ReferenceEquals(transactionHistory, transactionSession.History) &&
                         transactionHistory.State.Revision == originalRevision &&
                         transactionHistory.Journal.SequenceEqual(originalJournal) &&
                         transactionHistory.CanUndo == originalCanUndo &&
                         transactionHistory.CanRedo == originalCanRedo &&
                         transactionSession.IsDirty == originalDirty && SourceMatchesSnapshot(sourceSnapshot);
            if (!dryRun)
            {
                return false;
            }
            Apply();
            var appliedProject = transactionSession.Project;
            var applied = _applied && appliedProject is not null &&
                           !ReferenceEquals(originalProject, appliedProject);
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
            if (saved && transactionSession.Session is { } workspace)
            {
                reopened = new StudioSessionController();
                persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(transactionSession.Project!);
            }
            var sourceUntouched = SourceMatchesSnapshot(sourceSnapshot);
            reopened?.Close();
            reopened = null;
            Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX4A_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceTempRoot is not null)
            {
                Close();
            }
        }
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx4AEvidenceProject();
        if (snapshot is null || evidenceProject is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux4a-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject.Project);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot).Succeeded)
            {
                evidenceSession.Close();
                _lastEvidenceResourcesReleased = DeleteEvidenceRoot(tempRoot);
                _evidenceTempRoot = _lastEvidenceResourcesReleased ? null : tempRoot;
                _session = _sourceSession;
                _lastSourceSessionRestored = true;
                return false;
            }
            _evidenceSourceSnapshot = snapshot;
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            _evidenceUsesRealCryptSource = evidenceProject.UsesRealCryptSource;
            _evidenceUsesCanonicalFallback = evidenceProject.UsesCanonicalFallback;
            _lastEvidencePreviewDryRun = false;
            _lastEvidenceResourcesReleased = false;
            _lastSourceSessionRestored = false;
            OpenBoundSession();
            var ready = _choices.Any(item => item.Slug == "ash-watch") && SourceMatchesSnapshot(snapshot) &&
                        (_evidenceUsesRealCryptSource ^ _evidenceUsesCanonicalFallback);
            if (!ready)
            {
                Close();
            }
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX4A_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            evidenceSession.Close();
            var released = DeleteEvidenceRoot(tempRoot);
            _evidenceSourceSnapshot = null;
            _evidenceSession = null;
            _evidenceTempRoot = released ? null : tempRoot;
            _session = _sourceSession;
            _evidenceUsesRealCryptSource = false;
            _evidenceUsesCanonicalFallback = false;
            _lastEvidencePreviewDryRun = false;
            _lastEvidenceResourcesReleased = released;
            _lastSourceSessionRestored = true;
            return false;
        }
    }

    private StudioFactionEvidenceSourceSnapshotUx4A? CaptureSourceSnapshot()
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
            _sourceSession.CaptureFactionApprovalUx4A());
    }

    private bool SourceMatchesSnapshot(StudioFactionEvidenceSourceSnapshotUx4A snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureFactionApprovalUx4A() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceSourceSnapshot = null;
        _session = _sourceSession;
        _evidenceUsesRealCryptSource = false;
        _evidenceUsesCanonicalFallback = false;
        _lastEvidencePreviewDryRun = false;
        var released = true;
        try
        {
            evidenceSession?.Close();
        }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX4A_EVIDENCE_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
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
                !leaf.StartsWith("openrpg-forge-studio-ux4a-evidence-", StringComparison.Ordinal))
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
                $"OPENRPG_FORGE_STUDIO_UX4A_EVIDENCE_DELETE_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
    }

    private string ChoiceName(Guid id) =>
        _choices.SingleOrDefault(item => item.FactionId == id)?.Label ?? "Faction préservée";

    private static void RebuildOption(OptionButton option, string placeholder, IEnumerable<string> values)
    {
        option.Clear();
        option.AddItem(placeholder);
        foreach (var value in values)
        {
            option.AddItem(value);
        }
        option.Select(0);
    }

    private static void SelectChoice(
        OptionButton option,
        IReadOnlyList<StudioFactionChoiceUx4A> choices,
        Guid id)
    {
        var index = choices.ToList().FindIndex(item => item.FactionId == id);
        option.Select(index >= 0 ? index + 1 : 0);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Accent);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        const float inset = 24;
        _dialog.Position = new Vector2(inset, inset);
        _dialog.Size = new Vector2(
            Math.Max(960, Size.X - inset * 2),
            Math.Max(720, Size.Y - inset * 2));
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private static OptionButton Option(string placeholder, float width)
    {
        var option = new OptionButton { CustomMinimumSize = new Vector2(width, 34) };
        option.AddItem(placeholder);
        return option;
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static LineEdit Field(GridContainer grid, string label, string placeholder)
    {
        var field = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        AddControl(grid, label, field);
        return field;
    }

    private static void AddControl(GridContainer grid, string label, Control control)
    {
        grid.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        grid.AddChild(control);
    }

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);
        tabs.AddChild(scroll);
        return content;
    }

    private static IEnumerable<Label> DescendantLabels(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label)
            {
                yield return label;
            }
            foreach (var descendant in DescendantLabels(child))
            {
                yield return descendant;
            }
        }
    }
}
