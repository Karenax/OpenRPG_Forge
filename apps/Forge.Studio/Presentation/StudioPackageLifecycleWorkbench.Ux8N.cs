using System.Text.RegularExpressions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPackageLifecycleWorkbenchUx8N : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private string? _evidenceVault;
    private SourceSnapshotUx8N? _sourceSnapshot;
    private string _evidenceMode = string.Empty;
    private StudioUx8NCuratedDraft? _curatedDraft;
    private StudioUx8NResourceDraft? _resourceDraft;
    private StudioUx8NMigrationDraft? _migrationDraft;
    private StudioUx8NUninstallDraft? _uninstallDraft;
    private StudioUx8NPreview? _preview;
    private IReadOnlyList<StudioUx8NResourceChoice> _resourceChoices = [];
    private IReadOnlyList<Guid> _packageIds = [];
    private IReadOnlyList<Guid?> _replacementIds = [];
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private TabContainer _tabs = null!;
    private LineEdit _channel = null!;
    private LineEdit _compatibility = null!;
    private LineEdit _licenses = null!;
    private CheckBox _trustedSignature = null!;
    private CheckBox _offlineFallback = null!;
    private OptionButton _resourceChoice = null!;
    private LineEdit _aliases = null!;
    private LineEdit _references = null!;
    private OptionButton _migrationPackage = null!;
    private LineEdit _targetVersion = null!;
    private OptionButton _uninstallPackage = null!;
    private OptionButton _replacement = null!;
    private CheckBox _removeVault = null!;
    private CheckBox _approval = null!;
    private Label _summary = null!;
    private Label _diagnostics = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is { Succeeded: true, CanApply: true, CanonicalDryRun: true };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx8N();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8N(this));
            return Visible && text.Contains("ORF-CONT-010", StringComparison.Ordinal) &&
                   text.Contains("ORF-CONT-019", StringComparison.Ordinal) &&
                   text.Contains("ORF-CONT-026", StringComparison.Ordinal) &&
                   text.Contains("ORF-CONT-027", StringComparison.Ordinal) &&
                   text.Contains("aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun code arbitraire", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8N(text);
        }
    }

    internal StudioPackageLifecycleWorkbenchUx8N(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8N();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx8N();
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
        _session.CancelAllUx8NApprovals();
        ResetUx8N();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx8N(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-n-ui-{Guid.NewGuid():N}");
        _evidenceVault = Path.Combine(_evidenceRoot, "vault");
        _evidenceMode = mode;
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded ||
            !controller.PrepareUx8NEvidenceFixture(_evidenceRoot, _evidenceVault))
        {
            controller.Close();
            DeleteEvidenceUx8N();
            RestoreSourceSessionUx8N();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx8N();
        _moduleChoice.Select(1);
        SelectModuleUx8N(1);
        ConfigureEvidenceUx8N(mode);
        PreviewCurrentUx8N();
        var humanText = string.Join(' ', DescendantTextUx8N(this));
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady && ShowsHumanSurface &&
                    EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_N_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                 $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                 $"route010={humanText.Contains("ORF-CONT-010", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                 $"route019={humanText.Contains("ORF-CONT-019", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                 $"route026={humanText.Contains("ORF-CONT-026", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                 $"route027={humanText.Contains("ORF-CONT-027", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                 $"dry_label={humanText.Contains("aperçu sec", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()} " +
                 $"arbitrary_label={humanText.Contains("aucun code arbitraire", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()} " +
                 $"uuid_visible={ContainsUuidUx8N(humanText).ToString().ToLowerInvariant()} " +
                 $"preview_code={(_preview?.Diagnostics.FirstOrDefault()?.Code ?? "none")} " +
                 $"preview_message={StudioSessionController.HumanUx8N(_preview?.Message).Replace(' ', '_')} " +
                 $"source_untouched={EvidenceSourceUntouched.ToString().ToLowerInvariant()}");
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiEvidence()
    {
        if (!UsesIsolatedEvidence || !PreviewReady || _evidenceRoot is null) return false;
        var tab = CurrentTabKeyUx8N();
        var beforeProject = _session.Project;
        var beforeRevision = _session.History?.State.Revision;
        var draft = ReadCurrentDraftUx8N();
        if (beforeProject is null || draft is null) return false;
        var preview = PreviewDraftUx8N(draft);
        var applied = preview.CanApply && ApplyDraftUx8N(draft).Succeeded;
        var changed = applied && _session.History?.State.Revision != beforeRevision;
        var undo = changed && _session.UndoUx8N(tab).Succeeded;
        var redo = undo && _session.RedoUx8N(tab).Succeeded;
        var save = redo && _session.SaveUx8N().Succeeded;
        var expected = _session.Project is null ? string.Empty : ProjectJsonSerializer.Serialize(_session.Project);
        var reopened = new StudioSessionController();
        var reopenedOk = save && reopened.Open(_evidenceRoot).Succeeded && reopened.Project is not null &&
                         string.Equals(expected, ProjectJsonSerializer.Serialize(reopened.Project),
                             StringComparison.Ordinal);
        reopened.Close();
        return preview.CanonicalDryRun && applied && changed && undo && redo && save && reopenedOk &&
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
        ResetUx8N();
        DeleteEvidenceUx8N();
        RestoreSourceSessionUx8N();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx8N();
        _moduleChoice.Select(1);
        SelectModuleUx8N(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx8N();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx8N;
        UpdateBoundsUx8N();
    }

    private void BuildUiUx8N()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(0.015f, 0.02f, 0.03f, 0.96f);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 445;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux8NDialog", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("Registre & cycle de vie des packages", 20, StudioStyle.Text));
        heading.AddChild(StudioStyle.Label(
            "Trois autorités séparées · politique curatée, registre namespacé et opérations physiques gouvernées.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var routes = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("h_separation", 8);
        routes.AddChild(RouteCardUx8N("ORF-CONT-010", "Packs curatés", "Canal, compatibilité, licences, signature et repli."));
        routes.AddChild(RouteCardUx8N("ORF-CONT-019", "Namespaces", "package:type/path, alias et références sans collision."));
        routes.AddChild(RouteCardUx8N("ORF-CONT-026", "Migrations", "Étape hôte exacte, dry-run, backup, changelog et restauration."));
        routes.AddChild(RouteCardUx8N("ORF-CONT-027", "Désinstallation sûre", "Impact, remplacement, backup et blobs exclusivement orphelins."));
        root.AddChild(routes);
        root.AddChild(StudioStyle.Label(
            "Frontière de confiance · aucun code arbitraire de package ou d’agent n’est exécuté par ces opérations.",
            9, StudioStyle.Muted, wrap: true));

        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModuleUx8N((int)index);
        root.AddChild(LabeledRowUx8N("Module explicite", _moduleChoice));

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 430),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            ClipContents = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        tabHost.AddChild(_tabs);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _tabs.TabChanged += _ => OnTabChangedUx8N();
        BuildCuratedTabUx8N();
        BuildResourceTabUx8N();
        BuildMigrationTabUx8N();
        BuildUninstallTabUx8N();

        _approval = new CheckBox
        {
            Text = "J’approuve ce seul aperçu sec pour la révision affichée",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _approval.Toggled += _ => InvalidateUx8N();
        root.AddChild(_approval);

        var review = StudioStyle.Panel("Ux8NReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 118);
        var reviewMargin = StudioStyle.Margin(10, 8, 10, 8);
        review.AddChild(reviewMargin);
        var reviewBody = new VBoxContainer();
        reviewMargin.AddChild(reviewBody);
        _summary = StudioStyle.Label("Sélectionnez le module puis préparez un aperçu sec.", 10,
            StudioStyle.Text, wrap: true);
        _diagnostics = StudioStyle.Label(string.Empty, 9, StudioStyle.Danger, wrap: true);
        reviewBody.AddChild(_summary);
        reviewBody.AddChild(_diagnostics);
        root.AddChild(review);

        _status = StudioStyle.Label(
            "Aucun code arbitraire, téléchargement, publication ou suppression de fichier workspace n’est exécuté sans action explicite.",
            9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        _previewButton = ActionButtonUx8N("Prévisualiser", PreviewCurrentUx8N, 132, true);
        _applyButton = ActionButtonUx8N("Appliquer", ApplyCurrentUx8N, 108);
        _undoButton = ActionButtonUx8N("Annuler", UndoCurrentUx8N, 96);
        _redoButton = ActionButtonUx8N("Rétablir", RedoCurrentUx8N, 96);
        _saveButton = ActionButtonUx8N("Enregistrer", SaveUx8N, 112);
        actions.AddChild(_previewButton);
        actions.AddChild(_applyButton);
        actions.AddChild(_undoButton);
        actions.AddChild(_redoButton);
        actions.AddChild(_saveButton);
        root.AddChild(actions);

        ConnectInvalidationUx8N();
        UpdateActionsUx8N();
    }

    private void BuildCuratedTabUx8N()
    {
        var body = AddTabUx8N("Packs curatés");
        body.AddChild(StudioStyle.Label("Politique persistée dans les extensions runtime du module.", 11,
            StudioStyle.Text, wrap: true));
        _channel = FieldUx8N(body, "Canal", "stable");
        _compatibility = FieldUx8N(body, "Compatibilité", "1.x");
        _licenses = FieldUx8N(body, "Licences SPDX autorisées", "CC0-1.0, MIT");
        _trustedSignature = CheckUx8N(body, "Exiger une signature de confiance");
        _offlineFallback = CheckUx8N(body, "Autoriser le repli local/hors ligne");
        body.AddChild(StudioStyle.Label(
            "L’aperçu interroge la décision canonique avec une entrée autorisée et une entrée hostile; il n’accède à aucun catalogue distant.",
            9, StudioStyle.Muted, wrap: true));
    }

    private void BuildResourceTabUx8N()
    {
        var body = AddTabUx8N("Namespaces");
        body.AddChild(StudioStyle.Label("Adresse canonique dérivée du package et identité interne stable.", 11,
            StudioStyle.Text, wrap: true));
        _resourceChoice = ChoiceUx8N(body, "Ressource", index => SelectResourceUx8N(index));
        _aliases = FieldUx8N(body, "Alias URI", "package:type/alias");
        _references = FieldUx8N(body, "Références URI", "package:type/resource");
        body.AddChild(StudioStyle.Label(
            "Le validateur refuse alias ambigus, traversal, références absentes, licence, provenance ou hash manquant.",
            9, StudioStyle.Muted, wrap: true));
    }

    private void BuildMigrationTabUx8N()
    {
        var body = AddTabUx8N("Migrations");
        body.AddChild(StudioStyle.Label("Migration de compatibilité à étape hôte compilée.", 11,
            StudioStyle.Text, wrap: true));
        _migrationPackage = ChoiceUx8N(body, "Package installé", index => SelectMigrationPackageUx8N(index));
        _targetVersion = FieldUx8N(body, "Version cible", "1.1.0");
        body.AddChild(StudioStyle.Label(
            "Cette surface exécute seulement l’étape hôte exacte approuvée : backup confiné, changelog et restauration. " +
            "La transformation proposée ici est métadonnées uniquement; aucun script de module ou d’agent n’est accepté.",
            9, StudioStyle.Muted, wrap: true));
    }

    private void BuildUninstallTabUx8N()
    {
        var body = AddTabUx8N("Désinstallation sûre");
        body.AddChild(StudioStyle.Label("Analyse d’impact avant toute mutation du catalogue ou du Vault.", 11,
            StudioStyle.Text, wrap: true));
        _uninstallPackage = ChoiceUx8N(body, "Package à retirer", index => SelectUninstallPackageUx8N(index));
        _replacement = ChoiceUx8N(body, "Remplacement installé", _ => InvalidateUx8N());
        _removeVault = CheckUx8N(body, "Retirer aussi le package du Vault externe");
        body.AddChild(StudioStyle.Label(
            "Sans remplacement, tout dépendant bloque. Le Vault crée un backup et ne collecte que les blobs devenus exclusivement orphelins. " +
            "Les fichiers du workspace restent intacts.", 9, StudioStyle.Muted, wrap: true));
    }

    private void OpenCurrentUx8N()
    {
        Visible = true;
        ResetUx8N();
        _moduleChoice.AddItem("Choisissez explicitement le module…");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(_session.Project.Module.DisplayName));
        UpdateBoundsUx8N();
        UpdateActionsUx8N();
    }

    private void SelectModuleUx8N(int index)
    {
        if (_updating || index != 1 || _session.Project is null)
        {
            _explicitSelection = false;
            return;
        }
        _explicitSelection = true;
        ReloadAllUx8N();
        SetStatusUx8N("Module sélectionné · choisissez une opération et préparez son aperçu sec.", false);
    }

    private void ReloadAllUx8N()
    {
        if (!_explicitSelection) return;
        _updating = true;
        _curatedDraft = _session.CreateCuratedPackDraftUx8N(true);
        if (_curatedDraft is not null)
        {
            _channel.Text = _curatedDraft.Channel;
            _compatibility.Text = _curatedDraft.Compatibility;
            _licenses.Text = string.Join(", ", _curatedDraft.AllowedLicenses);
            _trustedSignature.ButtonPressed = _curatedDraft.RequireTrustedSignature;
            _offlineFallback.ButtonPressed = _curatedDraft.AllowOfflineFallback;
        }
        _resourceChoices = _session.ResourceChoicesUx8N();
        _resourceChoice.Clear();
        foreach (var item in _resourceChoices)
            _resourceChoice.AddItem($"{item.PackageLabel} · {item.ResourceLabel} · {item.Address}");
        var catalog = _session.CreatePackageDraftUx8G();
        _packageIds = catalog?.Packages.Select(item => item.Id).ToArray() ?? [];
        _migrationPackage.Clear();
        _uninstallPackage.Clear();
        foreach (var package in catalog?.Packages ?? [])
        {
            var label = $"{StudioSessionController.HumanUx8N(package.PackageNamespace)} · {package.Version}";
            _migrationPackage.AddItem(label);
            _uninstallPackage.AddItem(label);
        }
        _updating = false;
        if (_resourceChoices.Count > 0) SelectResourceUx8N(0);
        if (_packageIds.Count > 0)
        {
            SelectMigrationPackageUx8N(0);
            SelectUninstallPackageUx8N(0);
        }
        _approval.ButtonPressed = false;
        _preview = null;
        PopulatePreviewUx8N();
        UpdateActionsUx8N();
    }

    private void SelectResourceUx8N(long index)
    {
        if (_updating || index < 0 || index >= _resourceChoices.Count) return;
        _resourceDraft = _session.CreateResourceDraftUx8N(_resourceChoices[(int)index]);
        _updating = true;
        _aliases.Text = string.Join(", ", _resourceDraft?.Aliases ?? []);
        _references.Text = string.Join(", ", _resourceDraft?.References ?? []);
        _updating = false;
        InvalidateUx8N();
    }

    private void SelectMigrationPackageUx8N(long index)
    {
        if (_updating || index < 0 || index >= _packageIds.Count) return;
        _migrationDraft = _session.CreateMigrationDraftUx8N(_packageIds[(int)index]);
        _updating = true;
        _targetVersion.Text = _migrationDraft?.TargetVersion ?? string.Empty;
        _updating = false;
        InvalidateUx8N();
    }

    private void SelectUninstallPackageUx8N(long index)
    {
        if (_updating || index < 0 || index >= _packageIds.Count) return;
        _uninstallDraft = _session.CreateUninstallDraftUx8N(_packageIds[(int)index]);
        var catalog = _session.CreatePackageDraftUx8G();
        _updating = true;
        _replacement.Clear();
        _replacement.AddItem("Aucun remplacement");
        var ids = new List<Guid?> { null };
        foreach (var package in catalog?.Packages.Where(item => item.Id != _packageIds[(int)index]) ?? [])
        {
            _replacement.AddItem($"{StudioSessionController.HumanUx8N(package.PackageNamespace)} · {package.Version}");
            ids.Add(package.Id);
        }
        _replacementIds = ids;
        _replacement.Select(0);
        _updating = false;
        InvalidateUx8N();
    }

    private object? ReadCurrentDraftUx8N()
    {
        if (!_explicitSelection) return null;
        return _tabs.CurrentTab switch
        {
            0 when _curatedDraft is not null => _curatedDraft with
            {
                Channel = _channel.Text.Trim(),
                Compatibility = _compatibility.Text.Trim(),
                AllowedLicenses = SplitValuesUx8N(_licenses.Text),
                RequireTrustedSignature = _trustedSignature.ButtonPressed,
                AllowOfflineFallback = _offlineFallback.ButtonPressed,
                Approved = _approval.ButtonPressed,
            },
            1 when _resourceDraft is not null => _resourceDraft with
            {
                Aliases = SplitValuesUx8N(_aliases.Text),
                References = SplitValuesUx8N(_references.Text),
                Approved = _approval.ButtonPressed,
            },
            2 when _migrationDraft is not null => _migrationDraft with
            {
                TargetVersion = _targetVersion.Text.Trim(),
                Approved = _approval.ButtonPressed,
            },
            3 when _uninstallDraft is not null => _uninstallDraft with
            {
                ReplacementPackageId = _replacement.Selected >= 0 && _replacement.Selected < _replacementIds.Count
                    ? _replacementIds[_replacement.Selected]
                    : null,
                RemoveFromAssetVault = _removeVault.ButtonPressed,
                Approved = _approval.ButtonPressed,
            },
            _ => null,
        };
    }

    private StudioUx8NPreview PreviewDraftUx8N(object draft) => draft switch
    {
        StudioUx8NCuratedDraft curated => _session.PreviewCuratedPacksUx8N(curated),
        StudioUx8NResourceDraft resource => _session.PreviewResourceNamespaceUx8N(resource),
        StudioUx8NMigrationDraft migration => _session.PreviewMigrationUx8N(migration),
        StudioUx8NUninstallDraft uninstall => _session.PreviewUninstallUx8N(uninstall),
        _ => throw new ArgumentOutOfRangeException(nameof(draft)),
    };

    private StudioUx8NActionResult ApplyDraftUx8N(object draft) => draft switch
    {
        StudioUx8NCuratedDraft curated => _session.ApplyCuratedPacksUx8N(curated),
        StudioUx8NResourceDraft resource => _session.ApplyResourceNamespaceUx8N(resource),
        StudioUx8NMigrationDraft migration => _session.ApplyMigrationUx8N(migration),
        StudioUx8NUninstallDraft uninstall => _session.ApplyUninstallUx8N(uninstall),
        _ => new(false, "Opération inconnue."),
    };

    private void PreviewCurrentUx8N()
    {
        var draft = ReadCurrentDraftUx8N();
        if (draft is null)
        {
            SetStatusUx8N("Sélectionnez le module et une entrée.", true);
            return;
        }
        _preview = PreviewDraftUx8N(draft);
        PopulatePreviewUx8N();
        SetStatusUx8N(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx8N();
    }

    private void ApplyCurrentUx8N()
    {
        var draft = ReadCurrentDraftUx8N();
        if (draft is null) return;
        var result = ApplyDraftUx8N(draft);
        SetStatusUx8N(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            ReloadAllUx8N();
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx8N();
    }

    private void UndoCurrentUx8N()
    {
        var result = _session.UndoUx8N(CurrentTabKeyUx8N());
        SetStatusUx8N(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            ReloadAllUx8N();
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx8N();
    }

    private void RedoCurrentUx8N()
    {
        var result = _session.RedoUx8N(CurrentTabKeyUx8N());
        SetStatusUx8N(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            ReloadAllUx8N();
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx8N();
    }

    private void SaveUx8N()
    {
        var result = _session.SaveUx8N();
        SetStatusUx8N(result.Message, !result.Succeeded);
        UpdateActionsUx8N();
    }

    private void PopulatePreviewUx8N()
    {
        if (_preview is null)
        {
            _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant d’appliquer.";
            _diagnostics.Text = string.Empty;
            return;
        }
        _summary.Text = _preview.Message + "\n" + string.Join(" · ", _preview.Lines);
        _diagnostics.Text = _preview.Diagnostics.Count == 0 ? "Aucun diagnostic bloquant." :
            string.Join(" · ", _preview.Diagnostics.Select(item =>
                $"{item.Code} · {StudioSessionController.HumanUx8N(item.Path)} · {StudioSessionController.HumanUx8N(item.Message)}"));
    }

    private void ConfigureEvidenceUx8N(string mode)
    {
        var tab = mode.Contains("namespace", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("migration", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
        _tabs.CurrentTab = tab;
        _updating = true;
        switch (tab)
        {
            case 0:
                _channel.Text = "preview";
                _compatibility.Text = "1.x";
                _licenses.Text = "CC0-1.0, MIT";
                _trustedSignature.ButtonPressed = true;
                _offlineFallback.ButtonPressed = true;
                break;
            case 1:
                SelectChoiceContainingUx8N(_resourceChoice, "studio.ux8g.core");
                _updating = false;
                SelectResourceUx8N(_resourceChoice.Selected);
                _updating = true;
                _aliases.Text = "studio.ux8g.core:text/ui/legacy-theme";
                _references.Text = "studio.ux8g.addon:text/ui.theme";
                break;
            case 2:
                SelectChoiceContainingUx8N(_migrationPackage, "studio.ux8g.core");
                _updating = false;
                SelectMigrationPackageUx8N(_migrationPackage.Selected);
                _updating = true;
                _targetVersion.Text = "1.3.0";
                break;
            case 3:
                SelectChoiceContainingUx8N(_uninstallPackage, "studio.ux8g.core");
                _updating = false;
                SelectUninstallPackageUx8N(_uninstallPackage.Selected);
                _updating = true;
                SelectChoiceContainingUx8N(_replacement, "studio.ux8n.replacement");
                _removeVault.ButtonPressed = true;
                break;
        }
        _approval.ButtonPressed = true;
        _updating = false;
        InvalidateUx8N(cancelApproval: false, preserveApproval: true);
    }

    private void OnTabChangedUx8N()
    {
        if (_updating) return;
        _approval.ButtonPressed = false;
        InvalidateUx8N();
    }

    private void ConnectInvalidationUx8N()
    {
        foreach (var line in new[] { _channel, _compatibility, _licenses, _aliases, _references, _targetVersion })
            line.TextChanged += _ => InvalidateUx8N();
        foreach (var check in new[] { _trustedSignature, _offlineFallback, _removeVault })
            check.Toggled += _ => InvalidateUx8N();
    }

    private void InvalidateUx8N(bool cancelApproval = true, bool preserveApproval = false)
    {
        if (_updating) return;
        if (cancelApproval) _session.CancelAllUx8NApprovals();
        _preview = null;
        if (!preserveApproval)
        {
            _updating = true;
            _approval.ButtonPressed = false;
            _updating = false;
        }
        PopulatePreviewUx8N();
        UpdateActionsUx8N();
    }

    private void UpdateActionsUx8N()
    {
        if (_previewButton is null) return;
        var draft = _explicitSelection && ReadCurrentDraftUx8N() is not null;
        _previewButton.Disabled = !draft;
        _applyButton.Disabled = _preview?.CanApply != true;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.IsDirty;
    }

    private void ResetUx8N()
    {
        _updating = true;
        if (_moduleChoice is not null) _moduleChoice.Clear();
        _resourceChoice?.Clear();
        _migrationPackage?.Clear();
        _uninstallPackage?.Clear();
        _replacement?.Clear();
        _curatedDraft = null;
        _resourceDraft = null;
        _migrationDraft = null;
        _uninstallDraft = null;
        _preview = null;
        _resourceChoices = [];
        _packageIds = [];
        _replacementIds = [];
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        if (_summary is not null) _summary.Text = "Sélectionnez explicitement le module.";
        if (_diagnostics is not null) _diagnostics.Text = string.Empty;
        _updating = false;
    }

    private string CurrentTabKeyUx8N() => _tabs.CurrentTab switch
    {
        0 => "curated",
        1 => "resource",
        2 => "migration",
        3 => "uninstall",
        _ => "resource",
    };

    private SourceSnapshotUx8N CaptureSourceUx8N(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CapturePackageLifecycleApprovalUx8N());

    private bool SourceUnchangedUx8N()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        return source is null || snapshot is null ||
               ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.Dirty && source.ActiveAreaId == snapshot.AreaId &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.SelectedEntity &&
               source.CapturePackageLifecycleApprovalUx8N() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx8N()
    {
        if (_sourceSession is null) return;
        _session = _sourceSession;
        _sourceSession = null;
        _sourceSessionRestored = true;
        _sourceSnapshot = null;
    }

    private void DeleteEvidenceUx8N()
    {
        var root = _evidenceRoot;
        if (root is not null)
        {
            try
            {
                var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                if (string.Equals(Path.GetDirectoryName(full)?.TrimEnd(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar), temp, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux8-n-ui-", StringComparison.Ordinal) &&
                    Directory.Exists(full)) Directory.Delete(full, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                SetStatusUx8N($"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
            }
        }
        _evidenceResourcesReleased = root is null || !Directory.Exists(root);
        _evidenceRoot = null;
        _evidenceVault = null;
    }

    private void UpdateBoundsUx8N()
    {
        var viewport = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(1280, viewport.X - 48), Math.Max(720, viewport.Y - 36));
    }

    private void SetStatusUx8N(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(text, error);
    }

    private VBoxContainer AddTabUx8N(string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var margin = StudioStyle.Margin(12, 10, 12, 10);
        scroll.AddChild(margin);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 9);
        margin.AddChild(body);
        _tabs.AddChild(scroll);
        return body;
    }

    private static LineEdit FieldUx8N(VBoxContainer parent, string label, string placeholder)
    {
        var field = new LineEdit
        {
            PlaceholderText = placeholder,
            CustomMinimumSize = new Vector2(420, 34),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(field);
        parent.AddChild(LabeledRowUx8N(label, field));
        return field;
    }

    private static OptionButton ChoiceUx8N(VBoxContainer parent, string label, Action<long> selected)
    {
        var choice = new OptionButton
        {
            CustomMinimumSize = new Vector2(420, 34),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyOptionButton(choice);
        choice.ItemSelected += index => selected(index);
        parent.AddChild(LabeledRowUx8N(label, choice));
        return choice;
    }

    private static CheckBox CheckUx8N(VBoxContainer parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static HBoxContainer LabeledRowUx8N(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);
        var caption = StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true);
        caption.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(caption);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(control);
        return row;
    }

    private static Control RouteCardUx8N(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux8N" + route.Replace("-", string.Empty), StudioStyle.SurfaceRaised, 6, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var body = new VBoxContainer();
        margin.AddChild(body);
        body.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text, wrap: true));
        body.AddChild(StudioStyle.Label(detail, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Button ActionButtonUx8N(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static IReadOnlyList<string> SplitValuesUx8N(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal).ToArray();

    private static void SelectChoiceContainingUx8N(OptionButton choice, string value)
    {
        for (var index = 0; index < choice.ItemCount; index++)
        {
            if (!choice.GetItemText(index).Contains(value, StringComparison.Ordinal)) continue;
            choice.Select(index);
            return;
        }
    }

    private static IEnumerable<string> DescendantTextUx8N(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Label label when !string.IsNullOrWhiteSpace(label.Text): yield return label.Text; break;
                case Button button when !string.IsNullOrWhiteSpace(button.Text): yield return button.Text; break;
                case CheckBox check when !string.IsNullOrWhiteSpace(check.Text): yield return check.Text; break;
                case LineEdit line when !string.IsNullOrWhiteSpace(line.Text): yield return line.Text; break;
            }
            foreach (var value in DescendantTextUx8N(child)) yield return value;
        }
    }

    private static bool ContainsUuidUx8N(string text) => Regex.IsMatch(text,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.CultureInvariant);

    private sealed record SourceSnapshotUx8N(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? AreaId,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? SelectedEntity,
        StudioUx8NApprovalSnapshot Approval);
}
