using Forge.Application.Commands;
using Forge.Application.Assets;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Validation;
using Forge.Mcp.Dialogues;
using Forge.Mcp.Assets;
using Forge.Mcp.Scripts;
using Forge.ModuleFormat.Workspaces;
using Forge.ModuleFormat.Assets;

namespace Forge.Mcp.Projects;

public sealed class ForgeProjectAgentService
{
    private static readonly HashSet<string> AllowedScriptBindings =
        new(StringComparer.Ordinal)
        {
            "scriptPath",
            "phaseTwoScript",
            "deathScript",
        };

    private readonly object _gate = new();
    private readonly ProjectSession _session;
    private readonly ProjectCommandHistory _history;
    private readonly SetDialogueHandler _dialogueHandler;
    private readonly SetEntityOverridesHandler _overridesHandler;
    private readonly Dictionary<string, StoredDialoguePlan> _dialoguePlans = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    private ForgeProjectAgentService(ProjectSession session, IClock clock)
    {
        _session = session;
        _clock = clock;
        _history = new ProjectCommandHistory(new ProjectState(session.Project, new ProjectRevision(0)));
        _dialogueHandler = new SetDialogueHandler(clock);
        _overridesHandler = new SetEntityOverridesHandler(clock);
    }

    public string WorkspaceDirectory => _session.RootDirectory;

    public static ForgeProjectAgentOpenResult Open(string workspaceDirectory, IClock? clock = null)
    {
        var opened = ProjectSession.Open(workspaceDirectory);
        return opened.Session is null
            ? new ForgeProjectAgentOpenResult(
                null,
                opened.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray())
            : new ForgeProjectAgentOpenResult(
                new ForgeProjectAgentService(opened.Session, clock ?? new SystemClock()),
                []);
    }

    public ForgeProjectSummary GetSummary()
    {
        lock (_gate)
        {
            var project = _history.State.Project;
            return new ForgeProjectSummary(
                project.Id.ToString(),
                project.Title,
                project.Module.Id.ToString(),
                project.Module.Slug,
                project.Module.DisplayName,
                project.Module.EntryAreaId.ToString(),
                _history.State.Revision.Value,
                project.Blueprints.Count,
                project.Areas.Count,
                project.Areas.Sum(area => area.Entities.Count),
                project.Dialogues.Count,
                project.Quests.Count);
        }
    }

    public long GetCurrentRevision()
    {
        lock (_gate)
        {
            return _history.State.Revision.Value;
        }
    }

    public IReadOnlyList<AgentDiagnostic> GetDiagnostics()
    {
        lock (_gate)
        {
            return ProjectDiagnosticCatalog.Build(_history.State.Project)
                .Select(Map)
                .ToArray();
        }
    }

