using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Forge.Application.Scripting;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6IStartMarker = "// <studio:ux6-i-world-movement>";
    private const string Ux6IEndMarker = "// </studio:ux6-i-world-movement>";
    private const string Ux6IProvenance = "studio:ux6-i:world-movement-utilities";

    private static readonly IReadOnlySet<string> MovementModesUx6I =
        new HashSet<string>(["walk", "run", "fly", "swim"], StringComparer.Ordinal);

    internal StudioUx6ICatalog WorldMovementCatalogUx6I()
    {
        var project = Project;
        if (project is null) return new([], []);
        var areas = project.Areas
            .Select(area => new StudioUx6IAreaChoice(
                area.Id.Value,
                area.Slug,
                area.DisplayName,
                area.Entities.SelectMany(entity => entity.Tags)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .GroupBy(tag => tag, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(group => new StudioUx6IChoice(group.Key, HumanizeTagUx6I(group.Key),
                        $"{group.Count()} entité(s) dans {area.DisplayName}"))
                    .ToArray(),
                area.Entities.Count))
            .Where(area => area.Tags.Count > 1)
            .OrderBy(area => area.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var blueprints = project.Blueprints
            .OrderBy(blueprint => blueprint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(blueprint => new StudioUx6IChoice(blueprint.Slug, blueprint.DisplayName,
                $"Blueprint {blueprint.Kind} · origine {blueprint.PaletteOrigin}"))
            .ToArray();
        return new(areas, blueprints);
    }

    internal StudioUx6IWorldDraft? DefaultWorldMovementDraftUx6I()
    {
        var catalog = WorldMovementCatalogUx6I();
        var area = catalog.Areas.FirstOrDefault(item => item.Tags.Count > 1);
        if (area is null || catalog.Blueprints.Count == 0) return null;
        return new(area.Id, area.Tags[0].Key, area.Tags[1].Key, catalog.Blueprints[0].Key, "run",
            3, 0, 4, 0, 0, 3, 2, 0, 1, 2, 2, 5, "Gardienne", "ash-guard",
            "^[a-z-]+$", 2);
    }

    internal StudioUx6ISourcePlan BuildWorldMovementSourceUx6I(
        StudioUx6FDraft source,
        StudioUx6IWorldDraft world)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(world);
        var diagnostics = ValidateWorldMovementDraftUx6I(world);
        if (diagnostics.Count > 0)
            return new(false, "Le bloc R013 est incomplet · corrigez les champs localisés.", string.Empty,
                source.Source, diagnostics);
        var block = WorldMovementBlockUx6I(world);
        var start = source.Source.IndexOf(Ux6IStartMarker, StringComparison.Ordinal);
        var end = source.Source.IndexOf(Ux6IEndMarker, StringComparison.Ordinal);
        string candidate;
        if (start >= 0 || end >= 0)
        {
            if (start < 0 || end < start)
                return new(false, "Le bloc UX6-I existant est incomplet et reste protégé.", string.Empty,
                    source.Source,
                    [new("script.world.r013_block_invalid", "script/source", "error",
                        "Les deux marqueurs UX6-I sont requis.")]);
            candidate = source.Source[..start] + block + source.Source[(end + Ux6IEndMarker.Length)..];
        }
        else
        {
            candidate = source.Source.TrimEnd() + Environment.NewLine + Environment.NewLine + block +
                        Environment.NewLine;
        }
        return new(true,
            "Bloc R013 versionné généré · relisez-le puis insérez-le explicitement dans le brouillon.",
            block, candidate, []);
    }

    internal StudioUx6IRuntimeResult PreviewWorldMovementUx6I(
        StudioUx6FDraft source,
        StudioUx6IWorldDraft world)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(world);
        var diagnostics = ValidateWorldMovementDraftUx6I(world);
        if (diagnostics.Count > 0)
            return FailedRuntimeUx6I("Simulation R013 refusée · valeurs ou références invalides.", diagnostics);
        var project = Project;
        var area = project?.Areas.SingleOrDefault(item => item.Id.Value == world.AreaId);
        if (project is null || area is null)
            return FailedRuntimeUx6I("La zone sélectionnée n’existe plus dans le projet.",
                [new("script.world.area_missing", "script/world/area", "error",
                    "Sélectionnez une zone réelle.")]);

        try
        {
            var runtime = new NativeForgeScriptRuntime();
            var nativeSource = NativeWorldMovementSourceUx6I(world);
            var compilation = runtime.Compile(nativeSource);
            if (!compilation.Success || compilation.Artifact is not ForgeScriptArtifact artifact)
                return FailedRuntimeUx6I("La source R013 native ne compile pas.",
                    compilation.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray());
            var execution = runtime.Execute(artifact, new ForgeScriptQuotas(40_000, 1000, 32, 8));
            var recovered = runtime.Execute(artifact, new ForgeScriptQuotas(40_000, 1000, 32, 8));
            var abusiveCompilation = runtime.Compile("void main() { while (true) { int value = 1; } }");
            var abusive = abusiveCompilation.Artifact is ForgeScriptArtifact abusiveArtifact
                ? runtime.Execute(abusiveArtifact, new ForgeScriptQuotas(300, 1000, 32, 8))
                : new ForgeScriptExecutionResult(ForgeScriptExecutionStatus.Failed, [],
                    abusiveCompilation.Diagnostics, null);
            const string forbiddenSource = "void main() { file_open(\"project.json\"); }";
            var forbiddenDiagnostics = ForgeScriptCapabilityManifest.ValidateSource(forbiddenSource,
                source.Id);
            var forbiddenCompilation = runtime.Compile(forbiddenSource);

            var blueprints = project.Blueprints.ToDictionary(item => item.Slug, item => item.Id,
                StringComparer.Ordinal);
            var commandService = new ForgeScriptWorldCommandService([area], blueprints);
            var command = new ForgeScriptWorldIntentCommand(
                Guid.Parse("6e000000-0000-4000-8000-000000000091"), 0, Ux6IProvenance,
                execution.Events, true);
            var preview = commandService.Preview(command);
            var dryRun = preview.Succeeded && commandService.State.Revision == 0 &&
                         !commandService.CanUndo && !commandService.CanRedo;
            var applied = commandService.Apply(command);
            var undone = commandService.Undo();
            var redone = commandService.Redo();
            var invalid = commandService.Apply(new ForgeScriptWorldIntentCommand(
                Guid.Parse("6e000000-0000-4000-8000-000000000092"), 1, Ux6IProvenance,
                [new ForgeScriptEvent("world.raw_engine_pointer", "1")], true));
            var stale = commandService.Apply(command with
            {
                TransactionId = Guid.Parse("6e000000-0000-4000-8000-000000000093"),
                ExpectedRevision = 0,
            });

            var events = execution.Events;
            var areaAndQueries = events.Count(item => item.Kind == "world.area") == 1 &&
                                 events.Count(item => item.Kind == "world.query") >= 2 &&
                                 redone.State.AreaHandles.Count == 1 && redone.State.EntityHandles.Count >= 2;
            var spawnDestroy = events.Any(item => item.Kind == "world.spawn") &&
                               events.Any(item => item.Kind == "world.destroy") &&
                               redone.State.Areas.Single().Entities.Count == area.Entities.Count;
            var movement = events.Count(item => item.Kind == "movement.started") == 3 &&
                           redone.State.MovementTasks.Count == 3;
            var teleportedVelocity = events.Any(item => item.Kind == "movement.teleported") &&
                                     events.Any(item => item.Kind == "movement.velocity") &&
                                     redone.State.Velocities.Count == 1;
            var cancelled = events.Any(item => item.Kind == "movement.completed") &&
                            redone.State.MovementTasks.Values.Count(item =>
                                item.Status == ScriptMovementTaskStatus.Cancelled) == 1;
            var utilities = events.Any(item => item.Kind == "say" &&
                                                item.Value == world.LocalInteger.ToString(
                                                    CultureInfo.InvariantCulture)) &&
                            events.Count(item => item.Kind == "say") >= 3;
            var unicodeRegex = events.Any(item => item.Kind == "say" && item.Value == "unicode-regex-ok");
            var locals = events.Count(item => item.Kind == "local.set") == 5;
            var manifest = ForgeScriptCapabilityManifest.SchemaVersion == 1 &&
                           ForgeScriptCapabilityManifest.ApiVersion == "1.0.0" &&
                           new[] { "world", "movement", "utilities", "blackboard" }.All(id =>
                               ForgeScriptCapabilityManifest.Capabilities.Any(item => item.Id == id));
            var forbidden = forbiddenDiagnostics.Any(item =>
                                item.Code == "script.sandbox.capability_forbidden") &&
                            !forbiddenCompilation.Success;
            var quota = abusive.Status == ForgeScriptExecutionStatus.QuotaExceeded;
            var runtimeRecovered = recovered.Status == ForgeScriptExecutionStatus.Completed;
            var nativeCompleted = execution.Status == ForgeScriptExecutionStatus.Completed &&
                                  execution.Diagnostics.Count == 0;
            var commandHistory = applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                                 undone.Revision == 0 && redone.Revision == 1;
            var invalidBlocked = !invalid.Succeeded && invalid.Diagnostics.Any(item =>
                item.Code == "script.world.intent_invalid") && commandService.State.Revision == 1;
            var staleBlocked = !stale.Succeeded && stale.Diagnostics.Any(item =>
                item.Code == "script.world.revision_conflict") && commandService.State.Revision == 1;
            var succeeded = manifest && forbidden && quota && runtimeRecovered && nativeCompleted &&
                            areaAndQueries && spawnDestroy && movement && teleportedVelocity && cancelled &&
                            utilities && unicodeRegex && locals && dryRun && commandHistory &&
                            invalidBlocked && staleBlocked;
            var resultDiagnostics = execution.Diagnostics.Select(ToStudioDiagnosticUx6F)
                .Concat(preview.Diagnostics.Select(item => new StudioUx6FDiagnostic(item.Code, item.Path,
                    item.Severity, item.Message)))
                .ToArray();
            return new(succeeded,
                succeeded
                    ? "R013 simulé : monde, mouvement, utilitaires, variables locales et sandbox sont cohérents."
                    : "La simulation R013 n’a pas satisfait tous les invariants.",
                manifest, forbidden, quota, runtimeRecovered, nativeCompleted, areaAndQueries,
                spawnDestroy, movement, teleportedVelocity, cancelled, utilities, unicodeRegex, locals,
                dryRun, commandHistory, invalidBlocked, staleBlocked, preview.SemanticDiff,
                events.Select(item => $"{item.Kind} · {item.Value}").ToArray(), resultDiagnostics);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or
                                           FormatException or OverflowException or DllNotFoundException or
                                           BadImageFormatException)
        {
            return FailedRuntimeUx6I($"Simulation R013 refusée : {exception.Message}",
                [new("script.world.r013_preview_failed", "script/world/r013", "error", exception.Message)]);
        }
    }

    internal StudioUx6ISmokeResult RunUx6IContractSmoke()
    {
        var sourceProject = Project;
        if (Session is null || sourceProject is null || History is null) return FailedSmokeUx6I();
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-i",
            Guid.NewGuid().ToString("N"));
        var released = false;
        StudioUx6ISmokeResult result;
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded) return FailedSmokeUx6I();
            var document = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(document);
            var editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(document.RelativePath, out var initial, out _) ||
                initial is null)
                return FailedSmokeUx6I();
            var catalog = editing.WorldMovementCatalogUx6I();
            var world = editing.DefaultWorldMovementDraftUx6I();
            if (world is null) return FailedSmokeUx6I();
            var invalidBlocked = !editing.BuildWorldMovementSourceUx6I(initial,
                world with { TargetTag = world.SubjectTag }).Succeeded;
            var plan = editing.BuildWorldMovementSourceUx6I(initial, world);
            var candidate = initial with { Source = plan.CandidateSource };
            var runtime = editing.PreviewWorldMovementUx6I(candidate, world);
            var preview = editing.PreviewScriptSourceUx6F(candidate);
            var compilation = editing.CompileScriptSourceUx6F(candidate);
            var sandbox = editing.RunScriptSandboxUx6F(candidate);
            var stale = !editing.ApplyApprovedScriptSourceUx6F(candidate with
            {
                Source = candidate.Source + Environment.NewLine + "// approbation périmée",
            }).Succeeded;
            editing.PreviewScriptSourceUx6F(candidate);
            var applied = editing.ApplyApprovedScriptSourceUx6F(candidate);
            var undone = editing.UndoScriptSourceUx6F();
            var undoDocument = new ForgeScriptSourceStore(root).Load(candidate.RelativePath);
            var redone = editing.RedoScriptSourceUx6F();
            var redoDocument = new ForgeScriptSourceStore(root).Load(candidate.RelativePath);
            var reopened = new StudioSessionController();
            var persistence = reopened.Open(root).Succeeded &&
                              reopened.TryOpenCanonicalSourceUx6F(candidate.RelativePath,
                                  out var reopenedDraft, out _) && reopenedDraft?.Source == candidate.Source;
            reopened.Close();
            var applyUndoRedo = applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                                undoDocument.Source == document.Source && redoDocument.Source == candidate.Source;
            var neighbors = ReferenceEquals(editing.Project, editing.Session?.Project) &&
                            editing.Project is not null &&
                            string.Equals(ProjectJsonSerializer.Serialize(editing.Project),
                                ProjectJsonSerializer.Serialize(sourceProject), StringComparison.Ordinal);
            var sharedAuthority = initial.Id == document.Id && candidate.Id == document.Id &&
                                  editing.CurrentScriptDraftUx6F()?.Id == document.Id;
            var worldApi = invalidBlocked && runtime.AreaAndQueries && runtime.SpawnAndDestroy;
            var movementApi = runtime.MovementTasks && runtime.TeleportAndVelocity &&
                              runtime.MovementCancelled;
            var utilityApi = runtime.UtilitiesDeterministic && runtime.UnicodeAndRegex;
            var sandboxBoundary = runtime.ManifestReady && runtime.ForbiddenCapabilityBlocked &&
                                  runtime.QuotaInterrupted && runtime.RuntimeRecovered;
            var succeeded = catalog.Complete && plan.Succeeded && runtime.Succeeded && worldApi &&
                            movementApi && utilityApi && runtime.TypedLocals && sandboxBoundary &&
                            preview.Succeeded && compilation.Succeeded && sandbox.Succeeded && stale &&
                            applyUndoRedo && persistence && neighbors;
            result = new(succeeded, true, sharedAuthority, catalog.Complete,
                plan.ReviewedSource.Contains("current_area", StringComparison.Ordinal), worldApi,
                movementApi, utilityApi, runtime.TypedLocals, sandboxBoundary, preview.CanonicalDryRun,
                compilation.Succeeded, sandbox.Succeeded, stale, applyUndoRedo, persistence, neighbors, false);
            editing.Close();
        }
        finally
        {
            released = DeleteUx6FTemporaryWorkspace(root);
        }
        return result with
        {
            Succeeded = result.Succeeded && released,
            TemporaryWorkspaceReleased = released,
        };
    }

    private IReadOnlyList<StudioUx6FDiagnostic> ValidateWorldMovementDraftUx6I(
        StudioUx6IWorldDraft world)
    {
        var catalog = WorldMovementCatalogUx6I();
        var area = catalog.Areas.FirstOrDefault(item => item.Id == world.AreaId);
        var diagnostics = new List<StudioUx6FDiagnostic>();
        Add(area is null, "script.world.area_missing", "script/world/area",
            "Choisissez une zone réelle contenant au moins deux tags.");
        Add(area is not null && !area.Tags.Any(item => item.Key == world.SubjectTag),
            "script.world.subject_missing", "script/world/query/subject", "Choisissez un tag sujet réel.");
        Add(area is not null && (!area.Tags.Any(item => item.Key == world.TargetTag) ||
                                 world.SubjectTag == world.TargetTag),
            "script.world.target_invalid", "script/world/query/target",
            "Choisissez un second tag réel distinct.");
        Add(!catalog.Blueprints.Any(item => item.Key == world.SpawnBlueprint),
            "script.world.blueprint_missing", "script/world/spawn/blueprint",
            "Choisissez un blueprint réel du projet.");
        Add(!MovementModesUx6I.Contains(world.MovementMode), "script.movement.mode_invalid",
            "script/movement/mode", "Le mode doit être marche, course, vol ou nage.");
        var numbers = new[]
        {
            world.DestinationX, world.DestinationY, world.DestinationZ,
            world.VelocityX, world.VelocityY, world.VelocityZ,
            world.SpawnX, world.SpawnY, world.SpawnZ, world.FollowDistance, world.LocalNumber,
        };
        Add(numbers.Any(value => !double.IsFinite(value)) || world.FollowDistance is < 0 or > 512,
            "script.world.numeric_invalid", "script/world/values",
            "Les vecteurs et nombres doivent être finis; la distance de suivi reste entre 0 et 512.");
        Add(string.IsNullOrWhiteSpace(world.LocalText) ||
            Encoding.UTF8.GetByteCount(world.LocalText) > ForgeScriptCapabilityManifest.MaximumStringBytes,
            "script.local.string_invalid", "script/locals/text",
            "Le texte local est requis et doit respecter le quota UTF-8.");
        Add(string.IsNullOrWhiteSpace(world.RegexValue) || string.IsNullOrWhiteSpace(world.RegexPattern) ||
            Encoding.UTF8.GetByteCount(world.RegexValue) > ForgeScriptCapabilityManifest.MaximumRegexInputBytes ||
            Encoding.UTF8.GetByteCount(world.RegexPattern) >
            ForgeScriptCapabilityManifest.MaximumRegexPatternBytes || !ValidRegexUx6I(world.RegexPattern),
            "script.utility.regex_invalid", "script/utilities/regex",
            "Valeur et expression régulière doivent respecter les quotas et une syntaxe valide.");
        Add(world.FloatPrecision is < 0 or > 9, "script.utility.precision_invalid",
            "script/utilities/precision", "La précision doit rester entre 0 et 9.");
        return diagnostics;

        void Add(bool condition, string code, string path, string message)
        {
            if (condition) diagnostics.Add(new(code, path, "error", message));
        }
    }

    private static string WorldMovementBlockUx6I(StudioUx6IWorldDraft world) =>
        $"{Ux6IStartMarker}{Environment.NewLine}" +
        "// API ForgeScript 1.0.0 · R013 · bloc auteur revu.\n" +
        "void StudioWorldMovementScenario()\n" +
        "{\n" +
        "    AreaRef area = current_area();\n" +
        $"    EntityRef subject = find_entity(area, \"{QuoteUx6I(world.SubjectTag)}\");\n" +
        $"    EntityRef target = find_entity(area, \"{QuoteUx6I(world.TargetTag)}\");\n" +
        $"    Vec3 destination = vec3({FloatUx6I(world.DestinationX)}, {FloatUx6I(world.DestinationY)}, {FloatUx6I(world.DestinationZ)});\n" +
        "    Vec3 direction = vec_normalize(destination);\n" +
        "    Quat rotation = quat_normalize(quat(0.0f, 0.0f, 0.0f, 1.0f));\n" +
        "    float distance = vec_length(destination);\n" +
        "    float alignment = vec_dot(direction, direction);\n" +
        $"    local_set_int(subject, \"studio.stage\", {world.LocalInteger});\n" +
        $"    local_set_float(subject, \"studio.speed\", {DoubleUx6I(world.LocalNumber)});\n" +
        $"    local_set_string(subject, \"studio.title\", \"{QuoteUx6I(world.LocalText)}\");\n" +
        "    local_set_entity(subject, \"studio.target\", target);\n" +
        "    local_set_vector(subject, \"studio.home\", destination);\n" +
        "    int64 stage = local_get_int(subject, \"studio.stage\", 0);\n" +
        "    double speed = local_get_float(subject, \"studio.speed\", 0);\n" +
        "    string title = local_get_string(subject, \"studio.title\", \"missing\");\n" +
        "    EntityRef localTarget = local_get_entity(subject, \"studio.target\", 0);\n" +
        "    Vec3 home = local_get_vector(subject, \"studio.home\", vec3(0.0f, 0.0f, 0.0f));\n" +
        $"    MovementTaskRef move = move_to(subject, home, \"{QuoteUx6I(world.MovementMode)}\");\n" +
        "    MovementTaskRef face = face_towards(subject, destination);\n" +
        $"    MovementTaskRef escort = follow(subject, localTarget, {FloatUx6I(world.FollowDistance)});\n" +
        $"    set_velocity(subject, vec3({FloatUx6I(world.VelocityX)}, {FloatUx6I(world.VelocityY)}, {FloatUx6I(world.VelocityZ)}));\n" +
        "    teleport(subject, area, destination);\n" +
        $"    EntityRef spawned = spawn_at(area, \"{QuoteUx6I(world.SpawnBlueprint)}\", vec3({FloatUx6I(world.SpawnX)}, {FloatUx6I(world.SpawnY)}, {FloatUx6I(world.SpawnZ)}));\n" +
        "    Vec3 observed = entity_position(spawned);\n" +
        "    destroy_entity(spawned);\n" +
        "    cancel_movement(move);\n" +
        $"    bool valid = regex_match(\"{QuoteUx6I(world.RegexValue)}\", \"{QuoteUx6I(world.RegexPattern)}\");\n" +
        "    uint letters = unicode_length(title);\n" +
        "    say(format_int(stage));\n" +
        $"    say(format_float(speed + distance + alignment + rotation.w + observed.y, {world.FloatPrecision}));\n" +
        "    if (valid && letters > 0) { say(\"unicode-regex-ok\"); }\n" +
        "}\n" + Ux6IEndMarker;

    private static string NativeWorldMovementSourceUx6I(StudioUx6IWorldDraft world)
    {
        var block = WorldMovementBlockUx6I(world);
        var body = block[(block.IndexOf("void StudioWorldMovementScenario()", StringComparison.Ordinal))..];
        body = body.Replace("void StudioWorldMovementScenario()", "void main()", StringComparison.Ordinal);
        var marker = body.LastIndexOf(Ux6IEndMarker, StringComparison.Ordinal);
        return marker >= 0 ? body[..marker].TrimEnd() : body;
    }

    private static string QuoteUx6I(string value) => (value ?? string.Empty)
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    private static string FloatUx6I(double value)
    {
        var literal = value.ToString("0.######", CultureInfo.InvariantCulture);
        return literal.Contains(".", StringComparison.Ordinal) ? literal + "f" : literal + ".0f";
    }

    private static string DoubleUx6I(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static bool ValidRegexUx6I(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string HumanizeTagUx6I(string tag)
    {
        var text = tag.Replace('-', ' ').Replace('_', ' ').Trim();
        return text.Length == 0 ? "Tag protégé" : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
    }

    private static StudioUx6IRuntimeResult FailedRuntimeUx6I(
        string message,
        IReadOnlyList<StudioUx6FDiagnostic> diagnostics) =>
        new(false, message, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, "no-change", [], diagnostics);

    private static StudioUx6ISmokeResult FailedSmokeUx6I() =>
        new(false, false, false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false);
}
