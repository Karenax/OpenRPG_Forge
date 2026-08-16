using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed class StudioPlaceableAssemblyValidationOverlay : PanelContainer
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private readonly OptionButton _placeable;
    private readonly OptionButton _socketChoice;
    private readonly OptionButton _attachmentChoice;
    private readonly OptionButton _childBlueprint;
    private readonly LineEdit _socketId;
    private readonly LineEdit _socketKind;
    private readonly LineEdit _socketTags;
    private readonly CheckBox _snapEnabled;
    private readonly SpinBox _maximumAttachments;
    private readonly TransformEditor _socketTransform;
    private readonly LineEdit _attachmentTags;
    private readonly LineEdit _prefabId;
    private readonly TransformEditor _attachmentTransform;
    private readonly VBoxContainer _verbList;
    private readonly LineEdit _previewContext;
    private readonly LineEdit _animationIds;
    private readonly LineEdit _lockIds;
    private readonly LineEdit _trapIds;
    private readonly LineEdit _inventoryIds;
    private readonly LineEdit _physicsIds;
    private readonly SpinBox _drawCallBudget;
    private readonly SpinBox _scriptBudget;
    private readonly Label _availability;
    private readonly Label _assemblyPreview;
    private readonly Label _verbPreview;
    private readonly Label _validationPreview;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _apply;
    private readonly ScrollContainer _formScroll;
    private readonly List<BlueprintId> _placeableIds = [];
    private readonly List<BlueprintId> _childIds = [];
    private readonly List<Guid> _socketIds = [];
    private readonly List<Guid> _attachmentIds = [];
    private readonly List<VerbRow> _verbRows = [];
    private StudioUx1AACatalog? _catalog;
    private StudioUx1AAEvidenceWorkspace? _evidenceWorkspace;
    private StudioUx1AAEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private StudioPlaceableAssemblyValidationDraft? _draft;
    private StudioUx1AAPreview? _preview;
    private bool _loading;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;

    internal event Action? CloseRequested;
    internal event Action? ProjectChanged;
    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal int PlaceableCount => _placeableIds.Count;
    internal bool PreviewReady => _preview is { Succeeded: true, CanApply: true };
    internal bool UsesIsolatedEvidence => _evidenceWorkspace is not null;
    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceWorkspace is null && _lastEvidenceResourcesReleased;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = new StringBuilder()
                .Append(_socketId.Text)
                .Append(_socketKind.Text)
                .Append(_socketTags.Text)
                .Append(_attachmentTags.Text)
                .Append(_prefabId.Text)
                .Append(_previewContext.Text)
                .Append(_animationIds.Text)
                .Append(_lockIds.Text)
                .Append(_trapIds.Text)
                .Append(_inventoryIds.Text)
                .Append(_physicsIds.Text)
                .Append(_assemblyPreview.Text)
                .Append(_verbPreview.Text)
                .Append(_validationPreview.Text)
                .Append(_summary.Text)
                .Append(_status.Text);
            foreach (var row in _verbRows)
            {
                text.Append(row.HumanText);
            }
            return !text.ToString().Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.ToString().Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.ToString().Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.ToString().Contains('\\');
        }
    }

    internal StudioPlaceableAssemblyValidationOverlay(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioPlaceableAssemblyValidationOverlay";
        Visible = false;
        ZIndex = 21;
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Chrome, StudioStyle.Border, 9, 2));

        var margin = StudioStyle.Margin(18, 14, 18, 14);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        root.AddChild(header);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("SOCKETS, ACTIONS ET VALIDATION", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Assembler et vérifier un placeable sans données techniques cachées", 20));
        titles.AddChild(StudioStyle.Label(
            "Socket local · blueprint enfant · actions contextuelles · contrôles collision, références et budgets · aperçu obligatoire",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon";
        close.Pressed += () => CloseRequested?.Invoke();
        header.AddChild(close);

        var workspace = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        workspace.AddThemeConstantOverride("separation", 10);
        root.AddChild(workspace);
        _formScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
            CustomMinimumSize = new Vector2(980, 0),
        };
        workspace.AddChild(_formScroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 10);
        _formScroll.AddChild(form);

        var selection = AddCard(form, "PLACEABLE À CONFIGURER",
            "La sélection reste explicite. Les prérequis manquants sont expliqués et ne sont jamais créés implicitement.");
        _placeable = AddOption(selection, "Objet");
        _availability = StudioStyle.Label(string.Empty, 10, StudioStyle.Warning, wrap: true);
        selection.AddChild(_availability);

        var socket = AddCard(form, "SOCKET ET ASSEMBLAGE",
            "Choisissez un socket canonique. Les autres sockets et assemblages restent intacts.");
        _socketChoice = AddOption(socket, "Socket édité");
        _socketId = AddLine(socket, "Nom stable du socket", "assembly");
        _socketKind = AddLine(socket, "Type", "prop");
        _socketTags = AddLine(socket, "Tags acceptés", "lever, rune");
        var socketPolicy = new HBoxContainer();
        socketPolicy.AddThemeConstantOverride("separation", 12);
        socket.AddChild(socketPolicy);
        _snapEnabled = AddCheck(socketPolicy, "Aligner l’assemblage sur le socket");
        _maximumAttachments = AddSpin(socketPolicy, "Capacité", 1, 1000, 1, string.Empty);
        _socketTransform = new TransformEditor("Transformation locale du socket");
        socket.AddChild(_socketTransform.Root);

        _attachmentChoice = AddOption(socket, "Assemblage édité");
        _childBlueprint = AddOption(socket, "Blueprint enfant");
        _attachmentTags = AddLine(socket, "Tags de l’assemblage", "rune");
        _prefabId = AddLine(socket, "Prefab composé facultatif", "prefab:reliquaire-levier");
        _attachmentTransform = new TransformEditor("Transformation locale de l’assemblage");
        socket.AddChild(_attachmentTransform.Root);
        var previewAssembly = StudioStyle.Button("Prévisualiser l’assemblage", compact: true);
        socket.AddChild(previewAssembly);
        _assemblyPreview = PreviewLabel();
        socket.AddChild(_assemblyPreview);

        var verbs = AddCard(form, "INTERACTION MULTI-VERBES",
            "Les sept actions standard et les actions personnalisées sont ordonnées par priorité et filtrées par contexte.");
        _verbList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _verbList.AddThemeConstantOverride("separation", 7);
        verbs.AddChild(_verbList);
        _previewContext = AddLine(verbs, "Contexte de l’aperçu", "portable, unlocked, interlocutor");
        var previewVerbs = StudioStyle.Button("Prévisualiser les actions disponibles", compact: true);
        verbs.AddChild(previewVerbs);
        _verbPreview = PreviewLabel();
        verbs.AddChild(_verbPreview);

        var validation = AddCard(form, "VALIDATION DU PLACEABLE",
            "Déclarez les références autorisées et des budgets couvrant exactement les ressources canoniques.");
        _animationIds = AddLine(validation, "Animations autorisées", "animation:activation");
        _lockIds = AddLine(validation, "Références de verrou", "lock:principal");
        _trapIds = AddLine(validation, "Références de piège", "trap:principal");
        _inventoryIds = AddLine(validation, "Références d’inventaire", "inventory:principal");
        _physicsIds = AddLine(validation, "Profils physiques", "physics:principal");
        var budgets = new HBoxContainer();
        budgets.AddThemeConstantOverride("separation", 12);
        validation.AddChild(budgets);
        _drawCallBudget = AddSpin(budgets, "Budget d’appels de rendu", 1, 100000, 1, string.Empty);
        _scriptBudget = AddSpin(budgets, "Budget de scripts", 0, 100000, 1, string.Empty);
        var previewValidation = StudioStyle.Button("Vérifier les références et budgets", compact: true);
        validation.AddChild(previewValidation);
        _validationPreview = PreviewLabel();
        validation.AddChild(_validationPreview);

        var previewPanel = StudioStyle.Panel("Ux1AAPreview", StudioStyle.Surface, 8);
        previewPanel.CustomMinimumSize = new Vector2(430, 0);
        workspace.AddChild(previewPanel);
        var previewMargin = StudioStyle.Margin(14, 12, 14, 12);
        previewPanel.AddChild(previewMargin);
        var previewColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        previewColumn.AddThemeConstantOverride("separation", 9);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.Label("APERÇU GLOBAL", 10, StudioStyle.Accent));
        previewColumn.AddChild(StudioStyle.Label("Contrats avant application", 18));
        previewColumn.AddChild(StudioStyle.Label(
            "Chaque aperçu passe par la commande canonique en dry-run. Aucun nœud moteur ni fichier n’est modifié.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _summary = StudioStyle.Label(
            "Aucun aperçu global. Le module reste inchangé.",
            11,
            StudioStyle.Muted,
            wrap: true);
        _summary.SizeFlagsVertical = SizeFlags.ExpandFill;
        _summary.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        previewColumn.AddChild(_summary);
        var previewAll = StudioStyle.Button("Prévisualiser les trois parcours", primary: true);
        previewColumn.AddChild(previewAll);
        _apply = StudioStyle.Button("Appliquer", primary: true);
        _apply.Disabled = true;
        previewColumn.AddChild(_apply);
        var history = new HBoxContainer();
        history.AddThemeConstantOverride("separation", 8);
        previewColumn.AddChild(history);
        var undo = StudioStyle.Button("Annuler", compact: true);
        undo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        history.AddChild(undo);
        var redo = StudioStyle.Button("Rétablir", compact: true);
        redo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        history.AddChild(redo);
        _status = StudioStyle.Label("Surface prête · aucune écriture effectuée.", 10, StudioStyle.Muted, wrap: true);
        previewColumn.AddChild(_status);

        _placeable.ItemSelected += _ => SelectPlaceable();
        _socketChoice.ItemSelected += _ => SelectSocket();
        _attachmentChoice.ItemSelected += _ => SelectAttachment();
        _childBlueprint.ItemSelected += _ => OnChanged();
        foreach (var line in new[]
                 {
                     _socketId, _socketKind, _socketTags, _attachmentTags, _prefabId, _previewContext,
                     _animationIds, _lockIds, _trapIds, _inventoryIds, _physicsIds,
                 })
        {
            line.TextChanged += _ => OnChanged();
        }
        _snapEnabled.Toggled += _ => OnChanged();
        _maximumAttachments.ValueChanged += _ => OnChanged();
        _drawCallBudget.ValueChanged += _ => OnChanged();
        _scriptBudget.ValueChanged += _ => OnChanged();
        _socketTransform.Changed += OnChanged;
        _attachmentTransform.Changed += OnChanged;
        previewAssembly.Pressed += PreviewAssembly;
        previewVerbs.Pressed += PreviewVerbs;
        previewValidation.Pressed += PreviewValidation;
        previewAll.Pressed += PreviewAll;
        _apply.Pressed += Apply;
        undo.Pressed += Undo;
        redo.Pressed += Redo;
    }

    public override void _ExitTree()
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
    }

    internal void Open(BlueprintId? requestedBlueprintId = null)
    {
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
        OpenActive(requestedBlueprintId);
    }

    private void OpenActive(BlueprintId? requestedBlueprintId = null)
    {
        _session.CancelUx1AAPreview();
        Visible = true;
        _loading = true;
        var catalog = _session.CreateUx1AACatalog();
        _catalog = catalog;
        _placeable.Clear();
        _placeableIds.Clear();
        _placeable.AddItem("Choisir un placeable…");
        foreach (var option in catalog.Placeables)
        {
            _placeable.AddItem(option.Ready
                ? option.DisplayName
                : $"{option.DisplayName} · prérequis manquant");
            _placeableIds.Add(option.BlueprintId);
        }
        _placeable.Disabled = _placeableIds.Count == 0;
        _availability.Text = _placeableIds.Count == 0
            ? "État vide · ce module ne contient aucun blueprint Placeable."
            : string.Empty;
        var requested = requestedBlueprintId is null
            ? -1
            : _placeableIds.FindIndex(id => id == requestedBlueprintId);
        _placeable.Select(requested < 0 ? 0 : requested + 1);
        _loading = false;
        if (_placeable.Selected > 0)
        {
            SelectPlaceable();
        }
        else
        {
            ClearDraft("Choisissez explicitement un placeable pour ouvrir ses sockets, actions et contrôles.");
            SetNeutralStatus("Aucune sélection implicite · le module reste inchangé.");
        }
    }

    internal void Close()
    {
        _session.CloseUx1AA();
        _preview = null;
        _draft = null;
        Visible = false;
        ReleaseEvidenceWorkspace();
        CompleteEvidenceSourceAudit();
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        Open();
        _evidenceSourceSnapshot = sourceSnapshot;
        _lastEvidenceSourceUntouched = false;
        var readyIndex = ReadyPlaceableIndex();
        if (readyIndex < 0 && ActivateEvidenceWorkspace())
        {
            readyIndex = ReadyPlaceableIndex();
        }
        if (readyIndex < 0)
        {
            Close();
            return false;
        }
        _placeable.Select(readyIndex + 1);
        SelectPlaceable();
        if (_draft is null)
        {
            Close();
            return false;
        }
        SetEvidenceValues();
        PreviewAssembly();
        PreviewVerbs();
        PreviewValidation();
        PreviewAll();
        _formScroll.SetDeferred("scroll_vertical",
            mode.Contains("validation", StringComparison.OrdinalIgnoreCase) ? int.MaxValue : 0);
        var ready = PreviewReady && ShowsHumanSurface && SourceMatchesSnapshot(sourceSnapshot);
        if (!ready)
        {
            Close();
        }
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        Open();
        _evidenceSourceSnapshot = sourceSnapshot;
        _lastEvidenceSourceUntouched = false;
        if (!ActivateEvidenceWorkspace())
        {
            Close();
            return false;
        }
        var readyIndex = ReadyPlaceableIndex();
        if (readyIndex < 0)
        {
            Close();
            return false;
        }
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        _placeable.Select(readyIndex + 1);
        SelectPlaceable();
        if (_draft is null)
        {
            Close();
            return false;
        }
        var blueprintId = _draft.BlueprintId;
        var minimumDrawCallBudget = _draft.Validation.MinimumDrawCallBudget;
        var minimumScriptBudget = _draft.Validation.MinimumScriptBudget;
        SetEvidenceValues();
        var expectedSocketId = _socketId.Text;
        PreviewAssembly();
        PreviewVerbs();
        PreviewValidation();
        PreviewAll();
        var visible = PreviewReady && ShowsHumanSurface;
        var applied = _session.ApplyApprovedUx1AA();
        var profile = _session.Project?.Module.WorldFeatures.Placeables.SingleOrDefault(item =>
            item.BlueprintId == blueprintId);
        var composition = profile?.CompositionEvents;
        var validation = profile?.Foundation?.Validation;
        var canonical = applied.Succeeded && composition is not null && composition.Sockets.Any(socket =>
                            socket.SocketId == expectedSocketId && socket.Kind == "assembly-support" &&
                            socket.SnapEnabled && socket.MaximumAttachments >= 2) == true &&
                        composition.Attachments.Any(attachment =>
                            attachment.SocketId == expectedSocketId &&
                            attachment.PrefabId == "prefab:ux1-aa-assembly") &&
                        composition.MultiVerb.Verbs.Count >= 8 && validation is not null &&
                        validation.DrawCallBudget >= minimumDrawCallBudget &&
                        validation.ScriptBudget >= minimumScriptBudget;
        var undo = _session.Undo();
        var restored = undo.Succeeded && ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        Close();
        var sourceUntouched = EvidenceSourceUntouched;
        var valid = visible && canonical && restored && sourceUntouched;
        if (!valid)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AA_UI_TRANSACTION_FAILED visible={visible.ToString().ToLowerInvariant()} " +
                $"apply={applied.Succeeded.ToString().ToLowerInvariant()} canonical={canonical.ToString().ToLowerInvariant()} " +
                $"undo={undo.Succeeded.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()} " +
                $"source_untouched={sourceUntouched.ToString().ToLowerInvariant()}");
        }
        return valid;
    }

    private int ReadyPlaceableIndex() =>
        _catalog?.Placeables.ToList().FindIndex(option => option.Ready) ?? -1;

    private bool ActivateEvidenceWorkspace()
    {
        ReleaseEvidenceWorkspace();
        var workspace = _sourceSession.CreateUx1AAEvidenceWorkspace();
        if (workspace is null)
        {
            return false;
        }
        _evidenceWorkspace = workspace;
        _lastEvidenceResourcesReleased = false;
        _session = workspace.Session;
        OpenActive();
        return true;
    }

    private void ReleaseEvidenceWorkspace()
    {
        if (_evidenceWorkspace is null)
        {
            _session = _sourceSession;
            return;
        }
        var workspace = _evidenceWorkspace;
        _evidenceWorkspace = null;
        _session = _sourceSession;
        workspace.Dispose();
        _lastEvidenceResourcesReleased = workspace.ResourcesReleased;
        if (!_lastEvidenceResourcesReleased)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AA_EVIDENCE_RELEASE_FAILED resources_released=false");
        }
    }

    private StudioUx1AAEvidenceSourceSnapshot? CaptureSourceSnapshot()
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
            _sourceSession.SelectedEntityId);
    }

    private bool SourceMatchesSnapshot(StudioUx1AAEvidenceSourceSnapshot snapshot)
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
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId;
    }

    private void CompleteEvidenceSourceAudit()
    {
        if (_evidenceSourceSnapshot is not { } snapshot)
        {
            return;
        }
        _lastEvidenceSourceUntouched = SourceMatchesSnapshot(snapshot);
        _evidenceSourceSnapshot = null;
        if (!_lastEvidenceSourceUntouched)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX1AA_EVIDENCE_SOURCE_CHANGED source_untouched=false");
        }
    }

    private void SelectPlaceable()
    {
        if (_loading || _placeable.Selected <= 0 || _placeable.Selected > _placeableIds.Count)
        {
            return;
        }
        var blueprintId = _placeableIds[_placeable.Selected - 1];
        var draft = _session.CreateUx1AADraft(blueprintId);
        if (draft is null)
        {
            _availability.Text = _session.DescribeUx1AAAvailability(blueprintId);
            ClearDraft("Ce placeable ne peut pas être modifié ici tant que ses prérequis canoniques manquent.");
            SetStatus(_availability.Text, true);
            return;
        }
        _availability.Text = "Prérequis canoniques vérifiés · aucune capacité voisine ne sera synthétisée.";
        _availability.AddThemeColorOverride("font_color", StudioStyle.Success);
        Load(draft);
    }

    private void SelectSocket()
    {
        if (_loading || _draft is null || _socketChoice.Selected < 0 ||
            _socketChoice.Selected >= _socketIds.Count)
        {
            return;
        }
        var selected = _socketIds[_socketChoice.Selected];
        Load(_session.CreateUx1AADraft(_draft.BlueprintId, selected));
        SetNeutralStatus("Socket chargé · les modifications non appliquées du socket précédent ont été abandonnées.");
    }

    private void SelectAttachment()
    {
        if (_loading || _draft is null || _attachmentChoice.Selected < 0 ||
            _attachmentChoice.Selected >= _attachmentIds.Count)
        {
            return;
        }
        var socketId = _draft.Socket.Id;
        var selected = _attachmentIds[_attachmentChoice.Selected];
        Load(_session.CreateUx1AADraft(_draft.BlueprintId, socketId, selected));
        SetNeutralStatus("Assemblage chargé · les autres assemblages restent intacts.");
    }

    private void Load(StudioPlaceableAssemblyValidationDraft? draft)
    {
        _loading = true;
        _session.CancelUx1AAPreview();
        _draft = draft;
        _preview = null;
        _apply.Disabled = true;
        if (draft is null)
        {
            _loading = false;
            ClearDraft("Le profil sélectionné n’est plus disponible.");
            return;
        }
        _socketChoice.Clear();
        _socketIds.Clear();
        if (draft.SocketChoices.Count == 0)
        {
            _socketChoice.AddItem("Nouveau socket canonique");
            _socketIds.Add(draft.Socket.Id);
        }
        else
        {
            foreach (var option in draft.SocketChoices)
            {
                _socketChoice.AddItem(option.DisplayName);
                _socketIds.Add(option.Id);
            }
        }
        _socketChoice.Select(Math.Max(0, _socketIds.FindIndex(id => id == draft.Socket.Id)));
        _socketId.Text = draft.Socket.SocketId;
        _socketId.Editable = !draft.Socket.Existing;
        _socketKind.Text = draft.Socket.Kind;
        _socketTags.Text = draft.Socket.AllowedTagsText;
        _snapEnabled.ButtonPressed = draft.Socket.SnapEnabled;
        _maximumAttachments.Value = draft.Socket.MaximumAttachments;
        _socketTransform.Load(draft.Socket.Transform);

        _attachmentChoice.Clear();
        _attachmentIds.Clear();
        if (draft.AttachmentChoices.Count == 0)
        {
            _attachmentChoice.AddItem("Nouvel assemblage canonique");
            _attachmentIds.Add(draft.Attachment.Id);
        }
        else
        {
            foreach (var option in draft.AttachmentChoices)
            {
                _attachmentChoice.AddItem(option.DisplayName);
                _attachmentIds.Add(option.Id);
            }
        }
        _attachmentChoice.Select(Math.Max(0, _attachmentIds.FindIndex(id => id == draft.Attachment.Id)));
        _childBlueprint.Clear();
        _childIds.Clear();
        _childBlueprint.AddItem("Choisir un blueprint enfant…");
        var childOptions = _catalog?.ChildPlaceables
                               .Where(option => option.BlueprintId != draft.BlueprintId)
                               .ToArray() ?? [];
        foreach (var option in childOptions)
        {
            _childBlueprint.AddItem(option.DisplayName);
            _childIds.Add(option.BlueprintId);
        }
        var childIndex = draft.Attachment.ChildBlueprintId is null
            ? -1
            : _childIds.FindIndex(id => id == draft.Attachment.ChildBlueprintId);
        _childBlueprint.Select(childIndex < 0 ? 0 : childIndex + 1);
        _attachmentTags.Text = draft.Attachment.TagsText;
        _prefabId.Text = draft.Attachment.PrefabId;
        _attachmentTransform.Load(draft.Attachment.Transform);

        RebuildVerbRows(draft.Verbs);
        _previewContext.Text = draft.PreviewContextText;
        _animationIds.Text = draft.Validation.AnimationIdsText;
        _lockIds.Text = draft.Validation.LockIdsText;
        _trapIds.Text = draft.Validation.TrapIdsText;
        _inventoryIds.Text = draft.Validation.InventoryIdsText;
        _physicsIds.Text = draft.Validation.PhysicsProfileIdsText;
        _drawCallBudget.Value = draft.Validation.DrawCallBudget;
        _scriptBudget.Value = draft.Validation.ScriptBudget;
        ResetPreviewLabels();
        _summary.Text = "Aucun aperçu global. Les données du module restent inchangées.";
        _loading = false;
        SetNeutralStatus("Configuration chargée sans mutation.");
    }

    private void ClearDraft(string summary)
    {
        _session.CancelUx1AAPreview();
        _draft = null;
        _preview = null;
        _apply.Disabled = true;
        _summary.Text = summary;
        ResetPreviewLabels();
    }

    private StudioPlaceableAssemblyValidationDraft? Capture()
    {
        if (_draft is null)
        {
            return null;
        }
        var child = _childBlueprint.Selected <= 0 || _childBlueprint.Selected > _childIds.Count
            ? null
            : _childIds[_childBlueprint.Selected - 1];
        return _draft with
        {
            Socket = _draft.Socket with
            {
                SocketId = _socketId.Text,
                Kind = _socketKind.Text,
                Transform = _socketTransform.Capture(),
                AllowedTagsText = _socketTags.Text,
                SnapEnabled = _snapEnabled.ButtonPressed,
                MaximumAttachments = (int)_maximumAttachments.Value,
            },
            Attachment = _draft.Attachment with
            {
                SocketId = _socketId.Text,
                ChildBlueprintId = child,
                TagsText = _attachmentTags.Text,
                Transform = _attachmentTransform.Capture(),
                PrefabId = _prefabId.Text,
            },
            Verbs = _verbRows.Select(row => row.Capture()).ToArray(),
            PreviewContextText = _previewContext.Text,
            Validation = _draft.Validation with
            {
                AnimationIdsText = _animationIds.Text,
                LockIdsText = _lockIds.Text,
                TrapIdsText = _trapIds.Text,
                InventoryIdsText = _inventoryIds.Text,
                PhysicsProfileIdsText = _physicsIds.Text,
                DrawCallBudget = (int)_drawCallBudget.Value,
                ScriptBudget = (int)_scriptBudget.Value,
            },
        };
    }

    private void PreviewAssembly() => PreviewPath(_session.PreviewUx1AAAssembly, _assemblyPreview);

    private void PreviewVerbs() => PreviewPath(_session.PreviewUx1AAMultiVerb, _verbPreview);

    private void PreviewValidation() => PreviewPath(_session.PreviewUx1AAValidation, _validationPreview);

    private void PreviewPath(
        Func<StudioPlaceableAssemblyValidationDraft, StudioUx1AAPathPreview> preview,
        Label target)
    {
        var draft = Capture();
        if (draft is null)
        {
            return;
        }
        var result = preview(draft);
        target.Text = result.Summary;
        target.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Warning);
        SetStatus(result.Summary, !result.Succeeded);
    }

    private void PreviewAll()
    {
        var draft = Capture();
        if (draft is null)
        {
            return;
        }
        _preview = _session.PreviewUx1AA(draft);
        _summary.Text = _preview.Summary;
        _summary.AddThemeColorOverride("font_color", _preview.Succeeded ? StudioStyle.Success : StudioStyle.Warning);
        _apply.Disabled = !_preview.CanApply;
        SetStatus(_preview.Message, !_preview.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedUx1AA();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }
        var selected = _draft?.BlueprintId;
        ProjectChanged?.Invoke();
        Open(selected);
        SetStatus(result.Message, false);
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            var selected = _draft?.BlueprintId;
            ProjectChanged?.Invoke();
            Open(selected);
            SetStatus(result.Message, false);
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            var selected = _draft?.BlueprintId;
            ProjectChanged?.Invoke();
            Open(selected);
            SetStatus(result.Message, false);
        }
    }

    private void OnChanged()
    {
        if (_loading)
        {
            return;
        }
        _session.CancelUx1AAPreview();
        _preview = null;
        _apply.Disabled = true;
        _summary.Text = "Aperçu à recalculer · aucune donnée n’a été appliquée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        ResetPreviewLabels();
        SetNeutralStatus("Brouillon modifié · prévisualisez à nouveau avant d’appliquer.");
    }

    private void SetEvidenceValues()
    {
        if (_draft is null)
        {
            return;
        }
        _loading = true;
        if (!_draft.Socket.Existing)
        {
            _socketId.Text = "ux1-aa-assembly";
        }
        _socketKind.Text = "assembly-support";
        _socketTags.Text = MergeValues(_socketTags.Text, "assembly", "ux1-aa");
        _snapEnabled.ButtonPressed = true;
        _maximumAttachments.Value = Math.Max(2, _maximumAttachments.Value);
        _socketTransform.SetEvidence(new(1, 1, 0), new(0, 90, 0), Vector3Value.One, Vector3Value.Zero, .25, true);
        if (_childIds.Count > 0)
        {
            _childBlueprint.Select(1);
        }
        _attachmentTags.Text = "ux1-aa";
        _prefabId.Text = "prefab:ux1-aa-assembly";
        _attachmentTransform.SetEvidence(new(.5f, 0, 0), new(0, 45, 0), Vector3Value.One,
            Vector3Value.Zero, .25, false);
        foreach (var row in _verbRows)
        {
            row.SetEvidence();
        }
        _previewContext.Text = "portable, custom-context, unlocked, interlocutor, climbable";
        if (string.IsNullOrWhiteSpace(_lockIds.Text)) _lockIds.Text = "lock:ux1-aa";
        if (string.IsNullOrWhiteSpace(_trapIds.Text)) _trapIds.Text = "trap:ux1-aa";
        if (string.IsNullOrWhiteSpace(_inventoryIds.Text)) _inventoryIds.Text = "inventory:ux1-aa";
        if (string.IsNullOrWhiteSpace(_physicsIds.Text)) _physicsIds.Text = "physics:ux1-aa";
        _drawCallBudget.Value = Math.Max(4, _draft.Validation.MinimumDrawCallBudget);
        _scriptBudget.Value = Math.Max(3, _draft.Validation.MinimumScriptBudget);
        _loading = false;
        OnChanged();
    }

    private void RebuildVerbRows(IReadOnlyList<StudioUx1AAVerbDraft> verbs)
    {
        foreach (var child in _verbList.GetChildren())
        {
            _verbList.RemoveChild(child);
            child.QueueFree();
        }
        _verbRows.Clear();
        foreach (var verb in verbs)
        {
            var row = new VerbRow(verb);
            row.Changed += OnChanged;
            _verbRows.Add(row);
            _verbList.AddChild(row.Root);
        }
    }

    private void ResetPreviewLabels()
    {
        foreach (var label in new[] { _assemblyPreview, _verbPreview, _validationPreview })
        {
            label.Text = "Aucun aperçu pour cette section.";
            label.AddThemeColorOverride("font_color", StudioStyle.Muted);
        }
    }

    private void SetNeutralStatus(string message)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
        StatusChanged?.Invoke(message, false);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Warning : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private static VBoxContainer AddCard(Container parent, string title, string description)
    {
        var panel = StudioStyle.Panel($"Ux1AA{title.Replace(" ", string.Empty)}", StudioStyle.Surface, 8);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(panel);
        var margin = StudioStyle.Margin(12, 10, 12, 10);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        column.AddChild(StudioStyle.Label(description, 10, StudioStyle.Muted, wrap: true));
        return column;
    }

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
    }

    private static LineEdit AddGroupedLine(Container parent, string label, string placeholder)
    {
        var group = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        group.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder };
        StudioStyle.ApplyLineEdit(line);
        group.AddChild(line);
        parent.AddChild(group);
        return line;
    }

    private static OptionButton AddOption(Container parent, string label)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        parent.AddChild(option);
        return option;
    }

    private static CheckBox AddCheck(Container parent, string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        parent.AddChild(check);
        return check;
    }

    private static SpinBox AddSpin(
        Container parent,
        string label,
        double minimum,
        double maximum,
        double step,
        string suffix)
    {
        var group = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        group.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            Suffix = suffix,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        group.AddChild(spin);
        parent.AddChild(group);
        return spin;
    }

    private static Label PreviewLabel()
    {
        var label = StudioStyle.Label("Aucun aperçu pour cette section.", 10, StudioStyle.Muted, wrap: true);
        label.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        return label;
    }

    private static string MergeValues(string current, params string[] additions) => string.Join(", ", current
        .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Concat(additions)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal));

    private sealed class TransformEditor
    {
        private readonly SpinBox[] _position;
        private readonly SpinBox[] _rotation;
        private readonly SpinBox[] _scale;
        private readonly SpinBox[] _pivot;
        private readonly SpinBox _snap;
        private readonly CheckBox _align;
        private readonly LineEdit _animation;
        private bool _loading;

        internal event Action? Changed;
        internal VBoxContainer Root { get; }

        internal TransformEditor(string title)
        {
            Root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            Root.AddThemeConstantOverride("separation", 5);
            Root.AddChild(StudioStyle.Label(title, 11));
            var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 7);
            grid.AddThemeConstantOverride("v_separation", 5);
            Root.AddChild(grid);
            _position = Vector(grid, "Position", -1_000_000, 1_000_000, .01);
            _rotation = Vector(grid, "Rotation", -360, 360, .1);
            _scale = Vector(grid, "Échelle", .02, 100, .01);
            _pivot = Vector(grid, "Pivot", -1_000_000, 1_000_000, .01);
            var policy = new HBoxContainer();
            policy.AddThemeConstantOverride("separation", 12);
            Root.AddChild(policy);
            _snap = AddSpin(policy, "Pas de snap", 0, 10000, .01, string.Empty);
            _align = AddCheck(policy, "Aligner à la surface");
            _animation = AddLine(Root, "Animation de transformation facultative", "animation:activation");
            foreach (var spin in _position.Concat(_rotation).Concat(_scale).Concat(_pivot).Append(_snap))
            {
                spin.ValueChanged += _ => Notify();
            }
            _align.Toggled += _ => Notify();
            _animation.TextChanged += _ => Notify();
        }

        internal void Load(StudioUx1AATransformDraft draft)
        {
            _loading = true;
            Set(_position, draft.Position);
            Set(_rotation, draft.RotationDegrees);
            Set(_scale, draft.Scale);
            Set(_pivot, draft.Pivot);
            _snap.Value = draft.SnapIncrement;
            _align.ButtonPressed = draft.AlignToSurface;
            _animation.Text = draft.TransformAnimationId;
            _loading = false;
        }

        internal StudioUx1AATransformDraft Capture() => new(
            Get(_position),
            Get(_rotation),
            Get(_scale),
            Get(_pivot),
            _snap.Value,
            _align.ButtonPressed,
            _animation.Text);

        internal void SetEvidence(
            Vector3Value position,
            Vector3Value rotation,
            Vector3Value scale,
            Vector3Value pivot,
            double snap,
            bool align)
        {
            _loading = true;
            Set(_position, position);
            Set(_rotation, rotation);
            Set(_scale, scale);
            Set(_pivot, pivot);
            _snap.Value = snap;
            _align.ButtonPressed = align;
            _animation.Text = string.Empty;
            _loading = false;
        }

        private void Notify()
        {
            if (!_loading) Changed?.Invoke();
        }

        private static SpinBox[] Vector(Container parent, string label, double min, double max, double step) =>
        [
            AddSpin(parent, $"{label} X", min, max, step, string.Empty),
            AddSpin(parent, $"{label} Y", min, max, step, string.Empty),
            AddSpin(parent, $"{label} Z", min, max, step, string.Empty),
        ];

        private static void Set(IReadOnlyList<SpinBox> fields, Vector3Value value)
        {
            fields[0].Value = value.X;
            fields[1].Value = value.Y;
            fields[2].Value = value.Z;
        }

        private static Vector3Value Get(IReadOnlyList<SpinBox> fields) => new(
            (float)fields[0].Value,
            (float)fields[1].Value,
            (float)fields[2].Value);
    }

    private sealed class VerbRow
    {
        private readonly StudioUx1AAVerbDraft _source;
        private readonly LineEdit _verbId;
        private readonly LineEdit _labelKey;
        private readonly SpinBox _priority;
        private readonly LineEdit _required;
        private readonly LineEdit _forbidden;
        private readonly LineEdit _eventBinding;
        private bool _loading;

        internal event Action? Changed;
        internal PanelContainer Root { get; }
        internal string HumanText => string.Join(' ', _verbId.Text, _labelKey.Text, _required.Text,
            _forbidden.Text, _eventBinding.Text);

        internal VerbRow(StudioUx1AAVerbDraft source)
        {
            _source = source;
            Root = StudioStyle.Panel($"Ux1AAVerb{source.Id:N}", StudioStyle.SurfaceRaised, 6);
            var margin = StudioStyle.Margin(9, 7, 9, 7);
            Root.AddChild(margin);
            var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", 5);
            margin.AddChild(column);
            var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 7);
            grid.AddThemeConstantOverride("v_separation", 5);
            column.AddChild(grid);
            _verbId = AddGroupedLine(grid, "Action", "inspect");
            _labelKey = AddGroupedLine(grid, "Libellé", "placeable.verb.inspect");
            _priority = AddSpin(grid, "Priorité", -10000, 10000, 1, string.Empty);
            _required = AddGroupedLine(grid, "Contextes requis", "portable");
            _forbidden = AddGroupedLine(grid, "Contextes interdits", "anchored");
            _eventBinding = AddGroupedLine(grid, "Événement", "InspectRequested");
            _loading = true;
            _verbId.Text = source.VerbId;
            _labelKey.Text = source.LabelKey;
            _priority.Value = source.BasePriority;
            _required.Text = source.RequiredContextText;
            _forbidden.Text = source.ForbiddenContextText;
            _eventBinding.Text = source.EventBinding;
            _loading = false;
            foreach (var line in new[] { _verbId, _labelKey, _required, _forbidden, _eventBinding })
            {
                line.TextChanged += _ => Notify();
            }
            _priority.ValueChanged += _ => Notify();
        }

        internal StudioUx1AAVerbDraft Capture() => _source with
        {
            VerbId = _verbId.Text,
            LabelKey = _labelKey.Text,
            BasePriority = (int)_priority.Value,
            RequiredContextText = _required.Text,
            ForbiddenContextText = _forbidden.Text,
            EventBinding = _eventBinding.Text,
        };

        internal void SetEvidence()
        {
            _loading = true;
            var id = _verbId.Text.Trim().ToLowerInvariant();
            switch (id)
            {
                case "push":
                    _priority.Value = 90;
                    _forbidden.Text = "anchored";
                    break;
                case "take":
                    _priority.Value = 80;
                    _required.Text = "portable";
                    break;
                case "open":
                    _required.Text = "unlocked";
                    break;
                case "talk":
                    _required.Text = "interlocutor";
                    break;
                case "climb":
                    _required.Text = "climbable";
                    break;
                default:
                    if (id is not ("inspect" or "use"))
                    {
                        _required.Text = "custom-context";
                    }
                    break;
            }
            _loading = false;
        }

        private void Notify()
        {
            if (!_loading) Changed?.Invoke();
        }
    }
}
