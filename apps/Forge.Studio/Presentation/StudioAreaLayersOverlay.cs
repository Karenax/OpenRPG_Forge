using Forge.Domain.Entities;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAreaLayerList : ItemList
{
    private const string PayloadKind = "openrpg-forge.studio-area-layer";
    private readonly List<Guid> _layerIds = [];

    internal event Action<Guid>? LayerSelected;

    internal event Action<Guid, Guid?>? LayerDropped;

    internal StudioAreaLayerList()
    {
        SelectMode = SelectModeEnum.Single;
        SameColumnWidth = true;
        MaxColumns = 1;
        AllowRmbSelect = true;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ItemSelected += index =>
        {
            if (index >= 0 && index < _layerIds.Count)
            {
                LayerSelected?.Invoke(_layerIds[(int)index]);
            }
        };
    }

    internal void SetLayers(
        IReadOnlyList<(Guid Id, string Label, string Tooltip)> layers,
        Guid? selectedLayerId)
    {
        _layerIds.Clear();
        Clear();
        var selectedIndex = -1;
        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index];
            _layerIds.Add(layer.Id);
            AddItem(layer.Label);
            SetItemTooltip(index, layer.Tooltip);
            if (selectedLayerId == layer.Id)
            {
                selectedIndex = index;
            }
        }
        if (selectedIndex >= 0)
        {
            Select(selectedIndex);
            EnsureCurrentIsVisible();
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var index = GetItemAtPosition(atPosition, true);
        if (index < 0 || index >= _layerIds.Count)
        {
            return default;
        }

        Select(index);
        var preview = StudioStyle.Label($"  Déplacer {GetItemText(index)}  ", 11, StudioStyle.Text);
        preview.AddThemeStyleboxOverride(
            "normal",
            StudioStyle.Box(StudioStyle.Selection, StudioStyle.Success, 7, 1));
        SetDragPreview(preview);
        return new Godot.Collections.Dictionary
        {
            ["kind"] = PayloadKind,
            ["layerId"] = _layerIds[index].ToString(),
        };
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => TryReadLayer(data, out _);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!TryReadLayer(data, out var sourceId))
        {
            return;
        }
        var targetIndex = GetItemAtPosition(atPosition, true);
        Guid? targetId = targetIndex >= 0 && targetIndex < _layerIds.Count
            ? _layerIds[targetIndex]
            : null;
        if (targetId == sourceId)
        {
            return;
        }
        LayerDropped?.Invoke(sourceId, targetId);
    }

    private static bool TryReadLayer(Variant data, out Guid layerId)
    {
        layerId = Guid.Empty;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }
        var dictionary = data.AsGodotDictionary();
        return dictionary.ContainsKey("kind") && dictionary.ContainsKey("layerId") &&
               string.Equals(dictionary["kind"].AsString(), PayloadKind, StringComparison.Ordinal) &&
               Guid.TryParse(dictionary["layerId"].AsString(), out layerId);
    }
}

internal sealed partial class StudioAreaLayersOverlay : Control
{
    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly Label _area;
    private readonly StudioAreaLayerList _layers;
    private readonly Label _empty;
    private readonly Button _addRoot;
    private readonly Button _addChild;
    private readonly Button _remove;
    private readonly LineEdit _name;
    private readonly OptionButton _parent;
    private readonly LineEdit _owner;
    private readonly CheckBox _visible;
    private readonly CheckBox _locked;
    private readonly CheckBox _export;
    private readonly CheckBox _requireRuntime;
    private readonly OptionButton _entity;
    private readonly Button _assign;
    private readonly Label _assigned;
    private readonly CheckBox _includeHidden;
    private readonly Button _selectGroup;
    private readonly Label _effectiveState;
    private readonly Label _summary;
    private readonly Label _selectionSummary;
    private readonly Label _exportSummary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;
    private StudioAreaLayersDraft? _draft;
    private StudioAreaLayerCatalog? _catalog;
    private StudioAreaLayersPreview? _result;
    private Guid? _selectedLayerId;
    private readonly List<Guid?> _parentIds = [];
    private bool _updating;
    private bool _applied;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? AreaChanged;

    internal event Action<IReadOnlyList<EntityId>, EntityId?>? GroupSelectionRequested;

    internal bool IsOpen => Visible;

    internal int LayerCount => _draft?.Layers.Count ?? 0;

