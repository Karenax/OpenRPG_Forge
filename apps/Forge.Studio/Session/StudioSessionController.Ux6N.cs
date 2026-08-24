using System.Text;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Scripting;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6NStartMarker = "// <studio:ux6-n-live-tools>";
    private const string Ux6NEndMarker = "// </studio:ux6-n-live-tools>";

    internal StudioUx6NLiveDraft DefaultLiveToolsDraftUx6N() => new(
        "forgotten-forest",
        1,
        2,
        "sha256:active-reviewed",
        "sha256:candidate-reviewed",
        "score",
        "int",
        ["dialogue"],
        ["quest"],
        true,
        true,
        "debug:studio-r099",
        "server-admin",
        true,
        "OnUse",
        "scripts/runtime-tools.as",
        12,
        "count",
        2,
        "hero.mana",
        5,
        "hero",
        "entity:hero",
        "OnUse",
        "over");

    internal StudioUx6NSourcePlan BuildLiveToolsSourceUx6N(
        StudioUx6FDraft source,
        StudioUx6NLiveDraft live)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(live);
        var diagnostics = ValidateLiveToolsUx6N(live);
        if (diagnostics.Count > 0)
            return new(false, "Le scénario hot reload/débogueur est incomplet.", string.Empty,
                source.Source, diagnostics);

        var block = LiveToolsBlockUx6N(live);
        var start = source.Source.IndexOf(Ux6NStartMarker, StringComparison.Ordinal);
        var end = source.Source.IndexOf(Ux6NEndMarker, StringComparison.Ordinal);
        string candidate;
        if (start >= 0 || end >= 0)
        {
            if (start < 0 || end < start)
                return new(false, "Le bloc UX6-N existant est incomplet et reste protégé.",
                    string.Empty, source.Source,
                    [new("script.live_tools.block_invalid", "script/source", "error",
                        "Les deux marqueurs UX6-N sont requis.")]);
            candidate = source.Source[..start] + block +
                        source.Source[(end + Ux6NEndMarker.Length)..];
        }
        else
        {
            candidate = source.Source.TrimEnd() + Environment.NewLine + Environment.NewLine +
                        block + Environment.NewLine;
        }

        return new(true,
            "Bloc R099 hot reload/débogueur généré · relisez-le avant insertion.",
            block, candidate, []);
    }

    internal StudioUx6NLiveResult PreviewLiveToolsUx6N(
        StudioUx6FDraft source,
        StudioUx6NLiveDraft live)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(live);
        var diagnostics = ValidateLiveToolsUx6N(live);
        if (diagnostics.Count > 0)
            return FailedLiveToolsUx6N("Simulation refusée · paramètres invalides.", diagnostics);

        try
        {
            var active = ModuleUx6N(live, candidate: false);
            var candidate = ModuleUx6N(live, candidate: true);
            var deferred = ForgeScriptRuntimeTools.Reload(active, candidate, false, (_, _) => true);
            var applied = ForgeScriptRuntimeTools.Reload(active, candidate,
                live.AtHandlerBoundary, (_, _) => live.MigrationSucceeds);
            var rollback = ForgeScriptRuntimeTools.Reload(active, candidate, true, (_, _) => false);
            var invalid = ForgeScriptRuntimeTools.Reload(active,
                candidate with { Version = active.Version }, true, (_, _) => true);
            var expectedDependencies = active.Dependencies.Union(candidate.Dependencies)
                .Order(StringComparer.Ordinal).ToArray();

            var document = ToDocumentUx6F(source);
            var adapter = new ForgeScriptRuntimeToolAdapter();
            var resolution = adapter.Resolve(document, true);
            if (!resolution.Available || resolution.Handle is null)
                return FailedLiveToolsUx6N("L’adaptateur R099 ne résout pas la source canonique.",
                    [new("script.live_tools.adapter_missing", "script/runtime-tools", "error",
                        resolution.DiagnosticCode ?? "Adaptateur indisponible.")]);
            var initialState = adapter.Build(resolution.Handle, document,
                new AudioState(new Dictionary<string, AudioCue>(StringComparer.Ordinal),
                    "camera", "default", []),
                new WorldClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    TimeSpan.Zero, 1, 0), active);
            var adapterReload = adapter.Reload(resolution.Handle, initialState, candidate,
                live.AtHandlerBoundary, (_, _) => live.MigrationSucceeds);

            var frame = new DebugFrame(live.Function, live.SourcePath, live.SourceLine,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [live.LocalName] = live.LocalValue,
                });
            var breakpoint = $"{live.SourcePath}:{live.SourceLine}";
            var snapshot = ForgeScriptRuntimeTools.Break(live.DebugSessionId, live.DebugAuthority,
                [frame], new Dictionary<string, object?>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [live.EntityAlias] = live.EntityReference,
                }, [live.EventName], new HashSet<string>([breakpoint], StringComparer.Ordinal),
                live.RemoteDebug);
            var watched = ForgeScriptRuntimeTools.SetWatch(snapshot,
                live.WatchExpression, live.WatchValue);
            var inspected = ForgeScriptRuntimeTools.InspectEntity(watched, live.EntityAlias);
            var stepped = ForgeScriptRuntimeTools.Step(watched, live.StepMode);
            var continued = ForgeScriptRuntimeTools.Step(watched, "continue");
            var permissionBlocked = ThrowsLiveUx6N<UnauthorizedAccessException>(() =>
                ForgeScriptRuntimeTools.Break("debug:denied", "player", [frame],
                    new Dictionary<string, object?>(), new Dictionary<string, string>(), []));
            var remoteBlocked = ThrowsLiveUx6N<InvalidOperationException>(() =>
                ForgeScriptRuntimeTools.Break("debug:remote-denied", "local-author", [frame],
                    new Dictionary<string, object?>(), new Dictionary<string, string>(), [],
                    new HashSet<string>(), true));
            var adapterDebug = adapter.Break(resolution.Handle, adapterReload.State,
                live.DebugSessionId, live.DebugAuthority, [frame],
                new Dictionary<string, object?> { [live.WatchExpression] = live.WatchValue },
                new Dictionary<string, string> { [live.EntityAlias] = live.EntityReference },
                [live.EventName], new HashSet<string>([breakpoint], StringComparer.Ordinal),
                live.RemoteDebug);
            adapterDebug = adapter.Step(resolution.Handle, adapterDebug, live.StepMode);

            var apiNames = ForgeScriptApiCatalog.Registrations.Select(item => item.Name)
                .ToHashSet(StringComparer.Ordinal);
            var apiCatalog = new[] { "hot_reload_request", "debug_break", "debug_step" }
                .All(apiNames.Contains);

            var native = new NativeForgeScriptRuntime();
            var compilation = native.Compile(NativeLiveToolsSourceUx6N(live));
            var nativeDiagnostics = compilation.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray();
            if (!compilation.Success || compilation.Artifact is not ForgeScriptArtifact artifact)
                return FailedLiveToolsUx6N("Le scénario natif UX6-N ne compile pas.", nativeDiagnostics);
            var execution = native.Execute(artifact,
                new ForgeScriptQuotas(30_000, 1000, 32, 8, 8 * 1024 * 1024, 2026));
            var executionDiagnostics = execution.Diagnostics.Select(ToStudioDiagnosticUx6F).ToArray();
            var nativeExecution = execution.Status == ForgeScriptExecutionStatus.Completed &&
                                  executionDiagnostics.Length == 0;
            var nativeReload = execution.Events.Any(item => item.Kind == "hot-reload.requested");
            var nativeDebug = execution.Events.Any(item => item.Kind == "debug.break") &&
                              execution.Events.Any(item => item.Kind == "debug.step");

            var reloadDeferred = !deferred.Applied && deferred.Strategy == "deferred" &&
                                 deferred.Active == active;
            var reloadApplied = applied.Applied && applied.Active == candidate &&
                                applied.Strategy == "migrated";
            var dependenciesInvalidated = applied.Invalidated.SequenceEqual(expectedDependencies);
            var migrationRollback = !rollback.Applied && rollback.Strategy == "restart" &&
                                    rollback.Active == active;
            var invalidVersionBlocked = !invalid.Applied && invalid.Strategy == "restart" &&
                                        invalid.Diagnostic == "SCRIPT_HOT_RELOAD_VERSION_INVALID";
            var adapterReloadReady = adapterReload.Result.Applied &&
                                     adapterReload.State.ActiveModule.Version == live.CandidateVersion;
            var debugSession = snapshot.State == "paused" &&
                               snapshot.SessionId == live.DebugSessionId &&
                               snapshot.Remote == live.RemoteDebug;
            var breakpoints = snapshot.Breakpoints.Contains(breakpoint);
            var stackAndLocals = snapshot.Stack.Count == 1 &&
                                 snapshot.Stack[0].Locals.TryGetValue(live.LocalName, out var local) &&
                                 Equals(local, live.LocalValue);
            var watches = watched.Watches.TryGetValue(live.WatchExpression, out var watch) &&
                          Equals(watch, live.WatchValue);
            var entityInspection = inspected == live.EntityReference;
            var eventsReady = snapshot.Events.Contains(live.EventName, StringComparer.Ordinal) &&
                              stepped.Events.Contains($"debug-step:{live.StepMode}", StringComparer.Ordinal);
            var guardedStepping = (live.StepMode == "continue" ? stepped.State == "running" :
                                      stepped.State == "paused") && continued.State == "running";
            var remotePermission = !live.RemoteDebug || live.DebugAuthority == "server-admin";
            var invalidPermissionBlocked = permissionBlocked && remoteBlocked;
            var adapterDebugger = adapterDebug.Debugger is not null &&
                                  adapterDebug.Debugger.Breakpoints.Contains(breakpoint) &&
                                  adapterDebug.Debugger.Revision == 1;
            var succeeded = reloadDeferred && reloadApplied && dependenciesInvalidated &&
                            migrationRollback && invalidVersionBlocked && adapterReloadReady &&
                            debugSession && breakpoints && stackAndLocals && watches &&
                            entityInspection && eventsReady && guardedStepping && remotePermission &&
                            invalidPermissionBlocked && adapterDebugger && apiCatalog &&
                            compilation.Success && nativeExecution && nativeReload && nativeDebug;
            var trace = new[]
            {
                $"reload différé · {deferred.Diagnostic}",
                $"reload appliqué · version {applied.Active.Version} · invalidations {string.Join(", ", applied.Invalidated)}",
                $"rollback · {rollback.Diagnostic}",
                $"debug · {snapshot.Authority} · {(snapshot.Remote ? "distant" : "local")} · {breakpoint}",
                $"watch · {live.WatchExpression} = {live.WatchValue}",
                $"entité · {live.EntityAlias} → {inspected}",
            }.Concat(execution.Events.Select(item => $"natif · {item.Kind} · {item.Value}")).ToArray();
            return new(succeeded,
                succeeded
                    ? "Hot reload et débogueur R099 simulés puis exécutés dans la sandbox native."
                    : "Preuve R099 incomplète · consultez les sous-invariants.",
                reloadDeferred, reloadApplied, dependenciesInvalidated, migrationRollback,
                invalidVersionBlocked, adapterReloadReady, debugSession, breakpoints,
                stackAndLocals, watches, entityInspection, eventsReady, guardedStepping,
                remotePermission, invalidPermissionBlocked, adapterDebugger, apiCatalog,
                compilation.Success, nativeExecution, nativeReload, nativeDebug,
                trace, executionDiagnostics);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or
                                           UnauthorizedAccessException or OverflowException or
                                           DllNotFoundException or BadImageFormatException)
        {
            return FailedLiveToolsUx6N($"Simulation UX6-N refusée : {exception.Message}",
                [new("script.live_tools.failed", "script/runtime-tools", "error", exception.Message)]);
        }
    }

    internal StudioUx6NSmokeResult RunUx6NContractSmoke()
    {
        var sourceSession = Session;
        var sourceProject = Project;
        var sourceHistory = History;
        if (sourceSession is null || sourceProject is null || sourceHistory is null)
            return FailedSmokeUx6N();
        var sourceRevision = sourceHistory.State.Revision;
        var sourceJournal = sourceHistory.Journal.ToArray();
        var sourceDirty = IsDirty;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-n",
            Guid.NewGuid().ToString("N"));
        var released = false;
        StudioSessionController? editing = null;
        StudioUx6NSmokeResult result;
        try
        {
            if (!ProjectSession.Create(root, sourceProject).Succeeded)
                return FailedSmokeUx6N();
            var document = EvidenceDocumentUx6F();
            new ForgeScriptSourceStore(root).Save(document);
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded ||
                !editing.TryOpenCanonicalSourceUx6F(document.RelativePath, out var initial, out _) ||
                initial is null)
                return FailedSmokeUx6N();
            var draft = editing.DefaultLiveToolsDraftUx6N();
            var invalidVersion = !editing.BuildLiveToolsSourceUx6N(initial,
                draft with { CandidateVersion = draft.ActiveVersion }).Succeeded;
            var invalidPermission = !editing.BuildLiveToolsSourceUx6N(initial,
                draft with { RemoteDebug = true, DebugAuthority = "local-author" }).Succeeded;
            var plan = editing.BuildLiveToolsSourceUx6N(initial, draft);
            var candidate = initial with { Source = plan.CandidateSource };
            var simulation = editing.PreviewLiveToolsUx6N(candidate, draft);
            var preview = editing.PreviewScriptSourceUx6F(candidate);
            var compilation = editing.CompileScriptSourceUx6F(candidate);
            var stale = !editing.ApplyApprovedScriptSourceUx6F(candidate with
            {
                BaseRevision = candidate.BaseRevision + 1,
            }).Succeeded;
            editing.PreviewScriptSourceUx6F(candidate);
            var applied = editing.ApplyApprovedScriptSourceUx6F(candidate);
            var undone = editing.UndoScriptSourceUx6F();
            var redone = editing.RedoScriptSourceUx6F();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(root).Succeeded &&
                            reopened.TryOpenCanonicalSourceUx6F(candidate.RelativePath,
                                out var reopenedDraft, out _) &&
                            reopenedDraft?.Source == candidate.Source;
            reopened.Close();
            var sourcePreserved = ReferenceEquals(sourceSession, Session) &&
                                  ReferenceEquals(sourceProject, Project) &&
                                  ReferenceEquals(sourceHistory, History) &&
                                  History.State.Revision == sourceRevision &&
                                  History.Journal.SequenceEqual(sourceJournal) &&
                                  IsDirty == sourceDirty;
            result = new(
                plan.Succeeded && simulation.Succeeded && preview.Succeeded && preview.CanApply &&
                compilation.Succeeded && invalidVersion && invalidPermission && stale &&
                applied.Succeeded && undone.Succeeded && redone.Succeeded && persisted && sourcePreserved,
                true,
                candidate.Id == initial.Id && candidate.RelativePath == initial.RelativePath,
                plan.ReviewedSource.Contains("<studio:ux6-n-live-tools>", StringComparison.Ordinal),
                simulation.ReloadDeferred && simulation.ReloadApplied &&
                simulation.DependenciesInvalidated && simulation.MigrationRollback,
                simulation.DebugSession && simulation.Breakpoints && simulation.StackAndLocals &&
                simulation.Watches && simulation.EntityInspection && simulation.GuardedStepping,
                simulation.AdapterReload && simulation.AdapterDebugger,
                simulation.NativeCompilation && simulation.NativeExecution &&
                simulation.NativeReloadIntent && simulation.NativeDebugIntents,
                invalidVersion,
                invalidPermission,
                preview.CanonicalDryRun,
                compilation.Succeeded,
                stale,
                applied.Succeeded && undone.Succeeded && redone.Succeeded,
                persisted,
                sourcePreserved,
                false);
        }
        finally
        {
            editing?.Close();
            released = DeleteUx6FTemporaryWorkspace(root);
        }

        return result with
        {
            Succeeded = result.Succeeded && released,
            TemporaryWorkspaceReleased = released,
        };
    }

    private static IReadOnlyList<StudioUx6FDiagnostic> ValidateLiveToolsUx6N(StudioUx6NLiveDraft live)
    {
        var diagnostics = new List<StudioUx6FDiagnostic>();
        Add(!SafeTokenUx6N(live.ModuleId) || live.ActiveVersion <= 0 ||
            live.CandidateVersion <= live.ActiveVersion,
            "script.hot_reload.version_invalid", "script/runtime/reload/version",
            "Module et versions croissantes sont requis.");
        Add(string.IsNullOrWhiteSpace(live.ActiveSourceHash) ||
            string.IsNullOrWhiteSpace(live.CandidateSourceHash) ||
            live.ActiveSourceHash.Length > 128 || live.CandidateSourceHash.Length > 128 ||
            string.Equals(live.ActiveSourceHash, live.CandidateSourceHash, StringComparison.Ordinal),
            "script.hot_reload.hash_invalid", "script/runtime/reload/sourceHash",
            "Les empreintes active et candidate doivent être distinctes et bornées.");
        Add(!SafeTokenUx6N(live.StateKey) || !SafeTokenUx6N(live.StateType),
            "script.hot_reload.schema_invalid", "script/runtime/reload/stateSchema",
            "Clé et type d’état sont requis.");
        Add(!ValidDependenciesUx6N(live.ActiveDependencies) ||
            !ValidDependenciesUx6N(live.CandidateDependencies),
            "script.hot_reload.dependencies_invalid", "script/runtime/reload/dependencies",
            "Les dépendances doivent être lisibles, uniques et bornées.");
        Add(live.DebugAuthority is not ("local-author" or "server-admin") ||
            live.RemoteDebug && live.DebugAuthority != "server-admin",
            "script.debug.authority_invalid", "script/runtime/debug/authority",
            "Le débogage distant exige l’autorité administrateur serveur.");
        Add(string.IsNullOrWhiteSpace(live.DebugSessionId) || live.DebugSessionId.Length > 128 ||
            !SafeTokenUx6N(live.Function) ||
            !ForgeScriptSourceValidator.IsSafeSourcePath(live.SourcePath) ||
            live.SourceLine is <= 0 or > 1_000_000,
            "script.debug.frame_invalid", "script/runtime/debug/frame",
            "Session, fonction, source sûre et ligne positive sont requises.");
        Add(!SafeTokenUx6N(live.LocalName) || string.IsNullOrWhiteSpace(live.WatchExpression) ||
            live.WatchExpression.Length > 256 || !SafeTokenUx6N(live.EntityAlias) ||
            string.IsNullOrWhiteSpace(live.EntityReference) || live.EntityReference.Length > 128 ||
            !SafeTokenUx6N(live.EventName),
            "script.debug.inspectors_invalid", "script/runtime/debug/inspectors",
            "Locale, watch, entité et événement doivent rester lisibles et bornés.");
        Add(live.StepMode is not ("into" or "over" or "out" or "continue"),
            "script.debug.step_invalid", "script/runtime/debug/step",
            "Choisissez into, over, out ou continue.");
        return diagnostics;

        void Add(bool condition, string code, string path, string message)
        {
            if (condition) diagnostics.Add(new(code, path, "error", message));
        }
    }

    private static HotReloadModule ModuleUx6N(StudioUx6NLiveDraft live, bool candidate) => new(
        live.ModuleId,
        candidate ? live.CandidateVersion : live.ActiveVersion,
        new Dictionary<string, string>(StringComparer.Ordinal) { [live.StateKey] = live.StateType },
        new HashSet<string>(candidate ? live.CandidateDependencies : live.ActiveDependencies,
            StringComparer.Ordinal),
        candidate ? live.CandidateSourceHash : live.ActiveSourceHash);

    private static string LiveToolsBlockUx6N(StudioUx6NLiveDraft live)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Ux6NStartMarker);
        builder.AppendLine("// API ForgeScript 1.0.0 · R099 · hot reload et débogueur revus.");
        builder.AppendLine($"// Module {QuoteUx6N(live.ModuleId)} · versions {live.ActiveVersion} → {live.CandidateVersion} · état {QuoteUx6N(live.StateKey)}:{QuoteUx6N(live.StateType)}.");
        builder.AppendLine($"// Debug {QuoteUx6N(live.Function)} · {QuoteUx6N(live.SourcePath)}:{live.SourceLine} · watch {QuoteUx6N(live.WatchExpression)}.");
        builder.AppendLine("void StudioLiveToolsScenario()");
        builder.AppendLine("{");
        builder.AppendLine($"    HotReloadRef reload = hot_reload_request(\"{QuoteUx6N(live.ModuleId)}\", {live.CandidateVersion}, \"{QuoteUx6N(live.CandidateSourceHash)}\");");
        builder.AppendLine($"    DebugSessionRef debug = debug_break(\"{QuoteUx6N(live.DebugSessionId)}\", \"{QuoteUx6N(live.DebugAuthority)}\");");
        builder.AppendLine($"    debug_step(debug, \"{QuoteUx6N(live.StepMode)}\");");
        builder.AppendLine("}");
        builder.Append(Ux6NEndMarker);
        return builder.ToString();
    }

    private static string NativeLiveToolsSourceUx6N(StudioUx6NLiveDraft live)
    {
        var block = LiveToolsBlockUx6N(live);
        var start = block.IndexOf("void StudioLiveToolsScenario()", StringComparison.Ordinal);
        var end = block.LastIndexOf(Ux6NEndMarker, StringComparison.Ordinal);
        return block[start..end]
            .Replace("void StudioLiveToolsScenario()", "void main()", StringComparison.Ordinal)
            .TrimEnd();
    }

    private static bool ValidDependenciesUx6N(IReadOnlyList<string> values) =>
        values.Count is > 0 and <= 16 && values.All(SafeTokenUx6N) &&
        values.Distinct(StringComparer.Ordinal).Count() == values.Count;

    private static bool SafeTokenUx6N(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or ':');

    private static string QuoteUx6N(string value) => (value ?? string.Empty)
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    private static bool ThrowsLiveUx6N<TException>(Action action) where TException : Exception
    {
        try { action(); return false; }
        catch (TException) { return true; }
    }

    private static StudioUx6NLiveResult FailedLiveToolsUx6N(
        string message,
        IReadOnlyList<StudioUx6FDiagnostic> diagnostics) =>
        new(false, message, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false, false, false, false,
            false, false, [], diagnostics);

    private static StudioUx6NSmokeResult FailedSmokeUx6N() =>
        new(false, false, false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false);
}
