using System.Security.Cryptography;
using Forge.Application.Commands;
using Forge.Application.Assets;
using Forge.Application.Creator;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Core.Assets;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly TransformEntityHandler _transformEntity = new(new SystemClock());
    private readonly SetEntityOverridesHandler _setEntityOverrides = new(new SystemClock());
    private readonly SetEntityTagsHandler _setEntityTags = new(new SystemClock());
    private readonly PlaceEntityHandler _placeEntity = new(new SystemClock());
    private readonly PasteEntitiesHandler _pasteEntities = new(new SystemClock());
    private readonly RemoveEntityHandler _removeEntity = new(new SystemClock());
    private readonly InstallAssetBlueprintHandler _installAssetBlueprint = new(new SystemClock());
    private readonly PlacementValidator _placementValidator = new();
    private readonly SetEditorKeymapHandler _setEditorKeymap = new(new SystemClock());
    private EditorWorkspaceProfileCommandHistory? _workspaceProfileHistory;
    private EditorWorkspaceProfile? _persistedWorkspaceProfile;
    private IReadOnlyList<EditorWorkspaceProfile> _workspaceProfiles = [];
    private EditorKeymapCommandHistory? _keymapHistory;
    private EditorKeymapProfile? _persistedKeymapProfile;
    private readonly Dictionary<AreaId, EntityId?> _areaSelections = [];
    private CreatorSearchProfile? _searchProfile;
    private IReadOnlyList<CreatorWorkspaceResource>? _workspaceResources;
    private Forge.Domain.Projects.ProjectDefinition? _resourceCatalogProject;
    private CreatorWorkspaceResourceCatalog? _resourceCatalog;

    internal ProjectSession? Session { get; private set; }

    internal ProjectCommandHistory? History { get; private set; }

    internal IReadOnlyList<AssetPackageManifest> AssetManifests { get; private set; } = [];

    internal AreaId? ActiveAreaId { get; private set; }

    internal EntityId? SelectedEntityId { get; private set; }

    internal bool IsOpen => Session is not null && History is not null;

    internal bool IsDirty => Session is not null && History is not null &&
        !ReferenceEquals(Session.Project, History.State.Project);

    internal bool CanUndo => History?.CanUndo == true;

    internal bool CanRedo => History?.CanRedo == true;

    internal Forge.Domain.Projects.ProjectDefinition? Project => History?.State.Project;

    internal AreaDefinition? ActiveArea =>
        Project?.Areas.FirstOrDefault(area => area.Id == ActiveAreaId) ?? Project?.Areas.FirstOrDefault();

    internal EditorWorkspaceProfile WorkspaceProfile => _workspaceProfileHistory?.Current ??
        throw new InvalidOperationException("No Studio workspace profile is active.");

    internal IReadOnlyList<EditorWorkspaceProfile> WorkspaceProfiles => _workspaceProfiles;

    internal EditorKeymapProfile KeymapProfile => _keymapHistory?.State.Profile ??
        throw new InvalidOperationException("No Studio keymap profile is active.");

    internal bool CanUndoWorkspaceProfile => _workspaceProfileHistory is not null;

    internal bool CanUndoKeymap => _keymapHistory?.CanUndo == true;

    internal bool CanRedoKeymap => _keymapHistory?.CanRedo == true;

    internal bool WorkspaceProfileHasUnsavedChanges => _persistedWorkspaceProfile is not null &&
        WorkspaceProfileFingerprint(_persistedWorkspaceProfile) != WorkspaceProfileFingerprint(WorkspaceProfile);

    internal bool KeymapHasUnsavedChanges => _persistedKeymapProfile is not null &&
        KeymapFingerprint(_persistedKeymapProfile) != KeymapFingerprint(KeymapProfile);

    internal CreatorSearchProfile SearchProfile => _searchProfile ??
        throw new InvalidOperationException("No Studio search profile is active.");

    internal string? WorkspacePreferenceWarning { get; private set; }

    internal StudioOpenResult Open(string workspaceDirectory, string? preferredAreaId = null)
    {
        var opened = ProjectSession.Open(workspaceDirectory);
        if (!opened.Succeeded || opened.Session is null)
        {
            Close();
            return new StudioOpenResult(false, opened.Diagnostics);
        }

        var manifests = AssetPackageManifestStore.Load(opened.Session.RootDirectory);
        if (!manifests.Succeeded)
        {
            Close();
            return new StudioOpenResult(false, manifests.Diagnostics);
        }

        Session = opened.Session;
        History = new ProjectCommandHistory(new ProjectState(Session.Project, new ProjectRevision(0)));
        AssetManifests = manifests.Manifests;
        _areaSelections.Clear();
        CloseUx0C();
        CloseUx0D();
        CloseUx0I();
        CloseUx0J();
        CloseUx1A();
        CloseUx1B();
        CloseUx1C();
        CloseUx1D();
        CloseUx1E();
        CloseUx1W();
        CloseUx1X();
        CloseUx1Y();
        CloseUx1Z();
        CloseUx1AA();
        CloseUx1AB();
        CloseUx1AC();
        CloseUx1AD();
        CloseUx1AE();
        CloseUx1AF();
        CloseUx1AG();
        CloseUx1AH();
        CloseUx1AI();
        CloseUx1AJ();
        CloseUx2A();
        CloseUx2LwB();
        CloseUx2LwC();
        CloseUx2LwD();
        CloseUx2LwE();
        CloseUx2LwF();
        CloseUx2LwG();
        CloseUx2LwH();
        CloseUx2B();
        CloseUx2C();
        CloseUx2EnvB();
        CloseUx2EnvC();
        CloseUx2EnvD();
        CancelAudioApprovalsUx2EnvE();
        CancelAudioEmittersApprovalUx2EnvF();
        CancelAudioWorkbenchApprovalUx2EnvG();
        CancelAudioRangeApprovalUx2EnvH();
        CancelAudioScatterApprovalUx2EnvI();
        CancelMapLodApprovalUx2EnvJ();
        CancelWaterGraphicsApprovalUx2EnvK();
        CloseUx2NavB();
        CloseUx2NavC();
        CloseUx2NavD();
        CloseUx2NavE();
        CloseUx2NavF();
        CloseUx2NavG();
        CloseUx2NavH();
        CloseUx2NavI();
        CloseUx2NavJ();
        CloseUx4A();
        CloseUx5A();
        ActiveAreaId = ResolveArea(preferredAreaId)?.Id ??
            Project!.Areas.FirstOrDefault(area => area.Id == Project.Module.EntryAreaId)?.Id ??
            Project.Areas.FirstOrDefault()?.Id;
        SelectedEntityId = null;
        LoadWorkspacePreferences();
        return new StudioOpenResult(true, []);
    }

    internal bool SelectArea(AreaId areaId)
    {
        return ActivateAreaTab(areaId, persist: true).Succeeded;
    }

    internal bool SelectEntity(EntityId? entityId)
    {
        return SelectEntities(entityId is null ? [] : [entityId], entityId);
    }

    internal CreatorExplorerNode BuildExplorer() => CreatorProjectExplorer.Build(
        Project ?? throw new InvalidOperationException("No project is open."),
        WorkspaceProfile.Shell.ExplorerGrouping);

    internal IReadOnlyList<CreatorSearchResult> Search(string text, int? limit = null)
    {
        var project = Project ?? throw new InvalidOperationException("No project is open.");
        var profile = SearchProfile;
        var catalog = BuildResourceCatalog(project);
        var supplemental = catalog.Documents.Where(document =>
            (document.Kind == CreatorContentKind.Script && profile.IncludeScripts) ||
            (document.Kind == CreatorContentKind.Asset && profile.IncludeAssets)).ToArray();
        var diagnostics = profile.IncludeDiagnostics
            ? CreatorDiagnostics.FromProject(project, "studio:ux0-a-search")
            : [];
        return CreatorSearchIndex.Build(project, diagnostics, supplemental)
            .Search(new CreatorSearchQuery(text ?? string.Empty, Limit: limit ?? profile.ResultLimit));
    }

    internal IReadOnlyList<CreatorReference> FindReferences(CreatorContentKind kind, string id)
    {
        var project = Project ?? throw new InvalidOperationException("No project is open.");
        var catalog = BuildResourceCatalog(project);
        return CreatorReferenceIndex.Build(project, catalog.References).Find(kind, id);
    }

    internal StudioActionResult ConfigureDisplayFilter(EditorDisplayFilter filter, bool persist)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var current = WorkspaceProfile;
        var candidate = EditorWorkspaceProfilePlanner.ConfigureProductivity(
            current,
            filter,
            current.RepeatedPlacementProfile,
            current.MultiArea,
            current.Help,
            current.RolePolicy);
        return ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-a-display-filter", persist);
    }

    internal StudioActionResult ConfigureExplorerGrouping(EditorExplorerGrouping grouping, bool persist)
    {
        var current = WorkspaceProfile;
        var shell = new EditorShellConfiguration(
            current.Shell.Id,
            EditorShellConfiguration.CurrentSchemaVersion,
            "studio:ux0-a-explorer",
            current.Shell.Layout,
            grouping,
            current.Shell.ViewportMode,
            current.Shell.PaletteFilter,
            current.Shell.DirectManipulation);
        return ApplyWorkspaceProfileCandidate(
            EditorWorkspaceProfilePlanner.ConfigureShell(current, shell),
            "studio:ux0-a-explorer",
            persist);
    }

    internal StudioActionResult UndoWorkspaceProfile(bool persist)
    {
        if (_workspaceProfileHistory is null)
        {
            return new StudioActionResult(false, "Aucun profil d’espace de travail n’est ouvert.");
        }

        var result = _workspaceProfileHistory.Undo();
        if (!result.Succeeded)
        {
            return new StudioActionResult(false, "Aucun changement de profil à annuler.");
        }

        if (persist && !TryPersistWorkspaceProfile(result.Profile, out var error))
        {
            _workspaceProfileHistory.Redo();
            return new StudioActionResult(false, error);
        }

        RestoreAreaContext(result.Profile.ActiveAreaId);

        return new StudioActionResult(true, "Profil d’affichage précédent restauré");
    }

    internal StudioActionResult RedoWorkspaceProfile(bool persist)
    {
        if (_workspaceProfileHistory is null)
        {
            return new StudioActionResult(false, "Aucun profil d’espace de travail n’est ouvert.");
        }

        var result = _workspaceProfileHistory.Redo();
        if (!result.Succeeded)
        {
            return new StudioActionResult(false, "Aucun changement de profil à rétablir.");
        }

        if (persist && !TryPersistWorkspaceProfile(result.Profile, out var error))
        {
            _workspaceProfileHistory.Undo();
            return new StudioActionResult(false, error);
        }

        RestoreAreaContext(result.Profile.ActiveAreaId);
        return new StudioActionResult(true, "Profil d’espace de travail rétabli");
    }

    internal StudioActionResult SaveDisplayFilterPreset(string name, EditorDisplayFilter filter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(filter);
        var current = WorkspaceProfile;
        var presetFilter = new EditorDisplayFilter(
            filter.VisibleKinds,
            filter.RequiredTags,
            filter.Author,
            filter.State,
            filter.IsolateSelection,
            filter.GhostHidden,
            filter.VisibleLayerIds,
            filter.VisibleVolumeKinds,
            Guid.NewGuid(),
            EditorDisplayFilter.CurrentSchemaVersion,
            "studio:ux0-a-saved-view");
        var preset = CopyWorkspaceProfile(
            current,
            EditorWorkspaceProfileId.From(Guid.NewGuid()),
            name.Trim(),
            presetFilter,
            "studio:ux0-a-saved-view");
        if (!TryPersistWorkspaceProfile(preset, out var error))
        {
            return new StudioActionResult(false, error);
        }

        _workspaceProfileHistory = new EditorWorkspaceProfileCommandHistory(preset);
        return new StudioActionResult(true, $"Vue « {preset.Name} » enregistrée dans .forge");
    }

    internal StudioActionResult SelectWorkspaceProfile(Guid profileId)
    {
        var selected = _workspaceProfiles.FirstOrDefault(profile => profile.Id.Value == profileId);
        if (selected is null)
        {
            return new StudioActionResult(false, "Le profil d’affichage sélectionné n’existe plus.");
        }

        var reconciled = EditorWorkspaceProfilePlanner.ReconcileAreas(
            selected,
            Project!.Areas.Select(area => area.Id).ToArray(),
            ActiveAreaId);
        if (!TryPersistWorkspaceProfile(reconciled, out var error))
        {
            return new StudioActionResult(false, error);
        }

        _workspaceProfileHistory = new EditorWorkspaceProfileCommandHistory(reconciled);
        _persistedWorkspaceProfile = reconciled;
        RestoreAreaContext(reconciled.ActiveAreaId);
        return new StudioActionResult(true, $"Vue « {reconciled.Name} » activée");
    }

    internal StudioActionResult PreviewWorkspaceProfile(EditorWorkspaceProfile candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (_workspaceProfileHistory is null || Project is null)
        {
            return new StudioActionResult(false, "Aucun profil d’espace de travail n’est ouvert.");
        }

        var validation = EditorWorkspaceProjectValidator.Validate(candidate, Project);
        if (validation.Count > 0)
        {
            return new StudioActionResult(false, $"{validation[0].Code} · {validation[0].Message}");
        }

        var history = candidate.Id == _workspaceProfileHistory.Current.Id
            ? _workspaceProfileHistory
            : new EditorWorkspaceProfileCommandHistory(
                _workspaceProfiles.FirstOrDefault(profile => profile.Id == candidate.Id) ?? candidate);
        var preview = history.Execute(new UpdateEditorWorkspaceProfileCommand(
            Guid.NewGuid(),
            EditorWorkspaceProfileCommandHistory.CurrentSchemaVersion,
            "studio:ux0-b-preview",
            candidate,
            DryRun: true));
        return preview.Succeeded
            ? new StudioActionResult(true,
                $"Aperçu valide · {candidate.OpenAreaTabs.Count} onglet(s) · profil {candidate.Name}")
            : new StudioActionResult(false, preview.Diagnostics[0].Message);
    }

    internal StudioActionResult ApplyWorkspaceProfile(EditorWorkspaceProfile candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (_workspaceProfileHistory is null || candidate.Id == _workspaceProfileHistory.Current.Id)
        {
            return ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-b-workspace", persist: false);
        }

        var validation = EditorWorkspaceProjectValidator.Validate(candidate, Project!);
        if (validation.Count > 0)
        {
            return new StudioActionResult(false, $"{validation[0].Code} · {validation[0].Message}");
        }

        var seed = _workspaceProfiles.FirstOrDefault(profile => profile.Id == candidate.Id) ?? candidate;
        var history = new EditorWorkspaceProfileCommandHistory(seed);
        var command = new UpdateEditorWorkspaceProfileCommand(
            Guid.NewGuid(),
            EditorWorkspaceProfileCommandHistory.CurrentSchemaVersion,
            "studio:ux0-b-profile-selection",
            candidate);
        var preview = history.Execute(command with { DryRun = true });
        var applied = preview.Succeeded ? history.Execute(command) : preview;
        if (!applied.Succeeded)
        {
            return new StudioActionResult(false, applied.Diagnostics[0].Message);
        }

        _workspaceProfileHistory = history;
        RestoreAreaContext(applied.Profile.ActiveAreaId);
        return new StudioActionResult(true, $"Profil « {applied.Profile.Name} » appliqué en mémoire");
    }

    internal StudioActionResult SaveWorkspaceProfile()
    {
        if (_workspaceProfileHistory is null)
        {
            return new StudioActionResult(false, "Aucun profil d’espace de travail n’est ouvert.");
        }

        return TryPersistWorkspaceProfile(_workspaceProfileHistory.Current, out var error)
            ? new StudioActionResult(true, "Disposition et profil enregistrés dans .forge")
            : new StudioActionResult(false, error);
    }

    internal StudioActionResult SaveNamedWorkspaceProfile(string name)
    {
        if (_workspaceProfileHistory is null)
        {
            return new StudioActionResult(false, "Aucun profil d’espace de travail n’est ouvert.");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            return new StudioActionResult(false, "Donnez un nom lisible au profil avant de l’enregistrer.");
        }

        var current = _workspaceProfileHistory.Current;
        var named = CopyWorkspaceProfile(
            current,
            EditorWorkspaceProfileId.From(Guid.NewGuid()),
            name.Trim(),
            current.DisplayFilter,
            "studio:ux0-b-named-profile");
        if (!TryPersistWorkspaceProfile(named, out var error))
        {
            return new StudioActionResult(false, error);
        }

        _workspaceProfileHistory = new EditorWorkspaceProfileCommandHistory(named);
        _persistedWorkspaceProfile = named;
        return new StudioActionResult(true, $"Profil « {named.Name} » enregistré et activé");
    }

    internal StudioActionResult OpenAreaTab(AreaId areaId, bool activate, bool persist)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        if (Project?.Areas.All(area => area.Id != areaId) != false)
        {
            return new StudioActionResult(false, "La zone demandée n’existe plus dans le projet.");
        }

        RememberAreaSelection();
        var candidate = EditorWorkspaceProfilePlanner.OpenArea(WorkspaceProfile, areaId, activate);
        var result = ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-b-open-area", persist);
        if (result.Succeeded && activate)
        {
            RestoreAreaContext(areaId);
        }
        return result.Succeeded
            ? new StudioActionResult(true, activate ? "Zone ouverte et activée" : "Zone ajoutée aux onglets")
            : result;
    }

    internal StudioActionResult ActivateAreaTab(AreaId areaId, bool persist) =>
        OpenAreaTab(areaId, activate: true, persist);

    internal StudioActionResult CloseAreaTab(AreaId areaId, bool persist)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        if (!WorkspaceProfile.OpenAreaTabs.Contains(areaId))
        {
            return new StudioActionResult(false, "Cet onglet de zone est déjà fermé.");
        }
        if (WorkspaceProfile.OpenAreaTabs.Count == 1)
        {
            return new StudioActionResult(false, "Gardez au moins une zone ouverte dans le Studio.");
        }

        RememberAreaSelection();
        var candidate = EditorWorkspaceProfilePlanner.CloseArea(WorkspaceProfile, areaId);
        var result = ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-b-close-area", persist);
        if (result.Succeeded)
        {
            RestoreAreaContext(candidate.ActiveAreaId);
        }
        return result.Succeeded
            ? new StudioActionResult(true, "Onglet fermé · état des autres zones conservé")
            : result;
    }

    internal StudioActionResult ConfigureAreaComparison(AreaId? comparisonAreaId, bool persist)
    {
        if (comparisonAreaId is not null &&
            (!WorkspaceProfile.OpenAreaTabs.Contains(comparisonAreaId) || comparisonAreaId == ActiveAreaId))
        {
            return new StudioActionResult(false, "Choisissez une autre zone déjà ouverte pour la comparaison.");
        }

        var current = WorkspaceProfile;
        var withComparison = EditorWorkspaceProfilePlanner.SetComparison(current, comparisonAreaId);
        var multiArea = new EditorMultiAreaProfile(
            current.MultiArea.Id,
            EditorMultiAreaProfile.CurrentSchemaVersion,
            "studio:ux0-b-comparison",
            splitViewEnabled: comparisonAreaId is not null,
            current.MultiArea.SplitAxis,
            current.MultiArea.SplitRatio,
            comparisonReadOnly: true,
            current.MultiArea.CrossAreaCopyEnabled);
        var candidate = EditorWorkspaceProfilePlanner.ConfigureProductivity(
            withComparison,
            withComparison.DisplayFilter,
            withComparison.RepeatedPlacementProfile,
            multiArea,
            withComparison.Help,
            withComparison.RolePolicy);
        var result = ApplyWorkspaceProfileCandidate(candidate, "studio:ux0-b-comparison", persist);
        return result.Succeeded
            ? new StudioActionResult(true, comparisonAreaId is null
                ? "Comparaison fermée"
                : "Comparaison 3D ouverte en lecture seule")
            : result;
    }

    internal StudioActionResult PreviewKeymap(EditorKeymapProfile candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (_keymapHistory is null)
        {
            return new StudioActionResult(false, "Aucun profil de raccourcis n’est ouvert.");
        }

        var command = new SetEditorKeymapCommand(
            CommandId.From(Guid.NewGuid()),
            _keymapHistory.State.Revision,
            candidate,
            DryRun: true);
        var result = _setEditorKeymap.Handle(_keymapHistory.State, command);
        return result.Succeeded
            ? new StudioActionResult(true, $"Dry-run valide · {result.Patch?.Summary}")
            : new StudioActionResult(false, FormatKeymapDiagnostics(result));
    }

    internal StudioActionResult ApplyKeymap(EditorKeymapProfile candidate)
    {
        var preview = PreviewKeymap(candidate);
        if (!preview.Succeeded || _keymapHistory is null)
        {
            return preview;
        }

        var result = _setEditorKeymap.Handle(
            _keymapHistory.State,
            new SetEditorKeymapCommand(
                CommandId.From(Guid.NewGuid()),
                _keymapHistory.State.Revision,
                candidate,
                DryRun: false));
        if (!result.Succeeded)
        {
            return new StudioActionResult(false, FormatKeymapDiagnostics(result));
        }

        _keymapHistory.Record(result);
        return new StudioActionResult(true,
            $"Raccourcis appliqués en mémoire · révision {result.State.Revision.Value}");
    }

    internal StudioActionResult UndoKeymap()
    {
        if (_keymapHistory?.Undo() != true)
        {
            return new StudioActionResult(false, "Aucune modification de raccourci à annuler.");
        }
        return new StudioActionResult(true, "Raccourcis précédents restaurés");
    }

    internal StudioActionResult RedoKeymap()
    {
        if (_keymapHistory?.Redo() != true)
        {
            return new StudioActionResult(false, "Aucune modification de raccourci à rétablir.");
        }
        return new StudioActionResult(true, "Raccourcis rétablis");
    }

    internal StudioActionResult SaveKeymap()
    {
        if (_keymapHistory is null || Session is null)
        {
            return new StudioActionResult(false, "Aucun profil de raccourcis n’est ouvert.");
        }

        var saved = EditorKeymapProfileStore.Save(Session.RootDirectory, _keymapHistory.State.Profile);
        if (!saved.Succeeded)
        {
            return new StudioActionResult(false,
                string.Join(" · ", saved.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        _persistedKeymapProfile = _keymapHistory.State.Profile;
        return new StudioActionResult(true, "Raccourcis enregistrés atomiquement dans .forge");
    }

    internal StudioActionResult TransformEntity(EntityId entityId, TransformValue transform)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(transform);
        var commandId = CommandId.From(Guid.NewGuid());
        return ExecuteMutation(
            dryRun => _transformEntity.Handle(
                History!.State,
                new TransformEntityCommand(
                    commandId,
                    History.State.Revision,
                    ActiveAreaId!,
                    entityId,
                    transform,
                    dryRun)),
            "Transformation appliquée");
    }

    internal StudioActionResult SetEntityOverrides(EntityId entityId, EntityPropertyOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(overrides);
        var commandId = CommandId.From(Guid.NewGuid());
        return ExecuteMutation(
            dryRun => _setEntityOverrides.Handle(
                History!.State,
                new SetEntityOverridesCommand(
                    commandId,
                    History.State.Revision,
                    ActiveAreaId!,
                    entityId,
                    overrides,
                    dryRun)),
            "Propriétés locales appliquées");
    }

    internal StudioActionResult SetEntityTags(EntityId entityId, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(tags);
        var commandId = CommandId.From(Guid.NewGuid());
        return ExecuteMutation(
            dryRun => _setEntityTags.Handle(
                History!.State,
                new SetEntityTagsCommand(
                    commandId,
                    History.State.Revision,
                    ActiveAreaId!,
                    entityId,
                    tags,
                    dryRun)),
            "Tags appliqués");
    }

    internal BlueprintDefinition? FindAssetBlueprint(string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        if (Project is null) return null;
        var assetId = AssetCatalogItemId.ForExternalId(externalId);
        return Project.Blueprints.FirstOrDefault(blueprint =>
            AssetBlueprintFactory.TryGetAssetId(blueprint, out var candidate) && candidate == assetId);
    }

    internal StudioAssetInstallResult InstallAsset(StudioAssetLibraryItem libraryItem, string vaultRoot)
    {
        ArgumentNullException.ThrowIfNull(libraryItem);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        if (History is null || Session is null)
        {
            return new StudioAssetInstallResult(false, "Aucun module n'est ouvert.");
        }
        var existing = FindAssetBlueprint(libraryItem.ExternalId);
        if (existing is not null)
        {
            return new StudioAssetInstallResult(
                true,
                $"{libraryItem.DisplayName} est déjà disponible dans la palette",
                existing.Id,
                Reused: true);
        }

        var prepared = PrepareAssetInstall(libraryItem, vaultRoot, Session.RootDirectory);
        return !prepared.Succeeded || prepared.Prepared is null
            ? new StudioAssetInstallResult(false, prepared.Message)
            : CommitAssetInstall(prepared.Prepared);
    }

    internal StudioAssetPreparationResult PrepareAssetInstall(
        StudioAssetLibraryItem libraryItem,
        string vaultRoot,
        string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(libraryItem);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (!libraryItem.CanInstall ||
            !AssetCatalogValidator.IsSafeRelativePath(libraryItem.RelativeModelPath))
        {
            return new StudioAssetPreparationResult(
                false,
                "Ce modèle n'a pas de variante GLB locale avec une licence vérifiée.");
        }

        vaultRoot = Path.GetFullPath(vaultRoot);
        workspaceRoot = Path.GetFullPath(workspaceRoot);
        var sourcePath = Path.GetFullPath(Path.Combine(
            vaultRoot,
            libraryItem.RelativeModelPath.Replace('/', Path.DirectorySeparatorChar)));
        var vaultPrefix = vaultRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!sourcePath.StartsWith(vaultPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(sourcePath))
        {
            return new StudioAssetPreparationResult(false, "Le fichier choisi n'existe plus dans le Vault local.");
        }

        try
        {
            var contentSha256 = HashFile(sourcePath);
            var item = new AssetCatalogItem(
                AssetCatalogItemId.ForExternalId(libraryItem.ExternalId),
                libraryItem.ExternalId,
                libraryItem.DisplayName,
                libraryItem.Source,
                libraryItem.ExternalId.Split(':').Last(),
                string.IsNullOrWhiteSpace(libraryItem.SourcePage)
                    ? $"asset://vault/{libraryItem.ExternalId}"
                    : libraryItem.SourcePage,
                libraryItem.LicenseSpdx,
                string.IsNullOrWhiteSpace(libraryItem.LicenseUrl)
                    ? $"asset://vault/{libraryItem.ExternalId}/license"
                    : libraryItem.LicenseUrl,
                libraryItem.LicenseVerified,
                contentSha256,
                libraryItem.FileSize,
                libraryItem.Categories,
                libraryItem.Tags,
                "downloaded-and-extracted",
                AssetAvailability.ExtractedAvailable,
                libraryItem.RelativeModelPath,
                libraryItem.RelativePreviewPath,
                null,
                $"asset-atlas:{libraryItem.ExternalId}",
                author: libraryItem.Author,
                attributionRequired: libraryItem.AttributionRequired,
                attributionText: libraryItem.AttributionText);
            var profile = AssetImportPlanner.CreateDefaultProfile(item, AssetProcessingKind.Model);
            var planned = AssetImportPlanner.Plan(item, profile);
            if (!planned.Succeeded || planned.Plan is null)
            {
                return new StudioAssetPreparationResult(
                    false,
                    string.Join(" · ", planned.Diagnostics.Select(diagnostic => diagnostic.Message)));
            }

            var executed = AssetImportExecutor.Execute(planned.Plan, vaultRoot, workspaceRoot);
            if (!executed.Succeeded || executed.Execution is null)
            {
                return new StudioAssetPreparationResult(
                    false,
                    string.Join(" · ", executed.Diagnostics.Select(diagnostic => diagnostic.Message)));
            }
            if (executed.Execution.Status != AssetImportExecutionStatus.CanonicalReady)
            {
                return new StudioAssetPreparationResult(
                    false,
                    "Le modèle a été copié, mais sa conversion GLB canonique reste nécessaire.");
            }

            var manifest = AssetPackageManifestFactory.Create(
                item,
                profile,
                new AssetManifestArtifactInput(
                    executed.Execution.OutputRelativePath,
                    executed.Execution.OutputContentSha256,
                    executed.Execution.OutputBytes,
                    CanonicalReady: true));
            return new StudioAssetPreparationResult(
                true,
                $"{libraryItem.DisplayName} prêt à être ajouté",
                new StudioPreparedAssetInstall(
                    workspaceRoot,
                    item,
                    manifest,
                    executed.Execution.Reused));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return new StudioAssetPreparationResult(false, $"Import refusé · {exception.Message}");
        }
    }

    internal StudioAssetInstallResult CommitAssetInstall(StudioPreparedAssetInstall prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (History is null || Session is null)
        {
            return new StudioAssetInstallResult(false, "Aucun module n'est ouvert.");
        }
        if (!string.Equals(
                Path.GetFullPath(Session.RootDirectory),
                prepared.WorkspaceRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            return new StudioAssetInstallResult(
                false,
                "Le module ouvert a changé pendant l'import. Aucun blueprint n'a été ajouté.");
        }

        var item = prepared.CatalogItem;
        var manifest = prepared.Manifest;
        var existing = FindAssetBlueprint(item.ExternalId);
        if (existing is not null)
        {
            return new StudioAssetInstallResult(
                true,
                $"{item.DisplayName} est déjà disponible dans la palette",
                existing.Id,
                Reused: true);
        }

        try
        {
            var commandId = CommandId.From(Guid.NewGuid());
            var preview = _installAssetBlueprint.Handle(
                History.State,
                new InstallAssetBlueprintCommand(
                    commandId,
                    History.State.Revision,
                    item,
                    manifest,
                    DryRun: true));
            if (!preview.Succeeded)
            {
                return new StudioAssetInstallResult(false, Failure(preview).Message);
            }

            var previousManifests = AssetManifests;
            var updatedManifests = previousManifests
                .Where(candidate => candidate.AssetId != manifest.AssetId)
                .Append(manifest)
                .OrderBy(candidate => candidate.AssetId.ToString(), StringComparer.Ordinal)
                .ToArray();
            var manifestSave = AssetPackageManifestStore.Save(Session.RootDirectory, updatedManifests);
            if (!manifestSave.Succeeded)
            {
                return new StudioAssetInstallResult(
                    false,
                    string.Join(" · ", manifestSave.Diagnostics.Select(diagnostic => diagnostic.Message)));
            }

            var applied = _installAssetBlueprint.Handle(
                History.State,
                new InstallAssetBlueprintCommand(
                    commandId,
                    History.State.Revision,
                    item,
                    manifest));
            if (!applied.Succeeded)
            {
                _ = AssetPackageManifestStore.Save(Session.RootDirectory, previousManifests);
                return new StudioAssetInstallResult(false, Failure(applied).Message);
            }
            History.Record(applied);
            AssetManifests = updatedManifests;
            var blueprint = AssetBlueprintFactory.Create(item, manifest);
            return new StudioAssetInstallResult(
                true,
                $"{item.DisplayName} ajouté à la palette",
                blueprint.Id,
                prepared.Reused);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return new StudioAssetInstallResult(false, $"Import refusé · {exception.Message}");
        }
    }

    internal StudioEntityActionResult PlaceEntity(BlueprintId blueprintId, Vector3Value requestedPosition)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        if (History is null || ActiveAreaId is null || Project is null)
        {
            return new StudioEntityActionResult(false, "Aucun module ou aucune zone active.");
        }

        var blueprint = Project.Blueprints.FirstOrDefault(candidate => candidate.Id == blueprintId);
        if (blueprint is null)
        {
            return new StudioEntityActionResult(false, "Le blueprint sélectionné n'existe plus.");
        }

        var placement = ResolvePlacement(blueprintId, requestedPosition);
        if (!placement.Succeeded)
        {
            return new StudioEntityActionResult(false, placement.Message);
        }

        var entityId = EntityId.From(Guid.NewGuid());
        var commandId = CommandId.From(Guid.NewGuid());
        var assetPlacement = ResolveAssetPlacement(blueprint);
        var result = ExecuteMutation(
            dryRun => _placeEntity.Handle(
                History.State,
                new PlaceEntityCommand(
                    commandId,
                    History.State.Revision,
                    ActiveAreaId,
                    entityId,
                    blueprintId,
                    new TransformValue(placement.Position, QuaternionValue.Identity, Vector3Value.One),
                    dryRun,
                    assetPlacement.Tags,
                    assetPlacement.Overrides)),
            $"{blueprint.DisplayName} ajouté");
        if (!result.Succeeded)
        {
            return new StudioEntityActionResult(false, result.Message);
        }

        SelectEntity(entityId);
        return new StudioEntityActionResult(true, result.Message + placement.Suffix, entityId);
    }

    internal StudioEntityActionResult DuplicateEntity(EntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        if (History is null || ActiveAreaId is null || ActiveArea is null)
        {
            return new StudioEntityActionResult(false, "Aucun module ou aucune zone active.");
        }

        var source = ActiveArea.Entities.FirstOrDefault(candidate => candidate.Id == entityId);
        if (source is null)
        {
            return new StudioEntityActionResult(false, "L'objet à dupliquer n'existe plus.");
        }

        var target = FindDuplicatePosition(source);
        if (target is null)
        {
            return new StudioEntityActionResult(false, "Aucun emplacement libre n'a été trouvé autour de l'objet.");
        }

        var duplicateId = EntityId.From(Guid.NewGuid());
        var payload = new EntityClipboardPayload(
            ActiveAreaId,
            [source],
            provenance: "studio-context-duplicate");
        var commandId = CommandId.From(Guid.NewGuid());
        var offset = new Vector3Value(
            target.Value.X - source.Transform.Position.X,
            target.Value.Y - source.Transform.Position.Y,
            target.Value.Z - source.Transform.Position.Z);
        var result = ExecuteMutation(
            dryRun => _pasteEntities.Handle(
                History.State,
                new PasteEntitiesCommand(
                    commandId,
                    History.State.Revision,
                    ActiveAreaId,
                    payload,
                    new Dictionary<EntityId, EntityId> { [source.Id] = duplicateId },
                    offset,
                    dryRun)),
            "Objet dupliqué à côté de l'original");
        if (!result.Succeeded)
        {
            return new StudioEntityActionResult(false, result.Message);
        }

        SelectEntity(duplicateId);
        return new StudioEntityActionResult(true, result.Message, duplicateId);
    }

    internal StudioActionResult RemoveEntity(EntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        var commandId = CommandId.From(Guid.NewGuid());
        var result = ExecuteMutation(
            dryRun => _removeEntity.Handle(
                History!.State,
                new RemoveEntityCommand(
                    commandId,
                    History.State.Revision,
                    ActiveAreaId!,
                    entityId,
                    dryRun)),
            "Objet supprimé · Ctrl+Z pour le restaurer");
        if (result.Succeeded)
        {
            var remaining = SelectedEntityIds.Where(id => id != entityId).ToArray();
            SelectEntities(remaining, remaining.LastOrDefault());
        }
        return result;
    }

    internal StudioActionResult Undo()
    {
        if (History is null)
        {
            return new StudioActionResult(false, "Aucun module n'est ouvert.");
        }

        var result = History.Undo();
        if (result.Succeeded)
        {
            ReconcileWorkspaceAreasAfterProjectMutation();
            InvalidateContentIndexes();
        }
        return result.Succeeded
            ? new StudioActionResult(true, "Modification annulée")
            : new StudioActionResult(false, "Rien à annuler");
    }

    internal StudioActionResult Redo()
    {
        if (History is null)
        {
            return new StudioActionResult(false, "Aucun module n'est ouvert.");
        }

        var result = History.Redo();
        if (result.Succeeded)
        {
            ReconcileWorkspaceAreasAfterProjectMutation();
            InvalidateContentIndexes();
        }
        return result.Succeeded
            ? new StudioActionResult(true, "Modification rétablie")
            : new StudioActionResult(false, "Rien à rétablir");
    }

    internal StudioActionResult Save()
    {
        if (Session is null || History is null)
        {
            return new StudioActionResult(false, "Aucun module n'est ouvert.");
        }

        var result = Session.Save(History.State.Project);
        if (result.Succeeded)
        {
            return new StudioActionResult(true, "Module enregistré atomiquement");
        }

        return new StudioActionResult(
            false,
            string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    internal void Close()
    {
        Session = null;
        History = null;
        AssetManifests = [];
        ActiveAreaId = null;
        SelectedEntityId = null;
        _workspaceProfileHistory = null;
        _persistedWorkspaceProfile = null;
        _workspaceProfiles = [];
        _keymapHistory = null;
        _persistedKeymapProfile = null;
        _areaSelections.Clear();
        CloseUx0C();
        CloseUx0D();
        CloseUx0I();
        CloseUx0J();
        CloseUx1A();
        CloseUx1B();
        CloseUx1C();
        CloseUx1D();
        CloseUx1E();
        CloseUx1W();
        CloseUx1X();
        CloseUx1Y();
        CloseUx1Z();
        CloseUx1AA();
        CloseUx1AB();
        CloseUx1AC();
        CloseUx1AD();
        CloseUx1AE();
        CloseUx1AF();
        CloseUx1AG();
        CloseUx1AH();
        CloseUx1AI();
        CloseUx1AJ();
        CloseUx2A();
        CloseUx2LwB();
        CloseUx2LwC();
        CloseUx2LwD();
        CloseUx2LwE();
        CloseUx2LwF();
        CloseUx2LwG();
        CloseUx2LwH();
        CloseUx2B();
        CloseUx2C();
        CloseUx2EnvB();
        CloseUx2EnvC();
        CloseUx2EnvD();
        CancelAudioApprovalsUx2EnvE();
        CancelAudioEmittersApprovalUx2EnvF();
        CancelAudioWorkbenchApprovalUx2EnvG();
        CancelAudioRangeApprovalUx2EnvH();
        CancelAudioScatterApprovalUx2EnvI();
        CancelMapLodApprovalUx2EnvJ();
        CancelWaterGraphicsApprovalUx2EnvK();
        CloseUx2NavB();
        CloseUx2NavC();
        CloseUx2NavD();
        CloseUx2NavE();
        CloseUx2NavF();
        CloseUx2NavG();
        CloseUx2NavH();
        CloseUx2NavI();
        CloseUx2NavJ();
        CloseUx4A();
        CloseUx5A();
        _searchProfile = null;
        _workspaceResources = null;
        _resourceCatalogProject = null;
        _resourceCatalog = null;
        WorkspacePreferenceWarning = null;
    }

    private void LoadWorkspacePreferences()
    {
        var root = Session!.RootDirectory;
        WorkspacePreferenceWarning = null;
        try
        {
            var loaded = EditorWorkspaceProfileStore.TryLoad(root) ??
                EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, ActiveAreaId);
            var reconciled = EditorWorkspaceProfilePlanner.ReconcileAreas(
                loaded,
                Project!.Areas.Select(area => area.Id).ToArray(),
                ActiveAreaId);
            _workspaceProfileHistory = new EditorWorkspaceProfileCommandHistory(reconciled);
            _persistedWorkspaceProfile = reconciled;
            _workspaceProfiles = EditorWorkspaceProfileStore.TryLoadCatalog(root)?.Profiles ?? [reconciled];
            RestoreAreaContext(reconciled.ActiveAreaId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               System.Text.Json.JsonException or InvalidDataException or ArgumentException)
        {
            var fallback = EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, ActiveAreaId);
            _workspaceProfileHistory = new EditorWorkspaceProfileCommandHistory(fallback);
            _persistedWorkspaceProfile = fallback;
            _workspaceProfiles = [fallback];
            WorkspacePreferenceWarning = $"Profil d’espace de travail ignoré : {exception.Message}";
        }

        var keymap = EditorKeymapProfileStore.Load(root);
        _keymapHistory = new EditorKeymapCommandHistory(keymap.Profile);
        _persistedKeymapProfile = keymap.Profile;
        if (!keymap.Succeeded)
        {
            var warning = $"Profil de raccourcis ignoré : {keymap.Diagnostics[0].Message}";
            WorkspacePreferenceWarning = string.IsNullOrWhiteSpace(WorkspacePreferenceWarning)
                ? warning
                : WorkspacePreferenceWarning + " · " + warning;
        }

        try
        {
            _searchProfile = CreatorSearchProfileStore.Load(root, Project!.Id.Value);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               System.Text.Json.JsonException or InvalidDataException or ArgumentException)
        {
            _searchProfile = CreatorSearchProfile.CreateDefault(Project!.Id.Value);
            WorkspacePreferenceWarning = string.IsNullOrWhiteSpace(WorkspacePreferenceWarning)
                ? $"Profil de recherche ignoré : {exception.Message}"
                : WorkspacePreferenceWarning + " · profil de recherche ignoré";
        }

        LoadUx0CPreferences(root);
        LoadUx0IProjectData(root);
    }

    private StudioActionResult ApplyWorkspaceProfileCandidate(
        EditorWorkspaceProfile candidate,
        string provenance,
        bool persist)
    {
        if (_workspaceProfileHistory is null || Project is null)
        {
            return new StudioActionResult(false, "Aucun profil d’espace de travail n’est ouvert.");
        }

        var validation = EditorWorkspaceProjectValidator.Validate(candidate, Project);
        if (validation.Count > 0)
        {
            return new StudioActionResult(false, $"{validation[0].Code} · {validation[0].Message}");
        }

        var command = new UpdateEditorWorkspaceProfileCommand(
            Guid.NewGuid(),
            EditorWorkspaceProfileCommandHistory.CurrentSchemaVersion,
            provenance,
            candidate);
        var preview = _workspaceProfileHistory.Execute(command with { DryRun = true });
        if (!preview.Succeeded)
        {
            return new StudioActionResult(false, preview.Diagnostics[0].Message);
        }

        var applied = _workspaceProfileHistory.Execute(command);
        if (!applied.Succeeded)
        {
            return new StudioActionResult(false, applied.Diagnostics[0].Message);
        }

        RestoreAreaContext(applied.Profile.ActiveAreaId);

        if (persist && !TryPersistWorkspaceProfile(applied.Profile, out var error))
        {
            var restored = _workspaceProfileHistory.Undo();
            RestoreAreaContext(restored.Profile.ActiveAreaId);
            return new StudioActionResult(false, error);
        }

        return new StudioActionResult(true, persist
            ? "Profil d’espace de travail validé et enregistré"
            : "Profil d’espace de travail validé en mémoire");
    }

    private bool TryPersistWorkspaceProfile(EditorWorkspaceProfile profile, out string error)
    {
        try
        {
            EditorWorkspaceProfileStore.Save(Session!.RootDirectory, profile);
            _workspaceProfiles = EditorWorkspaceProfileStore.TryLoadCatalog(Session.RootDirectory)?.Profiles ?? [profile];
            _persistedWorkspaceProfile = profile;
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               System.Text.Json.JsonException or InvalidDataException or ArgumentException)
        {
            error = $"Profil non enregistré : {exception.Message}";
            return false;
        }
    }

    private CreatorWorkspaceResourceCatalog BuildResourceCatalog(Forge.Domain.Projects.ProjectDefinition project)
    {
        if (ReferenceEquals(project, _resourceCatalogProject) && _resourceCatalog is not null)
        {
            return _resourceCatalog;
        }
        _workspaceResources ??= DiscoverWorkspaceResources();
        _resourceCatalogProject = project;
        _resourceCatalog = CreatorWorkspaceResourceCatalog.Build(project, _workspaceResources);
        return _resourceCatalog;
    }

    private IReadOnlyList<CreatorWorkspaceResource> DiscoverWorkspaceResources()
    {
        var root = Session!.RootDirectory;
        var resources = new List<CreatorWorkspaceResource>();
        var scriptsRoot = Path.Combine(root, "scripts");
        if (Directory.Exists(scriptsRoot))
        {
            foreach (var path in Directory.EnumerateFiles(scriptsRoot, "*.forge", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, path);
                resources.Add(CreatorWorkspaceResource.Create(
                    CreatorContentKind.Script,
                    relative,
                    File.ReadAllText(path),
                    ["script", "forgescript"],
                    id: WorkspaceResourceIdentityStore.Resolve(root, relative)));
            }
        }

        foreach (var folder in new[] { "assets", "content/assets" })
        {
            var assetRoot = Path.Combine(root, folder);
            if (!Directory.Exists(assetRoot))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(assetRoot, "*", SearchOption.AllDirectories)
                         .Where(path => !path.Contains(
                             $"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}",
                             StringComparison.OrdinalIgnoreCase)))
            {
                var relative = Path.GetRelativePath(root, path);
                resources.Add(CreatorWorkspaceResource.Create(
                    CreatorContentKind.Asset,
                    relative,
                    Path.GetFileNameWithoutExtension(path),
                    ["asset", Path.GetExtension(path).TrimStart('.')],
                    id: WorkspaceResourceIdentityStore.Resolve(root, relative)));
            }
        }

        return resources.GroupBy(resource => resource.Id).Select(group => group.First()).ToArray();
    }

    private static EditorWorkspaceProfile CopyWorkspaceProfile(
        EditorWorkspaceProfile source,
        EditorWorkspaceProfileId id,
        string name,
        EditorDisplayFilter displayFilter,
        string provenance) => new(
        id,
        name,
        source.Role,
        displayFilter,
        source.RepeatPlacement,
        source.OpenAreaTabs,
        source.ActiveAreaId,
        source.ComparisonAreaId,
        source.ContextualHelpEnabled,
        provenance,
        EditorWorkspaceProfile.CurrentSchemaVersion,
        source.UiScale,
        source.TextScale,
        source.PrimaryFontFamily,
        source.FallbackFontFamilies,
        source.PersonalFavoriteBlueprintIds,
        source.RecentBlueprintIds,
        source.Shell,
        source.RepeatedPlacementProfile,
        source.MultiArea,
        source.Help,
        source.RolePolicy,
        source.NamedPaletteCollections);

    private StudioActionResult ExecuteMutation(
        Func<bool, CommandResult> execute,
        string successMessage)
    {
        if (History is null || ActiveAreaId is null)
        {
            return new StudioActionResult(false, "Aucun module ou aucune zone active.");
        }

        var preview = execute(true);
        if (!preview.Succeeded)
        {
            return Failure(preview);
        }

        var applied = execute(false);
        if (!applied.Succeeded)
        {
            return Failure(applied);
        }

        History.Record(applied);
        return new StudioActionResult(true, successMessage);
    }

    private (bool Succeeded, Vector3Value Position, string Message, string Suffix) ResolvePlacement(
        BlueprintId blueprintId,
        Vector3Value requested)
    {
        var preview = _placementValidator.Validate(
            Project!,
            new PlacementValidationRequest(ActiveAreaId!, blueprintId, requested, .25f));
        if (preview.IsValid)
        {
            var adjusted = preview.CorrectedPosition != requested;
            return (true, preview.CorrectedPosition, preview.Message, adjusted ? " · position alignée" : string.Empty);
        }

        if (preview.CorrectedPosition != requested)
        {
            var correction = _placementValidator.Validate(
                Project!,
                new PlacementValidationRequest(
                    ActiveAreaId!,
                    blueprintId,
                    preview.CorrectedPosition,
                    .25f,
                    AutoCorrect: false));
            if (correction.IsValid)
            {
                return (true, correction.CorrectedPosition, correction.Message, " · position libre ajustée");
            }
        }

        return (false, requested, preview.Message, string.Empty);
    }

    private Vector3Value? FindDuplicatePosition(EntityInstance source)
    {
        var offsets = new (float X, float Z)[]
        {
            (1, 0), (0, 1), (-1, 0), (0, -1),
            (1, 1), (-1, 1), (-1, -1), (1, -1),
            (2, 0), (0, 2), (-2, 0), (0, -2),
        };
        foreach (var offset in offsets)
        {
            var candidate = new Vector3Value(
                source.Transform.Position.X + offset.X,
                source.Transform.Position.Y,
                source.Transform.Position.Z + offset.Z);
            var validation = _placementValidator.Validate(
                Project!,
                new PlacementValidationRequest(
                    ActiveAreaId!,
                    source.BlueprintId,
                    candidate,
                    .25f,
                    AutoCorrect: false));
            if (validation.IsValid)
            {
                return validation.CorrectedPosition;
            }
        }
        return null;
    }

    private (IReadOnlyList<string> Tags, EntityPropertyOverrides Overrides) ResolveAssetPlacement(
        BlueprintDefinition blueprint)
    {
        if (!AssetBlueprintFactory.TryGetAssetId(blueprint, out var assetId) || assetId is null)
        {
            return ([], EntityPropertyOverrides.Empty);
        }

        var manifest = AssetManifests.FirstOrDefault(candidate => candidate.AssetId == assetId);
        var tags = new[] { "asset" }
            .Concat(manifest?.ModelMetadata?.Styles ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var overrides = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String, assetId.ToString())]);
        return (tags, overrides);
    }

    private static StudioActionResult Failure(CommandResult result) => new(
        false,
        string.Join(" · ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));

    private void RememberAreaSelection()
    {
        if (ActiveAreaId is not null)
        {
            _areaSelections[ActiveAreaId] = SelectedEntityId;
            _areaSelectionSets[ActiveAreaId] = SelectedEntityIds.ToArray();
        }
    }

    private void RestoreAreaContext(AreaId? requestedAreaId)
    {
        var area = requestedAreaId is null
            ? Project?.Areas.FirstOrDefault()
            : Project?.Areas.FirstOrDefault(candidate => candidate.Id == requestedAreaId);
        if (area is null)
        {
            ActiveAreaId = null;
            SelectedEntityId = null;
            return;
        }

        ActiveAreaId = area.Id;
        var available = area.Entities.Select(entity => entity.Id).ToHashSet();
        var selectedIds = _areaSelectionSets.TryGetValue(area.Id, out var stored)
            ? stored.Where(available.Contains).Distinct().ToArray()
            : [];
        var selected = _areaSelections.TryGetValue(area.Id, out var active) &&
                       active is not null && selectedIds.Contains(active)
            ? active
            : selectedIds.LastOrDefault();
        _areaSelectionSets[area.Id] = selectedIds;
        _areaSelections[area.Id] = selected;
        SelectedEntityId = selected;
    }

    private static string WorkspaceProfileFingerprint(EditorWorkspaceProfile profile) =>
        Forge.ModuleFormat.Serialization.EditorWorkspaceProfileJsonSerializer.Serialize(profile);

    private static string KeymapFingerprint(EditorKeymapProfile profile) =>
        string.Join('|',
            profile.Id.Value,
            profile.SchemaVersion,
            profile.Preset,
            profile.Provenance,
            string.Join(';', profile.Bindings.Select(binding =>
                $"{binding.CommandId}={binding.Gesture.CanonicalText}")));

    private static string FormatKeymapDiagnostics(EditorKeymapCommandResult result) =>
        result.Diagnostics.Count == 0
            ? "Configuration de raccourcis refusée."
            : string.Join(" · ", result.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}"));

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private AreaDefinition? ResolveArea(string? areaId)
    {
        if (string.IsNullOrWhiteSpace(areaId) || Project is null)
        {
            return null;
        }

        try
        {
            var parsed = AreaId.Parse(areaId);
            return Project.Areas.FirstOrDefault(area => area.Id == parsed);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
