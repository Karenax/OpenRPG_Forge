using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Domain.WorldGeneration;

namespace Forge.Application.WorldGeneration;

public sealed record WorldGenerationProviderEvidence(
    WorldGenerationStage Stage,
    WorldGenerationBackend RequestedBackend,
    string ProviderKey,
    string ProviderVersion,
    string EvidenceSha256);

public sealed record WorldGenerationValidationReport(
    int SchemaVersion,
    Guid JobId,
    Guid PlanId,
    Guid RecipeId,
    Guid AreaId,
    long ProjectRevision,
    WorldGenerationRecipeRevision RecipeRevision,
    string RecipeFingerprint,
    string PlanFingerprint,
    string DerivedSceneSha256,
    string ContentFingerprint,
    string AssetSelectionFingerprint,
    bool ValidationSucceeded,
    bool CollisionValidated,
    bool NavigationValidated,
    bool ManualEditsPreserved,
    IReadOnlyList<WorldGenerationProviderEvidence> Providers,
    IReadOnlyList<WorldGenerationValidationIssue> Diagnostics,
    WorldGenerationTerrainSurfaceEvidence? TerrainSurface = null,
    WorldGenerationEnvironmentOccupancyEvidence? EnvironmentOccupancy = null)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record PromoteWorldGenerationCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid PromotionPlanId,
    ProjectRevision BaseRevision,
    WorldGenerationRecipeRevision BaseRecipeRevision,
    Guid JobPlanId,
    WorldGenerationValidationReport ValidationReport,
    string DerivedSceneRelativePath,
    string CanonicalSceneRelativePath,
    string? ExpectedCanonicalSceneSha256,
    string AcceptanceProvenance,
    AssistantPermissionLevel Permission,
    bool DryRun);

public sealed record WorldGenerationCanonicalPromotion(
    int SchemaVersion,
    Guid PromotionId,
    Guid TransactionId,
    Guid JobId,
    Guid PlanId,
    Guid RecipeId,
    Guid AreaId,
    long BaseProjectRevision,
    long NewProjectRevision,
    long RecipeRevision,
    string RecipeFingerprint,
    string PlanFingerprint,
    int Seed,
    string DerivedSceneRelativePath,
    string CanonicalSceneRelativePath,
    string CanonicalSceneSha256,
    string ContentFingerprint,
    string AssetSelectionFingerprint,
    IReadOnlyList<WorldGenerationProviderEvidence> Providers,
    IReadOnlyList<string> ProtectedNodePaths,
    string AcceptanceProvenance,
    WorldGenerationTerrainSurfaceEvidence? TerrainSurface = null,
    WorldGenerationEnvironmentOccupancyEvidence? EnvironmentOccupancy = null,
    WorldGenerationProductionVisualEvidence? ProductionVisuals = null)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record WorldGenerationPromotionCommandResult(
    bool Succeeded,
    CommandResult Command,
    Guid TransactionId,
    Guid PromotionPlanId,
    WorldGenerationCanonicalPromotion? Promotion,
    bool Applied);

public sealed class PromoteWorldGenerationHandler
{
    public const string RevisionConflictCode = "world_generation.promotion.revision_conflict";
    public const string RecipeRevisionConflictCode = "world_generation.promotion.recipe_revision_conflict";
    public const string TransactionRequiredCode = "world_generation.promotion.transaction_required";
    public const string PlanRequiredCode = "world_generation.promotion.plan_required";
    public const string PermissionDeniedCode = "world_generation.promotion.permission_denied";
    public const string JobNotSucceededCode = "world_generation.promotion.job_not_succeeded";
    public const string EvidenceInvalidCode = "world_generation.promotion.evidence_invalid";
    public const string AcceptanceRequiredCode = "world_generation.promotion.acceptance_required";
    public const string PlanNotPreviewedCode = "world_generation.promotion.plan_not_previewed";
    private const string CommandName = "promote_world_generation";
    private const string DerivedRoot = "world/generation/artifacts/";
    private readonly IClock _clock;
    private readonly Dictionary<Guid, (ProjectRevision ProjectRevision,
        WorldGenerationRecipeRevision RecipeRevision, Guid TransactionId, string Fingerprint)> _previews = [];

