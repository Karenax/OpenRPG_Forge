using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux2LwCProvenance = "studio:ux2-lw-c:area-user-event";
    private readonly SetAreaAuthoringHandler _setAreaAuthoringUx2LwC = new(new SystemClock());
    private StudioUx2LwCDraft? _approvedAreaUserEventUx2LwC;
    private ProjectRevision? _approvedAreaUserEventRevisionUx2LwC;
    private string? _approvedAreaUserEventSourceFingerprintUx2LwC;
    private string? _approvedAreaUserEventDraftFingerprintUx2LwC;

    internal IReadOnlyList<StudioUx2LwCAreaChoice> AreaUserEventAreasUx2LwC
    {
        get
        {
            var project = Project;
            if (project is null)
            {
                return [];
            }
            var evidenceTarget = ActiveAreaId ?? project.Areas.FirstOrDefault()?.Id;
            var duplicateNames = project.Areas.GroupBy(area => area.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.CurrentCultureIgnoreCase);
            return project.Areas.Select(area =>
                {
                    var name = HumanAreaUserEventAreaNameUx2LwC(area);
                    var label = duplicateNames[area.DisplayName] > 1
                        ? $"{name} · {HumanAreaUserEventSlugUx2LwC(area.Slug)}"
                        : name;
                    var count = area.Authoring.UserEvents.Count;
                    return new StudioUx2LwCAreaChoice(area.Id,
                        $"{label} · {(count == 0 ? "aucun événement" : count == 1 ? "1 événement" : $"{count} événements")}",
                        count, area.Id == evidenceTarget);
                })
                .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    internal IReadOnlyList<StudioUx2LwCEventChoice> AreaUserEventChoicesUx2LwC(AreaId? areaId)
    {
        if (areaId is null)
        {
            return [];
        }
        var area = Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null)
        {
            return [];
        }
        return area.Authoring.UserEvents.Select(binding => new StudioUx2LwCEventChoice(
                binding.Id,
                $"{HumanAreaUserEventNameUx2LwC(binding.NamespacedName)} · " +
                (binding.SchemaVersion == AreaUserEvent.CurrentSchemaVersion
                    ? $"payload {binding.PayloadFields.Count} champ(s)"
                    : "mise à niveau du payload requise"),
                binding.SchemaVersion != AreaUserEvent.CurrentSchemaVersion))
            .OrderBy(choice => choice.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal IReadOnlyList<StudioUx2LwCScriptChoice> AreaUserEventScriptsUx2LwC()
    {
        var session = Session;
        if (session is null)
        {
            return [];
        }
        var scriptRoot = Path.Combine(session.RootDirectory, "scripts");
        if (!Directory.Exists(scriptRoot))
        {
            return [];
        }
        try
        {
            return Directory.EnumerateFiles(scriptRoot, "*.forge", SearchOption.AllDirectories)
                .Select(path =>
                {
                    var relative = Path.GetRelativePath(session.RootDirectory, path).Replace('\\', '/');
                    var source = File.ReadAllText(path);
                    var handlers = AreaUserEventHandlersUx2LwC(source);
                    return new StudioUx2LwCScriptChoice(relative,
                        HumanAreaUserEventScriptNameUx2LwC(relative), handlers,
                        $"Source locale du module · {HumanAreaUserEventFolderUx2LwC(relative)}");
                })
                .Where(choice => choice.Handlers.Count > 0)
                .OrderBy(choice => choice.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(choice => choice.RelativePath, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    internal StudioUx2LwCDraft? CreateAreaUserEventDraftUx2LwC(AreaId? areaId, Guid? eventId)
    {
        var history = History;
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        var binding = eventId is null ? null : area?.Authoring.UserEvents.SingleOrDefault(item => item.Id == eventId);
        if (history is null || area is null || binding is null)
        {
            return null;
        }
        return DraftFromAreaUserEventUx2LwC(area, binding, history.State.Revision, isNew: false);
    }

    internal StudioUx2LwCDraft? CreateNewAreaUserEventDraftUx2LwC(AreaId? areaId)
    {
        var history = History;
        var area = areaId is null ? null : Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null)
        {
            return null;
        }
        var eventId = StableAreaUserEventIdUx2LwC(area.Id);
        if (area.Authoring.UserEvents.Any(item => item.Id == eventId))
        {
            eventId = Guid.NewGuid();
        }
        return new StudioUx2LwCDraft(
            area.Id,
            eventId,
            true,
            false,
            history.State.Revision,
            AreaUserEventSourceFingerprintUx2LwC(area),
            HumanAreaUserEventAreaNameUx2LwC(area),
            $"area.{area.Slug}.signal",
            "schema://area/signal/v1",
            1,
            AreaUserEvent.CurrentForgeScriptApiVersion,
            string.Empty,
            [],
            false,
            Ux2LwCProvenance,
            []);
    }

    internal StudioUx2LwCPreview PreviewAreaUserEventUx2LwC(StudioUx2LwCDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelAreaUserEventPreviewUx2LwC();
        var history = History;
        var project = Project;
        if (history is null || project is null)
        {
            return FailedAreaUserEventPreviewUx2LwC("Aucun module n’est ouvert.");
        }
        if (draft.BaseRevision != history.State.Revision)
        {
            return FailedAreaUserEventPreviewUx2LwC(
                "Le projet a changé depuis l’ouverture de cette fiche. Rechargez-la avant l’aperçu.");
        }
        var area = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (area is null)
        {
            return FailedAreaUserEventPreviewUx2LwC("La zone sélectionnée n’existe plus.");
        }
        if (!string.Equals(draft.SourceFingerprint, AreaUserEventSourceFingerprintUx2LwC(area),
                StringComparison.Ordinal))
        {
            return FailedAreaUserEventPreviewUx2LwC(
                "La collection d’événements a changé. Rechargez la fiche avant l’aperçu.");
        }
        if (!TryBuildAreaUserEventCandidateUx2LwC(draft, area, out var binding, out var candidateArea,
                out var payload, out var diagnostics))
        {
            return new(false, false,
                diagnostics.FirstOrDefault()?.Message ?? "La fiche contient une valeur invalide.",
                new([]), diagnostics, string.Empty, "Payload non validé", "Script non validé", false, false);
        }

        var command = new SetAreaAuthoringCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            area.Id, candidateArea!.Authoring, DryRun: true);
        var dryRun = _setAreaAuthoringUx2LwC.Handle(history.State, command);
        var diff = BuildAreaUserEventDiffUx2LwC(area, candidateArea, draft.EventId);
        if (!dryRun.Succeeded)
        {
            var commandDiagnostics = dryRun.Diagnostics.Select(item => new StudioUx2LwCDiagnostic(item.Code,
                $"areas/{area.Id}/authoring/userEvents/{draft.EventId:D}",
                HumanizeAreaUserEventDiagnosticUx2LwC(item.Code))).ToArray();
            return new(false, false,
                commandDiagnostics.FirstOrDefault()?.Message ?? "La commande canonique a refusé cet aperçu.",
                diff, commandDiagnostics, dryRun.Patch?.Summary ?? string.Empty,
                "Payload non validé", "Script non validé", false, false);
        }

        var runtimePreview = binding is null
            ? null
            : AreaUserEventRuntimeService.Dispatch(candidateArea, binding.NamespacedName, payload!,
                new NativeForgeScriptRuntime(), ReadAreaUserEventScriptSourceUx2LwC,
                requiredAtRuntime: binding.Required, dryRun: true,
                correlationId: StableAreaUserEventCorrelationIdUx2LwC(binding.Id));
        if (runtimePreview is { Succeeded: false })
        {
            var runtimeDiagnostics = runtimePreview.Diagnostics.Select(item => new StudioUx2LwCDiagnostic(
                item.Code, item.Path, HumanizeAreaUserEventDiagnosticUx2LwC(item.Code))).ToArray();
            return new(false, false,
                runtimeDiagnostics.FirstOrDefault()?.Message ?? "Le payload ou le script n’est pas prêt.",
                diff, runtimeDiagnostics, dryRun.Patch?.Summary ?? string.Empty,
                PayloadSummaryUx2LwC(draft.PayloadFields), ScriptSummaryUx2LwC(draft), false, false);
        }

        var canApply = diff.HasChanges;
        if (canApply)
        {
            _approvedAreaUserEventUx2LwC = draft;
            _approvedAreaUserEventRevisionUx2LwC = history.State.Revision;
            _approvedAreaUserEventSourceFingerprintUx2LwC = draft.SourceFingerprint;
            _approvedAreaUserEventDraftFingerprintUx2LwC = AreaUserEventDraftFingerprintUx2LwC(draft);
        }
        return new(true, canApply,
            draft.Delete
                ? "Suppression prévisualisée par la commande canonique ; aucun script n’a été exécuté."
                : "Payload, abonnements et compilation validés en aperçu sec ; aucun script n’a été exécuté.",
            diff, [], dryRun.Patch?.Summary ?? string.Empty,
            draft.Delete ? "Payload retiré avec l’événement" : PayloadSummaryUx2LwC(draft.PayloadFields),
            draft.Delete ? "Aucun dispatch après suppression" : ScriptSummaryUx2LwC(draft),
            runtimePreview?.Compilation?.Success == true, false);
    }

    internal StudioUx2LwCActionResult ApplyApprovedAreaUserEventUx2LwC()
    {
        var history = History;
        var project = Project;
        var draft = _approvedAreaUserEventUx2LwC;
        if (history is null || project is null || draft is null ||
            _approvedAreaUserEventRevisionUx2LwC != history.State.Revision ||
            !string.Equals(_approvedAreaUserEventSourceFingerprintUx2LwC, draft.SourceFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(_approvedAreaUserEventDraftFingerprintUx2LwC,
                AreaUserEventDraftFingerprintUx2LwC(draft), StringComparison.Ordinal))
        {
            CancelAreaUserEventPreviewUx2LwC();
            return new(false, "Prévisualisez à nouveau cette fiche avant de l’appliquer.");
        }
        var area = project.Areas.SingleOrDefault(item => item.Id == draft.AreaId);
        if (area is null || !string.Equals(draft.SourceFingerprint,
                AreaUserEventSourceFingerprintUx2LwC(area), StringComparison.Ordinal))
        {
            CancelAreaUserEventPreviewUx2LwC();
            return new(false, "La zone a changé. Rechargez la fiche avant de l’appliquer.");
        }
        if (!TryBuildAreaUserEventCandidateUx2LwC(draft, area, out _, out var candidateArea,
                out _, out var diagnostics))
        {
            CancelAreaUserEventPreviewUx2LwC();
            return new(false, diagnostics.FirstOrDefault()?.Message ?? "La fiche est invalide.");
        }
        var applied = _setAreaAuthoringUx2LwC.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, area.Id,
                candidateArea!.Authoring, DryRun: false));
        if (!applied.Succeeded)
        {
            CancelAreaUserEventPreviewUx2LwC();
            return new(false, HumanizeAreaUserEventDiagnosticUx2LwC(applied.Diagnostics.FirstOrDefault()?.Code));
        }
        history.Record(applied);
        CancelAreaUserEventPreviewUx2LwC();
        return new(true, draft.Delete
            ? "Événement retiré de la zone."
            : "Événement de zone appliqué par la commande canonique.");
    }

    internal void CancelAreaUserEventPreviewUx2LwC()
    {
        _approvedAreaUserEventUx2LwC = null;
        _approvedAreaUserEventRevisionUx2LwC = null;
        _approvedAreaUserEventSourceFingerprintUx2LwC = null;
        _approvedAreaUserEventDraftFingerprintUx2LwC = null;
    }

    internal void CloseUx2LwC() => CancelAreaUserEventPreviewUx2LwC();

    internal StudioUx2LwCApprovalSnapshot CaptureAreaUserEventApprovalUx2LwC() => new(
        _approvedAreaUserEventUx2LwC,
        _approvedAreaUserEventRevisionUx2LwC,
        _approvedAreaUserEventSourceFingerprintUx2LwC,
        _approvedAreaUserEventDraftFingerprintUx2LwC);

    internal void RestoreAreaUserEventApprovalUx2LwC(StudioUx2LwCApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedAreaUserEventUx2LwC = snapshot.Draft;
        _approvedAreaUserEventRevisionUx2LwC = snapshot.Revision;
        _approvedAreaUserEventSourceFingerprintUx2LwC = snapshot.SourceFingerprint;
        _approvedAreaUserEventDraftFingerprintUx2LwC = snapshot.DraftFingerprint;
    }

    private bool TryBuildAreaUserEventCandidateUx2LwC(
        StudioUx2LwCDraft draft,
        AreaDefinition sourceArea,
        out AreaUserEvent? binding,
        out AreaDefinition? candidateArea,
        out IReadOnlyDictionary<string, object?>? payload,
        out IReadOnlyList<StudioUx2LwCDiagnostic> diagnostics)
    {
        var problems = new List<StudioUx2LwCDiagnostic>();
        var path = $"areas/{sourceArea.Id}/authoring/userEvents/{draft.EventId:D}";
        if (draft.Delete)
        {
            if (draft.IsNew || sourceArea.Authoring.UserEvents.All(item => item.Id != draft.EventId))
                problems.Add(new("studio.area_user_event.delete_missing", path,
                    "L’événement à retirer n’existe plus dans cette zone."));
            binding = null;
            payload = new Dictionary<string, object?>();
        }
        else
        {
            var script = AreaUserEventScriptsUx2LwC().SingleOrDefault(item =>
                string.Equals(item.RelativePath, draft.ScriptPath, StringComparison.Ordinal));
            if (script is null)
                problems.Add(new("studio.area_user_event.script_missing", $"{path}/scriptPath",
                    "Choisissez un script réel encore présent dans le module."));
            if (draft.Subscriptions.Count == 0)
                problems.Add(new("studio.area_user_event.subscription_required", $"{path}/subscriptions",
                    "Choisissez au moins un handler fourni par ce script."));
            else if (script is not null && draft.Subscriptions.Any(handler =>
                         !script.Handlers.Contains(handler, StringComparer.Ordinal)))
                problems.Add(new("studio.area_user_event.handler_missing", $"{path}/subscriptions",
                    "Le handler choisi n’est pas déclaré par le script sélectionné."));

            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var field in draft.PayloadFields)
            {
                var name = field.Name.Trim();
                var type = field.Type.Trim().ToLowerInvariant();
                if (name.Length == 0 || !fieldNames.Add(name))
                {
                    problems.Add(new("studio.area_user_event.payload_field_duplicate", $"{path}/payloadFields",
                        "Chaque champ de payload doit avoir un nom unique et non vide."));
                    continue;
                }
                if (!AreaUserEventValidator.IsSupportedPayloadType(type) ||
                    !TryParseAreaUserEventPayloadValueUx2LwC(type, field.Value, out var parsed))
                {
                    problems.Add(new("studio.area_user_event.payload_value_invalid",
                        $"{path}/payloadFields/{name}",
                        $"La valeur du champ « {HumanAreaUserEventTokenUx2LwC(name)} » ne correspond pas au type choisi."));
                    continue;
                }
                fields[name] = type;
                values[name] = parsed;
            }
            if (draft.PayloadFields.Count == 0)
                problems.Add(new("studio.area_user_event.payload_field_required", $"{path}/payloadFields",
                    "Ajoutez au moins un champ au payload avant l’aperçu."));
            var authoredBinding = new AreaUserEvent(draft.EventId, draft.NamespacedName, draft.PayloadSchema,
                draft.ScriptPath, draft.PayloadVersion, draft.Subscriptions, draft.Required,
                Ux2LwCProvenance, AreaUserEvent.CurrentSchemaVersion, fields,
                draft.ForgeScriptApiVersion);
            binding = authoredBinding;
            payload = values;
            var isolatedArea = sourceArea.WithAuthoring(new AreaAuthoringProfile(
                sourceArea.Authoring.SchemaVersion, [authoredBinding], sourceArea.Authoring.Map,
                sourceArea.Authoring.Layers, sourceArea.Authoring.Experience,
                sourceArea.Authoring.RequireLayerRuntimeMetadata));
            problems.AddRange(AreaUserEventValidator.ValidateExecutable(isolatedArea).Select(item =>
                new StudioUx2LwCDiagnostic(item.Code, item.Path,
                    HumanizeAreaUserEventDiagnosticUx2LwC(item.Code))));
        }

        if (problems.Count > 0)
        {
            candidateArea = null;
            diagnostics = problems.DistinctBy(item => (item.Code, item.Path)).ToArray();
            return false;
        }

        var candidateBinding = binding;
        var events = draft.Delete
            ? sourceArea.Authoring.UserEvents.Where(item => item.Id != draft.EventId).ToArray()
            : sourceArea.Authoring.UserEvents.Any(item => item.Id == draft.EventId)
                ? sourceArea.Authoring.UserEvents.Select(item =>
                    item.Id == draft.EventId ? candidateBinding! : item).ToArray()
                : sourceArea.Authoring.UserEvents.Append(candidateBinding!).ToArray();
        var source = sourceArea.Authoring;
        var authoring = new AreaAuthoringProfile(source.SchemaVersion, events, source.Map, source.Layers,
            source.Experience, source.RequireLayerRuntimeMetadata);
        candidateArea = sourceArea.WithAuthoring(authoring);
        diagnostics = [];
        return true;
    }

    private static StudioUx2LwCDraft DraftFromAreaUserEventUx2LwC(
        AreaDefinition area,
        AreaUserEvent binding,
        ProjectRevision revision,
        bool isNew) => new(
        area.Id,
        binding.Id,
        isNew,
        false,
        revision,
        AreaUserEventSourceFingerprintUx2LwC(area),
        HumanAreaUserEventAreaNameUx2LwC(area),
        binding.NamespacedName,
        binding.PayloadSchema,
        binding.ApiVersion,
        binding.ForgeScriptApiVersion,
        binding.ScriptPath,
        binding.Subscriptions,
        binding.Required,
        binding.Provenance,
        binding.PayloadFields.Select(field => new StudioUx2LwCPayloadFieldDraft(field.Key, field.Value, string.Empty))
            .OrderBy(field => field.Name, StringComparer.Ordinal).ToArray());

    private static bool TryParseAreaUserEventPayloadValueUx2LwC(string type, string text, out object? value)
    {
        var source = text.Trim();
        switch (type)
        {
            case "string":
                value = text;
                return true;
            case "bool" when bool.TryParse(source, out var boolean):
                value = boolean;
                return true;
            case "int" when int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var integer):
                value = integer;
                return true;
            case "long" when long.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var longInteger):
                value = longInteger;
                return true;
            case "float" when double.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var floating) && double.IsFinite(floating):
                value = floating;
                return true;
            case "guid" when Guid.TryParse(source, out var guid) && guid != Guid.Empty:
                value = guid;
                return true;
            default:
                value = null;
                return false;
        }
    }

    private static StudioUx2LwCSemanticDiff BuildAreaUserEventDiffUx2LwC(
        AreaDefinition source,
        AreaDefinition candidate,
        Guid eventId)
    {
        var before = source.Authoring.UserEvents.SingleOrDefault(item => item.Id == eventId);
        var after = candidate.Authoring.UserEvents.SingleOrDefault(item => item.Id == eventId);
        var changes = new List<string>();
        if (before is null && after is not null)
            changes.Add($"Créer « {HumanAreaUserEventNameUx2LwC(after.NamespacedName)} » dans {HumanAreaUserEventAreaNameUx2LwC(source)}");
        else if (before is not null && after is null)
            changes.Add($"Retirer « {HumanAreaUserEventNameUx2LwC(before.NamespacedName)} » de cette zone");
        else if (before is not null && after is not null)
        {
            if (!string.Equals(before.NamespacedName, after.NamespacedName, StringComparison.Ordinal))
                changes.Add($"Renommer l’événement en « {HumanAreaUserEventNameUx2LwC(after.NamespacedName)} »");
            if (!before.PayloadFields.SequenceEqual(after.PayloadFields))
                changes.Add($"Payload · {after.PayloadFields.Count} champ(s) typé(s)");
            if (!string.Equals(before.ScriptPath, after.ScriptPath, StringComparison.Ordinal))
                changes.Add($"Script · {HumanAreaUserEventScriptNameUx2LwC(after.ScriptPath)}");
            if (!before.Subscriptions.SequenceEqual(after.Subscriptions))
                changes.Add($"Abonnements · {string.Join(", ", after.Subscriptions.Select(HumanAreaUserEventTokenUx2LwC))}");
            if (before.Required != after.Required)
                changes.Add(after.Required ? "Source requise au runtime" : "Source optionnelle au runtime");
            if (before.SchemaVersion != after.SchemaVersion)
                changes.Add("Mettre le payload au contrat typé actuel");
        }
        return new(changes);
    }

    internal ProjectDefinition? CreateUx2LwCEvidenceProject() => Project;

    internal static bool CopyUx2LwCEvidenceScripts(string sourceRoot, string destinationRoot)
    {
        try
        {
            var sourceScripts = Path.Combine(sourceRoot, "scripts");
            if (!Directory.Exists(sourceScripts))
            {
                return true;
            }
            foreach (var sourcePath in Directory.EnumerateFiles(sourceScripts, "*.forge", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceRoot, sourcePath);
                var destinationPath = Path.Combine(destinationRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal StudioUx2LwCSmokeResult RunUx2LwCContractSmoke()
    {
        var sourceSession = Session;
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceSession is null || sourceProject is null || sourceHistory is null)
        {
            return FailedUx2LwCSmoke();
        }
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceApproval = CaptureAreaUserEventApprovalUx2LwC();
        var sourceDirty = IsDirty;
        var sourceCanUndo = sourceHistory.CanUndo;
        var sourceCanRedo = sourceHistory.CanRedo;
        var sourceAreaId = ActiveAreaId;
        var sourceSelection = SelectedEntityIds.ToArray();
        var sourceSelectedEntity = SelectedEntityId;
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-c-" + Guid.NewGuid().ToString("N"));
        var editing = new StudioSessionController();
        var reopened = new StudioSessionController();
        var removed = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, sourceProject);
            if (!created.Succeeded || !CopyUx2LwCEvidenceScripts(sourceSession.RootDirectory, tempRoot) ||
                !editing.Open(tempRoot, ActiveAreaId?.ToString()).Succeeded)
                return FailedUx2LwCSmoke();

            var areas = editing.AreaUserEventAreasUx2LwC;
            var explicitArea = editing.CreateNewAreaUserEventDraftUx2LwC(null) is null && areas.Count > 0;
            var targetChoice = areas.FirstOrDefault(item => item.IsEvidenceTarget) ?? areas.FirstOrDefault();
            if (targetChoice is null)
                return FailedUx2LwCSmoke() with { ExplicitAreaSelection = explicitArea };
            var explicitEvent = editing.CreateAreaUserEventDraftUx2LwC(targetChoice.AreaId, null) is null;
            var scripts = editing.AreaUserEventScriptsUx2LwC();
            var realScripts = scripts.Count > 0 && scripts.All(script => File.Exists(Path.Combine(tempRoot,
                script.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
            var selectedScript = scripts.FirstOrDefault(item => item.Handlers.Contains("main", StringComparer.Ordinal)) ??
                                 scripts.FirstOrDefault();
            if (selectedScript is null)
                return FailedUx2LwCSmoke() with
                {
                    ExplicitAreaSelection = explicitArea,
                    ExplicitEventSelection = explicitEvent,
                };
            var selectedHandler = selectedScript.Handlers[0];

            StudioUx2LwCDraft Configure(StudioUx2LwCDraft draft) => draft with
            {
                NamespacedName = $"area.{editing.Project!.Areas.Single(item => item.Id == draft.AreaId).Slug}.signal",
                PayloadSchema = "schema://area/signal/v1",
                PayloadVersion = 1,
                ForgeScriptApiVersion = AreaUserEvent.CurrentForgeScriptApiVersion,
                ScriptPath = selectedScript.RelativePath,
                Subscriptions = [selectedHandler],
                Required = true,
                PayloadFields = [new("message", "string", "Signal de zone")],
            };

            var initial = Configure(editing.CreateNewAreaUserEventDraftUx2LwC(targetChoice.AreaId)!);
            var invalidPayload = editing.PreviewAreaUserEventUx2LwC(initial with
            {
                PayloadFields = [new("intensite", "int", "forte")],
            });
            var missingScript = editing.PreviewAreaUserEventUx2LwC(initial with
            {
                ScriptPath = "scripts/source-absente.forge",
            });
            var missingHandler = editing.PreviewAreaUserEventUx2LwC(initial with
            {
                Subscriptions = ["OnMissingAreaEvent"],
            });
            var invalidPayloadBlocked = !invalidPayload.Succeeded && invalidPayload.Diagnostics.Any(item =>
                item.Code == "studio.area_user_event.payload_value_invalid");
            var missingScriptBlocked = !missingScript.Succeeded && missingScript.Diagnostics.Any(item =>
                item.Code == "studio.area_user_event.script_missing");
            var missingHandlerBlocked = !missingHandler.Succeeded && missingHandler.Diagnostics.Any(item =>
                item.Code == "studio.area_user_event.handler_missing");
            var structuredDiagnostics = invalidPayload.Diagnostics.Concat(missingScript.Diagnostics)
                .Concat(missingHandler.Diagnostics).All(item =>
                    !string.IsNullOrWhiteSpace(item.Code) && !string.IsNullOrWhiteSpace(item.Path));

            var targetBeforeLegacy = editing.Project!.Areas.Single(item => item.Id == targetChoice.AreaId);
            var legacy = new AreaUserEvent(Guid.Parse("a0240000-0000-4000-8000-00000000c001"),
                $"area.{targetBeforeLegacy.Slug}.legacy", "schema://area/legacy/v1",
                selectedScript.RelativePath, 1, [selectedHandler], false, "migration:1.9");
            var legacyArea = targetBeforeLegacy.WithAuthoring(new(targetBeforeLegacy.Authoring.SchemaVersion,
                targetBeforeLegacy.Authoring.UserEvents.Append(legacy).ToArray(), targetBeforeLegacy.Authoring.Map,
                targetBeforeLegacy.Authoring.Layers, targetBeforeLegacy.Authoring.Experience,
                targetBeforeLegacy.Authoring.RequireLayerRuntimeMetadata));
            var legacyResult = AreaUserEventRuntimeService.Dispatch(legacyArea, legacy.NamespacedName,
                new Dictionary<string, object?>(), new NativeForgeScriptRuntime(), _ => "void main() {}",
                dryRun: true);
            var legacyBlocked = !legacyResult.Succeeded && legacyResult.Diagnostics.Any(item =>
                item.Code == AreaUserEventValidator.SchemaUpgradeRequiredCode);

            var beforePreviewProject = editing.Project;
            var beforePreviewHistory = editing.History!;
            var beforePreviewRevision = beforePreviewHistory.State.Revision;
            var beforePreviewJournal = beforePreviewHistory.Journal.ToArray();
            var beforePreviewDirty = editing.IsDirty;
            var preview = editing.PreviewAreaUserEventUx2LwC(initial);
            var afterPreviewHistory = editing.History;
            var dryRunPure = preview.Succeeded && ReferenceEquals(beforePreviewProject, editing.Project) &&
                             ReferenceEquals(beforePreviewHistory, afterPreviewHistory) &&
                             afterPreviewHistory?.State.Revision == beforePreviewRevision &&
                             (afterPreviewHistory?.Journal ?? []).SequenceEqual(beforePreviewJournal) &&
                             editing.IsDirty == beforePreviewDirty;
            var compiledWithoutExecution = preview.Compiled && !preview.Executed &&
                                           preview.Message.Contains("aucun script n’a été exécuté",
                                               StringComparison.OrdinalIgnoreCase);

            var staleDraft = Configure(editing.CreateNewAreaUserEventDraftUx2LwC(targetChoice.AreaId)!);
            var revisionMutation = ApplyUx2LwCRevisionMutation(editing, targetChoice.AreaId);
            var stalePreview = editing.PreviewAreaUserEventUx2LwC(staleDraft);
            var staleDraftBlocked = revisionMutation && !stalePreview.Succeeded &&
                                    stalePreview.Message.Contains("projet a changé", StringComparison.OrdinalIgnoreCase) &&
                                    editing.Undo().Succeeded;

            var staleApprovalDraft = Configure(editing.CreateNewAreaUserEventDraftUx2LwC(targetChoice.AreaId)!);
            var staleApprovalPreview = editing.PreviewAreaUserEventUx2LwC(staleApprovalDraft);
            var staleApprovalMutation = ApplyUx2LwCRevisionMutation(editing, targetChoice.AreaId);
            var staleApprovalApply = editing.ApplyApprovedAreaUserEventUx2LwC();
            var staleApprovalBlocked = staleApprovalPreview.Succeeded && staleApprovalMutation &&
                                       !staleApprovalApply.Succeeded && editing.Undo().Succeeded;

            var sourceProjectForApply = editing.Project!;
            var sourceTarget = sourceProjectForApply.Areas.Single(item => item.Id == targetChoice.AreaId);
            var sourceNeighbors = sourceProjectForApply.Areas.Where(item => item.Id != targetChoice.AreaId).ToArray();
            var fresh = Configure(editing.CreateNewAreaUserEventDraftUx2LwC(targetChoice.AreaId)!);
            var freshPreview = editing.PreviewAreaUserEventUx2LwC(fresh);
            var applied = editing.ApplyApprovedAreaUserEventUx2LwC();
            var appliedProject = editing.Project!;
            var appliedTarget = appliedProject.Areas.Single(item => item.Id == targetChoice.AreaId);
            var appliedBinding = appliedTarget.Authoring.UserEvents.SingleOrDefault(item => item.Id == fresh.EventId);
            var canonicalCommand = freshPreview.Succeeded && applied.Succeeded && appliedBinding is not null &&
                                   editing.History!.Journal.LastOrDefault()?.CommandName == "set_area_authoring" &&
                                   appliedBinding.PayloadFields.GetValueOrDefault("message") == "string";
            var collectionPreserved = sourceTarget.Authoring.UserEvents.All(sourceEvent => ReferenceEquals(
                sourceEvent, appliedTarget.Authoring.UserEvents.Single(item => item.Id == sourceEvent.Id))) &&
                appliedTarget.Authoring.UserEvents.Count == sourceTarget.Authoring.UserEvents.Count + 1;
            var targetNeighbors = ReferenceEquals(sourceTarget.Authoring.Map, appliedTarget.Authoring.Map) &&
                                  ReferenceEquals(sourceTarget.Authoring.Layers, appliedTarget.Authoring.Layers) &&
                                  ReferenceEquals(sourceTarget.Authoring.Experience, appliedTarget.Authoring.Experience) &&
                                  sourceTarget.Authoring.RequireLayerRuntimeMetadata ==
                                  appliedTarget.Authoring.RequireLayerRuntimeMetadata &&
                                  ReferenceEquals(sourceTarget.Foundation, appliedTarget.Foundation) &&
                                  ReferenceEquals(sourceTarget.Navigation, appliedTarget.Navigation) &&
                                  ReferenceEquals(sourceTarget.Environment, appliedTarget.Environment) &&
                                  ReferenceEquals(sourceTarget.World, appliedTarget.World) &&
                                  ReferenceEquals(sourceTarget.RuntimePolicies, appliedTarget.RuntimePolicies) &&
                                  ReferenceEquals(sourceTarget.Playtest, appliedTarget.Playtest) &&
                                  sourceTarget.Entities.SequenceEqual(appliedTarget.Entities) &&
                                  sourceTarget.Portals.SequenceEqual(appliedTarget.Portals);
            var projectNeighbors = ReferenceEquals(sourceProjectForApply.Module, appliedProject.Module) &&
                                   sourceProjectForApply.Blueprints.SequenceEqual(appliedProject.Blueprints) &&
                                   sourceProjectForApply.Dialogues.SequenceEqual(appliedProject.Dialogues) &&
                                   sourceProjectForApply.Quests.SequenceEqual(appliedProject.Quests) &&
                                   sourceNeighbors.All(neighbor => ReferenceEquals(neighbor,
                                       appliedProject.Areas.Single(item => item.Id == neighbor.Id)));

            var appliedAreaJson = AreaJsonSerializer.Serialize(appliedTarget);
            var undo = editing.Undo();
            var undone = undo.Succeeded && ReferenceEquals(sourceProjectForApply, editing.Project);
            var redo = editing.Redo();
            var redone = redo.Succeeded && ReferenceEquals(appliedProject, editing.Project);
            var undoRedo = undone && redone;
            var saved = editing.Save();
            var reopenedResult = reopened.Open(tempRoot, targetChoice.AreaId.ToString());
            var restoredProject = reopened.Project;
            var restoredTarget = restoredProject?.Areas.SingleOrDefault(item => item.Id == targetChoice.AreaId);
            var persistence = saved.Succeeded && reopenedResult.Succeeded && restoredProject is not null &&
                              restoredTarget is not null && string.Equals(appliedAreaJson,
                                  AreaJsonSerializer.Serialize(restoredTarget), StringComparison.Ordinal) &&
                              ProjectRoundTripMatchesUx2A(appliedProject, restoredProject, targetChoice.AreaId);

            var removalDraft = reopened.CreateAreaUserEventDraftUx2LwC(targetChoice.AreaId, fresh.EventId)! with
            {
                Delete = true,
            };
            var removalPreview = reopened.PreviewAreaUserEventUx2LwC(removalDraft);
            var removalApplied = reopened.ApplyApprovedAreaUserEventUx2LwC();
            var removalSaved = reopened.Save();
            var removalOpened = removed.Open(tempRoot, targetChoice.AreaId.ToString());
            var optionalRemoval = removalPreview.Succeeded && removalApplied.Succeeded && removalSaved.Succeeded &&
                                  removalOpened.Succeeded && removed.Project!.Areas
                                      .Single(item => item.Id == targetChoice.AreaId).Authoring.UserEvents
                                      .All(item => item.Id != fresh.EventId);

            var humanText = !ContainsTechnicalIdentifierUx2LwC(string.Join(' ', areas.Select(item => item.Label)) +
                            " " + string.Join(' ', freshPreview.Diff.Changes) + " " + freshPreview.Message);
            var currentSourceHistory = History;
            var sourceUntouched = ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, currentSourceHistory) &&
                                  currentSourceHistory?.State.Revision == sourceRevision &&
                                  (currentSourceHistory?.Journal ?? []).SequenceEqual(sourceJournal) &&
                                  currentSourceHistory?.CanUndo == sourceCanUndo &&
                                  currentSourceHistory?.CanRedo == sourceCanRedo &&
                                  IsDirty == sourceDirty && ActiveAreaId == sourceAreaId &&
                                  SelectedEntityIds.SequenceEqual(sourceSelection) &&
                                  SelectedEntityId == sourceSelectedEntity &&
                                  CaptureAreaUserEventApprovalUx2LwC() == sourceApproval;
            var succeeded = explicitArea && explicitEvent && realScripts && invalidPayloadBlocked &&
                            missingScriptBlocked && missingHandlerBlocked && legacyBlocked && dryRunPure &&
                            compiledWithoutExecution && staleDraftBlocked && staleApprovalBlocked && canonicalCommand &&
                            undoRedo && persistence && targetNeighbors && projectNeighbors && collectionPreserved &&
                            optionalRemoval && structuredDiagnostics && humanText && sourceUntouched;
            return new(succeeded, explicitArea, explicitEvent, realScripts, invalidPayloadBlocked,
                missingScriptBlocked, missingHandlerBlocked, legacyBlocked, dryRunPure, compiledWithoutExecution,
                staleDraftBlocked, staleApprovalBlocked, canonicalCommand, undoRedo, persistence, targetNeighbors,
                projectNeighbors, collectionPreserved, optionalRemoval, structuredDiagnostics, humanText,
                sourceUntouched);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_C_CONTRACT_FAILED " +
                              $"type={exception.GetType().Name} message={exception.Message}");
            return FailedUx2LwCSmoke();
        }
        finally
        {
            editing.Close();
            reopened.Close();
            removed.Close();
            DeleteUx2LwCTemporaryWorkspace(tempRoot);
        }
    }

    internal static bool DeleteUx2LwCTemporaryWorkspace(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return true;
        try
        {
            var root = Path.GetFullPath(directory);
            var temp = Path.GetFullPath(Path.GetTempPath());
            var prefix = temp.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            var leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("openrpg-forge-studio-ux2-lw-c-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            return !Directory.Exists(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool ApplyUx2LwCRevisionMutation(StudioSessionController controller, AreaId areaId)
    {
        var history = controller.History;
        var area = controller.Project?.Areas.SingleOrDefault(item => item.Id == areaId);
        if (history is null || area is null)
            return false;
        var source = area.Authoring;
        var map = source.Map;
        var candidateMap = new AreaMapProfile(map.Enabled, !map.FogOfWar, map.Projection, map.FloorCount,
            map.Pins, map.Id, map.SchemaVersion, map.Provenance, map.Generate2D, map.Generate3D,
            map.ExplorationScope, map.ExplorationCellSize, map.RevealApi);
        var candidate = new AreaAuthoringProfile(source.SchemaVersion, source.UserEvents, candidateMap,
            source.Layers, source.Experience, source.RequireLayerRuntimeMetadata);
        var result = new SetAreaAuthoringHandler(new SystemClock()).Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, area.Id, candidate, DryRun: false));
        if (!result.Succeeded)
            return false;
        history.Record(result);
        return true;
    }

    private string? ReadAreaUserEventScriptSourceUx2LwC(string relativePath)
    {
        var session = Session;
        var choice = AreaUserEventScriptsUx2LwC().SingleOrDefault(item =>
            string.Equals(item.RelativePath, relativePath, StringComparison.Ordinal));
        if (session is null || choice is null)
            return null;
        try
        {
            var root = Path.GetFullPath(session.RootDirectory);
            var path = Path.GetFullPath(Path.Combine(root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                   File.Exists(path)
                ? File.ReadAllText(path)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> AreaUserEventHandlersUx2LwC(string source)
    {
        var handlers = new HashSet<string>(StringComparer.Ordinal);
        if (Regex.IsMatch(source, @"\bvoid\s+main\s*\(", RegexOptions.CultureInvariant))
            handlers.Add("main");
        foreach (Match match in Regex.Matches(source,
                     @"\bvoid\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(\s*CustomEventRef(?:\s+[A-Za-z_][A-Za-z0-9_]*)?\s*\)",
                     RegexOptions.CultureInvariant))
            handlers.Add(match.Groups[1].Value);
        return handlers.Order(StringComparer.Ordinal).ToArray();
    }

    private static string AreaUserEventSourceFingerprintUx2LwC(AreaDefinition area) =>
        HashTypedAreaUserEventUx2LwC(builder =>
        {
            AppendTypedAreaUserEventUx2LwC(builder, "area", AreaJsonSerializer.Serialize(area));
            AppendTypedAreaUserEventUx2LwC(builder, "route", "ORF-AREA-024");
        });

    private static string AreaUserEventDraftFingerprintUx2LwC(StudioUx2LwCDraft draft) =>
        HashTypedAreaUserEventUx2LwC(builder =>
        {
            AppendTypedAreaUserEventUx2LwC(builder, "area", draft.AreaId.ToString());
            AppendTypedAreaUserEventUx2LwC(builder, "event", draft.EventId.ToString("D"));
            AppendTypedAreaUserEventUx2LwC(builder, "new", draft.IsNew ? "1" : "0");
            AppendTypedAreaUserEventUx2LwC(builder, "delete", draft.Delete ? "1" : "0");
            AppendTypedAreaUserEventUx2LwC(builder, "revision", draft.BaseRevision.Value.ToString(
                CultureInfo.InvariantCulture));
            AppendTypedAreaUserEventUx2LwC(builder, "source", draft.SourceFingerprint);
            AppendTypedAreaUserEventUx2LwC(builder, "name", draft.NamespacedName);
            AppendTypedAreaUserEventUx2LwC(builder, "schema", draft.PayloadSchema);
            AppendTypedAreaUserEventUx2LwC(builder, "payloadVersion", draft.PayloadVersion.ToString(
                CultureInfo.InvariantCulture));
            AppendTypedAreaUserEventUx2LwC(builder, "scriptApi", draft.ForgeScriptApiVersion);
            AppendTypedAreaUserEventUx2LwC(builder, "script", draft.ScriptPath);
            AppendTypedAreaUserEventUx2LwC(builder, "required", draft.Required ? "1" : "0");
            foreach (var subscription in draft.Subscriptions.Order(StringComparer.Ordinal))
                AppendTypedAreaUserEventUx2LwC(builder, "subscription", subscription);
            foreach (var field in draft.PayloadFields.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                AppendTypedAreaUserEventUx2LwC(builder, "field.name", field.Name);
                AppendTypedAreaUserEventUx2LwC(builder, "field.type", field.Type);
                AppendTypedAreaUserEventUx2LwC(builder, "field.value", field.Value);
            }
        });

    private static string HashTypedAreaUserEventUx2LwC(Action<StringBuilder> append)
    {
        var builder = new StringBuilder();
        append(builder);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendTypedAreaUserEventUx2LwC(StringBuilder builder, string name, string value)
    {
        builder.Append(name.Length).Append(':').Append(name).Append('=')
            .Append(value.Length).Append(':').Append(value).Append(';');
    }

    private static Guid StableAreaUserEventIdUx2LwC(AreaId areaId) =>
        StableAreaUserEventGuidUx2LwC($"event:{areaId}");

    private static Guid StableAreaUserEventCorrelationIdUx2LwC(Guid eventId) =>
        StableAreaUserEventGuidUx2LwC($"correlation:{eventId:D}");

    private static Guid StableAreaUserEventGuidUx2LwC(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ux2-lw-c:{value}"));
        return new Guid(bytes[..16]);
    }

    private static StudioUx2LwCPreview FailedAreaUserEventPreviewUx2LwC(string message) => new(
        false, false, message, new([]), [], string.Empty, "Payload non validé", "Script non validé",
        false, false);

    private static StudioUx2LwCSmokeResult FailedUx2LwCSmoke() => new(
        false, false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false);

    private static string PayloadSummaryUx2LwC(IReadOnlyList<StudioUx2LwCPayloadFieldDraft> fields) =>
        fields.Count == 0 ? "Payload vide" :
        string.Join(" · ", fields.Select(field =>
            $"{HumanAreaUserEventTokenUx2LwC(field.Name)} ({field.Type})"));

    private static string ScriptSummaryUx2LwC(StudioUx2LwCDraft draft) =>
        $"{HumanAreaUserEventScriptNameUx2LwC(draft.ScriptPath)} · " +
        string.Join(", ", draft.Subscriptions.Select(HumanAreaUserEventTokenUx2LwC)) +
        (draft.Required ? " · requis" : " · optionnel");

    internal static string HumanAreaUserEventScriptNameUx2LwC(string? relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath ?? string.Empty);
        return HumanAreaUserEventTokenUx2LwC(stem.Length == 0 ? "script sans nom" : stem);
    }

    private static string HumanAreaUserEventFolderUx2LwC(string relativePath)
    {
        var folder = Path.GetDirectoryName(relativePath)?.Replace('\\', '/').Split('/').LastOrDefault();
        return HumanAreaUserEventTokenUx2LwC(folder is null or "scripts" ? "scripts du projet" : folder);
    }

    private static string HumanAreaUserEventAreaNameUx2LwC(AreaDefinition area) =>
        ContainsTechnicalIdentifierUx2LwC(area.DisplayName)
            ? "Zone sans nom lisible"
            : area.DisplayName.Trim();

    private static string HumanAreaUserEventSlugUx2LwC(string slug) =>
        ContainsTechnicalIdentifierUx2LwC(slug) ? "zone" : HumanAreaUserEventTokenUx2LwC(slug);

    private static string HumanAreaUserEventNameUx2LwC(string eventName) =>
        HumanAreaUserEventTokenUx2LwC(eventName.Split('.').LastOrDefault() ?? "événement");

    internal static string HumanAreaUserEventTokenUx2LwC(string value)
    {
        var words = value.Replace('_', ' ').Replace('-', ' ').Replace('.', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0 ? "Sans nom" :
            string.Join(' ', words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    internal static bool ContainsTechnicalIdentifierUx2LwC(string text) =>
        !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text,
            @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b",
            RegexOptions.CultureInvariant);

    private static string HumanizeAreaUserEventDiagnosticUx2LwC(string? code) => code switch
    {
        AreaUserEventValidator.SchemaUpgradeRequiredCode =>
            "Ce contrat ancien doit recevoir des champs de payload typés avant tout dispatch.",
        AreaUserEventValidator.NameInvalidCode =>
            "Le nom doit rester dans l’espace de noms de la zone et utiliser des segments lisibles.",
        AreaUserEventValidator.PayloadSchemaInvalidCode =>
            "Le schéma doit utiliser un identifiant schema://area/... sans requête ni fragment.",
        AreaUserEventValidator.PayloadFieldsInvalidCode or AreaUserEventRuntimeService.PayloadInvalidCode =>
            "Vérifiez les noms, types et valeurs du payload.",
        AreaUserEventValidator.ScriptInvalidCode or AreaUserEventRuntimeService.SourceMissingCode or
            "studio.area_user_event.script_missing" =>
            "Choisissez une source ForgeScript réelle du module.",
        AreaUserEventValidator.SubscriptionsInvalidCode or AreaUserEventRuntimeService.HandlerMissingCode or
            "studio.area_user_event.handler_missing" =>
            "Choisissez un handler réellement déclaré par le script.",
        AreaUserEventRuntimeService.ApiMismatchCode or AreaUserEventValidator.ApiUnsupportedCode =>
            "La version ForgeScript choisie n’est pas prise en charge par ce runtime.",
        AreaUserEventRuntimeService.CompileFailedCode =>
            "La source choisie ne compile pas avec ce payload. Corrigez le script avant l’application.",
        null => "La commande canonique n’a pas pu valider cette fiche.",
        _ => "La fiche contient une valeur invalide. Consultez le chemin localisé ci-dessous.",
    };
}
