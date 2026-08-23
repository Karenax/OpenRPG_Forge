using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Localization;
using Forge.Application.Projects;
using Forge.Application.Quality;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Audio;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private static readonly (string Key, string Label, string[] Routes, string Description)[]
        ValidationCategoriesUx7A =
        [
            ("schema", "Schémas", ["ORF-QA-001"], "Documents JSON versionnés et lisibles."),
            ("references", "Références", ["ORF-QA-002"], "Identités et liens typés résolus dans le projet."),
            ("graphs", "Graphes", ["ORF-QA-003"], "Dialogues et quêtes connectés sans cible absente."),
            ("geometry", "Géométrie", ["ORF-QA-004"], "Volumes, sockets et placements bornés."),
            ("navigation", "Navigation", ["ORF-QA-005"], "Surfaces, portails et points d’apparition analysés."),
            ("localization", "Localisation", ["ORF-QA-006"], "Couverture, variables, pluriels et débordements estimés."),
            ("license", "Licences", ["ORF-QA-007"], "SPDX, provenance et autorisations de distribution."),
            ("area", "Zone prête", ["ORF-AREA-037"], "Neuf contrôles de préparation sur la zone choisie."),
            ("audio", "Audio", ["ORF-AUD-020"], "Catalogues, chemins, tailles et empreintes des clips."),
            ("clickable", "Diagnostics cliquables", ["ORF-QA-021"], "Cibles humaines et corrections prévisualisables."),
        ];

    private bool _lastUx7ATemporaryReleased = true;

    internal bool LastUx7ATemporaryReleased => _lastUx7ATemporaryReleased;

    internal IReadOnlyList<StudioUx7AAreaChoice> ValidationAreaChoicesUx7A() =>
        Project?.Areas.OrderBy(area => area.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(area => new StudioUx7AAreaChoice(area.Id, area.DisplayName)).ToArray() ?? [];

    internal StudioUx7AReport? BuildValidationReportUx7A(AreaId areaId)
    {
        var project = Project;
        var history = History;
        var session = Session;
        if (project is null || history is null || session is null ||
            project.Areas.All(area => area.Id != areaId)) return null;

        var root = session.RootDirectory;
        var profile = QualityFoundationProfileStore.Load(root, project.Id.Value).Profile;
        var canonical = WorkspaceCanonicalValidationService.Run(root, project, profile);
        var audio = WorkspaceAudioValidator.Validate(root, project);
        var readiness = AreaReadinessService.Run(project, areaId);
        var extraction = LocalizationCatalogExtractor.Extract(project);
        var localization = LocalizationQualityService.Analyze(extraction.Catalog);
        var licenses = DistributionComplianceValidator.Validate(project, ModuleTargetKind.Shipping);
        var creator = CreatorDiagnostics.FromProject(project, $"ux7-a:{history.State.Revision.Value}");
        var issues = new List<StudioUx7AIssue>();

        CreatorDiagnostic? Actionable(string code, string path) => creator.FirstOrDefault(item =>
            string.Equals(item.Code, code, StringComparison.Ordinal) &&
            string.Equals(item.DocumentPath, path, StringComparison.Ordinal));

        void Add(string category, StudioUx7ASeverity severity, string code, string message,
            string path, string authority, CreatorDiagnostic? actionable = null)
        {
            actionable ??= Actionable(code, path);
            var target = actionable?.Target ?? ResolveValidationTargetUx7A(project, path);
            issues.Add(new(category, severity, code, HumanValidationTextUx7A(code, message), path,
                HumanValidationPathUx7A(project, path), target, actionable,
                actionable?.QuickFixes ?? [], authority));
        }

        foreach (var diagnostic in canonical.Diagnostics)
        {
            var category = diagnostic.Category switch
            {
                "schemas" => "schema",
                "references" => "references",
                "graphs" => "graphs",
                "geometry" => "geometry",
                "navigation" => "navigation",
                _ => "references",
            };
            Add(category, StudioUx7ASeverity.Error, diagnostic.Code, diagnostic.Message,
                diagnostic.Path, "Validation canonique du workspace");
        }
        foreach (var diagnostic in readiness.Diagnostics)
            Add("area", StudioUx7ASeverity.Error, diagnostic.Code, diagnostic.Message,
                diagnostic.Path, $"Préparation de zone · {diagnostic.Category}");
        foreach (var diagnostic in audio)
            Add("audio", StudioUx7ASeverity.Error, diagnostic.Code, diagnostic.Message,
                diagnostic.Path, "Validateur audio du workspace");
        foreach (var conflict in extraction.Diagnostics)
        {
            var parts = conflict.Split(':', 2);
            Add("localization", StudioUx7ASeverity.Error, parts[0],
                parts.Length > 1 ? $"Clé de localisation conflictuelle : {parts[1]}." : conflict,
                "module/localization/messages", "Extracteur de catalogue de localisation");
        }
        foreach (var issue in localization.Issues)
        {
            var severity = issue.Severity switch
            {
                LocalizationQualitySeverity.Error => StudioUx7ASeverity.Error,
                LocalizationQualitySeverity.Warning => StudioUx7ASeverity.Warning,
                _ => StudioUx7ASeverity.Information,
            };
            Add("localization", severity, issue.Code,
                $"{issue.MessageKey} · {issue.Locale} · {issue.Detail}",
                $"module/localization/messages/{issue.MessageKey}/{issue.Locale}",
                "Analyse de qualité de localisation");
        }
        foreach (var diagnostic in licenses)
            Add("license", StudioUx7ASeverity.Error, diagnostic.Code, diagnostic.Message,
                diagnostic.Path, "Conformité de distribution Shipping");
        foreach (var diagnostic in creator)
            Add("clickable", ToUx7ASeverity(diagnostic.Severity), diagnostic.Code, diagnostic.Message,
                diagnostic.DocumentPath, diagnostic.Source, diagnostic);

        var distinct = issues.DistinctBy(issue =>
                (issue.CategoryKey, issue.Code, issue.CanonicalPath, issue.Message))
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => Array.FindIndex(ValidationCategoriesUx7A,
                category => category.Key == issue.CategoryKey))
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.CanonicalPath, StringComparer.Ordinal)
            .ToArray();
        var categories = ValidationCategoriesUx7A.Select(category =>
        {
            var count = distinct.Count(issue => issue.CategoryKey == category.Key);
            return new StudioUx7ACategory(category.Key, category.Label, category.Routes,
                category.Description, count, count == 0);
        }).ToArray();
        var fingerprint = HashValidationUx7A(JsonSerializer.Serialize(new
        {
            revision = history.State.Revision.Value,
            area = areaId.ToString(),
            canonicalFingerprint = canonical.Fingerprint,
            readinessFingerprint = readiness.Fingerprint,
            localization = localization.Issues.Select(issue => new { issue.Code, issue.MessageKey, issue.Locale }),
            licenses = licenses.Select(issue => new { issue.Code, issue.Path }),
            audio = audio.Select(issue => new { issue.Code, issue.Path }),
            issues = distinct.Select(issue => new { issue.CategoryKey, issue.Code, issue.CanonicalPath }),
        }));
        return new(history.State.Revision, areaId, fingerprint, categories, distinct,
            distinct.Count(issue => issue.Severity == StudioUx7ASeverity.Error),
            distinct.Count(issue => issue.Severity == StudioUx7ASeverity.Warning),
            distinct.Count(issue => issue.Severity == StudioUx7ASeverity.Information),
            ReadOnlyValidation: true, RuntimeClaimsBounded: true);
    }

    internal StudioDiagnosticFixPreview PreviewValidationFixUx7A(StudioUx7AIssue issue,
        DiagnosticQuickFixDefinition fix)
    {
        if (issue.Actionable is null || issue.QuickFixes.All(candidate => candidate.Id != fix.Id))
            return new(false, "Cette ligne ne porte pas de correction canonique.", "Aucun changement.", false);
        return PreviewDiagnosticQuickFix(issue.Actionable, fix);
    }

    internal StudioActionResult ApplyValidationFixUx7A() => ApplyApprovedDiagnosticQuickFix();

    internal StudioUx7AApprovalSnapshot CaptureValidationApprovalUx7A() =>
        new(_approvedDiagnosticId, _approvedQuickFixId);

    internal void RestoreValidationApprovalUx7A(StudioUx7AApprovalSnapshot snapshot)
    {
        _approvedDiagnosticId = snapshot.DiagnosticId;
        _approvedQuickFixId = snapshot.QuickFixId;
    }

    internal void CancelValidationApprovalUx7A() => ClearApprovedDiagnosticFix();

    internal void CloseUx7A() => CancelValidationApprovalUx7A();

    internal bool SeedValidationEvidenceUx7A()
    {
        if (Project is null || History is null || Session is null) return false;
        var area = Project.Areas.FirstOrDefault(candidate => candidate.Entities.Count > 0);
        var entity = area?.Entities.FirstOrDefault();
        if (area is null || entity is null) return false;
        var outside = new EntityInstance(entity.Id, entity.BlueprintId,
            new TransformValue(
                new Vector3Value(area.Dimensions.Width * 2f, entity.Transform.Position.Y,
                    area.Dimensions.Depth * 2f),
                entity.Transform.Rotation, entity.Transform.Scale),
            entity.Tags, entity.Overrides, entity.BlueprintVersionRequirement);
        var invalidArea = area.WithEntities(area.Entities.Select(candidate => candidate.Id == entity.Id
            ? outside : candidate).ToArray());
        var invalidProject = new ProjectDefinition(Project.Id, Project.Title, Project.Module,
            Project.Blueprints, Project.Areas.Select(candidate => candidate.Id == area.Id ? invalidArea : candidate)
                .ToArray(), Project.Dialogues, Project.Quests);
        History = new ProjectCommandHistory(new ProjectState(invalidProject, History.State.Revision));
        File.WriteAllText(Path.Combine(Session.RootDirectory, "build.profile.json"),
            "{\"profile\":\"preuve-ux7-a-sans-version\"}", new UTF8Encoding(false));
        CancelValidationApprovalUx7A();
        return true;
    }

    internal StudioUx7ASmokeResult RunUx7AContractSmoke()
    {
        var empty = new StudioUx7ASmokeResult(false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false);
        if (Project is null) return empty;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux7-a-contract-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var result = empty;
        _lastUx7ATemporaryReleased = false;
        try
        {
            if (!ProjectSession.Create(root, Project).Succeeded || !editing.Open(root).Succeeded ||
                !editing.SeedValidationEvidenceUx7A()) return empty;
            var areaId = editing.Project!.Areas.First(area => area.Entities.Count > 0).Id;
            var report = editing.BuildValidationReportUx7A(areaId);
            if (report is null) return empty;
            var routes = report.Categories.SelectMany(category => category.Routes).Distinct().ToArray();
            var tenRoutes = routes.Length == 10;
            var canonical = new[] { "schema", "references", "graphs", "geometry", "navigation" }
                .All(key => report.Categories.Any(category => category.Key == key));
            var areaReady = report.Categories.Any(category => category.Key == "area") &&
                            report.Issues.Any(issue => issue.CategoryKey == "area");
            var audioReady = report.Categories.Any(category => category.Key == "audio");
            var localizationReady = report.Categories.Any(category => category.Key == "localization");
            var licenseReady = report.Categories.Any(category => category.Key == "license");
            var issue = report.Issues.FirstOrDefault(candidate =>
                candidate.CategoryKey == "clickable" && candidate.QuickFixes.Count > 0 &&
                candidate.Target?.Kind == CreatorSelectionKind.Entity);
            if (issue is null) return empty;
            var fix = issue.QuickFixes[0];
            var preview = editing.PreviewValidationFixUx7A(issue, fix);
            var invalidProject = editing.Project!;
            var revision = editing.History!.State.Revision;
            var staleApproval = editing.CaptureValidationApprovalUx7A();
            var resolvedOnce = editing.ApplyValidationFixUx7A();
            editing.RestoreValidationApprovalUx7A(staleApproval);
            var stale = resolvedOnce.Succeeded && !editing.ApplyValidationFixUx7A().Succeeded;
            editing.History = new ProjectCommandHistory(new ProjectState(invalidProject, revision));
            report = editing.BuildValidationReportUx7A(areaId)!;
            issue = report.Issues.First(candidate => candidate.CategoryKey == "clickable" &&
                                                    candidate.QuickFixes.Count > 0 &&
                                                    candidate.Target?.Kind == CreatorSelectionKind.Entity);
            fix = issue.QuickFixes[0];
            preview = editing.PreviewValidationFixUx7A(issue, fix);
            var applied = preview.CanApply && editing.ApplyValidationFixUx7A().Succeeded;
            var canonicalCommand = editing.History.Journal.LastOrDefault()?.CommandName ==
                                   "apply_diagnostic_quick_fix";
            var after = editing.Project;
            var undo = editing.Undo();
            var undone = ReferenceEquals(invalidProject, editing.Project);
            var redo = editing.Redo();
            var redone = ReferenceEquals(after, editing.Project);
            var saved = editing.Save();
            editing.Close();
            var opened = reopened.Open(root);
            var persisted = saved.Succeeded && opened.Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) ==
                            ProjectJsonSerializer.Serialize(after!);
            var human = report.Issues.All(candidate =>
                !Regex.IsMatch(candidate.HumanPath, GuidPatternUx7A(), RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant));
            var clickable = report.Categories.Any(category => category.Key == "clickable") &&
                            issue.Actionable is not null;
            var deepLink = issue.Target is { Kind: CreatorSelectionKind.Entity };
            var succeeded = tenRoutes && canonical && areaReady && audioReady && localizationReady &&
                            licenseReady && clickable && deepLink && preview.Succeeded && stale && applied &&
                            canonicalCommand && undo.Succeeded && undone && redo.Succeeded && redone && persisted &&
                            human && report.ReadOnlyValidation && report.RuntimeClaimsBounded;
            result = new(succeeded, tenRoutes, canonical, areaReady, audioReady, localizationReady,
                licenseReady, clickable, deepLink, preview.Succeeded, stale, canonicalCommand,
                undo.Succeeded && redo.Succeeded, persisted, human, report.RuntimeClaimsBounded, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException)
        {
            result = empty;
        }
        finally
        {
            editing.Close();
            reopened.Close();
            _lastUx7ATemporaryReleased = DeleteTemporaryBlueprintUx3A(root);
        }
        return result with
        {
            Succeeded = result.Succeeded && _lastUx7ATemporaryReleased,
            TemporaryReleased = _lastUx7ATemporaryReleased,
        };
    }

    private static StudioUx7ASeverity ToUx7ASeverity(CreatorDiagnosticSeverity severity) => severity switch
    {
        CreatorDiagnosticSeverity.Error => StudioUx7ASeverity.Error,
        CreatorDiagnosticSeverity.Warning => StudioUx7ASeverity.Warning,
        _ => StudioUx7ASeverity.Information,
    };

    private static CreatorSelection ResolveValidationTargetUx7A(ProjectDefinition project, string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0] == "areas" && Guid.TryParse(parts[1], out var areaGuid) &&
            project.Areas.FirstOrDefault(area => area.Id.Value == areaGuid) is { } area)
        {
            if (parts.Length >= 4 && parts[2] == "entities")
            {
                EntityInstance? entity = null;
                if (Guid.TryParse(parts[3], out var entityGuid))
                    entity = area.Entities.FirstOrDefault(candidate => candidate.Id.Value == entityGuid);
                else if (int.TryParse(parts[3], out var index) && index >= 0 && index < area.Entities.Count)
                    entity = area.Entities[index];
                if (entity is not null)
                    return new(CreatorSelectionKind.Entity, entity.Id.ToString(), "Entité signalée",
                        $"areas/{area.Id}/entities/{entity.Id}");
            }
            return new(CreatorSelectionKind.Area, area.Id.ToString(), area.DisplayName, $"areas/{area.Id}");
        }
        return new(CreatorSelectionKind.Project, project.Id.ToString(), project.Title, "project.json");
    }

    private static string HumanValidationPathUx7A(ProjectDefinition project, string path)
    {
        var value = path.Replace('\\', '/');
        foreach (var area in project.Areas) value = value.Replace(area.Id.ToString(), area.DisplayName,
            StringComparison.OrdinalIgnoreCase);
        foreach (var dialogue in project.Dialogues) value = value.Replace(dialogue.Id.ToString(),
            dialogue.DisplayName, StringComparison.OrdinalIgnoreCase);
        foreach (var quest in project.Quests) value = value.Replace(quest.Id.ToString(), quest.DisplayName,
            StringComparison.OrdinalIgnoreCase);
        value = Regex.Replace(value, GuidPatternUx7A(), "élément protégé",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return value.Replace("areas/", "Zones / ", StringComparison.OrdinalIgnoreCase)
            .Replace("/entities/", " / Entités / ", StringComparison.OrdinalIgnoreCase)
            .Replace("/transform", " / Transformation", StringComparison.OrdinalIgnoreCase)
            .Replace("module/", "Module / ", StringComparison.OrdinalIgnoreCase)
            .Replace("project.json", "Projet", StringComparison.OrdinalIgnoreCase);
    }

    private static string HumanValidationTextUx7A(string code, string value)
    {
        var cleaned = Regex.Replace(value.Replace('\\', '/'), GuidPatternUx7A(), "élément protégé",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return code switch
        {
            AreaGeometryValidator.OutOfBoundsCode =>
                "Une entité dépasse les limites de la zone sélectionnée.",
            AreaGeometryValidator.CollisionOverlapCode =>
                "Deux volumes de collision se chevauchent dans la zone sélectionnée.",
            AreaGeometryValidator.SocketMismatchCode =>
                "Un raccord de géométrie ne correspond pas à son voisin.",
            AreaGeometryValidator.InvalidScaleCode =>
                "Une entité utilise une échelle invalide.",
            AreaNavigationValidator.MissingSpawnCode =>
                "La zone ne possède aucun point d’apparition joueur utilisable.",
            AreaNavigationValidator.UnreachableEntityCode =>
                "Une entité n’est accessible depuis aucun point d’apparition joueur.",
            AreaNavigationValidator.LinkOutOfBoundsCode =>
                "Un lien de navigation sort des limites de la zone.",
            AreaNavigationValidator.LinkEndpointBlockedCode =>
                "Une extrémité de lien de navigation est bloquée.",
            AreaNavigationValidator.FlightLinkUnavailableCode =>
                "Un lien aérien exige un profil de navigation compatible.",
            WorkspaceCanonicalValidationService.SchemaDocumentInvalidCode =>
                "Un document du workspace est illisible ou ne déclare pas une version compatible.",
            WorkspaceAudioValidator.ResourceUnsafeCode =>
                "Une ressource audio utilise un chemin non autorisé.",
            WorkspaceAudioValidator.ResourceMissingCode =>
                "Une ressource audio référencée est absente du workspace.",
            WorkspaceAudioValidator.ResourceSizeCode =>
                "La taille réelle d’une ressource audio diffère de son manifeste.",
            WorkspaceAudioValidator.ResourceHashCode =>
                "L’empreinte d’une ressource audio diffère de son manifeste.",
            _ when code.StartsWith("distribution.", StringComparison.Ordinal) =>
                "La préparation de distribution signale une licence, une provenance ou une approbation incomplète.",
            _ when code.StartsWith("localization.", StringComparison.Ordinal) =>
                "La qualité de localisation signale une traduction absente, conflictuelle ou trop longue.",
            _ => cleaned,
        };
    }

    private static string GuidPatternUx7A() =>
        "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";

    private static string HashValidationUx7A(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