    public PromoteWorldGenerationHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public WorldGenerationPromotionCommandResult Handle(
        ProjectState state,
        WorldGenerationRecipeState recipeState,
        WorldGenerationJob job,
        PromoteWorldGenerationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(recipeState);
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "Project changed before canonical promotion.");
        if (command.BaseRecipeRevision != recipeState.Revision)
            return Failure(state, command, RecipeRevisionConflictCode,
                "World-generation recipes changed before canonical promotion.");
        if (command.TransactionId == Guid.Empty)
            return Failure(state, command, TransactionRequiredCode,
                "Canonical promotion requires a transaction ID.");
        if (command.PromotionPlanId == Guid.Empty)
            return Failure(state, command, PlanRequiredCode,
                "Canonical promotion requires a preview plan ID.");
        if (command.Permission < AssistantPermissionLevel.ApplyApprovedPlan)
            return Failure(state, command, PermissionDeniedCode,
                "Canonical promotion requires ApplyApprovedPlan permission.");
        if (job.Status != WorldGenerationJobStatus.Succeeded)
            return Failure(state, command, JobNotSucceededCode,
                "Only a successfully completed world-generation job can be promoted.");
        if (command.AcceptanceProvenance?.StartsWith("human-forward-plus:", StringComparison.Ordinal) != true)
            return Failure(state, command, AcceptanceRequiredCode,
                "Canonical promotion requires explicit human Forward+ validation provenance using the 'human-forward-plus:' prefix.");

        var evidenceDiagnostics = ValidateEvidence(state, recipeState, job, command);
        if (evidenceDiagnostics.Count > 0)
            return Failure(state, command, EvidenceInvalidCode, string.Join(" | ", evidenceDiagnostics));