    public AgentAssetCatalogResult GetAssets(AgentAssetCatalogQuery? query = null)
    {
        query ??= new AgentAssetCatalogQuery();
        lock (_gate)
        {
            var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(_session.RootDirectory) ??
                AssetCatalogLoader.FindRepositoryRoot(AppContext.BaseDirectory);
            if (repositoryRoot is null)
            {
                return new AgentAssetCatalogResult(
                    1,
                    0,
                    0,
                    [],
                    [],
                    [],
                    [new AgentDiagnostic(
                        AssetCatalogLoader.CatalogMissingCode,
                        "The OpenRPG Forge Asset Vault catalogs could not be located.",
                        "asset_manifests/catalogs")]);
            }

            var load = AssetCatalogLoader.Load(repositoryRoot);
            if (load.Catalog is null)
            {
                return new AgentAssetCatalogResult(
                    1,
                    0,
                    0,
                    [],
                    [],
                    [],
                    load.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }

            var browser = new AssetCatalogBrowser(load.Catalog);
            var items = browser.Search(new AssetCatalogQuery(
                query.Text,
                query.Source,
                query.Category,
                query.LicenseSpdx,
                ParseAssetAvailability(query.Availability),
                query.MaximumResults));
            return new AgentAssetCatalogResult(
                load.Catalog.SchemaVersion,
                load.Catalog.Items.Count,
                load.Catalog.Items.Count(item => item.Availability != AssetAvailability.CatalogOnly),
                browser.Sources,
                browser.Categories,
                items.Select(item => new AgentAssetCatalogItem(
                    item.Id.ToString(),
                    item.SchemaVersion,
                    item.ExternalId,
                    item.DisplayName,
                    item.Source,
                    item.SourceAssetId,
                    item.SourcePage,
                    item.LicenseSpdx,
                    item.LicenseVerified,
                    item.ContentSha256,
                    item.TotalBytes,
                    item.Categories,
                    item.Tags,
                    item.CatalogStatus,
                    item.Availability.ToString(),
                    item.RelativeModelPath,
                    item.RelativePreviewPath,
                    item.ThumbnailUrl,
                    item.CanPlanImport,
                    item.Provenance)).ToArray(),
                load.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
    }

    public DialoguePlanResult PlanDialogue(DialoguePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != DialoguePlanSchema.CurrentVersion)
            {
                return DialogueFailure(
                    request.BaseRevision,
                    "dialogue_plan.schema_unsupported",
                    $"Dialogue plan schema {request.SchemaVersion} is unsupported; expected {DialoguePlanSchema.CurrentVersion}.");
            }

            DialogueDefinition dialogue;
            try
            {
                dialogue = MapDialogue(request);
            }
            catch (Exception exception) when (
                exception is ArgumentException or FormatException or OverflowException)
            {
                return DialogueFailure(request.BaseRevision, "dialogue_plan.invalid", exception.Message);
            }

            var command = new SetDialogueCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(request.BaseRevision),
                dialogue,
                DryRun: true);
            var preview = _dialogueHandler.Handle(_history.State, command);
            if (!preview.Succeeded)
            {
                return new DialoguePlanResult(
                    false,
                    null,
                    request.BaseRevision,
                    preview.Patch,
                    preview.Inverse,
                    preview.Diagnostics.Select(Map).ToArray(),
                    false,
                    false,
                    []);
            }

            var planId = Guid.NewGuid().ToString("D");
            var audit = new List<PlanAuditEntry>
            {
                new(planId, "planned", request.BaseRevision, _clock.UtcNow, true),
            };
            _dialoguePlans.Add(
                planId,
                new StoredDialoguePlan(planId, dialogue, request.BaseRevision, preview.Patch!, preview.Inverse!, audit));
            return ToResult(_dialoguePlans[planId]);
        }
    }

