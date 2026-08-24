using Forge.Application.Entities;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Spatial;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioZoneDynamicOperationsOverlay : Control
{
    private static readonly RuntimeReferenceCopyMode[] ReferenceModes =
    [
        RuntimeReferenceCopyMode.RemapInternal,
        RuntimeReferenceCopyMode.PreserveExternal,
        RuntimeReferenceCopyMode.DropExternal,
    ];

    private static readonly string[] KnownStateComponents = ["transform", "combat", "inventory", "blackboard"];

    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly Label _source;
    private readonly Button _scaleTab;
    private readonly Button _dynamicTab;
    private readonly Button _copyTab;
    private readonly VBoxContainer _scalePanel;
    private readonly VBoxContainer _dynamicPanel;
    private readonly VBoxContainer _copyPanel;
    private readonly Label _selectionSummary;
    private readonly SpinBox _scaleX;
    private readonly SpinBox _scaleY;
    private readonly SpinBox _scaleZ;
    private readonly CheckBox _uniformScale;
    private readonly CheckBox _relativeScale;
    private readonly Label _scaleSummary;
    private readonly CheckBox _dynamicEnabled;
    private readonly SpinBox _dynamicMaximum;
    private readonly SpinBox _dynamicLifetime;
    private readonly CheckBox _dynamicDependencies;
    private readonly SpinBox _dynamicRequested;
    private readonly Label _dynamicSummary;
    private readonly OptionButton _copySource;
    private readonly SpinBox _copyMaximum;
    private readonly SpinBox _copyRequested;
    private readonly CheckBox _copyRequireAuthority;
    private readonly CheckBox _copyHasAuthority;
    private readonly CheckBox _copyOwnership;
    private readonly OptionButton _copyReferenceMode;
    private readonly Dictionary<string, CheckBox> _copyStateChecks = new(StringComparer.Ordinal);
    private readonly SpinBox _copyOffsetX;
    private readonly SpinBox _copyOffsetY;
    private readonly SpinBox _copyOffsetZ;
    private readonly Label _copySummary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;
    private StudioZoneScaleDraft? _scaleDraft;
    private StudioDynamicAreaDraft? _dynamicDraft;
    private StudioRuntimeCopyDraft? _copyDraft;
    private IReadOnlyList<StudioZoneEntityChoice> _entityChoices = [];
    private bool _updating;
    private bool _operationApplied;
    private int _section;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? ZoneChanged;

    internal bool IsOpen => Visible;

    internal int SectionCount => 3;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(
                ' ',
                _source.Text,
                _scaleTab.Text,
                _dynamicTab.Text,
                _copyTab.Text,
                _selectionSummary.Text,
                _scaleSummary.Text,
                _dynamicSummary.Text,
                _copySummary.Text,
                _status.Text);
            return SectionCount == 3 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("project.json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioZoneDynamicOperationsOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioZoneDynamicOperationsOverlay";
        Visible = false;
        ZIndex = 446;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .78f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("ZoneDynamicOperationsDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("OUTILS DE ZONE", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Échelle, zones dynamiques et copies runtime", 20));
        heading.AddChild(StudioStyle.Label(
            "Aperçu obligatoire · commandes annulables · les simulations runtime ne créent aucun objet dans l’éditeur",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer l’opération en attente · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        _source = StudioStyle.Label(string.Empty, 12, StudioStyle.Success, wrap: true);
        root.AddChild(_source);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _scaleTab = TabButton("Échelle", "Mettre un ou plusieurs objets à l’échelle sur les trois axes.");
        _scaleTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_scaleTab);
        _dynamicTab = TabButton("Zones dynamiques", "Configurer la création temporaire de zones sous autorité serveur.");
        _dynamicTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_dynamicTab);
        _copyTab = TabButton("Copies runtime", "Configurer et simuler la copie transactionnelle d’un objet.");
        _copyTab.Pressed += () => ShowSection(2);
        tabs.AddChild(_copyTab);

        var body = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _scalePanel = PanelColumn();
        panels.AddChild(_scalePanel);
        _scalePanel.AddChild(StudioStyle.Label("Mettre la sélection à l’échelle", 17));
        _scalePanel.AddChild(StudioStyle.Label(
            "La sélection de la vue 3D est traitée en une seule commande. Les limites et la géométrie sont contrôlées avant application.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _selectionSummary = StudioStyle.Label("Sélection · aucun objet", 11, StudioStyle.Success, wrap: true);
        _scalePanel.AddChild(_selectionSummary);
        var scaleGrid = Grid();
        _scalePanel.AddChild(scaleGrid);
        _scaleX = AddField(scaleGrid, "Axe X", ScaleBox());
        _scaleY = AddField(scaleGrid, "Axe Y", ScaleBox());
        _scaleZ = AddField(scaleGrid, "Axe Z", ScaleBox());
        _uniformScale = AddField(scaleGrid, "Proportions", Check("Utiliser la valeur X sur les trois axes"));
        _relativeScale = AddField(scaleGrid, "Mode", Check("Multiplier l’échelle actuelle"));
        _scalePanel.AddChild(PreviewCard(
            "RÉSULTAT SUR LA SÉLECTION",
            out _scaleSummary,
            "Prévisualisez pour contrôler l’échelle résultante de chaque objet."));

        _dynamicPanel = PanelColumn();
        panels.AddChild(_dynamicPanel);
        _dynamicPanel.AddChild(StudioStyle.Label("Configurer la capacité globale des zones dynamiques", 17));
        _dynamicPanel.AddChild(StudioStyle.Label(
            "Cet écran valide uniquement la politique globale du module. Il n’instancie pas la zone active et ne lit pas ses dépendances.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var dynamicGrid = Grid();
        _dynamicPanel.AddChild(dynamicGrid);
        _dynamicEnabled = AddField(dynamicGrid, "Création dynamique", Check("Autoriser au runtime"));
        _dynamicMaximum = AddField(dynamicGrid, "Instances simultanées", Number(1, 1024, 1, " zones"));
        _dynamicLifetime = AddField(dynamicGrid, "Durée maximale", Number(1, 604800, 30, " s"));
        _dynamicDependencies = AddField(dynamicGrid, "Dépendances", Check("Résoudre avant l’instanciation"));
        _dynamicRequested = AddField(dynamicGrid, "Simulation", Number(0, 1024, 1, " zones"));
        _dynamicPanel.AddChild(PreviewCard(
            "APERÇU DE POLITIQUE GLOBALE",
            out _dynamicSummary,
            "Prévisualisez les quotas globaux sans créer de zone ni écrire le module."));

        _copyPanel = PanelColumn();
        panels.AddChild(_copyPanel);
        _copyPanel.AddChild(StudioStyle.Label("Copier un objet pendant l’exécution", 17));
        _copyPanel.AddChild(StudioStyle.Label(
            "L’aperçu dérive seulement l’état authored du document. Propriétaire et liens sont fournis par le runtime et ne peuvent pas être simulés ici.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var copyGrid = Grid();
        _copyPanel.AddChild(copyGrid);
        _copySource = AddField(copyGrid, "Objet source", Option());
        _copyMaximum = AddField(copyGrid, "Copies par transaction", Number(1, 256, 1, " copies"));
        _copyRequested = AddField(copyGrid, "Simulation", Number(1, 256, 1, " copies"));
        _copyRequireAuthority = AddField(copyGrid, "Règle serveur", Check("Exiger l’autorité serveur"));
        _copyHasAuthority = AddField(copyGrid, "Contexte simulé", Check("Le serveur a autorisé la demande"));
        _copyOwnership = AddField(copyGrid, "Propriété", Check("Copier le propriétaire de l’objet source"));
        _copyReferenceMode = AddField(
            copyGrid,
            "Liens vers d’autres objets",
            Option("Rediriger les liens internes", "Conserver les liens externes", "Retirer les liens externes"));
        var stateColumn = new VBoxContainer();
        foreach (var (key, label) in new[]
                 {
                     ("transform", "Position et orientation"),
                     ("combat", "État de combat"),
                     ("inventory", "Inventaire"),
                     ("blackboard", "État des scripts"),
                 })
        {
            var check = Check(label);
            _copyStateChecks[key] = check;
            stateColumn.AddChild(check);
        }
        AddField(copyGrid, "État à conserver", stateColumn);
        _copyOffsetX = AddField(copyGrid, "Décalage X", OffsetBox());
        _copyOffsetY = AddField(copyGrid, "Décalage Y", OffsetBox());
        _copyOffsetZ = AddField(copyGrid, "Décalage Z", OffsetBox());
        _copyPanel.AddChild(PreviewCard(
            "COPIES SIMULÉES",
            out _copySummary,
            "Prévisualisez pour contrôler propriété, liens, état et quota de transaction."));

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
        _undo = StudioStyle.Button("Annuler l’opération", compact: true);
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

    internal void OpenScale() => Open(0);

    internal void OpenDynamicArea() => Open(1);

    internal void OpenRuntimeCopy() => Open(2);

    internal void Close()
    {
        if (!Visible)
        {
            return;
        }

        _session.CancelZoneDynamicOperationUx1W();
        _scaleDraft = null;
        _dynamicDraft = null;
        _copyDraft = null;
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        if (mode.EndsWith("dynamic", StringComparison.OrdinalIgnoreCase))
        {
            OpenDynamicArea();
            if (!Visible || _dynamicDraft is null)
            {
                return false;
            }
            _updating = true;
            _dynamicEnabled.ButtonPressed = true;
            _dynamicMaximum.Value = 8;
            _dynamicLifetime.Value = 300;
            _dynamicDependencies.ButtonPressed = true;
            _dynamicRequested.Value = 2;
            _updating = false;
            Preview();
        }
        else if (mode.EndsWith("copy", StringComparison.OrdinalIgnoreCase))
        {
            OpenRuntimeCopy();
            if (!Visible || _copyDraft is null)
            {
                return false;
            }
            _updating = true;
            _copySource.Selected = 0;
            _copyMaximum.Value = 4;
            _copyRequested.Value = Math.Min(2, _copyMaximum.Value);
            _copyRequireAuthority.ButtonPressed = true;
            _copyHasAuthority.ButtonPressed = true;
            _copyOwnership.ButtonPressed = true;
            _copyReferenceMode.Selected = 0;
            _updating = false;
            Preview();
        }
        else
        {
            var evidenceSelection = _session.ActiveArea?.Entities.Take(2).Select(entity => entity.Id).ToArray() ?? [];
            if (evidenceSelection.Length == 0 ||
                !_session.SelectEntities(evidenceSelection, evidenceSelection[^1]))
            {
                SetStatus("Sélectionnez explicitement au moins un objet avant l’aperçu d’échelle.", true);
                return false;
            }
            OpenScale();
            if (!Visible || _scaleDraft is null)
            {
                return false;
            }
            _updating = true;
            _scaleX.Value = 1.25;
            _scaleY.Value = .8;
            _scaleZ.Value = 1.5;
            _uniformScale.ButtonPressed = false;
            _relativeScale.ButtonPressed = false;
            _updating = false;
            Preview();
        }

        return Visible && !_apply.Disabled && ShowsHumanSurface;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalAreaId = _session.ActiveAreaId;
        var originalSelection = _session.SelectedEntityIds.ToArray();
        var originalActive = _session.SelectedEntityId;
        var area = _session.ActiveArea;
        if (originalProject is null || originalAreaId is null || area is null || area.Entities.Count == 0)
        {
            return false;
        }

        var evidenceSelection = area.Entities.Take(2).Select(entity => entity.Id).ToArray();
        _session.SelectEntities(evidenceSelection, evidenceSelection[^1]);
        OpenScale();
        _updating = true;
        _scaleX.Value = 1.1;
        _scaleY.Value = .9;
        _scaleZ.Value = 1.05;
        _uniformScale.ButtonPressed = false;
        _relativeScale.ButtonPressed = true;
        _updating = false;
        Preview();
        var scalePreview = !_apply.Disabled;
        Apply();
        var scaleApplied = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var scaleUndone = ReferenceEquals(originalProject, _session.Project) && !_redo.Disabled;
        Redo();
        var scaleRedone = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var scaleRestored = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        Close();

        OpenDynamicArea();
        var dynamicDraft = _dynamicDraft!;
        _updating = true;
        _dynamicEnabled.ButtonPressed = true;
        _dynamicMaximum.Value = Math.Max(2, dynamicDraft.MaximumInstances);
        _dynamicRequested.Value = 1;
        _dynamicDependencies.ButtonPressed = true;
        _updating = false;
        Preview();
        var dynamicPreview = !_apply.Disabled;
        Apply();
        var dynamicApplied = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var dynamicUndone = ReferenceEquals(originalProject, _session.Project) && !_redo.Disabled;
        Redo();
        var dynamicRedone = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var dynamicRestored = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        Close();

        OpenRuntimeCopy();
        var copyDraft = _copyDraft!;
        _updating = true;
        _copySource.Selected = 0;
        _copyMaximum.Value = copyDraft.MaximumCopiesPerTransaction == 256
            ? 255
            : copyDraft.MaximumCopiesPerTransaction + 1;
        _copyRequested.Value = 1;
        _copyRequireAuthority.ButtonPressed = true;
        _copyHasAuthority.ButtonPressed = true;
        _copyOwnership.ButtonPressed = true;
        _copyReferenceMode.Selected = 0;
        _updating = false;
        Preview();
        var copyPreview = !_apply.Disabled;
        Apply();
        var copyApplied = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var copyUndone = ReferenceEquals(originalProject, _session.Project) && !_redo.Disabled;
        Redo();
        var copyRedone = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var copyRestored = ReferenceEquals(originalProject, _session.Project) && !_session.IsDirty;
        Close();

        _session.SelectEntities(originalSelection, originalActive);
        return scalePreview && scaleApplied && scaleUndone && scaleRedone && scaleRestored &&
               dynamicPreview && dynamicApplied && dynamicUndone && dynamicRedone && dynamicRestored &&
               copyPreview && copyApplied && copyUndone && copyRedone && copyRestored && ShowsHumanSurface;
    }

    private void Open(int section)
    {
        _scaleDraft = _session.CreateZoneScaleDraftUx1W();
        _dynamicDraft = _session.CreateDynamicAreaDraftUx1W();
        _copyDraft = _session.CreateRuntimeCopyDraftUx1W();
        _entityChoices = _session.ZoneEntityChoicesUx1W;
        if (_dynamicDraft is null || section == 0 && _scaleDraft is null || section == 2 && _copyDraft is null)
        {
            SetStatus(
                section == 2
                    ? "Ajoutez au moins un objet à la zone avant de simuler une copie runtime."
                    : "Ouvrez une zone avant d’utiliser cet outil.",
                true);
            _session.CancelZoneDynamicOperationUx1W();
            _scaleDraft = null;
            _dynamicDraft = null;
            _copyDraft = null;
            Visible = false;
            return;
        }

        _operationApplied = false;
        LoadDrafts();
        SetEditingEnabled(true);
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        ShowSection(section);
        if (section == 0 && _scaleDraft?.EntityIds.Count == 0)
        {
            SetStatus(
                "Sélection explicite requise · fermez l’outil, sélectionnez un objet dans la vue 3D, puis rouvrez-le.",
                true);
        }
        else
        {
            SetStatus("Outil ouvert · prévisualisation obligatoire avant application", false);
        }
    }

    private void LoadDrafts()
    {
        _updating = true;
        var area = _session.ActiveArea!;
        _source.Text = $"Zone active · {area.DisplayName} · {area.Entities.Count} objet(s)";
        if (_scaleDraft is not null)
        {
            _selectionSummary.Text = _scaleDraft.EntityIds.Count == 0
                ? "Sélection requise · fermez cet outil, sélectionnez au moins un objet dans la vue 3D, puis rouvrez-le."
                : $"Sélection explicite · {_scaleDraft.EntityIds.Count} objet(s)";
            _selectionSummary.AddThemeColorOverride(
                "font_color",
                _scaleDraft.EntityIds.Count == 0 ? StudioStyle.Danger : StudioStyle.Success);
            _scaleX.Value = _scaleDraft.Scale.X;
            _scaleY.Value = _scaleDraft.Scale.Y;
            _scaleZ.Value = _scaleDraft.Scale.Z;
            _uniformScale.ButtonPressed = false;
            _relativeScale.ButtonPressed = _scaleDraft.Multiply;
        }

        var dynamic = _dynamicDraft!;
        _dynamicEnabled.ButtonPressed = dynamic.Enabled;
        _dynamicMaximum.Value = dynamic.MaximumInstances;
        _dynamicLifetime.Value = dynamic.LifetimeSeconds;
        _dynamicDependencies.ButtonPressed = dynamic.ResolveDependencies;
        _dynamicRequested.Value = dynamic.RequestedInstances;

        _copySource.Clear();
        foreach (var choice in _entityChoices)
        {
            _copySource.AddItem(choice.Label);
        }
        if (_copyDraft is not null)
        {
            var copy = _copyDraft!;
            var sourceIndex = _entityChoices.ToList().FindIndex(choice => choice.Id == copy.SourceEntityId);
            _copySource.Selected = Math.Max(0, sourceIndex);
            _copyMaximum.Value = copy.MaximumCopiesPerTransaction;
            _copyRequested.Value = copy.RequestedCopies;
            _copyRequireAuthority.ButtonPressed = copy.RequireServerAuthority;
            _copyHasAuthority.ButtonPressed = copy.HasServerAuthority;
            _copyOwnership.ButtonPressed = copy.CopyOwnership;
            _copyReferenceMode.Selected = Math.Max(0, Array.IndexOf(ReferenceModes, copy.ReferenceMode));
            foreach (var (name, check) in _copyStateChecks)
            {
                check.ButtonPressed = copy.StateComponents.Contains(name, StringComparer.Ordinal);
            }
            _copyOffsetX.Value = copy.PositionOffset.X;
            _copyOffsetY.Value = copy.PositionOffset.Y;
            _copyOffsetZ.Value = copy.PositionOffset.Z;
        }
        else
        {
            _copySummary.Text = "Copie indisponible · la zone active ne contient aucun objet source.";
            _copySummary.AddThemeColorOverride("font_color", StudioStyle.Danger);
        }
        _updating = false;
        InvalidatePreview();
    }

    private StudioZoneScaleDraft ReadScaleDraft()
    {
        var x = (float)_scaleX.Value;
        var scale = _uniformScale.ButtonPressed
            ? new Vector3Value(x, x, x)
            : new Vector3Value(x, (float)_scaleY.Value, (float)_scaleZ.Value);
        return _scaleDraft! with { Scale = scale, Multiply = _relativeScale.ButtonPressed };
    }

    private StudioDynamicAreaDraft ReadDynamicDraft() => _dynamicDraft! with
    {
        Enabled = _dynamicEnabled.ButtonPressed,
        MaximumInstances = (int)_dynamicMaximum.Value,
        LifetimeSeconds = (int)_dynamicLifetime.Value,
        ResolveDependencies = _dynamicDependencies.ButtonPressed,
        RequestedInstances = (int)_dynamicRequested.Value,
    };

    private StudioRuntimeCopyDraft ReadCopyDraft()
    {
        if (_copyDraft is null || _entityChoices.Count == 0)
        {
            throw new InvalidOperationException("Runtime copy requires a documented source entity.");
        }
        var sourceIndex = Math.Clamp(_copySource.Selected, 0, _entityChoices.Count - 1);
        var visibleState = _copyStateChecks.Where(item => item.Value.ButtonPressed).Select(item => item.Key);
        var hiddenState = _copyDraft.StateComponents.Where(item => !KnownStateComponents.Contains(item, StringComparer.Ordinal));
        return _copyDraft with
        {
            SourceEntityId = _entityChoices[sourceIndex].Id,
            MaximumCopiesPerTransaction = (int)_copyMaximum.Value,
            RequestedCopies = (int)_copyRequested.Value,
            RequireServerAuthority = _copyRequireAuthority.ButtonPressed,
            HasServerAuthority = _copyHasAuthority.ButtonPressed,
            CopyOwnership = _copyOwnership.ButtonPressed,
            ReferenceMode = ReferenceModes[Math.Clamp(_copyReferenceMode.Selected, 0, ReferenceModes.Length - 1)],
            StateComponents = visibleState.Concat(hiddenState).Distinct(StringComparer.Ordinal).ToArray(),
            PositionOffset = new Vector3Value(
                (float)_copyOffsetX.Value,
                (float)_copyOffsetY.Value,
                (float)_copyOffsetZ.Value),
        };
    }

    private void Preview()
    {
        if (_section == 0)
        {
            if (_scaleDraft is null)
            {
                _apply.Disabled = true;
                SetStatus("Aucune zone active n’est disponible pour l’échelle.", true);
                return;
            }
            var result = _session.PreviewZoneScaleUx1W(ReadScaleDraft());
            UpdatePreview(_scaleSummary, result.Summary, result.Message, result.Succeeded, result.CanApply);
            return;
        }
        if (_section == 1)
        {
            var result = _session.PreviewDynamicAreaUx1W(ReadDynamicDraft());
            UpdatePreview(_dynamicSummary, result.Summary, result.Message, result.Succeeded, result.CanApply);
            return;
        }

        if (_copyDraft is null || _entityChoices.Count == 0)
        {
            _copySummary.Text = "Copie indisponible · ajoutez un objet source à la zone active.";
            _copySummary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            _apply.Disabled = true;
            SetStatus("La zone active ne contient aucun objet à copier.", true);
            return;
        }
        var copy = _session.PreviewRuntimeCopyUx1W(ReadCopyDraft());
        UpdatePreview(_copySummary, copy.Summary, copy.Message, copy.Succeeded, copy.CanApply);
    }

    private void UpdatePreview(Label summary, string text, string message, bool succeeded, bool canApply)
    {
        summary.Text = string.IsNullOrWhiteSpace(text)
            ? "Aucun résultat validé · corrigez les paramètres puis relancez l’aperçu."
            : text;
        summary.AddThemeColorOverride("font_color", succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _apply.Disabled = !canApply;
        SetStatus(message, !succeeded);
    }

    private void Apply()
    {
        var result = _section switch
        {
            0 => _session.ApplyApprovedZoneScaleUx1W(),
            1 => _session.ApplyApprovedDynamicAreaUx1W(),
            _ => _session.ApplyApprovedRuntimeCopyUx1W(),
        };
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }

        _operationApplied = true;
        SetEditingEnabled(false);
        _apply.Disabled = true;
        _preview.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        ZoneChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Succeeded ? "Opération annulée · l’état précédent est restauré." : result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = true;
            _redo.Disabled = false;
            ZoneChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Succeeded ? "Opération rétablie · enregistrez pour écrire le module." : result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            ZoneChanged?.Invoke();
        }
    }

    private void ShowSection(int section)
    {
        var requested = Math.Clamp(section, 0, 2);
        if (Visible && requested == 2 && (_copyDraft is null || _entityChoices.Count == 0))
        {
            _copySummary.Text = "Copie indisponible · ajoutez un objet source à la zone active.";
            _copySummary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetStatus("La zone active ne contient aucun objet à copier.", true);
            return;
        }
        _section = requested;
        _scalePanel.Visible = _section == 0;
        _dynamicPanel.Visible = _section == 1;
        _copyPanel.Visible = _section == 2;
        ApplyTabStyle(_scaleTab, _section == 0);
        ApplyTabStyle(_dynamicTab, _section == 1);
        ApplyTabStyle(_copyTab, _section == 2);
        if (!_operationApplied)
        {
            InvalidatePreview();
        }
    }

    private void ConnectInvalidation()
    {
        foreach (var spin in new[]
                 {
                     _scaleX, _scaleY, _scaleZ, _dynamicMaximum, _dynamicLifetime, _dynamicRequested,
                     _copyMaximum, _copyRequested, _copyOffsetX, _copyOffsetY, _copyOffsetZ,
                 })
        {
            spin.ValueChanged += _ => InvalidatePreview();
        }
        foreach (var check in new[]
                 {
                     _uniformScale, _relativeScale, _dynamicEnabled, _dynamicDependencies,
                     _copyRequireAuthority, _copyHasAuthority, _copyOwnership,
                 }.Concat(_copyStateChecks.Values))
        {
            check.Toggled += _ => InvalidatePreview();
        }
        _copySource.ItemSelected += _ => InvalidatePreview();
        _copyReferenceMode.ItemSelected += _ => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _operationApplied)
        {
            return;
        }

        _session.CancelZoneDynamicOperationUx1W();
        _apply.Disabled = true;
        _preview.Disabled = false;
        var summary = _section switch
        {
            0 => _scaleSummary,
            1 => _dynamicSummary,
            _ => _copySummary,
        };
        summary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void SetEditingEnabled(bool enabled)
    {
        foreach (var spin in new[]
                 {
                     _scaleX, _scaleY, _scaleZ, _dynamicMaximum, _dynamicLifetime, _dynamicRequested,
                     _copyMaximum, _copyRequested, _copyOffsetX, _copyOffsetY, _copyOffsetZ,
                 })
        {
            spin.Editable = enabled;
        }
        foreach (var check in new[]
                 {
                     _uniformScale, _relativeScale, _dynamicEnabled, _dynamicDependencies,
                     _copyRequireAuthority, _copyHasAuthority, _copyOwnership,
                 }.Concat(_copyStateChecks.Values))
        {
            check.Disabled = !enabled;
        }
        _copySource.Disabled = !enabled;
        _copyReferenceMode.Disabled = !enabled;
        _scaleTab.Disabled = !enabled;
        _dynamicTab.Disabled = !enabled;
        var copyAvailable = _copyDraft is not null && _entityChoices.Count > 0;
        _copyTab.Disabled = !enabled || !copyAvailable;
        _copyTab.TooltipText = copyAvailable
            ? "Configurer et simuler la copie transactionnelle d’un objet."
            : "Indisponible · ajoutez d’abord un objet source à la zone active.";
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
        var width = Math.Clamp((Size.X - 56) / scale, 820, 1100);
        var height = Math.Clamp((Size.Y - 72) / scale, 650, 820);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
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

    private static PanelContainer PreviewCard(string title, out Label summary, string initial)
    {
        var card = StudioStyle.Panel(title.Replace(' ', '-'), StudioStyle.SurfaceRaised, 8);
        card.CustomMinimumSize = new Vector2(0, 132);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        card.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        column.AddChild(StudioStyle.SectionLabel(title));
        summary = StudioStyle.Label(initial, 12, StudioStyle.Muted, wrap: true);
        column.AddChild(summary);
        return card;
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(245, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.AddChild(title);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton();
        foreach (var label in labels)
        {
            option.AddItem(label);
        }
        StudioStyle.ApplyOptionButton(option);
        return option;
    }

    private static CheckBox Check(string label) => new()
    {
        Text = label,
        FocusMode = FocusModeEnum.All,
    };

    private static SpinBox Number(double minimum, double maximum, double step, string suffix)
    {
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            Suffix = suffix,
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }

    private static SpinBox ScaleBox() => Number(
        ScaleEntitiesHandler.MinimumScale,
        ScaleEntitiesHandler.MaximumScale,
        .05,
        " ×");

    private static SpinBox OffsetBox() => Number(-1000, 1000, .25, " m");
}
