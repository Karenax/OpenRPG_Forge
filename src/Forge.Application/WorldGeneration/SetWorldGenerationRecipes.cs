using System.Security.Cryptography;
using System.Text;
using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Assistant;
using Forge.Domain.Projects;
using Forge.Domain.WorldGeneration;

namespace Forge.Application.WorldGeneration;

public sealed record SetWorldGenerationRecipesCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid PlanId,
    WorldGenerationRecipeRevision BaseRevision,
    WorldGenerationRecipeCatalog Catalog,
    AssistantPermissionLevel Permission,
    bool DryRun);

public sealed record WorldGenerationRecipeCommandResult(
    bool Succeeded,
    WorldGenerationRecipeState State,
    WorldGenerationRecipeRevision BaseRevision,
    WorldGenerationRecipeRevision? NewRevision,
    Guid TransactionId,
    Guid PlanId,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);

public sealed class SetWorldGenerationRecipesHandler
{
    public const string RevisionConflictCode = "world_generation.revision_conflict";
    public const string TransactionRequiredCode = "world_generation.transaction_required";
    public const string PlanRequiredCode = "world_generation.plan_required";
    public const string PlanNotPreviewedCode = "world_generation.plan_not_previewed";
    public const string PermissionDeniedCode = "world_generation.permission_denied";
    private const string CommandName = "set_world_generation_recipes";
    private readonly IClock _clock;
    private readonly Dictionary<Guid, (WorldGenerationRecipeRevision Revision, Guid TransactionId, string Fingerprint)> _previewedPlans = [];

    public SetWorldGenerationRecipesHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public WorldGenerationRecipeCommandResult Handle(
        WorldGenerationRecipeState state,
        SetWorldGenerationRecipesCommand command,
        ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(project);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "World-generation recipes changed before this command was applied.");
        if (command.TransactionId == Guid.Empty)
            return Failure(state, command, TransactionRequiredCode, "World-generation configuration requires a transaction ID.");
        if (command.PlanId == Guid.Empty)
            return Failure(state, command, PlanRequiredCode, "World-generation configuration requires a preview plan ID.");
        var requiredPermission = command.Catalog.Recipes.Any(item => item.ApprovedForGeneration)
            ? AssistantPermissionLevel.ApplyApprovedPlan
            : AssistantPermissionLevel.Configure;
        if (command.Permission < requiredPermission)
            return Failure(state, command, PermissionDeniedCode, $"World-generation configuration requires {requiredPermission} permission.");

        var diagnostics = Validate(command.Catalog, project)
            .Select(item => new CommandDiagnostic(item.Code, item.Message)).ToArray();
        if (diagnostics.Length > 0)
            return new(false, state, command.BaseRevision, null, command.TransactionId, command.PlanId,
                diagnostics, null, null, Audit(command, false));

        var patch = new ProjectPatch("replace", "world-generation/recipes",
            $"Set {command.Catalog.Recipes.Count} governed world-generation recipe or recipes.");
        var inverse = new ProjectPatch("replace", "world-generation/recipes",
            $"Restore {state.Catalog.Recipes.Count} governed world-generation recipe or recipes.");
        var fingerprint = Fingerprint(command.Catalog);
        if (command.DryRun)
        {
            lock (_previewedPlans)
            {
                if (_previewedPlans.Count >= 128) _previewedPlans.Clear();
                _previewedPlans[command.PlanId] = (command.BaseRevision, command.TransactionId, fingerprint);
            }
            return new(true, state, command.BaseRevision, null, command.TransactionId, command.PlanId,
                [], patch, inverse, Audit(command, false));
        }

        lock (_previewedPlans)
        {
            if (!_previewedPlans.TryGetValue(command.PlanId, out var preview) ||
                preview.Revision != command.BaseRevision || preview.TransactionId != command.TransactionId ||
                !string.Equals(preview.Fingerprint, fingerprint, StringComparison.Ordinal))
                return Failure(state, command, PlanNotPreviewedCode,
                    "World-generation configuration must match its successful dry-run transaction and current revision.");
            _previewedPlans.Remove(command.PlanId);
        }
        var revision = state.Revision.Next();
        return new(true, new WorldGenerationRecipeState(command.Catalog, revision), command.BaseRevision, revision,
            command.TransactionId, command.PlanId, [], patch, inverse, Audit(command, true));
    }

    private WorldGenerationRecipeCommandResult Failure(
        WorldGenerationRecipeState state,
        SetWorldGenerationRecipesCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, command.TransactionId, command.PlanId,
            [new CommandDiagnostic(code, message)], null, null, Audit(command, false));

    private AuditEntry Audit(SetWorldGenerationRecipesCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);

    private static IReadOnlyList<WorldGenerationValidationIssue> Validate(
        WorldGenerationRecipeCatalog catalog,
        ProjectDefinition project)
    {
        var diagnostics = new List<WorldGenerationValidationIssue>();
        if (catalog.Id == Guid.Empty || catalog.SchemaVersion != WorldGenerationRecipeCatalog.CurrentSchemaVersion)
            diagnostics.Add(new("world_generation.catalog_invalid", "World-generation catalog ID or schema is invalid.", "world-generation/catalog"));
        if (catalog.Recipes.Select(item => item.Id).Distinct().Count() != catalog.Recipes.Count)
            diagnostics.Add(new("world_generation.recipe_duplicate", "World-generation recipe IDs must be unique.", "world-generation/recipes"));
        diagnostics.AddRange(catalog.Recipes.SelectMany(WorldGenerationRecipeValidator.Validate));
        foreach (var recipe in catalog.Recipes.Where(recipe => project.Areas.All(area => area.Id.Value != recipe.AreaId)))
            diagnostics.Add(new("world_generation.area_missing", $"Recipe '{recipe.Title}' targets an area that does not exist.", $"world-generation/recipes/{recipe.Id}/areaId"));
        return diagnostics;
    }

    private static string Fingerprint(WorldGenerationRecipeCatalog catalog)
    {
        var value = new StringBuilder().Append(catalog.Id).Append('|').Append(catalog.SchemaVersion).Append('|')
            .Append(catalog.Provenance);
        foreach (var recipe in catalog.Recipes)
            value.Append('|').Append(recipe.Fingerprint());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString()))).ToLowerInvariant();
    }
}

public sealed class WorldGenerationRecipeCommandHistory
{
    private readonly Stack<(WorldGenerationRecipeCatalog Before, WorldGenerationRecipeCatalog After)> _undo = [];
    private readonly Stack<(WorldGenerationRecipeCatalog Before, WorldGenerationRecipeCatalog After)> _redo = [];

    public WorldGenerationRecipeState State { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public WorldGenerationRecipeCommandHistory(
        WorldGenerationRecipeCatalog? catalog = null,
        WorldGenerationRecipeRevision? revision = null)
    {
        State = new WorldGenerationRecipeState(catalog, revision);
    }

    public void Record(WorldGenerationRecipeCommandResult result)
    {
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
            throw new InvalidOperationException("Only an applied world-generation command at the current revision can be recorded.");
        _undo.Push((State.Catalog, result.State.Catalog));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry)) return false;
        _redo.Push(entry);
        State = new WorldGenerationRecipeState(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry)) return false;
        _undo.Push(entry);
        State = new WorldGenerationRecipeState(entry.After, State.Revision.Next());
        return true;
    }
}