    public DialoguePlanResult ApplyDialoguePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_dialoguePlans.TryGetValue(planId, out var plan))
            {
                return DialogueFailure(GetCurrentRevision(), "dialogue_plan.missing", $"Dialogue plan '{planId}' does not exist.");
            }

            if (plan.Applied)
            {
                return DialogueFailure(plan.BaseRevision, "dialogue_plan.already_applied", $"Dialogue plan '{planId}' is already applied.");
            }

            var command = new SetDialogueCommand(
                CommandId.From(Guid.NewGuid()),
                new ProjectRevision(plan.BaseRevision),
                plan.Dialogue,
                DryRun: false);
            var result = _dialogueHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                plan.Audit.Add(new PlanAuditEntry(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(plan, result.Diagnostics.Select(Map).ToArray());
            }

            var saved = _session.Save(result.State.Project);
            if (!saved.Succeeded)
            {
                plan.Audit.Add(new PlanAuditEntry(planId, "apply", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(
                    plan,
                    saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }

            _history.Record(result);
            plan.Applied = true;
            plan.AppliedRevision = result.NewRevision!.Value.Value;
            plan.Audit.Add(new PlanAuditEntry(planId, "apply", plan.AppliedRevision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public DialoguePlanResult RevertDialoguePlan(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        lock (_gate)
        {
            if (!_dialoguePlans.TryGetValue(planId, out var plan))
            {
                return DialogueFailure(GetCurrentRevision(), "dialogue_plan.missing", $"Dialogue plan '{planId}' does not exist.");
            }

            if (!plan.Applied || plan.Reverted)
            {
                return DialogueFailure(plan.BaseRevision, "dialogue_plan.not_applied", $"Dialogue plan '{planId}' is not currently applied.");
            }

            if (plan.AppliedRevision != _history.State.Revision.Value)
            {
                return DialogueFailure(
                    plan.BaseRevision,
                    "dialogue_plan.revision_conflict",
                    "Only the most recently applied plan can be reverted without overwriting later work.");
            }

            var undone = _history.Undo();
            if (!undone.Succeeded)
            {
                return DialogueFailure(plan.BaseRevision, "dialogue_plan.inverse_unavailable", "The inverse patch is unavailable.");
            }

            var saved = _session.Save(undone.State.Project);
            if (!saved.Succeeded)
            {
                _history.Redo();
                plan.Audit.Add(new PlanAuditEntry(planId, "revert", _history.State.Revision.Value, _clock.UtcNow, false));
                return WithFailure(
                    plan,
                    saved.Diagnostics.Select(item => new AgentDiagnostic(item.Code, item.Message, item.Path)).ToArray());
            }

            plan.Reverted = true;
            plan.Audit.Add(new PlanAuditEntry(planId, "revert", undone.State.Revision.Value, _clock.UtcNow, true));
            return ToResult(plan);
        }
    }

    public ScriptBindingPlanResult PlanScriptBinding(ScriptBindingPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1)
            {
                return BindingFailure(request.BaseRevision, "script_binding.schema_unsupported", "Script binding schema must be 1.");
            }

            if (!AllowedScriptBindings.Contains(request.Binding))
            {
                return BindingFailure(
                    request.BaseRevision,
                    "script_binding.binding_invalid",
                    "Binding must be scriptPath, phaseTwoScript or deathScript.");
            }

            if (!IsSafeForgeScriptPath(request.RelativeScriptPath))
            {
                return BindingFailure(
                    request.BaseRevision,
                    "script_binding.path_unsafe",
                    "The script path must be a relative .forge path without traversal segments.");
            }

            try
            {
                var areaId = AreaId.From(Guid.Parse(request.AreaId));
                var entityId = EntityId.From(Guid.Parse(request.EntityId));
                var entity = _history.State.Project.Areas
                    .SingleOrDefault(area => area.Id == areaId)?
                    .Entities.SingleOrDefault(candidate => candidate.Id == entityId);
                var existing = entity?.Overrides.Properties ?? [];
                var properties = existing
                    .Where(item => !string.Equals(item.Key, request.Binding, StringComparison.Ordinal))
                    .Append(new EntityPropertyOverride(
                        request.Binding,
                        EntityPropertyValueKind.String,
                        request.RelativeScriptPath.Replace('\\', '/')))
                    .ToArray();
                var command = new SetEntityOverridesCommand(
                    CommandId.From(Guid.NewGuid()),
                    new ProjectRevision(request.BaseRevision),
                    areaId,
                    entityId,
                    new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, properties),
                    DryRun: true);
                var result = _overridesHandler.Handle(_history.State, command);
                return new ScriptBindingPlanResult(
                    result.Succeeded,
                    result.Succeeded ? Guid.NewGuid().ToString("D") : null,
                    request.BaseRevision,
                    result.Patch,
                    result.Inverse,
                    result.Diagnostics.Select(Map).ToArray());
            }
            catch (Exception exception) when (
                exception is ArgumentException or FormatException or InvalidOperationException)
            {
                return BindingFailure(request.BaseRevision, "script_binding.invalid", exception.Message);
            }
        }
    }

    private static DialogueDefinition MapDialogue(DialoguePlanRequest request) => new(
        DialogueId.From(Guid.Parse(request.DialogueId)),
        request.Slug,
        request.DisplayName,
        DialogueNodeId.From(Guid.Parse(request.EntryNodeId)),
        request.Nodes.Select(MapNode).ToArray());

    private static DialogueNode MapNode(DialogueNodeInput node) => new(
        DialogueNodeId.From(Guid.Parse(node.NodeId)),
        string.IsNullOrWhiteSpace(node.SpeakerBlueprintId)
            ? null
            : BlueprintId.From(Guid.Parse(node.SpeakerBlueprintId)),
        new LocalizedText(node.LocalizationKey, node.FallbackText),
        (node.OnEnterActions ?? []).Select(MapAction).ToArray(),
        (node.Choices ?? []).Select(MapChoice).ToArray());

    private static DialogueChoice MapChoice(DialogueChoiceInput choice) => new(
        DialogueChoiceId.From(Guid.Parse(choice.ChoiceId)),
        new LocalizedText(choice.LocalizationKey, choice.FallbackText),
        (choice.Conditions ?? []).Select(MapCondition).ToArray(),
        (choice.Actions ?? []).Select(MapAction).ToArray(),
        string.IsNullOrWhiteSpace(choice.TargetNodeId)
            ? null
            : DialogueNodeId.From(Guid.Parse(choice.TargetNodeId)));

    private static DialogueCondition MapCondition(DialogueConditionInput condition) => new(
        Enum.Parse<DialogueConditionKind>(condition.Kind, ignoreCase: true),
        condition.Key,
        condition.ExpectedValue);

    private static DialogueAction MapAction(DialogueActionInput action) => new(
        Enum.Parse<DialogueActionKind>(action.Kind, ignoreCase: true),
        action.Key,
        action.Value);

    private static bool IsSafeForgeScriptPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            !string.Equals(Path.GetExtension(path), ".forge", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");
    }

    private static AgentDiagnostic Map(CommandDiagnostic diagnostic) => new(diagnostic.Code, diagnostic.Message);

    private static AssetAvailability? ParseAssetAvailability(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<AssetAvailability>(value, ignoreCase: true, out var availability) &&
            Enum.IsDefined(availability)
                ? availability
                : throw new ArgumentException(
                    $"Unknown asset availability '{value}'. Expected CatalogOnly, OriginalAvailable or ExtractedAvailable.",
                    nameof(value));
    }

    private static AgentDiagnostic Map(ActionableDiagnostic diagnostic) => new(
        diagnostic.Code,
        diagnostic.Message,
        diagnostic.DocumentPath,
        diagnostic.Id.ToString(),
        diagnostic.SchemaVersion,
        diagnostic.Severity.ToString(),
        diagnostic.SubjectUuid?.ToString("D"),
        diagnostic.Property,
        diagnostic.Source,
        diagnostic.QuickFixes.Select(fix => new AgentQuickFix(
            fix.Id.ToString(),
            fix.SchemaVersion,
            fix.Label,
            fix.Description,
            fix.Kind.ToString(),
            fix.AreaId.ToString(),
            fix.EntityId?.ToString(),
            fix.Provenance)).ToArray(),
        diagnostic.Provenance);

    private static DialoguePlanResult DialogueFailure(long revision, string code, string message) => new(
        false,
        null,
        revision,
        null,
        null,
        [new AgentDiagnostic(code, message)],
        false,
        false,
        []);

    private static DialoguePlanResult WithFailure(
        StoredDialoguePlan plan,
        IReadOnlyList<AgentDiagnostic> diagnostics) => new(
        false,
        plan.PlanId,
        plan.BaseRevision,
        plan.Diff,
        plan.Inverse,
        diagnostics,
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static DialoguePlanResult ToResult(StoredDialoguePlan plan) => new(
        true,
        plan.PlanId,
        plan.BaseRevision,
        plan.Diff,
        plan.Inverse,
        [],
        plan.Applied,
        plan.Reverted,
        plan.Audit.ToArray());

    private static ScriptBindingPlanResult BindingFailure(long revision, string code, string message) => new(
        false,
        null,
        revision,
        null,
        null,
        [new AgentDiagnostic(code, message)]);

    private sealed class StoredDialoguePlan(
        string planId,
        DialogueDefinition dialogue,
        long baseRevision,
        ProjectPatch diff,
        ProjectPatch inverse,
        List<PlanAuditEntry> audit)
    {
        public string PlanId { get; } = planId;

        public DialogueDefinition Dialogue { get; } = dialogue;

        public long BaseRevision { get; } = baseRevision;

        public ProjectPatch Diff { get; } = diff;

        public ProjectPatch Inverse { get; } = inverse;

        public List<PlanAuditEntry> Audit { get; } = audit;

        public bool Applied { get; set; }

        public bool Reverted { get; set; }

        public long? AppliedRevision { get; set; }
    }
}

public sealed record ForgeProjectAgentOpenResult(
    ForgeProjectAgentService? Service,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