        var area = state.Project.Areas.Single(item => item.Id.Value == job.Recipe.AreaId);
        var canonicalPath = CanonicalScenePath(job.Recipe.AreaId);
        var loading = area.World.Loading;
        var requiredResources = loading.RequiredResources.Concat([canonicalPath])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var provenance = $"world-generation-promotion:{command.PromotionPlanId:D}";
        var surfaceEvidence = command.ValidationReport.TerrainSurface!;
        var environmentEvidence = command.ValidationReport.EnvironmentOccupancy!;
        var productionArt = WorldGenerationProductionArtDirection.Materialize(
            state.Project, area, job.Recipe, surfaceEvidence, provenance);
        if (!productionArt.Succeeded)
            return Failure(state, command, EvidenceInvalidCode, string.Join(" | ", productionArt.Diagnostics));
        var productionTerrain = productionArt.Terrain!;
        var placementSurfaceFingerprint = WorldGenerationSpatialEvidenceFingerprint.ComputeTerrain(
            productionTerrain.Width,
            productionTerrain.Depth,
            productionTerrain.SamplesX,
            productionTerrain.SamplesZ,
            productionTerrain.HeightScale,
            productionTerrain.Heights,
            surfaceEvidence.CellDiagonal);
        var placementSurface = new TerrainPlacementSurfaceDefinition(
            StableId(area.Id.Value, "placement-surface"),
            TerrainPlacementSurfaceDefinition.CurrentSchemaVersion,
            productionTerrain.Width,
            productionTerrain.Depth,
            productionTerrain.SamplesX,
            productionTerrain.SamplesZ,
            productionTerrain.HeightScale,
            productionTerrain.Heights,
            surfaceEvidence.CellDiagonal,
            placementSurfaceFingerprint,
            provenance);
        var environmentOccupancy = new AreaEnvironmentOccupancyProfile(
            StableId(area.Id.Value, "environment-occupancy"),
            AreaEnvironmentOccupancyProfile.CurrentSchemaVersion,
            environmentEvidence.Width,
            environmentEvidence.Depth,
            environmentEvidence.CellSizeMeters,
            environmentEvidence.Cells,
            environmentEvidence.Fingerprint,
            provenance);
        var promotedLoading = new AreaLoadingProfile(
            loading.Id,
            loading.SchemaVersion,
            loading.AsyncStreaming,
            loading.PrefetchDependencies,
            loading.CancellationEnabled,
            loading.CacheBudgetMegabytes,
            loading.MaximumConcurrentLoads,
            loading.ChunkRadius,
            loading.TargetLoadMilliseconds,
            requiredResources,
            provenance);
        var world = new AreaWorldProfile(
            area.World.Id,
            area.World.SchemaVersion,
            productionArt.Terrain!,
            productionArt.Biomes,
            productionArt.Features,
            area.World.VolumetricNavigation,
            area.World.FlightRoutes,
            promotedLoading,
            provenance,
            placementSurface,
            environmentOccupancy);
        var updatedArea = area.WithWorld(world).WithEnvironment(productionArt.Environment!);
        var project = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            state.Project.Areas.Select(item => item.Id == area.Id ? updatedArea : item).ToArray(),
            state.Project.Dialogues,
            state.Project.Quests);
        var newRevision = state.Revision.Next();
        var promotion = new WorldGenerationCanonicalPromotion(
            WorldGenerationCanonicalPromotion.CurrentSchemaVersion,
            command.PromotionPlanId,
            command.TransactionId,
            job.JobId,
            job.PlanId,
            job.Recipe.Id,
            job.Recipe.AreaId,
            state.Revision.Value,
            newRevision.Value,
            recipeState.Revision.Value,
            job.RecipeFingerprint,
            job.Plan.PlanFingerprint,
            job.Recipe.Seed,
            command.DerivedSceneRelativePath,
            canonicalPath,
            command.ValidationReport.DerivedSceneSha256,
            command.ValidationReport.ContentFingerprint,
            command.ValidationReport.AssetSelectionFingerprint,
            command.ValidationReport.Providers.OrderBy(item => item.Stage).ToArray(),
            job.Recipe.Regeneration.ProtectedNodePaths,
            command.AcceptanceProvenance.Trim(),
            surfaceEvidence,
            environmentEvidence,
            productionArt.Evidence);
        var patch = new ProjectPatch(
            "add",
            $"areas/{area.Id}/world/generatedEvidence",
            $"Promote validated generated world '{job.Recipe.Title}' with canonical scene, placement surface and environment occupancy.");
        var inverse = new ProjectPatch(
            "remove",
            $"areas/{area.Id}/world/generatedEvidence",
            $"Restore the previous canonical world reference and spatial evidence for area {area.Id}.");
        var fingerprint = Fingerprint(command, promotion);

        if (command.DryRun)
        {
            lock (_previews)
            {
                if (_previews.Count >= 128) _previews.Clear();
                _previews[command.PromotionPlanId] =
                    (command.BaseRevision, command.BaseRecipeRevision, command.TransactionId, fingerprint);
            }
            return Success(state, state, command, promotion, patch, inverse, applied: false);
        }

        lock (_previews)
        {
            if (!_previews.TryGetValue(command.PromotionPlanId, out var preview) ||
                preview.ProjectRevision != command.BaseRevision ||
                preview.RecipeRevision != command.BaseRecipeRevision ||
                preview.TransactionId != command.TransactionId ||
                !string.Equals(preview.Fingerprint, fingerprint, StringComparison.Ordinal))
                return Failure(state, command, PlanNotPreviewedCode,
                    "Canonical promotion must match its successful preview, transaction and current revisions.");
            _previews.Remove(command.PromotionPlanId);
        }

        return Success(state, new ProjectState(project, newRevision), command, promotion, patch, inverse,
            applied: true);
    }

    public static string CanonicalScenePath(Guid areaId)
    {
        if (areaId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(areaId));
        return $"world/areas/{areaId:N}/generated-world.tscn";
    }

    private WorldGenerationPromotionCommandResult Success(
        ProjectState before,
        ProjectState after,
        PromoteWorldGenerationCommand command,
        WorldGenerationCanonicalPromotion promotion,
        ProjectPatch patch,
        ProjectPatch inverse,
        bool applied)
    {
        var core = new CommandResult(
            true,
            after,
            command.BaseRevision,
            applied ? after.Revision : null,
            [],
            patch,
            inverse,
            new AuditEntry(command.CommandId, CommandName, _clock.UtcNow, applied));
        return new(true, core, command.TransactionId, command.PromotionPlanId, promotion, applied);
    }

    private WorldGenerationPromotionCommandResult Failure(
        ProjectState state,
        PromoteWorldGenerationCommand command,
        string code,
        string message)
    {
        var core = new CommandResult(
            false,
            state,
            command.BaseRevision,
            null,
            [new CommandDiagnostic(code, message)],
            null,
            null,
            new AuditEntry(command.CommandId, CommandName, _clock.UtcNow, false));
        return new(false, core, command.TransactionId, command.PromotionPlanId, null, false);
    }

    private static IReadOnlyList<string> ValidateEvidence(
        ProjectState state,
        WorldGenerationRecipeState recipeState,
        WorldGenerationJob job,
        PromoteWorldGenerationCommand command)
    {
        var report = command.ValidationReport;
        var diagnostics = new List<string>();
        if (WorldGenerationJobValidator.Validate(job).Count > 0)
            diagnostics.Add("The completed job payload is invalid.");
        var recipe = recipeState.Catalog.Find(job.Recipe.Id);
        if (recipe is null || !recipe.ApprovedForGeneration ||
            !string.Equals(recipe.Fingerprint(), job.RecipeFingerprint, StringComparison.Ordinal))
            diagnostics.Add("The promoted job no longer matches an approved recipe.");
        if (job.ProjectRevision != state.Revision.Value || job.RecipeRevision != recipeState.Revision)
            diagnostics.Add("The job revisions are stale.");
        if (command.JobPlanId != job.PlanId)
            diagnostics.Add("The command does not target the supplied job.");
        if (report.SchemaVersion != WorldGenerationValidationReport.CurrentSchemaVersion ||
            report.JobId != job.JobId || report.PlanId != job.PlanId ||
            report.RecipeId != job.Recipe.Id || report.AreaId != job.Recipe.AreaId ||
            report.ProjectRevision != job.ProjectRevision || report.RecipeRevision != job.RecipeRevision ||
            !string.Equals(report.RecipeFingerprint, job.RecipeFingerprint, StringComparison.Ordinal) ||
            !string.Equals(report.PlanFingerprint, job.Plan.PlanFingerprint, StringComparison.Ordinal))
            diagnostics.Add("The validation report identity, revisions or fingerprints do not match the job.");
        if (!report.ValidationSucceeded || !report.CollisionValidated ||
            !report.NavigationValidated || !report.ManualEditsPreserved ||
            report.Diagnostics is null || report.Diagnostics.Count > 0)
            diagnostics.Add("The validation report does not prove collision, navigation and manual-edit preservation.");
        diagnostics.AddRange(WorldGenerationSpatialEvidenceValidator.Validate(
            report.TerrainSurface, report.EnvironmentOccupancy));
        diagnostics.AddRange(WorldGenerationProductionArtDirection.ValidateSource(
            state.Project,
            state.Project.Areas.Single(item => item.Id.Value == job.Recipe.AreaId),
            job.Recipe));
        if (report.TerrainSurface is { } qualitySurface)
            diagnostics.AddRange(WorldGenerationProductionArtDirection.ValidateSpatialQuality(
                state.Project.Areas.Single(item => item.Id.Value == job.Recipe.AreaId),
                qualitySurface,
                job.Recipe.Terrain.AdaptToStructures));
        if (report.TerrainSurface is { } surface &&
            (surface.Width != (int)Math.Round(job.Recipe.Bounds.WidthMeters) ||
             surface.Depth != (int)Math.Round(job.Recipe.Bounds.DepthMeters)))
            diagnostics.Add("Terrain-placement surface bounds do not match the approved recipe.");
        if (command.ExpectedCanonicalSceneSha256 is null &&
            Enum.GetValues<WorldGenerationStage>().Except(job.Plan.Steps.Select(item => item.Stage)).Any())
            diagnostics.Add("Initial canonical promotion requires a complete world-generation execution plan.");
        if (!IsSha256(report.DerivedSceneSha256) || !IsSha256(report.ContentFingerprint) ||
            !IsSha256(report.AssetSelectionFingerprint))
            diagnostics.Add("Validation report fingerprints must be lowercase SHA-256 values.");
        if (report.Providers is null || report.Providers.Count != job.Plan.Steps.Count ||
            report.Providers.Select(item => item.Stage).Distinct().Count() != job.Plan.Steps.Count)
        {
            diagnostics.Add("Validation report must identify exactly one provider for each generated stage.");
        }
        else
        {
            foreach (var step in job.Plan.Steps)
            {
                var provider = report.Providers.SingleOrDefault(item => item.Stage == step.Stage);
                if (provider is null || provider.RequestedBackend != step.Backend ||
                    !IsToken(provider.ProviderKey, 80) || !IsToken(provider.ProviderVersion, 160) ||
                    !IsSha256(provider.EvidenceSha256))
                    diagnostics.Add($"Provider evidence for stage '{step.Stage}' is missing or invalid.");
            }
        }
        if (!IsSafeRelativePath(command.DerivedSceneRelativePath) ||
            !command.DerivedSceneRelativePath.StartsWith(DerivedRoot, StringComparison.Ordinal) ||
            !command.DerivedSceneRelativePath.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add("Derived scene path must be a confined world-generation artifact.");
        if (!string.Equals(command.CanonicalSceneRelativePath, CanonicalScenePath(job.Recipe.AreaId),
                StringComparison.Ordinal))
            diagnostics.Add("Canonical scene path does not match the stable area destination.");
        if (command.ExpectedCanonicalSceneSha256 is { } expected && !IsSha256(expected))
            diagnostics.Add("Expected canonical scene fingerprint is invalid.");
        if (!IsToken(command.AcceptanceProvenance, 256))
            diagnostics.Add("Validation acceptance provenance is invalid.");
        return diagnostics;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsToken(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength && value.IndexOf('\0') < 0;

    private static bool IsSafeRelativePath(string? value)
    {
        if (!IsToken(value, 512) || value!.StartsWith('/') || value.StartsWith('\\') || value.Contains(':'))
            return false;
        var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(segment => segment is not "." and not ".." &&
            segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'));
    }

    private static string Fingerprint(
        PromoteWorldGenerationCommand command,
        WorldGenerationCanonicalPromotion promotion)
    {
        var value = new StringBuilder();
        Append(value, command.TransactionId.ToString("D"));
        Append(value, command.PromotionPlanId.ToString("D"));
        Append(value, command.BaseRevision.Value.ToString(CultureInfo.InvariantCulture));
        Append(value, command.BaseRecipeRevision.Value.ToString(CultureInfo.InvariantCulture));
        Append(value, promotion.JobId.ToString("D"));
        Append(value, promotion.PlanFingerprint);
        Append(value, promotion.CanonicalSceneSha256);
        Append(value, promotion.ContentFingerprint);
        Append(value, promotion.AssetSelectionFingerprint);
        Append(value, promotion.DerivedSceneRelativePath);
        Append(value, promotion.CanonicalSceneRelativePath);
        Append(value, command.ExpectedCanonicalSceneSha256 ?? string.Empty);
        Append(value, promotion.AcceptanceProvenance);
        Append(value, promotion.TerrainSurface?.Fingerprint ?? string.Empty);
        Append(value, promotion.EnvironmentOccupancy?.Fingerprint ?? string.Empty);
        Append(value, promotion.ProductionVisuals?.Fingerprint ?? string.Empty);
        foreach (var provider in promotion.Providers)
        {
            Append(value, provider.Stage.ToString());
            Append(value, provider.RequestedBackend.ToString());
            Append(value, provider.ProviderKey);
            Append(value, provider.ProviderVersion);
            Append(value, provider.EvidenceSha256);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder target, string value) =>
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');

    private static Guid StableId(Guid areaId, string discriminator)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{areaId:D}:{discriminator}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}