    internal bool DirectManipulationAvailable => true;

    internal bool ShowsHumanSurface
    {
        get
        {
            var rows = Enumerable.Range(0, _layers.ItemCount).Select(index => _layers.GetItemText(index));
            var text = string.Join(' ', rows.Append(_area.Text).Append(_empty.Text).Append(_assigned.Text)
                .Append(_summary.Text).Append(_selectionSummary.Text).Append(_exportSummary.Text).Append(_status.Text));
            return DirectManipulationAvailable &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("project.json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("/", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("\\", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioAreaLayersOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioAreaLayersOverlay";
        Visible = false;
        ZIndex = 450;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .78f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("AreaLayersDialog", StudioStyle.Chrome, 12, 2);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("CALQUES ET GROUPES", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Organiser la zone sans quitter la vue 3D", 20));
        heading.AddChild(StudioStyle.Label(
            "Glisser-déposer · visibilité et verrouillage hérités · sélection de groupe · export partiel",
            10,
            StudioStyle.Muted));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        _area = StudioStyle.Label(string.Empty, 12, StudioStyle.Success, wrap: true);
        root.AddChild(_area);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var treePanel = StudioStyle.Panel("AreaLayerTree", StudioStyle.SurfaceRaised, 10, 1);
        treePanel.CustomMinimumSize = new Vector2(440, 0);
        treePanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(treePanel);
        var treeMargin = StudioStyle.Margin(12, 12, 12, 12);
        treePanel.AddChild(treeMargin);
        var treeColumn = new VBoxContainer();
        treeColumn.AddThemeConstantOverride("separation", 8);
        treeMargin.AddChild(treeColumn);
        treeColumn.AddChild(StudioStyle.SectionLabel("ARBRE D’ÉDITION"));
        treeColumn.AddChild(StudioStyle.Label(
            "Faites glisser un calque sur un groupe pour l’y ranger. Déposez-le sous la liste pour le replacer à la racine.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _empty = StudioStyle.Label(
            "Aucun calque dans cette zone. Ajoutez un premier calque pour commencer.",
            11,
            StudioStyle.Warning,
            wrap: true);
        treeColumn.AddChild(_empty);
        _layers = new StudioAreaLayerList
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 360),
        };
        _layers.AddThemeColorOverride("font_color", StudioStyle.Text);
        _layers.AddThemeColorOverride("font_selected_color", StudioStyle.Text);
        _layers.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 7, 1));
        _layers.LayerSelected += SelectLayer;
        _layers.LayerDropped += MoveLayer;
        treeColumn.AddChild(_layers);
        var treeActions = new HBoxContainer();
        treeActions.AddThemeConstantOverride("separation", 6);
        treeColumn.AddChild(treeActions);
        _addRoot = StudioStyle.Button("+ Calque racine", compact: true);
        _addRoot.Pressed += () => AddLayer(null);
        treeActions.AddChild(_addRoot);
        _addChild = StudioStyle.Button("+ Sous-calque", compact: true);
        _addChild.Pressed += () => AddLayer(_selectedLayerId);
        treeActions.AddChild(_addChild);
        _remove = StudioStyle.Button("Retirer", compact: true);
        _remove.Pressed += RemoveLayer;
        treeActions.AddChild(_remove);

        var formScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(590, 0),
        };
        body.AddChild(formScroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8);
        formScroll.AddChild(form);
        form.AddChild(StudioStyle.SectionLabel("CALQUE SÉLECTIONNÉ"));
        _name = AddField(form, "Nom visible", Edit("Architecture, rencontres, secrets…"));
        _parent = AddField(form, "Groupe parent", Option());
        _parent.TooltipText = "Le glisser-déposer offre le même déplacement ; ce menu reste accessible au clavier.";
        _owner = AddField(form, "Responsable", Edit("Équipe ou personne responsable"));
        var flags = new HBoxContainer();
        flags.AddThemeConstantOverride("separation", 14);
        _visible = new CheckBox { Text = "Visible", ButtonPressed = true };
        _visible.TooltipText = "Un groupe masqué masque aussi tous ses sous-calques dans l’aperçu.";
        flags.AddChild(_visible);
        _locked = new CheckBox { Text = "Verrouillé" };
        _locked.TooltipText = "Un groupe verrouillé protège aussi ses sous-calques contre les déplacements et affectations.";
        flags.AddChild(_locked);
        _export = new CheckBox { Text = "Inclus dans l’export", ButtonPressed = true };
        flags.AddChild(_export);
        form.AddChild(flags);
        _effectiveState = StudioStyle.Label(
            "État effectif · prévisualisez pour calculer les héritages.",
            10,
            StudioStyle.Muted,
            wrap: true);
        form.AddChild(_effectiveState);

