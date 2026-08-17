using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Areas;
using Forge.Domain.Content;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.Validation;
using Forge.RpgKit.Domain.Doors;

namespace Forge.ModuleFormat.Quality;

public sealed record CanonicalValidationDiagnostic(
    Guid Id,
    int SchemaVersion,
    string Category,
    string Code,
    string Message,
    string Path,
    Guid? SubjectUuid,
    string Provenance);

public sealed record WorkspaceCanonicalValidationReport(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    Guid ProfileId,
    bool Passed,
    string Fingerprint,
    IReadOnlyDictionary<string, int> CategoryCounts,
    IReadOnlyList<CanonicalValidationDiagnostic> Diagnostics)
{
    public const int CurrentSchemaVersion = 1;
}

public static class WorkspaceCanonicalValidationService
{
    public const string SchemaDocumentInvalidCode = "qa.schema.document_invalid";
    public const string RuntimePolicyMissingCode = "qa.validation.runtime_policy_missing";
    public const string AreaLifecycleScriptMissingCode = "area.foundation.lifecycle_script_missing";
    public const string PortalHookScriptMissingCode = "area.portal.hook_script_missing";
    public const string AreaUserEventScriptMissingCode = "area.user_event.script_missing";

    public static WorkspaceCanonicalValidationReport Run(
        string workspaceRoot,
        ProjectDefinition project,
        QualityFoundationProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(profile);
        var root = Path.GetFullPath(workspaceRoot);
        var policy = profile.EffectiveValidation;
        var diagnostics = new List<(string Category, DomainDiagnostic Diagnostic)>();
        diagnostics.AddRange(QualityFoundationProfileValidator.Validate(profile)
            .Select(item => ("schemas", item)));
        diagnostics.AddRange(project.Module.WorldFeatures.Doors
            .Where(item => (item.Validation is not null || item.ValidationRequiredAtRuntime) &&
                           (policy.References.Enabled || item.ValidationRequiredAtRuntime))
            .SelectMany(item => DoorWorldValidationService.Validate(project, item.Id).Diagnostics)
            .Select(item => ("references", new DomainDiagnostic(item.Code, item.Message, item.Path))));

        if (policy.Schemas.Enabled)
        {
            diagnostics.AddRange(ValidateDocuments(root).Select(item => ("schemas", item)));
            diagnostics.AddRange(project.Areas.SelectMany(area => ValidateAreaSchemas(root, project, area))
                .Select(item => ("schemas", item)));
        }
        if (policy.References.Enabled)
        {
            diagnostics.AddRange(ProjectValidator.Validate(project).Select(item => ("references", item)));
            diagnostics.AddRange(ContentPackageResolver.Resolve(project.Module.Content.Packages).Diagnostics
                .Select(item => ("references", new DomainDiagnostic(item.Code, item.Message, item.Path))));
            diagnostics.AddRange(ValidateTypedReferences(project).Select(item => ("references", item)));
        }
        if (policy.Graphs.Enabled) diagnostics.AddRange(ValidateGraphs(project).Select(item => ("graphs", item)));
        if (policy.Geometry.Enabled)
            diagnostics.AddRange(project.Areas.SelectMany(area => AreaGeometryValidator.Validate(project, area))
                .Select(item => ("geometry", item)));
        if (policy.Navigation.Enabled)
        {
            foreach (var area in project.Areas)
                foreach (var state in Enum.GetValues<Forge.Domain.Navigation.NavigationDoorState>())
                {
                    try
                    {
                        diagnostics.AddRange(AreaNavigationValidator.AnalyzeAll(project, area, state)
                            .SelectMany(item => item.Diagnostics).Select(item => ("navigation", item)));
                    }
                    catch (InvalidOperationException exception)
                    {
                        diagnostics.Add(("navigation", new DomainDiagnostic("area.navigation.bake_too_dense",
                            exception.Message, $"areas/{area.Id}/navigation/cellSize")));
                    }
                }
        }

        var structured = diagnostics
            .DistinctBy(item => (item.Category, item.Diagnostic.Code, item.Diagnostic.Path))
            .Select(item => Map(item.Category, item.Diagnostic, project.Id.Value))
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();
        var categoryCounts = policy.Categories.Keys.ToDictionary(
            category => category,
            category => structured.Count(item => item.Category == category),
            StringComparer.Ordinal);
        var passed = structured.Length == 0;
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = policy.Id,
            categories = categoryCounts,
            diagnostics = structured.Select(item => new { item.Category, item.Code, item.Path }),
        })));
        return new(Stable($"report:{project.Id}:{fingerprint}"), WorkspaceCanonicalValidationReport.CurrentSchemaVersion,
            "openrpg-forge:qa-r027", policy.Id, passed, fingerprint, categoryCounts, structured);
    }

    public static IReadOnlyList<CanonicalValidationDiagnostic> BlockingDiagnostics(
        ProjectDefinition project,
        QualityFoundationProfile profile,
        WorkspaceCanonicalValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(report);
        var policy = profile.EffectiveValidation;
        var requiredDoorDiagnostics = project.Module.WorldFeatures.Doors
            .Where(item => item.ValidationRequiredAtRuntime)
            .SelectMany(item => DoorWorldValidationService.Validate(project, item.Id).Diagnostics)
            .Select(item => (item.Code, item.Path))
            .ToHashSet();
        return report.Diagnostics.Where(item =>
                (policy.Categories.TryGetValue(item.Category, out var category) &&
                 category.RequiredBeforeBuild) ||
                requiredDoorDiagnostics.Contains((item.Code, item.Path)))
            .ToArray();
    }

    public static IReadOnlyList<DomainDiagnostic> ValidateRuntimePolicy(
        QualityFoundationProfile? profile, bool required)
    {
        if (profile is null)
            return required
                ? [new(RuntimePolicyMissingCode, "Required canonical validation policy is absent.", "quality/validation")]
                : [];
        return QualityFoundationProfileValidator.Validate(profile);
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateDocuments(string root)
    {
        var paths = new List<string>();
        foreach (var relative in new[] { "forge.project.json", "project.json", "build.profile.json",
                     QualityFoundationProfileStore.RelativePath })
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) paths.Add(path);
        }
        var areaRoot = Path.Combine(root, "areas");
        if (Directory.Exists(areaRoot)) paths.AddRange(Directory.EnumerateFiles(areaRoot, "*.json"));
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var path in paths.Order(StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                var rootElement = document.RootElement;
                var versioned = rootElement.ValueKind == JsonValueKind.Object &&
                    (rootElement.TryGetProperty("schemaVersion", out var schema) && schema.ValueKind == JsonValueKind.Number ||
                     rootElement.TryGetProperty("formatVersion", out var format) && format.ValueKind == JsonValueKind.String);
                if (!versioned)
                    diagnostics.Add(new(SchemaDocumentInvalidCode,
                        "Canonical JSON document does not declare schemaVersion or formatVersion.", relative));
            }
            catch (JsonException exception)
            {
                diagnostics.Add(new(SchemaDocumentInvalidCode, exception.Message, relative));
            }
        }
        return diagnostics;
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateAreaSchemas(
        string root,
        ProjectDefinition project,
        AreaDefinition area)
    {
        var diagnostics = AreaFoundationValidator.Validate(area, area.Foundation).ToList();
        diagnostics.AddRange(AreaPlaytestProfileValidator.Validate(project, area, area.Playtest));
        diagnostics.AddRange(AreaUserEventValidator.ValidateExecutable(area));
        foreach (var binding in area.Authoring.UserEvents.Where(item => item.Required))
        {
            var relative = binding.ScriptPath.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(Path.Combine(root, relative)))
                diagnostics.Add(new(AreaUserEventScriptMissingCode,
                    $"Required area user-event script '{binding.ScriptPath}' does not exist.",
                    $"areas/{area.Id}/authoring/userEvents/{binding.Id:D}/scriptPath"));
        }
        foreach (var binding in new[] { area.Foundation.Lifecycle.Entered, area.Foundation.Lifecycle.Exited })
        {
            if (binding is null || !binding.Required || string.IsNullOrWhiteSpace(binding.ScriptPath)) continue;
            var relative = binding.ScriptPath.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(Path.Combine(root, relative)))
            {
                diagnostics.Add(new(AreaLifecycleScriptMissingCode,
                    $"Required lifecycle script '{binding.ScriptPath}' does not exist.",
                    $"areas/{area.Id}/foundation/lifecycle/{binding.EventName}"));
            }
        }
        foreach (var portal in area.Portals.Where(item => item.HooksRequiredAtRuntime))
        {
            foreach (var script in new[] { portal.BeforeTransitionScript, portal.AfterTransitionScript }
                         .Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                var relative = script.Replace('/', Path.DirectorySeparatorChar);
                if (!File.Exists(Path.Combine(root, relative)))
                    diagnostics.Add(new(PortalHookScriptMissingCode,
                        $"Required portal hook '{script}' does not exist.",
                        $"areas/{area.Id}/portals/{portal.Id}/hooks"));
            }
        }
        return diagnostics;
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateTypedReferences(ProjectDefinition project)
    {
        var resources = project.Blueprints.Select(item => new ValidationResource(item.Id.Value, "blueprint",
                "project", project.Module.Metadata.Version, true, $"blueprints/{item.Id}"))
            .Concat(project.Areas.Select(item => new ValidationResource(item.Id.Value, "area", "project",
                project.Module.Metadata.Version, true, $"areas/{item.Id}"))).ToArray();
        var references = project.Areas.SelectMany(area => area.Entities.Select(entity => new ValidationReference(
                area.Id.Value, entity.BlueprintId.Value, "blueprint", "project", project.Module.Metadata.Version,
                $"areas/{area.Id}/entities/{entity.Id}/blueprintId")))
            .Concat(project.Areas.SelectMany(area => area.Portals.Select(portal => new ValidationReference(
                area.Id.Value, portal.DestinationAreaId.Value, "area", "project", project.Module.Metadata.Version,
                $"areas/{area.Id}/portals/{portal.Id}/destinationAreaId"))))
            .ToArray();
        return TypedReferenceResolver.Validate(resources, references);
    }

    private static IReadOnlyList<DomainDiagnostic> ValidateGraphs(ProjectDefinition project)
    {
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var dialogue in project.Dialogues)
        {
            diagnostics.AddRange(GenericGraphValidator.Validate(new ValidationGraph(
                dialogue.Id.Value, dialogue.SchemaVersion, dialogue.Provenance,
                dialogue.Nodes.Select(node => new ValidationGraphNode(node.Id.Value, "dialogue-node",
                    $"dialogues/{dialogue.Id}/nodes/{node.Id}")).ToArray(),
                dialogue.Edges.Select(edge => new ValidationGraphEdge(edge.Id.Value, edge.SourceNodeId.Value,
                    edge.TargetNodeId.Value, edge.Kind.ToString(), $"dialogues/{dialogue.Id}/edges/{edge.Id}"))
                    .ToArray(),
                dialogue.EntryPoints.Select(entry => entry.NodeId.Value).Distinct().ToArray(), true)));
        }
        foreach (var quest in project.Quests)
        {
            diagnostics.AddRange(GenericGraphValidator.Validate(new ValidationGraph(
                quest.Id.Value, quest.SchemaVersion, quest.Provenance,
                quest.States.Select(state => new ValidationGraphNode(state.Id.Value, "quest-state",
                    $"quests/{quest.Id}/states/{state.Id}")).ToArray(),
                quest.Transitions.Select(transition => new ValidationGraphEdge(transition.Id.Value,
                    transition.FromStateId.Value, transition.ToStateId.Value, transition.EventBinding.Kind.ToString(),
                    $"quests/{quest.Id}/transitions/{transition.Id}")).ToArray(),
                [quest.InitialStateId.Value], true)));
        }
        return diagnostics;
    }

    private static CanonicalValidationDiagnostic Map(string category, DomainDiagnostic diagnostic, Guid projectId)
    {
        var subject = diagnostic.Path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Guid.TryParse(part, out var id) ? id : (Guid?)null).FirstOrDefault(id => id is not null);
        return new(Stable($"{category}:{diagnostic.Code}:{diagnostic.Path}"), 1, category, diagnostic.Code,
            diagnostic.Message, diagnostic.Path, subject ?? projectId, "derived-canonical-validation");
    }

    private static Guid Stable(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{value}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}
