using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private readonly SetArtisticStyleProfileHandler _setArtisticStyleUx3E = new(new SystemClock());
    private readonly ConfigureBlueprintPrefabHandler _configurePrefabUx3E = new(new SystemClock());

    private StudioUx3EStyleDraft? _approvedStyleDraftUx3E;
    private ProjectRevision? _approvedStyleRevisionUx3E;
    private string? _approvedStyleProjectFingerprintUx3E;
    private string? _approvedStyleDraftFingerprintUx3E;
    private StudioUx3EBudgetDraft? _approvedBudgetDraftUx3E;
    private ProjectRevision? _approvedBudgetRevisionUx3E;
    private string? _approvedBudgetProjectFingerprintUx3E;
    private string? _approvedBudgetDraftFingerprintUx3E;

    internal IReadOnlyList<StudioUx3EBlueprintChoice> AssetQualityBlueprintChoicesUx3E
    {
        get
        {
            if (Project is null) return [];
            var styled = Project.Module.ArtisticStyle.Assets.Select(item => item.BlueprintId).ToHashSet();
            return Project.Blueprints
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Slug, StringComparer.Ordinal)
                .Select(item => new StudioUx3EBlueprintChoice(
                    item.Id.Value,
                    $"{item.DisplayName} · {HumanBlueprintKindUx3A(item.Kind)} · {HumanOriginUx3A(item.PaletteOrigin)}",
                    item.IsReadOnly(), styled.Contains(item.Id), item.Authoring.AssetBudget is not null))
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx3EWorkspaceAssetChoice> WorkspaceGlbChoicesUx3E =>
        EnumerateWorkspaceAssetsUx3E(".glb");

    internal IReadOnlyList<StudioUx3EWorkspaceAssetChoice> WorkspacePngChoicesUx3E =>
        EnumerateWorkspaceAssetsUx3E(".png");

    internal StudioUx3EStyleDraft? CreateStyleDraftUx3E(Guid blueprintId)
    {
        if (Project is null || History is null || blueprintId == Guid.Empty) return null;
        var blueprint = Project.Blueprints.FirstOrDefault(item => item.Id.Value == blueprintId);
        if (blueprint is null) return null;
        var profile = Project.Module.ArtisticStyle;
        var asset = profile.Assets.FirstOrDefault(item => item.BlueprintId == blueprint.Id);
        return new StudioUx3EStyleDraft(
            History.State.Revision, ProjectFingerprintUx3A(Project), blueprintId,
            profile.MetersPerUnit, profile.TargetTexelDensity, profile.ScaleTolerancePercent,
            profile.TexelTolerancePercent, profile.ShaderFamily, profile.Era, profile.Realism,
            profile.Palette.ToArray(), profile.Provenance,
            asset?.MetersPerUnit ?? profile.MetersPerUnit,
            asset?.TexelDensity ?? profile.TargetTexelDensity,
            asset?.ShaderFamily ?? profile.ShaderFamily,
            asset?.Era ?? profile.Era,
            asset?.Realism ?? profile.Realism,
            asset?.Palette.ToArray() ?? profile.Palette.ToArray(),
            asset?.Provenance ?? "studio:ux3-e",
            false);
    }

    internal StudioUx3EBudgetDraft? CreateBudgetDraftUx3E(Guid blueprintId)
    {
        if (Project is null || History is null || blueprintId == Guid.Empty) return null;
        var blueprint = Project.Blueprints.FirstOrDefault(item => item.Id.Value == blueprintId);
        if (blueprint is null) return null;
        var budget = blueprint.Authoring.AssetBudget;
        return new StudioUx3EBudgetDraft(
            History.State.Revision, ProjectFingerprintUx3A(Project), blueprintId,
            budget?.Id ?? StableGuidUx3A(Project.Id.Value, $"ux3-e-budget:{blueprintId:D}"),
            budget?.Provenance ?? "studio:ux3-e",
            budget?.Metrics ?? new AssetBudgetMetrics(0, 0, 0, 0, 0, 0, 0),
            budget?.Platforms.ToArray() ??
            [
                new AssetPlatformBudget(AssetTargetPlatform.Windows, 100_000, 24, 48, 536_870_912, 32, 4, 12),
                new AssetPlatformBudget(AssetTargetPlatform.Linux, 100_000, 24, 48, 536_870_912, 32, 4, 12),
            ],
            budget?.RequiredAtRuntime ?? false,
            false);
    }

    internal StudioUx3EMetricResult MeasureArtisticAssetUx3E(
        Guid blueprintId, string glbRelativePath, string textureRelativePath)
    {
        if (Project is null || blueprintId == Guid.Empty ||
            Project.Blueprints.All(item => item.Id.Value != blueprintId))
            return FailedMetricUx3E("Choisissez un blueprint réel avant la mesure.");
        if (!TryResolveWorkspaceAssetUx3E(glbRelativePath, ".glb", out var glb) ||
            !TryResolveWorkspaceAssetUx3E(textureRelativePath, ".png", out var texture))
            return FailedMetricUx3E("Choisissez un GLB et une texture PNG présents dans le workspace.");
        try
        {
            var metrics = ArtisticAssetMetricExtractor.Extract(
                BlueprintId.From(blueprintId), glb, [texture],
                $"workspace://{NormalizeAssetPathUx3E(glbRelativePath)}");
            return new StudioUx3EMetricResult(
                true,
                $"Mesure locale · {metrics.DimensionsMeters.Width:0.###} × " +
                $"{metrics.DimensionsMeters.Height:0.###} × {metrics.DimensionsMeters.Depth:0.###} m, " +
                $"{metrics.EstimatedTexelDensity} texels/m.",
                metrics.DimensionsMeters.Width, metrics.DimensionsMeters.Height,
                metrics.DimensionsMeters.Depth, metrics.MaximumTexturePixels,
                metrics.EstimatedTexelDensity, metrics.ShaderFamily, metrics.Provenance);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return FailedMetricUx3E(HumanMetricErrorUx3E(exception.Message));
        }
    }

    internal StudioUx3EStylePreview PreviewStyleUx3E(StudioUx3EStyleDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelStyleApprovalUx3E();
        if (Project is null || History is null)
            return FailedStyleUx3E("Aucun module n’est ouvert.", "art.style.project_missing", "project");
        if (!draft.Approved)
            return FailedStyleUx3E("Approuvez le profil avant l’aperçu sec.",
                "art.style.approval_required", "module/artisticStyle");
        if (draft.BaseRevision != History.State.Revision ||
            !string.Equals(draft.SourceProjectFingerprint, ProjectFingerprintUx3A(Project), StringComparison.Ordinal))
            return FailedStyleUx3E("Le projet a changé ; rechargez le profil artistique.",
                "art.style.project_stale", "project");
        if (Project.Blueprints.All(item => item.Id.Value != draft.BlueprintId))
            return FailedStyleUx3E("Le blueprint sélectionné n’existe plus.",
                ArtisticCompatibilityValidator.MissingBlueprintCode, "module/artisticStyle/assets");

        ArtisticStyleProfile candidateProfile;
        StudioUx3EStyleDraft frozen;
        try
        {
            frozen = FreezeStyleDraftUx3E(draft);
            candidateProfile = BuildStyleProfileUx3E(Project.Module, frozen);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return FailedStyleUx3E(HumanStyleErrorUx3E(exception),
                "art.style.field_invalid", "module/artisticStyle");
        }

        var command = new SetArtisticStyleProfileCommand(
            CommandId.From(Guid.NewGuid()), draft.BaseRevision, candidateProfile, DryRun: true);
        var before = Project;
        var beforeRevision = History.State.Revision;
        var dryRun = _setArtisticStyleUx3E.Handle(History.State, command);
        var dry = dryRun.Succeeded && ReferenceEquals(before, Project) && History.State.Revision == beforeRevision;
        if (!dryRun.Succeeded)
            return FailedStyleUx3E(dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.",
                dryRun.Diagnostics.FirstOrDefault()?.Code ?? "art.style.command_rejected", "module/artisticStyle");

        var candidate = _setArtisticStyleUx3E.Handle(History.State, command with
        {
            CommandId = CommandId.From(Guid.NewGuid()),
            DryRun = false,
        });
        if (!candidate.Succeeded)
            return FailedStyleUx3E(candidate.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.",
                candidate.Diagnostics.FirstOrDefault()?.Code ?? "art.style.command_rejected", "module/artisticStyle");
        var warnings = ArtisticCompatibilityValidator.Analyze(candidate.State.Project)
            .Select(item => new StudioUx3EDiagnostic(item.Code, item.Path, item.Message)).ToArray();
        var localized = warnings.All(item => !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));
        var changed = !SameStyleProfileUx3E(Project.Module.ArtisticStyle, candidateProfile);
        if (!changed)
            return new StudioUx3EStylePreview(true, false, dry, localized, true,
                $"Aucun changement · {warnings.Length} avertissement(s) artistique(s).",
                dryRun.Patch?.Summary ?? string.Empty, dryRun.Inverse?.Summary ?? string.Empty,
                [], warnings, warnings.Length);

        _approvedStyleDraftUx3E = frozen;
        _approvedStyleRevisionUx3E = beforeRevision;
        _approvedStyleProjectFingerprintUx3E = draft.SourceProjectFingerprint;
        _approvedStyleDraftFingerprintUx3E = StyleDraftFingerprintUx3E(frozen);
        var sourceAsset = Project.Module.ArtisticStyle.Assets.FirstOrDefault(item => item.BlueprintId.Value == draft.BlueprintId);
        return new StudioUx3EStylePreview(true, dry, dry, localized, true,
            warnings.Length == 0
                ? "Aperçu valide · profil et asset sont artistiquement compatibles."
                : $"Aperçu valide · {warnings.Length} écart(s) artistique(s) restent visibles avant build.",
            dryRun.Patch?.Summary ?? string.Empty, dryRun.Inverse?.Summary ?? string.Empty,
            [
                $"Profil module · {Project.Module.ArtisticStyle.TargetTexelDensity} → {candidateProfile.TargetTexelDensity} texels/m.",
                $"Asset sélectionné · {(sourceAsset is null ? "non décrit" : "déjà décrit")} → profil mesuré et gouverné.",
                $"Compatibilité · échelle, densité, shader, époque, réalisme et palette · {warnings.Length} avertissement(s).",
            ], warnings, warnings.Length);
    }

    internal StudioUx3EBudgetPreview PreviewBudgetUx3E(StudioUx3EBudgetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelBudgetApprovalUx3E();
        if (Project is null || History is null)
            return FailedBudgetUx3E("Aucun module n’est ouvert.", "blueprint.asset_budget.project_missing", "project");
        if (!draft.Approved)
            return FailedBudgetUx3E("Approuvez le budget avant l’aperçu sec.",
                "blueprint.asset_budget.approval_required", "blueprints/authoring/assetBudget");
        if (draft.BaseRevision != History.State.Revision ||
            !string.Equals(draft.SourceProjectFingerprint, ProjectFingerprintUx3A(Project), StringComparison.Ordinal))
            return FailedBudgetUx3E("Le projet a changé ; rechargez le budget.",
                "blueprint.asset_budget.project_stale", "project");
        var source = Project.Blueprints.FirstOrDefault(item => item.Id.Value == draft.BlueprintId);
        if (source is null)
            return FailedBudgetUx3E("Le blueprint sélectionné n’existe plus.",
                "blueprint.asset_budget.blueprint_missing", "blueprints");
        if (source.IsReadOnly())
            return FailedBudgetUx3E("Un blueprint Standard installé est protégé en lecture seule.",
                ConfigureBlueprintPrefabHandler.ReadOnlyCode, $"blueprints/{source.Id}/authoring/assetBudget");

        var frozen = FreezeBudgetDraftUx3E(draft);
        var profile = new BlueprintAssetBudgetProfile(
            frozen.ProfileId, BlueprintAssetBudgetProfile.CurrentSchemaVersion, frozen.Provenance,
            frozen.Metrics, frozen.Platforms, frozen.RequiredAtRuntime);
        var evaluations = profile.Platforms.Select(item => BlueprintPrefabRuntimeService.Evaluate(profile.Metrics, item)).ToArray();
        var authoring = CopyAuthoringWithBudgetUx3E(source.Authoring, profile);
        var validationBlueprint = source.WithPaletteUpdate(authoring, source.Provenance);
        var validationProject = ReplaceBlueprintUx3E(Project, validationBlueprint);
        var diagnostics = BlueprintPrefabValidator.Validate(validationProject, validationBlueprint)
            .Select(item => new StudioUx3EDiagnostic(item.Code, item.Path, item.Message)).ToArray();
        if (diagnostics.Length > 0)
            return FailedBudgetUx3E(
                diagnostics.Any(item => item.Code == BlueprintPrefabValidator.AssetBudgetExceededCode)
                    ? "Le budget contient une limite bloquante dépassée ; corrigez-la avant le build."
                    : diagnostics[0].Message,
                diagnostics[0].Code, diagnostics[0].Path, diagnostics, evaluations, true);

        var command = BudgetCommandUx3E(source, profile, History.State.Revision, true);
        var before = Project;
        var beforeRevision = History.State.Revision;
        var dryRun = _configurePrefabUx3E.Handle(History.State, command);
        var dry = dryRun.Succeeded && ReferenceEquals(before, Project) && History.State.Revision == beforeRevision;
        if (!dryRun.Succeeded)
            return FailedBudgetUx3E(dryRun.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.",
                dryRun.Diagnostics.FirstOrDefault()?.Code ?? "blueprint.asset_budget.command_rejected",
                $"blueprints/{source.Id}/authoring/assetBudget", evaluations: evaluations, buildGateEvaluated: true);
        var changed = !SameBudgetProfileUx3E(source.Authoring.AssetBudget, profile);
        if (!changed)
            return new StudioUx3EBudgetPreview(true, false, dry, true, true,
                "Aucun changement de budget à appliquer.", dryRun.Patch?.Summary ?? string.Empty,
                dryRun.Inverse?.Summary ?? string.Empty, [], [], evaluations);

        _approvedBudgetDraftUx3E = frozen;
        _approvedBudgetRevisionUx3E = beforeRevision;
        _approvedBudgetProjectFingerprintUx3E = draft.SourceProjectFingerprint;
        _approvedBudgetDraftFingerprintUx3E = BudgetDraftFingerprintUx3E(frozen);
        var warningCount = evaluations.Count(item => item.Status == AssetBudgetEvaluationStatus.Warning);
        return new StudioUx3EBudgetPreview(true, dry, dry, true, true,
            warningCount == 0
                ? "Aperçu valide · tous les budgets plateforme sont conformes."
                : $"Aperçu valide · {warningCount} dépassement(s) non bloquant(s) restent signalés.",
            dryRun.Patch?.Summary ?? string.Empty, dryRun.Inverse?.Summary ?? string.Empty,
            [
                $"Mesures · {profile.Metrics.TriangleCount:N0} triangles, {profile.Metrics.MaterialCount} matériaux, " +
                $"{profile.Metrics.TextureCount} textures, {HumanBytesUx3E(profile.Metrics.MemoryBytes)}.",
                $"Mesures avancées · {profile.Metrics.AnimationCount} animations, {profile.Metrics.LodLevelCount} LOD, " +
                $"{profile.Metrics.CollisionShapeCount} collisions.",
                $"Plateformes · {profile.Platforms.Count} budget(s), {warningCount} avertissement(s), aucun blocage.",
            ], [], evaluations);
    }

    internal StudioUx3EActionResult ApplyApprovedStyleUx3E(StudioUx3EStyleDraft current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (Project is null || History is null || _approvedStyleDraftUx3E is null ||
            _approvedStyleRevisionUx3E != History.State.Revision ||
            _approvedStyleProjectFingerprintUx3E != ProjectFingerprintUx3A(Project) ||
            _approvedStyleDraftFingerprintUx3E != StyleDraftFingerprintUx3E(FreezeStyleDraftUx3E(current)))
            return new(false, "Aperçu artistique périmé · relancez l’aperçu sec.");
        var profile = BuildStyleProfileUx3E(Project.Module, _approvedStyleDraftUx3E);
        var result = _setArtisticStyleUx3E.Handle(History.State, new SetArtisticStyleProfileCommand(
            CommandId.From(Guid.NewGuid()), History.State.Revision, profile, DryRun: false));
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.");
        History.Record(result);
        CancelUx3EApprovals();
        return new(true, "Profil appliqué par set_artistic_style_profile · Ctrl+Z restaure le document canonique.");
    }

    internal StudioUx3EActionResult ApplyApprovedBudgetUx3E(StudioUx3EBudgetDraft current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (Project is null || History is null || _approvedBudgetDraftUx3E is null ||
            _approvedBudgetRevisionUx3E != History.State.Revision ||
            _approvedBudgetProjectFingerprintUx3E != ProjectFingerprintUx3A(Project) ||
            _approvedBudgetDraftFingerprintUx3E != BudgetDraftFingerprintUx3E(FreezeBudgetDraftUx3E(current)))
            return new(false, "Aperçu du budget périmé · relancez l’aperçu sec.");
        var source = Project.Blueprints.FirstOrDefault(item => item.Id.Value == current.BlueprintId);
        if (source is null) return new(false, "Le blueprint sélectionné n’existe plus.");
        var approved = _approvedBudgetDraftUx3E;
        var profile = new BlueprintAssetBudgetProfile(
            approved.ProfileId, BlueprintAssetBudgetProfile.CurrentSchemaVersion, approved.Provenance,
            approved.Metrics, approved.Platforms, approved.RequiredAtRuntime);
        var result = _configurePrefabUx3E.Handle(History.State,
            BudgetCommandUx3E(source, profile, History.State.Revision, false));
        if (!result.Succeeded)
            return new(false, result.Diagnostics.FirstOrDefault()?.Message ?? "Commande refusée.");
        History.Record(result);
        CancelUx3EApprovals();
        return new(true, "Budget appliqué par configure_blueprint_prefab · Ctrl+Z restaure l’état précédent.");
    }

    internal StudioUx3EActionResult UndoAssetQualityUx3E()
    {
        var result = Undo();
        CancelUx3EApprovals();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3EActionResult RedoAssetQualityUx3E()
    {
        var result = Redo();
        CancelUx3EApprovals();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3EActionResult SaveAssetQualityUx3E()
    {
        var result = Save();
        return new(result.Succeeded, result.Message);
    }

    internal StudioUx3EApprovalSnapshot CaptureAssetQualityApprovalUx3E() => new(
        _approvedStyleDraftUx3E, _approvedStyleRevisionUx3E,
        _approvedStyleProjectFingerprintUx3E, _approvedStyleDraftFingerprintUx3E,
        _approvedBudgetDraftUx3E, _approvedBudgetRevisionUx3E,
        _approvedBudgetProjectFingerprintUx3E, _approvedBudgetDraftFingerprintUx3E);

    internal void RestoreAssetQualityApprovalUx3E(StudioUx3EApprovalSnapshot snapshot)
    {
        _approvedStyleDraftUx3E = snapshot.StyleDraft;
        _approvedStyleRevisionUx3E = snapshot.StyleRevision;
        _approvedStyleProjectFingerprintUx3E = snapshot.StyleProjectFingerprint;
        _approvedStyleDraftFingerprintUx3E = snapshot.StyleDraftFingerprint;
        _approvedBudgetDraftUx3E = snapshot.BudgetDraft;
        _approvedBudgetRevisionUx3E = snapshot.BudgetRevision;
        _approvedBudgetProjectFingerprintUx3E = snapshot.BudgetProjectFingerprint;
        _approvedBudgetDraftFingerprintUx3E = snapshot.BudgetDraftFingerprint;
    }

    internal void CloseUx3E() => CancelUx3EApprovals();

    internal StudioUx3EEvidenceProject? CreateAssetQualityEvidenceProjectUx3E()
    {
        var evidence = CreateBlueprintEvidenceProjectUx3A();
        if (evidence is null) return null;
        return new StudioUx3EEvidenceProject(
            evidence.Project,
            evidence.EditableBlueprintId,
            "content/ux3-e/compatibility-evidence.glb",
            "content/ux3-e/compatibility-evidence.png",
            false,
            true);
    }

    internal static bool MaterializeAssetQualityEvidenceFilesUx3E(string root, StudioUx3EEvidenceProject evidence)
    {
        try
        {
            var glb = Path.Combine(root, evidence.GlbRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var png = Path.Combine(root, evidence.TextureRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(glb)!);
            Directory.CreateDirectory(Path.GetDirectoryName(png)!);
            File.WriteAllBytes(glb, CreateEvidenceGlbUx3E());
            File.WriteAllBytes(png, CreateEvidencePngUx3E(1024, 1536));
            return File.Exists(glb) && File.Exists(png);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal StudioUx3ESmokeResult RunUx3EContractSmoke()
    {
        var empty = EmptyAssetQualitySmokeUx3E();
        var evidence = CreateAssetQualityEvidenceProjectUx3E();
        if (evidence is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-e-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        StudioUx3ESmokeResult Execute()
        {
            try
            {
                if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
                    !MaterializeAssetQualityEvidenceFilesUx3E(root, evidence) ||
                    !editing.Open(root).Succeeded)
                    return empty;
                var choices = editing.AssetQualityBlueprintChoicesUx3E;
                var explicitSelection = editing.CreateStyleDraftUx3E(Guid.Empty) is null && choices.Count >= 1;
                var human = choices.All(item => !item.Label.Contains(item.Id.ToString("D"),
                    StringComparison.OrdinalIgnoreCase));
                var metric = editing.MeasureArtisticAssetUx3E(
                    evidence.EditableBlueprintId, evidence.GlbRelativePath, evidence.TextureRelativePath);
                var metricExtraction = metric.Succeeded && metric.HeightMeters == 3 &&
                                       metric.EstimatedTexelDensity == 512 &&
                                       metric.ShaderFamily == "pbr-metallic-roughness";

                var style = editing.CreateStyleDraftUx3E(evidence.EditableBlueprintId);
                if (style is null) return empty;
                var invalidStyle = editing.PreviewStyleUx3E(style with { TargetTexelDensity = 4, Approved = true });
                var invalidStyleBlocked = !invalidStyle.Succeeded;
                var configuredStyle = ChangedStyleDraftUx3E(style);
                var sourceProject = editing.Project!;
                var sourceJson = ProjectJsonSerializer.Serialize(sourceProject);
                var sourceBlueprint = sourceProject.Blueprints.Single(item => item.Id.Value == evidence.EditableBlueprintId);
                var sourceOtherBlueprints = sourceProject.Blueprints.Where(item => item.Id.Value != evidence.EditableBlueprintId).ToArray();
                var beforeRevision = editing.History!.State.Revision;
                var stylePreview = editing.PreviewStyleUx3E(configuredStyle);
                var sixWarnings = stylePreview.Succeeded && stylePreview.CompatibilityWarningCount == 6 &&
                                  stylePreview.BuildDiagnosticLocalized;
                var dryRun = stylePreview.Succeeded && ReferenceEquals(sourceProject, editing.Project) &&
                             editing.History.State.Revision == beforeRevision;
                var staleApproval = stylePreview.Succeeded &&
                    !editing.ApplyApprovedStyleUx3E(configuredStyle with
                    {
                        AssetTexelDensity = configuredStyle.AssetTexelDensity + 1,
                    }).Succeeded;

                stylePreview = editing.PreviewStyleUx3E(configuredStyle);
                var concurrent = editing._configurePrefabUx3E.Handle(editing.History.State,
                    BudgetCommandUx3E(sourceBlueprint, sourceBlueprint.Authoring.AssetBudget,
                        editing.History.State.Revision, false));
                var staleSource = false;
                if (concurrent.Succeeded)
                {
                    editing.History.Record(concurrent);
                    staleSource = !editing.ApplyApprovedStyleUx3E(configuredStyle).Succeeded;
                    editing.Undo();
                }

                style = editing.CreateStyleDraftUx3E(evidence.EditableBlueprintId);
                if (style is null) return empty;
                configuredStyle = ChangedStyleDraftUx3E(style);
                stylePreview = editing.PreviewStyleUx3E(configuredStyle);

                var budget = editing.CreateBudgetDraftUx3E(evidence.EditableBlueprintId);
                if (budget is null) return empty;
                var invalidBudget = editing.PreviewBudgetUx3E(budget with
                {
                    Metrics = budget.Metrics with { TriangleCount = -1 },
                    Approved = true,
                });
                var configuredBudget = ChangedBudgetDraftUx3E(budget);
                var failingBudget = configuredBudget with
                {
                    Platforms =
                    [
                        new AssetPlatformBudget(AssetTargetPlatform.Windows, 40_000, 8, 12, 64_000_000, 8, 4, 6),
                        new AssetPlatformBudget(AssetTargetPlatform.Linux, 10_000, 4, 4, 8_000_000, 2, 1, 1, false),
                        new AssetPlatformBudget(AssetTargetPlatform.Any, 5_000, 2, 2, 4_000_000, 1, 1, 1),
                    ],
                };
                var failingPreview = editing.PreviewBudgetUx3E(failingBudget);
                var buildGateBlocked = !failingPreview.Succeeded && failingPreview.BuildGateEvaluated &&
                                       failingPreview.Diagnostics.Any(item =>
                                           item.Code == BlueprintPrefabValidator.AssetBudgetExceededCode);
                var statuses = failingBudget.Platforms
                    .Select(item => BlueprintPrefabRuntimeService.Evaluate(failingBudget.Metrics, item)).ToArray();
                var passWarningFail = statuses.Any(item => item.Status == AssetBudgetEvaluationStatus.Passed) &&
                                      statuses.Any(item => item.Status == AssetBudgetEvaluationStatus.Warning) &&
                                      statuses.Any(item => item.Status == AssetBudgetEvaluationStatus.Failed);
                var sevenAxes = configuredBudget.Metrics is
                {
                    TriangleCount: > 0, MaterialCount: > 0, TextureCount: > 0, MemoryBytes: > 0,
                    AnimationCount: > 0, LodLevelCount: > 0, CollisionShapeCount: > 0,
                };
                var budgetPreview = editing.PreviewBudgetUx3E(configuredBudget);
                var approvals = editing.CaptureAssetQualityApprovalUx3E();
                var separateApprovals = stylePreview.Succeeded && budgetPreview.Succeeded &&
                                        approvals.StyleDraft is not null && approvals.BudgetDraft is not null;

                var styleApplied = editing.ApplyApprovedStyleUx3E(configuredStyle);
                var styleProject = editing.Project!;
                var styleJson = ProjectJsonSerializer.Serialize(styleProject);
                budget = editing.CreateBudgetDraftUx3E(evidence.EditableBlueprintId);
                if (budget is null) return empty;
                configuredBudget = ChangedBudgetDraftUx3E(budget);
                budgetPreview = editing.PreviewBudgetUx3E(configuredBudget);
                var budgetApplied = editing.ApplyApprovedBudgetUx3E(configuredBudget);
                var finalProject = editing.Project!;
                var finalJson = ProjectJsonSerializer.Serialize(finalProject);
                var finalBlueprint = finalProject.Blueprints.Single(item => item.Id.Value == evidence.EditableBlueprintId);
                var moduleTails = SameModuleTailsUx3E(sourceProject.Module, finalProject.Module);
                var authoringTails = SameAuthoringTailsUx3E(sourceBlueprint.Authoring, finalBlueprint.Authoring);
                var blueprintTails = SameBlueprintTailsUx3E(sourceBlueprint, finalBlueprint);
                var blueprintNeighbors = sourceOtherBlueprints.SequenceEqual(finalProject.Blueprints
                    .Where(item => item.Id.Value != evidence.EditableBlueprintId));
                var projectNeighbors = sourceProject.Id == finalProject.Id && sourceProject.Title == finalProject.Title &&
                                       sourceProject.Areas.SequenceEqual(finalProject.Areas) &&
                                       sourceProject.Dialogues.SequenceEqual(finalProject.Dialogues) &&
                                       sourceProject.Quests.SequenceEqual(finalProject.Quests);
                var canonical = styleApplied.Succeeded && budgetApplied.Succeeded &&
                                editing.History.Journal.Any(item => item.CommandName == "set_artistic_style_profile") &&
                                editing.History.Journal.Any(item => item.CommandName == "configure_blueprint_prefab");
                var undoBudget = editing.UndoAssetQualityUx3E();
                var budgetUndone = undoBudget.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == styleJson;
                var undoStyle = editing.UndoAssetQualityUx3E();
                var styleUndone = undoStyle.Succeeded && ProjectJsonSerializer.Serialize(editing.Project!) == sourceJson;
                var redoStyle = editing.RedoAssetQualityUx3E();
                var redoBudget = editing.RedoAssetQualityUx3E();
                var redone = redoStyle.Succeeded && redoBudget.Succeeded &&
                             ProjectJsonSerializer.Serialize(editing.Project!) == finalJson;
                var saved = editing.SaveAssetQualityUx3E();
                editing.Close();
                var persistence = saved.Succeeded && reopened.Open(root).Succeeded &&
                                  ProjectJsonSerializer.Serialize(reopened.Project!) == finalJson;
                reopened.Close();
                var succeeded = explicitSelection && evidence.UsesCanonicalFallback && metricExtraction && sixWarnings &&
                                invalidStyleBlocked && !invalidBudget.Succeeded && sevenAxes && passWarningFail &&
                                buildGateBlocked && dryRun && staleSource && staleApproval && separateApprovals &&
                                canonical && budgetUndone && styleUndone && redone && persistence && moduleTails &&
                                authoringTails && blueprintTails && blueprintNeighbors && projectNeighbors && human;
                return new StudioUx3ESmokeResult(
                    succeeded, explicitSelection, evidence.UsesRealModule, evidence.UsesCanonicalFallback,
                    metricExtraction, sixWarnings, invalidStyleBlocked, sevenAxes, passWarningFail,
                    buildGateBlocked, dryRun, staleSource, staleApproval, separateApprovals, canonical,
                    budgetUndone && styleUndone && redone, persistence, moduleTails, authoringTails,
                    blueprintTails, blueprintNeighbors, projectNeighbors, human, true, false);
            }
            catch
            {
                return empty;
            }
            finally
            {
                editing.Close();
                reopened.Close();
            }
        }

        var result = Execute();
        var released = DeleteTemporaryBlueprintUx3A(root);
        return result with { TemporaryWorkspaceReleased = released, Succeeded = result.Succeeded && released };
    }

    internal static StudioUx3EStyleDraft ChangedStyleDraftUx3E(StudioUx3EStyleDraft draft) => draft with
    {
        TargetMetersPerUnit = 1,
        TargetTexelDensity = 512,
        ScaleTolerancePercent = 10,
        TexelTolerancePercent = 10,
        TargetShaderFamily = "pbr-metallic-roughness",
        TargetEra = "original-fantasy",
        TargetRealism = ArtisticRealism.StylizedRealism,
        TargetPalette = ["earth", "stone"],
        TargetProvenance = "studio:ux3-e-profile",
        AssetMetersPerUnit = 2,
        AssetTexelDensity = 128,
        AssetShaderFamily = "unlit",
        AssetEra = "science-fiction",
        AssetRealism = ArtisticRealism.Realistic,
        AssetPalette = ["neon"],
        AssetProvenance = "studio:ux3-e-measured",
        Approved = true,
    };

    internal static StudioUx3EBudgetDraft ChangedBudgetDraftUx3E(StudioUx3EBudgetDraft draft) => draft with
    {
        Provenance = "studio:ux3-e-budget",
        Metrics = new AssetBudgetMetrics(20_000, 6, 8, 32_000_000, 4, 3, 4),
        Platforms =
        [
            new AssetPlatformBudget(AssetTargetPlatform.Windows, 40_000, 8, 12, 64_000_000, 8, 4, 6),
            new AssetPlatformBudget(AssetTargetPlatform.Linux, 10_000, 4, 4, 8_000_000, 2, 1, 1, false),
        ],
        RequiredAtRuntime = true,
        Approved = true,
    };

    internal string StyleDraftFingerprintUx3E(StudioUx3EStyleDraft draft)
    {
        var frozen = FreezeStyleDraftUx3E(draft);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(frozen.BaseRevision.Value);
        WriteBlueprintUx3A(writer, frozen.SourceProjectFingerprint);
        writer.Write(frozen.BlueprintId.ToByteArray());
        writer.Write(frozen.TargetMetersPerUnit);
        writer.Write(frozen.TargetTexelDensity);
        writer.Write(frozen.ScaleTolerancePercent);
        writer.Write(frozen.TexelTolerancePercent);
        WriteBlueprintUx3A(writer, frozen.TargetShaderFamily);
        WriteBlueprintUx3A(writer, frozen.TargetEra);
        writer.Write((int)frozen.TargetRealism);
        WriteBlueprintStringsUx3A(writer, frozen.TargetPalette);
        WriteBlueprintUx3A(writer, frozen.TargetProvenance);
        writer.Write(frozen.AssetMetersPerUnit);
        writer.Write(frozen.AssetTexelDensity);
        WriteBlueprintUx3A(writer, frozen.AssetShaderFamily);
        WriteBlueprintUx3A(writer, frozen.AssetEra);
        writer.Write((int)frozen.AssetRealism);
        WriteBlueprintStringsUx3A(writer, frozen.AssetPalette);
        WriteBlueprintUx3A(writer, frozen.AssetProvenance);
        writer.Write(frozen.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    internal string BudgetDraftFingerprintUx3E(StudioUx3EBudgetDraft draft)
    {
        var frozen = FreezeBudgetDraftUx3E(draft);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(frozen.BaseRevision.Value);
        WriteBlueprintUx3A(writer, frozen.SourceProjectFingerprint);
        writer.Write(frozen.BlueprintId.ToByteArray());
        writer.Write(frozen.ProfileId.ToByteArray());
        WriteBlueprintUx3A(writer, frozen.Provenance);
        WriteBudgetMetricsUx3E(writer, frozen.Metrics);
        writer.Write(frozen.Platforms.Count);
        foreach (var platform in frozen.Platforms)
        {
            writer.Write((int)platform.Platform);
            writer.Write(platform.MaximumTriangles);
            writer.Write(platform.MaximumMaterials);
            writer.Write(platform.MaximumTextures);
            writer.Write(platform.MaximumMemoryBytes);
            writer.Write(platform.MaximumAnimations);
            writer.Write(platform.MaximumLodLevels);
            writer.Write(platform.MaximumCollisionShapes);
            writer.Write(platform.GateBuild);
        }
        writer.Write(frozen.RequiredAtRuntime);
        writer.Write(frozen.Approved);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private IReadOnlyList<StudioUx3EWorkspaceAssetChoice> EnumerateWorkspaceAssetsUx3E(string extension)
    {
        if (Session is null || !Directory.Exists(Session.RootDirectory)) return [];
        var root = Path.GetFullPath(Session.RootDirectory);
        return Directory.EnumerateFiles(root, $"*{extension}", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredAssetPathUx3E(root, path))
            .Select(path => NormalizeAssetPathUx3E(Path.GetRelativePath(root, path)))
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .Take(512)
            .Select(path => new StudioUx3EWorkspaceAssetChoice(path, HumanAssetPathUx3E(path)))
            .ToArray();
    }

    private bool TryResolveWorkspaceAssetUx3E(string relativePath, string extension, out string absolute)
    {
        absolute = string.Empty;
        if (Session is null || string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return false;
        var normalized = NormalizeAssetPathUx3E(relativePath);
        if (!normalized.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
            normalized.Split('/').Any(segment => segment is "" or "." or "..")) return false;
        var root = Path.GetFullPath(Session.RootDirectory).TrimEnd(Path.DirectorySeparatorChar) +
                   Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(Session.RootDirectory,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate)) return false;
        absolute = candidate;
        return true;
    }

    private static ArtisticStyleProfile BuildStyleProfileUx3E(ModuleDefinition module, StudioUx3EStyleDraft draft)
    {
        var selected = new ArtisticAssetStyle(
            BlueprintId.From(draft.BlueprintId), draft.AssetMetersPerUnit, draft.AssetTexelDensity,
            draft.AssetShaderFamily, draft.AssetEra, draft.AssetRealism, draft.AssetPalette, draft.AssetProvenance);
        var assets = module.ArtisticStyle.Assets.Where(item => item.BlueprintId.Value != draft.BlueprintId)
            .Append(selected).ToArray();
        return new ArtisticStyleProfile(
            ArtisticStyleProfileId.From(module.Id.Value), draft.TargetMetersPerUnit, draft.TargetTexelDensity,
            draft.ScaleTolerancePercent, draft.TexelTolerancePercent, draft.TargetShaderFamily,
            draft.TargetEra, draft.TargetRealism, draft.TargetPalette, assets, draft.TargetProvenance);
    }

    private static StudioUx3EStyleDraft FreezeStyleDraftUx3E(StudioUx3EStyleDraft draft) => draft with
    {
        TargetShaderFamily = draft.TargetShaderFamily.Trim().ToLowerInvariant(),
        TargetEra = draft.TargetEra.Trim().ToLowerInvariant(),
        TargetPalette = NormalizePaletteUx3E(draft.TargetPalette),
        TargetProvenance = draft.TargetProvenance.Trim(),
        AssetShaderFamily = draft.AssetShaderFamily.Trim().ToLowerInvariant(),
        AssetEra = draft.AssetEra.Trim().ToLowerInvariant(),
        AssetPalette = NormalizePaletteUx3E(draft.AssetPalette),
        AssetProvenance = draft.AssetProvenance.Trim(),
    };

    private static StudioUx3EBudgetDraft FreezeBudgetDraftUx3E(StudioUx3EBudgetDraft draft) => draft with
    {
        Provenance = draft.Provenance.Trim(),
        Platforms = draft.Platforms.OrderBy(item => item.Platform).ToArray(),
    };

    private static IReadOnlyList<string> NormalizePaletteUx3E(IEnumerable<string> values) => values
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .Select(item => item.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .Take(12)
        .ToArray();

    private static ConfigureBlueprintPrefabCommand BudgetCommandUx3E(
        BlueprintDefinition source, BlueprintAssetBudgetProfile? budget, ProjectRevision revision, bool dryRun) => new(
        CommandId.From(Guid.NewGuid()), revision, source.Id, source.Authoring.PrefabVariant,
        source.Authoring.PrefabComposition, budget, source.Authoring.AuthorCollections, dryRun);

    private static BlueprintAuthoringMetadata CopyAuthoringWithBudgetUx3E(
        BlueprintAuthoringMetadata source, BlueprintAssetBudgetProfile budget) => new(
        source.SchemaVersion, source.Version, source.PackageNamespace, source.ManifestPath,
        source.ExposedParameters, source.Dependencies, source.PreviewPath, source.PlatformByteBudget,
        source.Kit, source.Styles, source.Compatibility, source.Tags, source.CategoryPaths,
        source.PromotedDefaults, source.AuthorCollections, source.PrefabVariant, source.PrefabComposition, budget);

    private static ProjectDefinition ReplaceBlueprintUx3E(ProjectDefinition project, BlueprintDefinition updated) => new(
        project.Id, project.Title, project.Module,
        project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
        project.Areas, project.Dialogues, project.Quests);

    private void CancelUx3EApprovals()
    {
        CancelStyleApprovalUx3E();
        CancelBudgetApprovalUx3E();
    }

    private void CancelStyleApprovalUx3E()
    {
        _approvedStyleDraftUx3E = null;
        _approvedStyleRevisionUx3E = null;
        _approvedStyleProjectFingerprintUx3E = null;
        _approvedStyleDraftFingerprintUx3E = null;
    }

    private void CancelBudgetApprovalUx3E()
    {
        _approvedBudgetDraftUx3E = null;
        _approvedBudgetRevisionUx3E = null;
        _approvedBudgetProjectFingerprintUx3E = null;
        _approvedBudgetDraftFingerprintUx3E = null;
    }

    private static bool SameStyleProfileUx3E(ArtisticStyleProfile left, ArtisticStyleProfile right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        left.MetersPerUnit.Equals(right.MetersPerUnit) && left.TargetTexelDensity == right.TargetTexelDensity &&
        left.ScaleTolerancePercent.Equals(right.ScaleTolerancePercent) &&
        left.TexelTolerancePercent.Equals(right.TexelTolerancePercent) &&
        left.ShaderFamily == right.ShaderFamily && left.Era == right.Era && left.Realism == right.Realism &&
        left.Palette.SequenceEqual(right.Palette, StringComparer.Ordinal) &&
        left.Assets.SequenceEqual(right.Assets) && left.Provenance == right.Provenance;

    private static bool SameBudgetProfileUx3E(BlueprintAssetBudgetProfile? left, BlueprintAssetBudgetProfile right) =>
        left is not null && left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        left.Provenance == right.Provenance && left.Metrics == right.Metrics &&
        left.Platforms.SequenceEqual(right.Platforms) && left.RequiredAtRuntime == right.RequiredAtRuntime;

    private static bool SameModuleTailsUx3E(ModuleDefinition left, ModuleDefinition right) =>
        left.Id == right.Id && left.Slug == right.Slug && left.DisplayName == right.DisplayName &&
        left.Description == right.Description && left.EntryAreaId == right.EntryAreaId &&
        left.Metadata == right.Metadata && left.Variables.SequenceEqual(right.Variables) &&
        left.Distribution == right.Distribution && left.Localization == right.Localization &&
        left.Content == right.Content && left.RuntimePolicies == right.RuntimePolicies &&
        left.Factions == right.Factions && left.WorldFeatures == right.WorldFeatures;

    private static bool SameAuthoringTailsUx3E(BlueprintAuthoringMetadata left, BlueprintAuthoringMetadata right) =>
        left.SchemaVersion == right.SchemaVersion && left.Version == right.Version &&
        left.PackageNamespace == right.PackageNamespace && left.ManifestPath == right.ManifestPath &&
        left.ExposedParameters.SequenceEqual(right.ExposedParameters, StringComparer.Ordinal) &&
        left.Dependencies.SequenceEqual(right.Dependencies, StringComparer.Ordinal) &&
        left.PreviewPath == right.PreviewPath && left.PlatformByteBudget == right.PlatformByteBudget &&
        left.Kit == right.Kit && left.Styles.SequenceEqual(right.Styles, StringComparer.Ordinal) &&
        left.Compatibility.SequenceEqual(right.Compatibility, StringComparer.Ordinal) &&
        left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal) &&
        left.CategoryPaths.SequenceEqual(right.CategoryPaths, StringComparer.Ordinal) &&
        left.PromotedDefaults.SequenceEqual(right.PromotedDefaults) &&
        left.AuthorCollections.SequenceEqual(right.AuthorCollections, StringComparer.Ordinal) &&
        Equals(left.PrefabVariant, right.PrefabVariant) && Equals(left.PrefabComposition, right.PrefabComposition);

    private static bool SameBlueprintTailsUx3E(BlueprintDefinition left, BlueprintDefinition right) =>
        left.Id == right.Id && left.Slug == right.Slug && left.DisplayName == right.DisplayName &&
        left.Kind == right.Kind && left.Combat == right.Combat && left.PaletteOrigin == right.PaletteOrigin &&
        left.IsFavorite == right.IsFavorite && left.CreaturePresentation == right.CreaturePresentation &&
        left.CreatureProgression == right.CreatureProgression && left.CreatureLoot == right.CreatureLoot &&
        left.CreatureRoleBehavior == right.CreatureRoleBehavior && left.CreatureEvents == right.CreatureEvents &&
        left.ItemAdvanced == right.ItemAdvanced && left.CreatureFoundation == right.CreatureFoundation &&
        left.ItemFoundation == right.ItemFoundation;

    private static void WriteBudgetMetricsUx3E(BinaryWriter writer, AssetBudgetMetrics metrics)
    {
        writer.Write(metrics.TriangleCount);
        writer.Write(metrics.MaterialCount);
        writer.Write(metrics.TextureCount);
        writer.Write(metrics.MemoryBytes);
        writer.Write(metrics.AnimationCount);
        writer.Write(metrics.LodLevelCount);
        writer.Write(metrics.CollisionShapeCount);
    }

    private static byte[] CreateEvidenceGlbUx3E()
    {
        const string json =
            "{\"asset\":{\"version\":\"2.0\"},\"accessors\":[{\"min\":[-1,0,-0.5],\"max\":[1,3,0.5]}]," +
            "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0}}]}]," +
            "\"materials\":[{\"pbrMetallicRoughness\":{}}]}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedLength = (jsonBytes.Length + 3) & ~3;
        var bytes = new byte[20 + paddedLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), paddedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x4E4F534A);
        jsonBytes.CopyTo(bytes.AsSpan(20));
        bytes.AsSpan(20 + jsonBytes.Length).Fill((byte)' ');
        return bytes;
    }

    private static byte[] CreateEvidencePngUx3E(int width, int height)
    {
        var bytes = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    private static bool IsIgnoredAssetPathUx3E(string root, string path)
    {
        var segments = Path.GetRelativePath(root, path)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => segment is ".forge" or ".godot" or "bin" or "obj" or
            "artifacts" or "tmp" or "native");
    }

    private static string NormalizeAssetPathUx3E(string path) => path.Replace('\\', '/').Trim();

    private static string HumanAssetPathUx3E(string path)
    {
        var sanitized = Regex.Replace(NormalizeAssetPathUx3E(path),
            "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            "dossier-protege", RegexOptions.CultureInvariant);
        return sanitized.Length <= 92 ? sanitized : "…/" + sanitized[^89..];
    }

    internal static string HumanAssetQualityPathUx3E(string path) => HumanAssetPathUx3E(path)
        .Replace("module/artisticStyle", "profil artistique du module", StringComparison.Ordinal)
        .Replace("blueprints/", "blueprint / ", StringComparison.Ordinal)
        .Replace("/authoring/assetBudget", " / budget d’asset", StringComparison.Ordinal)
        .Replace("/platforms/", " / plateforme / ", StringComparison.Ordinal);

    private static string HumanMetricErrorUx3E(string message) => message switch
    {
        "ART_STYLE_GLB_INVALID" => "Le fichier choisi n’est pas un GLB valide.",
        "ART_STYLE_GLB_JSON_MISSING" => "Le GLB ne contient pas de document glTF lisible.",
        "ART_STYLE_GLB_DOCUMENT_INVALID" => "Le GLB ne fournit pas de bornes POSITION exploitables.",
        "ART_STYLE_TEXTURE_INVALID" => "La texture choisie n’est pas un PNG valide.",
        "ART_STYLE_TEXTURE_METRICS_MISSING" => "La texture ne fournit aucune dimension exploitable.",
        _ => "La mesure locale a échoué sans modifier le projet.",
    };

    private static string HumanStyleErrorUx3E(Exception exception) =>
        (exception as ArgumentException)?.ParamName switch
        {
            "metersPerUnit" => "L’échelle doit rester entre 0,001 et 1 000 m par unité.",
            "targetTexelDensity" or "texelDensity" => "La densité doit rester entre 16 et 8 192 texels/m.",
            "scaleTolerancePercent" => "La tolérance d’échelle doit rester entre 0 et 100 %.",
            "texelTolerancePercent" => "La tolérance texel doit rester entre 0 et 400 %.",
            _ => "Shader, époque, palette et provenance doivent être renseignés.",
        };

    private static string HumanBytesUx3E(long value) => value >= 1_048_576
        ? $"{value / 1_048_576d:0.##} Mio"
        : $"{value / 1024d:0.##} Kio";

    private static StudioUx3EMetricResult FailedMetricUx3E(string message) =>
        new(false, message, 0, 0, 0, 0, 0, string.Empty, string.Empty);

    private static StudioUx3EStylePreview FailedStyleUx3E(string message, string code, string path,
        IReadOnlyList<StudioUx3EDiagnostic>? diagnostics = null) => new(
        false, false, false, true, true, message, string.Empty, string.Empty, [],
        diagnostics ?? [new StudioUx3EDiagnostic(code, path, message)], 0);

    private static StudioUx3EBudgetPreview FailedBudgetUx3E(string message, string code, string path,
        IReadOnlyList<StudioUx3EDiagnostic>? diagnostics = null,
        IReadOnlyList<AssetBudgetEvaluation>? evaluations = null,
        bool buildGateEvaluated = false) => new(
        false, false, false, buildGateEvaluated, true, message, string.Empty, string.Empty, [],
        diagnostics ?? [new StudioUx3EDiagnostic(code, path, message)], evaluations ?? []);

    private static StudioUx3ESmokeResult EmptyAssetQualitySmokeUx3E() => new(
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, true, false);
}