        form.AddChild(StudioStyle.SectionLabel("OBJETS DU CALQUE"));
        var assignment = new HBoxContainer();
        assignment.AddThemeConstantOverride("separation", 7);
        form.AddChild(assignment);
        _entity = Option();
        _entity.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _entity.TooltipText = "Choisir un objet par son nom et sa position, sans identifiant technique.";
        assignment.AddChild(_entity);
        _assign = StudioStyle.Button("Affecter ici", compact: true);
        _assign.TooltipText = "Déplacer l’objet depuis son ancien calque vers le calque sélectionné.";
        _assign.Pressed += AssignEntity;
        assignment.AddChild(_assign);
        _assigned = StudioStyle.Label("Aucun objet affecté.", 10, StudioStyle.Muted, wrap: true);
        form.AddChild(_assigned);

        form.AddChild(StudioStyle.SectionLabel("SÉLECTION ET EXPORT"));
        var selectionActions = new HBoxContainer();
        selectionActions.AddThemeConstantOverride("separation", 8);
        form.AddChild(selectionActions);
        _includeHidden = new CheckBox { Text = "Inclure les objets masqués" };
        _includeHidden.TooltipText = "Par défaut, la sélection de groupe respecte la visibilité héritée.";
        selectionActions.AddChild(_includeHidden);
        _selectGroup = StudioStyle.Button("Sélectionner dans la vue 3D", compact: true);
        _selectGroup.Disabled = true;
        _selectGroup.Pressed += SelectGroupInViewport;
        selectionActions.AddChild(_selectGroup);
        _requireRuntime = new CheckBox
        {
            Text = "Exiger ces métadonnées pendant le jeu",
            TooltipText = "Si cette option est active, une zone sans calque est refusée avec un message explicite.",
        };
        form.AddChild(_requireRuntime);

        var previewCard = StudioStyle.Panel("AreaLayerPreview", new Color("101510"), 9, 1);
        previewCard.CustomMinimumSize = new Vector2(0, 210);
        form.AddChild(previewCard);
        var previewMargin = StudioStyle.Margin(12, 10, 12, 10);
        previewCard.AddChild(previewMargin);
        var previewColumn = new VBoxContainer();
        previewColumn.AddThemeConstantOverride("separation", 7);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.SectionLabel("APERÇU CANONIQUE"));
        _summary = StudioStyle.Label(
            "Modifiez l’arbre puis prévisualisez avant toute application.",
            11,
            StudioStyle.Muted,
            wrap: true);
        previewColumn.AddChild(_summary);
        _selectionSummary = StudioStyle.Label("Sélection de groupe · aucun aperçu", 10, StudioStyle.Muted, wrap: true);
        previewColumn.AddChild(_selectionSummary);
        _exportSummary = StudioStyle.Label("Export partiel · aucun aperçu", 10, StudioStyle.Muted, wrap: true);
        previewColumn.AddChild(_exportSummary);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.TooltipText = "Valider la commande en lecture seule, les héritages, la sélection et l’export.";
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler les calques", compact: true);
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
    }

    internal void Open()
    {
        _draft = _session.CreateAreaLayersDraft();
        _catalog = _session.CreateAreaLayerCatalog();
        _result = null;
        _selectedLayerId = _draft.Layers.FirstOrDefault()?.Id;
        _applied = false;
        PopulateEntities();
        RebuildLayerTree();
        LoadSelectedLayer();
        _requireRuntime.ButtonPressed = _draft.RequireRuntimeMetadata;
        SetEditingEnabled(true);
        _preview.Disabled = false;
        _apply.Disabled = true;
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        _area.Text = $"Zone active · {_session.ActiveArea!.DisplayName} · {_session.ActiveArea.Entities.Count} objet(s)";
        SetStatus(
            _draft.Layers.Count == 0
                ? "État vide · ajoutez un premier calque, aucun document n’est modifié."
                : "Outil ouvert · glisser-déposer et aperçu disponibles.",
            false);
        if (_draft.Layers.Count == 0)
        {
            _addRoot.GrabFocus();
        }
        else
        {
            _layers.GrabFocus();
        }
    }

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }
        _session.CancelAreaLayers();
        _draft = null;
        _catalog = null;
        _result = null;
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        PrepareEvidenceDraft(mode);
        Preview();
        return Visible && LayerCount >= 3 && !_apply.Disabled && _result?.Succeeded == true &&
               ShowsHumanSurface && DirectManipulationAvailable;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        if (originalProject is null || originalAreaId is null || _session.ActiveArea is null)
        {
            return false;
        }

        Open();
        PrepareEvidenceDraft("ux1-d-tree");
        var child = _draft!.Layers.First(item => item.ParentId is not null && item.EntityIds.Count > 0);
        var root = _draft.Layers.First(item => item.ParentId is null);
        MoveLayer(child.Id, null);
        var movedToRoot = _draft.Layers.Single(item => item.Id == child.Id).ParentId is null;
        MoveLayer(child.Id, root.Id);
        var movedBack = _draft.Layers.Single(item => item.Id == child.Id).ParentId == root.Id;
        _selectedLayerId = root.Id;
        RebuildLayerTree();
        LoadSelectedLayer();
        Preview();
        var previewed = !_apply.Disabled && _result?.Hierarchy?.Count >= 3;
        SelectGroupInViewport();
        var selectionSynced = _result?.SelectedEntityIds is { Count: > 0 } selected &&
                              _session.SelectedEntityIds.SequenceEqual(selected);
        Apply();
        var applied = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var undone = ReferenceEquals(originalProject, _session.Project) && !_redo.Disabled;
        Redo();
        var redone = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var restored = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty &&
                       _session.ActiveAreaId == originalAreaId;
        var selectionRestored = _session.SelectEntities(originalSelection, originalActive);
        Close();
        return previewed && movedToRoot && movedBack && selectionSynced && applied && undone && redone && restored &&
               selectionRestored && ShowsHumanSurface;
    }

    private void PrepareEvidenceDraft(string mode)
    {
        var entities = _catalog!.Entities;
        var rootId = Guid.NewGuid();
        var actorsId = Guid.NewGuid();
        var privateId = Guid.NewGuid();
        var selectedActors = entities.Take(2).Select(item => item.Id).ToArray();
        var selectedPrivate = entities.Skip(2).Take(1).Select(item => item.Id).ToArray();
        var showInheritedLock = mode.EndsWith("locked", StringComparison.OrdinalIgnoreCase);
        _draft = _draft! with
        {
            RequireRuntimeMetadata = true,
            Layers =
            [
                new(rootId, "Monde", null, true, showInheritedLock, "Équipe monde", [], true,
                    "studio:ux1-d-evidence"),
                new(actorsId, "Rencontres", rootId, true, false, "Équipe gameplay", selectedActors, true,
                    "studio:ux1-d-evidence"),
                new(privateId, "Secrets", rootId, false, true, "Maître du jeu", selectedPrivate, false,
                    "studio:ux1-d-evidence"),
            ],
        };
        _selectedLayerId = mode.EndsWith("export", StringComparison.OrdinalIgnoreCase)
            ? rootId
            : actorsId;
        _includeHidden.ButtonPressed = false;
        _requireRuntime.ButtonPressed = true;
        RebuildLayerTree();
        LoadSelectedLayer();
        InvalidatePreview();
    }

    private void AddLayer(Guid? parentId)
    {
        var result = _session.AddAreaLayer(_draft!, parentId);
        ApplyEditResult(result);
        if (result.Succeeded)
        {
            _name.GrabFocus();
            _name.SelectAll();
        }
    }

    private void RemoveLayer()
    {
        if (_selectedLayerId is null)
        {
            SetStatus("Sélectionnez d’abord un calque à retirer.", true);
            return;
        }
        ApplyEditResult(_session.RemoveAreaLayer(_draft!, _selectedLayerId.Value));
    }

    private void MoveLayer(Guid layerId, Guid? parentId)
    {
        ApplyEditResult(_session.ReparentAreaLayer(_draft!, layerId, parentId));
    }

    private void AssignEntity()
    {
        if (_selectedLayerId is null || _entity.Selected < 0 || _entity.Selected >= _catalog!.Entities.Count)
        {
            SetStatus("Sélectionnez un calque et un objet à affecter.", true);
            return;
        }
        ApplyEditResult(_session.AssignAreaLayerEntity(
            _draft!,
            _selectedLayerId.Value,
            _catalog.Entities[_entity.Selected].Id));
    }

    private void ApplyEditResult(StudioAreaLayerEditResult result)
    {
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _draft = result.Draft;
        _selectedLayerId = result.SelectedLayerId;
        RebuildLayerTree();
        LoadSelectedLayer();
        InvalidatePreview();
    }

    private void SelectLayer(Guid layerId)
    {
        if (_updating || _draft is null || _draft.Layers.All(item => item.Id != layerId))
        {
            return;
        }
        _selectedLayerId = layerId;
        LoadSelectedLayer();
        InvalidatePreview();
    }

    private void UpdateSelectedLayer()
    {
        if (_updating || _draft is null || _selectedLayerId is null)
        {
            return;
        }
        var source = _draft.Layers.Single(item => item.Id == _selectedLayerId.Value);
        var replacement = source with
        {
            Name = _name.Text,
            Visible = _visible.ButtonPressed,
            Locked = _locked.ButtonPressed,
            Owner = _owner.Text,
            ExportEnabled = _export.ButtonPressed,
        };
        var result = _session.UpdateAreaLayer(_draft, replacement);
        _draft = result.Draft;
        RebuildLayerTree();
        InvalidatePreview();
    }

    private void ChangeParent(long index)
    {
        if (_updating || _selectedLayerId is null || index < 0 || index >= _parentIds.Count)
        {
            return;
        }
        MoveLayer(_selectedLayerId.Value, _parentIds[(int)index]);
    }

    private void Preview()
    {
        _draft = _draft! with { RequireRuntimeMetadata = _requireRuntime.ButtonPressed };
        var result = _session.PreviewAreaLayers(_draft, _selectedLayerId, _includeHidden.ButtonPressed);
        _result = result;
        _summary.Text = string.IsNullOrWhiteSpace(result.Summary)
            ? "Aucun aperçu disponible. Corrigez l’arbre puis réessayez."
            : result.Summary;
        _summary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _selectionSummary.Text = result.Succeeded
            ? result.SelectedEntityLabels is { Count: > 0 }
                ? "Sélection de groupe\n• " + string.Join("\n• ", result.SelectedEntityLabels)
                : "Sélection de groupe · aucun objet visible dans ce groupe"
            : "Sélection de groupe · aperçu refusé";
        _selectionSummary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Accent : StudioStyle.Muted);
        _exportSummary.Text = result.Succeeded
            ? $"Export partiel · {result.ExportLayerLabels?.Count ?? 0} calque(s), " +
              $"{result.ExportEntityLabels?.Count ?? 0} objet(s), {result.ExportBlueprintLabels?.Count ?? 0} modèle(s)" +
              (result.ExportLayerLabels is { Count: > 0 }
                  ? "\nCalques : " + string.Join(", ", result.ExportLayerLabels)
                  : string.Empty) +
              (result.ExportBlueprintLabels is { Count: > 0 }
                  ? "\nModèles : " + string.Join(", ", result.ExportBlueprintLabels)
                  : string.Empty)
            : "Export partiel · aperçu refusé";
        _exportSummary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Muted);
        _selectedLayerId = result.SelectedLayerId ?? _selectedLayerId;
        _apply.Disabled = !result.CanApply;
        _selectGroup.Disabled = !result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        RebuildLayerTree();
        LoadSelectedLayer();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedAreaLayers();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }
        _applied = true;
        SetEditingEnabled(false);
        _preview.Disabled = true;
        _apply.Disabled = true;
        _selectGroup.Disabled = false;
        _undo.Disabled = false;
        _redo.Disabled = true;
        AreaChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(
            result.Succeeded ? "Calques annulés · l’organisation précédente est restaurée." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = true;
            _redo.Disabled = false;
            AreaChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(
            result.Succeeded ? "Calques rétablis · enregistrez pour écrire le document." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            AreaChanged?.Invoke();
        }
    }

    private void SelectGroupInViewport()
    {
        if (_result is not { Succeeded: true, SelectedEntityIds: not null })
        {
            Preview();
        }
        if (_result is not { Succeeded: true, SelectedEntityIds: not null } ready)
        {
            return;
        }
        var active = ready.SelectedEntityIds.LastOrDefault();
        GroupSelectionRequested?.Invoke(ready.SelectedEntityIds, active);
        SetStatus(
            ready.SelectedEntityIds.Count == 0
                ? "Groupe vide · la sélection 3D a été vidée."
                : $"{ready.SelectedEntityIds.Count} objet(s) du groupe sélectionné(s) dans la vue 3D.",
            false);
    }

    private void PopulateEntities()
    {
        _updating = true;
        _entity.Clear();
        foreach (var entity in _catalog!.Entities)
        {
            _entity.AddItem(entity.DisplayName);
        }
        _entity.Disabled = _catalog.Entities.Count == 0;
        _assign.Disabled = _catalog.Entities.Count == 0 || _selectedLayerId is null;
        if (_catalog.Entities.Count > 0)
        {
            _entity.Select(0);
        }
        _updating = false;
    }

    private void RebuildLayerTree()
    {
        if (_draft is null)
        {
            return;
        }
        var effective = _result?.Hierarchy?.ToDictionary(item => item.LayerId) ?? [];
        var ordered = OrderLayers(_draft.Layers);
        var rows = ordered.Select(item =>
        {
            effective.TryGetValue(item.Layer.Id, out var state);
            var visible = state?.Visible ?? item.Layer.Visible;
            var locked = state?.Locked ?? item.Layer.Locked;
            var indentation = new string(' ', item.Depth * 3) + (item.Depth > 0 ? "↳ " : string.Empty);
            var label = $"{indentation}{(string.IsNullOrWhiteSpace(item.Layer.Name) ? "Calque sans nom" : item.Layer.Name)}" +
                        $" · {(visible ? "visible" : "masqué")} · {(locked ? "verrouillé" : "modifiable")}";
            var tooltip = $"Responsable : {item.Layer.Owner} · {item.Layer.EntityIds.Count} objet(s) · " +
                          $"{(item.Layer.ExportEnabled ? "inclus dans l’export" : "exclu de l’export")}";
            return (item.Layer.Id, label, tooltip);
        }).ToArray();
        _updating = true;
        _layers.SetLayers(rows, _selectedLayerId);
        _empty.Visible = rows.Length == 0;
        _layers.Visible = rows.Length > 0;
        _addChild.Disabled = _selectedLayerId is null || _applied;
        _remove.Disabled = _selectedLayerId is null || _applied;
        _updating = false;
    }

    private void LoadSelectedLayer()
    {
        if (_draft is null)
        {
            return;
        }
        var layer = _selectedLayerId is null
            ? null
            : _draft.Layers.SingleOrDefault(item => item.Id == _selectedLayerId.Value);
        _updating = true;
        PopulateParentOptions(layer);
        if (layer is null)
        {
            _name.Text = string.Empty;
            _owner.Text = string.Empty;
            _visible.ButtonPressed = true;
            _locked.ButtonPressed = false;
            _export.ButtonPressed = true;
            _assigned.Text = "Aucun calque sélectionné.";
            _effectiveState.Text = "Ajoutez ou sélectionnez un calque pour afficher ses propriétés.";
        }
        else
        {
            _name.Text = layer.Name;
            _owner.Text = layer.Owner;
            _visible.ButtonPressed = layer.Visible;
            _locked.ButtonPressed = layer.Locked;
            _export.ButtonPressed = layer.ExportEnabled;
            var names = layer.EntityIds.Select(id =>
                _catalog!.Entities.FirstOrDefault(item => item.Id == id)?.DisplayName ?? "Objet indisponible").ToArray();
            _assigned.Text = names.Length == 0
                ? "Aucun objet affecté à ce calque."
                : "Objets affectés\n• " + string.Join("\n• ", names);
            var effective = _result?.Hierarchy?.SingleOrDefault(item => item.LayerId == layer.Id);
            _effectiveState.Text = effective is null
                ? "État effectif · prévisualisez pour calculer les héritages."
                : $"État effectif · {(effective.Visible ? "visible" : "masqué par la hiérarchie")} · " +
                  $"{(effective.Locked ? "verrouillé par la hiérarchie" : "modifiable")} · profondeur {effective.Depth}";
        }
        var enabled = layer is not null && !_applied;
        _name.Editable = enabled;
        _owner.Editable = enabled;
        _parent.Disabled = !enabled;
        _visible.Disabled = !enabled;
        _locked.Disabled = !enabled;
        _export.Disabled = !enabled;
        _assign.Disabled = !enabled || _catalog!.Entities.Count == 0;
        _updating = false;
    }

    private void PopulateParentOptions(StudioAreaLayerEntryDraft? selected)
    {
        _parent.Clear();
        _parentIds.Clear();
        _parent.AddItem("Aucun groupe · racine");
        _parentIds.Add(null);
        var forbidden = selected is null ? new HashSet<Guid>() : Descendants(_draft!.Layers, selected.Id);
        if (selected is not null)
        {
            forbidden.Add(selected.Id);
        }
        foreach (var candidate in OrderLayers(_draft!.Layers).Where(item => !forbidden.Contains(item.Layer.Id)))
        {
            _parent.AddItem(new string(' ', candidate.Depth * 2) + candidate.Layer.Name);
            _parentIds.Add(candidate.Layer.Id);
        }
        var selectedIndex = _parentIds.FindIndex(item => item == selected?.ParentId);
        _parent.Select(Math.Max(0, selectedIndex));
    }

    private void ConnectInvalidation()
    {
        _name.TextChanged += _ => UpdateSelectedLayer();
        _owner.TextChanged += _ => UpdateSelectedLayer();
        _visible.Toggled += _ => UpdateSelectedLayer();
        _locked.Toggled += _ => UpdateSelectedLayer();
        _export.Toggled += _ => UpdateSelectedLayer();
        _parent.ItemSelected += ChangeParent;
        _requireRuntime.Toggled += _ => InvalidatePreview();
        _includeHidden.Toggled += _ => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _applied)
        {
            return;
        }
        _session.CancelAreaLayers();
        _result = null;
        _apply.Disabled = true;
        _selectGroup.Disabled = true;
        _summary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _selectionSummary.Text = "Sélection de groupe · aperçu à recalculer";
        _exportSummary.Text = "Export partiel · aperçu à recalculer";
        _effectiveState.Text = "État effectif · prévisualisez pour calculer les héritages.";
        RebuildLayerTree();
    }

    private void SetEditingEnabled(bool enabled)
    {
        _addRoot.Disabled = !enabled;
        _addChild.Disabled = !enabled || _selectedLayerId is null;
        _remove.Disabled = !enabled || _selectedLayerId is null;
        _requireRuntime.Disabled = !enabled;
        _includeHidden.Disabled = !enabled;
        _layers.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        LoadSelectedLayer();
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.75f : Size.X >= 2300 ? 1.25f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 900, 1180);
        var height = Math.Clamp((Size.Y - 72) / scale, 650, 790);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static IReadOnlyList<(StudioAreaLayerEntryDraft Layer, int Depth)> OrderLayers(
        IReadOnlyList<StudioAreaLayerEntryDraft> layers)
    {
        var result = new List<(StudioAreaLayerEntryDraft Layer, int Depth)>();
        var visited = new HashSet<Guid>();
        void Append(Guid? parentId, int depth)
        {
            foreach (var layer in layers.Where(item => item.ParentId == parentId)
                         .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (!visited.Add(layer.Id))
                {
                    continue;
                }
                result.Add((layer, depth));
                Append(layer.Id, depth + 1);
            }
        }
        Append(null, 0);
        foreach (var layer in layers.Where(item => !visited.Contains(item.Id))
                     .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            result.Add((layer, 0));
        }
        return result;
    }

    private static HashSet<Guid> Descendants(IReadOnlyList<StudioAreaLayerEntryDraft> layers, Guid root)
    {
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(root);
        while (queue.TryDequeue(out var current))
        {
            foreach (var child in layers.Where(item => item.ParentId == current && result.Add(item.Id)))
            {
                queue.Enqueue(child.Id);
            }
        }
        return result;
    }

    private static T AddField<T>(VBoxContainer form, string label, T control) where T : Control
    {
        form.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        form.AddChild(control);
        return control;
    }

    private static LineEdit Edit(string placeholder)
    {
        var edit = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private static OptionButton Option()
    {
        var option = new OptionButton();
        StudioStyle.ApplyOptionButton(option);
        return option;
    }
}
