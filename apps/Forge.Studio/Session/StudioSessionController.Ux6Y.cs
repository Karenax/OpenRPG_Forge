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

namespace Forge.Studio.Session;

internal sealed partial class StudioSessionController
{
    private const string Ux6YSelectionCode = "studio.runtime.operations.selection_required";
    private const string Ux6YSourceCode = "studio.runtime.operations.source_changed";
    private const string Ux6YProfileCode = "studio.runtime.operations.profile_invalid";
    private const string Ux6YProvenance = "studio:ux6-y:runtime-operations";
    private const string Ux6YBoundary =
        "Aperçu copié uniquement · aucun réseau, ForgeScript, ressource, cache moteur ou chargement de scène live n’est exécuté.";

    private readonly SetAreaEnvironmentHandler _setEnvironmentUx6Y = new(new SystemClock());
    private readonly SetAreaWorldProfileHandler _setWorldUx6Y = new(new SystemClock());
    private StudioUx6YDraft? _approvedRuntimeOperationsUx6Y;
    private ProjectRevision? _approvedRuntimeOperationsRevisionUx6Y;
    private string? _approvedRuntimeOperationsFingerprintUx6Y;

    internal IReadOnlyList<StudioUx6YAreaChoice> AreaChoicesUx6Y => Project?.Areas
        .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(item => item.Slug, StringComparer.Ordinal)
        .Select(item => new StudioUx6YAreaChoice(item.Id,
            $"{HumanUx6Y(item.DisplayName)} · {HumanUx6Y(item.Slug)}"))
        .ToArray() ?? [];

    internal IReadOnlyList<StudioUx6YWeatherStateChoice> WeatherStateChoicesUx6Y(AreaId areaId) =>
        Project?.Areas.SingleOrDefault(item => item.Id == areaId)?.Environment.Weather.States
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new StudioUx6YWeatherStateChoice(item.Id, HumanUx6Y(item.Name), item.Kind))
            .ToArray() ?? [];

    internal StudioUx6YDraft? CreateRuntimeOperationsDraftUx6Y(AreaId? areaId, bool explicitlySelected)
    {
        var project = Project;
        var history = History;
        if (!explicitlySelected || areaId is null || project is null || history is null) return null;
        var area = project.Areas.SingleOrDefault(item => item.Id == areaId);
        if (area is null) return null;
        var weather = area.Environment.Weather;
        var loading = area.World.Loading;
        return new(history.State.Revision, ProjectFingerprintUx6Y(project), area.Id, area.Environment, area.World,
            new(weather.InitialStateId, weather.ServerAuthoritative, weather.DeterministicSeed,
                weather.PersistState, weather.ReplicateState),
            new(loading.AsyncStreaming, loading.PrefetchDependencies, loading.CancellationEnabled,
                loading.CacheBudgetMegabytes, loading.MaximumConcurrentLoads, loading.ChunkRadius,
                loading.TargetLoadMilliseconds, loading.RequiredResources.ToArray()));
    }

    internal StudioUx6YPreview PreviewRuntimeOperationsUx6Y(StudioUx6YDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        CancelRuntimeOperationsApprovalUx6Y();
        if (!TryGetCurrentUx6Y(draft, out var history, out _, out var failure)) return failure!;
        if (!TryBuildCandidatesUx6Y(draft, out var environment, out var world, out var diagnostic))
            return FailedPreviewUx6Y("La politique météo ou de streaming est invalide.", diagnostic!);

        var weatherCommand = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()),
            history!.State.Revision, draft.AreaId, environment!, DryRun: true);
        var weatherDryRun = _setEnvironmentUx6Y.Handle(history.State, weatherCommand);
        if (!weatherDryRun.Succeeded)
            return FailedCommandUx6Y(weatherDryRun, "areas/environment/weather");

        var simulatedWeatherApply = _setEnvironmentUx6Y.Handle(history.State,
            weatherCommand with { DryRun = false });
        if (!simulatedWeatherApply.Succeeded)
            return FailedCommandUx6Y(simulatedWeatherApply, "areas/environment/weather");
        var loadingCommand = new SetAreaWorldProfileCommand(CommandId.From(Guid.NewGuid()),
            simulatedWeatherApply.State.Revision, draft.AreaId, world!, DryRun: true);
        var loadingDryRun = _setWorldUx6Y.Handle(simulatedWeatherApply.State, loadingCommand);
        if (!loadingDryRun.Succeeded)
            return FailedCommandUx6Y(loadingDryRun, "areas/world/loading");

        var weatherRuntime = EvaluateWeatherRuntimeUx6Y(draft.AreaId, environment!.Weather);
        var streamingRuntime = EvaluateStreamingRuntimeUx6Y(world!.Loading);
        var diff = BuildDiffUx6Y(draft.SourceEnvironment.Weather, environment.Weather,
            draft.SourceWorld.Loading, world.Loading);
        if (diff.Count > 0)
        {
            _approvedRuntimeOperationsUx6Y = FreezeDraftUx6Y(draft);
            _approvedRuntimeOperationsRevisionUx6Y = history.State.Revision;
            _approvedRuntimeOperationsFingerprintUx6Y = CandidateFingerprintUx6Y(draft);
        }
        return new(true, diff.Count > 0, true, true,
            diff.Count > 0
                ? "Météo runtime puis streaming prêts à appliquer dans deux commandes explicites."
                : "Aperçu sec valide · les deux politiques sont déjà identiques.",
            diff, [], weatherRuntime.Results, streamingRuntime.Results,
            weatherRuntime.Resolved, streamingRuntime.Resolved, true);
    }

    internal StudioUx6YActionResult ApplyApprovedRuntimeOperationsUx6Y(StudioUx6YDraft currentDraft)
    {
        ArgumentNullException.ThrowIfNull(currentDraft);
        var approved = _approvedRuntimeOperationsUx6Y;
        var history = History;
        var project = Project;
        if (approved is null || history is null || project is null ||
            _approvedRuntimeOperationsRevisionUx6Y != history.State.Revision ||
            approved.BaseRevision != history.State.Revision || currentDraft.BaseRevision != history.State.Revision ||
            !string.Equals(approved.SourceProjectFingerprint, ProjectFingerprintUx6Y(project), StringComparison.Ordinal) ||
            !string.Equals(_approvedRuntimeOperationsFingerprintUx6Y, CandidateFingerprintUx6Y(currentDraft),
                StringComparison.Ordinal) ||
            !TryBuildCandidatesUx6Y(approved, out var environment, out var world, out _))
        {
            CancelRuntimeOperationsApprovalUx6Y();
            return new(false, "Le projet ou le brouillon a changé · relancez l’aperçu sec.");
        }

        var weatherCommand = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            approved.AreaId, environment!, DryRun: true);
        var weatherDryRun = _setEnvironmentUx6Y.Handle(history.State, weatherCommand);
        if (!weatherDryRun.Succeeded)
        {
            CancelRuntimeOperationsApprovalUx6Y();
            return new(false, HumanCommandFailureUx6Y(weatherDryRun));
        }
        var weatherApplied = _setEnvironmentUx6Y.Handle(history.State, weatherCommand with { DryRun = false });
        if (!weatherApplied.Succeeded)
        {
            CancelRuntimeOperationsApprovalUx6Y();
            return new(false, HumanCommandFailureUx6Y(weatherApplied));
        }
        history.Record(weatherApplied);

        var loadingCommand = new SetAreaWorldProfileCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            approved.AreaId, world!, DryRun: true);
        var loadingDryRun = _setWorldUx6Y.Handle(history.State, loadingCommand);
        if (!loadingDryRun.Succeeded)
        {
            _ = Undo();
            CancelRuntimeOperationsApprovalUx6Y();
            return new(false, HumanCommandFailureUx6Y(loadingDryRun));
        }
        var loadingApplied = _setWorldUx6Y.Handle(history.State, loadingCommand with { DryRun = false });
        if (!loadingApplied.Succeeded)
        {
            _ = Undo();
            CancelRuntimeOperationsApprovalUx6Y();
            return new(false, HumanCommandFailureUx6Y(loadingApplied));
        }
        history.Record(loadingApplied);
        CancelRuntimeOperationsApprovalUx6Y();
        InvalidateContentIndexes();
        return new(true, "Météo runtime appliquée, puis streaming appliqué dans une seconde commande annulable.");
    }

    internal StudioUx6YActionResult UndoRuntimeOperationsUx6Y()
    {
        CancelRuntimeOperationsApprovalUx6Y();
        var loading = Undo();
        if (!loading.Succeeded) return new(false, loading.Message);
        var weather = Undo();
        if (!weather.Succeeded)
        {
            _ = Redo();
            return new(false, weather.Message);
        }
        InvalidateContentIndexes();
        return new(true, "Streaming puis météo annulés dans l’ordre inverse.");
    }

    internal StudioUx6YActionResult RedoRuntimeOperationsUx6Y()
    {
        CancelRuntimeOperationsApprovalUx6Y();
        var weather = Redo();
        if (!weather.Succeeded) return new(false, weather.Message);
        var loading = Redo();
        if (!loading.Succeeded)
        {
            _ = Undo();
            return new(false, loading.Message);
        }
        InvalidateContentIndexes();
        return new(true, "Météo puis streaming rétablis.");
    }

    internal StudioUx6YActionResult SaveRuntimeOperationsUx6Y()
    {
        var result = Save();
        return new(result.Succeeded, result.Succeeded
            ? "Projet enregistré avec ses politiques météo et streaming."
            : result.Message);
    }

    internal StudioUx6YApprovalSnapshot CaptureRuntimeOperationsApprovalUx6Y() =>
        new(_approvedRuntimeOperationsUx6Y, _approvedRuntimeOperationsRevisionUx6Y,
            _approvedRuntimeOperationsFingerprintUx6Y);

    internal void RestoreRuntimeOperationsApprovalUx6Y(StudioUx6YApprovalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _approvedRuntimeOperationsUx6Y = snapshot.Draft;
        _approvedRuntimeOperationsRevisionUx6Y = snapshot.Revision;
        _approvedRuntimeOperationsFingerprintUx6Y = snapshot.CandidateFingerprint;
    }

    internal void CancelRuntimeOperationsApprovalUx6Y()
    {
        _approvedRuntimeOperationsUx6Y = null;
        _approvedRuntimeOperationsRevisionUx6Y = null;
        _approvedRuntimeOperationsFingerprintUx6Y = null;
    }

    internal void CloseUx6Y() => CancelRuntimeOperationsApprovalUx6Y();

    internal string RuntimeOperationsDraftFingerprintUx6Y(StudioUx6YDraft draft) =>
        CandidateFingerprintUx6Y(draft);

    internal StudioUx6YSmokeResult RunUx6YContractSmoke()
    {
        var source = Project;
        var root = string.Empty;
        StudioSessionController? editing = null;
        StudioSessionController? reopened = null;
        var cleanup = true;
        try
        {
            var realReadable = source is not null && source.Areas.Count > 0 &&
                               source.Areas.All(item => item.Environment.Weather.States.Count > 0);
            var explicitSelection = CreateRuntimeOperationsDraftUx6Y(null, false) is null;
            if (source is null || source.Areas.Count == 0)
                return FailedSmokeUx6Y(explicitSelection, realReadable, cleanup);
            var areaId = source.Areas[0].Id;
            var initial = CreateRuntimeOperationsDraftUx6Y(areaId, true);
            if (initial is null) return FailedSmokeUx6Y(explicitSelection, realReadable, cleanup);
            var invalidWeather = PreviewRuntimeOperationsUx6Y(initial with
            {
                Weather = initial.Weather with { InitialStateId = Guid.Empty },
            });
            var invalidLoading = PreviewRuntimeOperationsUx6Y(initial with
            {
                Loading = initial.Loading with { CacheBudgetMegabytes = 8 },
            });
            var invalidConfiguration = !invalidWeather.Succeeded && !invalidLoading.Succeeded;
            var changed = ChangedRuntimeOperationsDraftUx6Y(initial);
            var beforeProject = Project;
            var beforeRevision = History!.State.Revision;
            var preview = PreviewRuntimeOperationsUx6Y(changed);
            var weatherDryRun = preview.Succeeded && preview.WeatherDryRun &&
                                ReferenceEquals(beforeProject, Project) && History.State.Revision == beforeRevision;
            var loadingDryRun = preview.Succeeded && preview.LoadingDryRun &&
                                ReferenceEquals(beforeProject, Project) && History.State.Revision == beforeRevision;
            var staleSource = !PreviewRuntimeOperationsUx6Y(changed with
            {
                BaseRevision = changed.BaseRevision.Next(),
            }).Succeeded;
            _ = PreviewRuntimeOperationsUx6Y(changed);
            var staleApproval = !ApplyApprovedRuntimeOperationsUx6Y(changed with
            {
                Loading = changed.Loading with { ChunkRadius = changed.Loading.ChunkRadius == 32 ? 31 : changed.Loading.ChunkRadius + 1 },
            }).Succeeded;

            root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-y-{Guid.NewGuid():N}");
            var evidenceProject = BuildEvidenceProjectUx6Y(source);
            if (!ProjectSession.Create(root, evidenceProject).Succeeded)
                return FailedSmokeUx6Y(explicitSelection, realReadable, cleanup);
            editing = new StudioSessionController();
            if (!editing.Open(root).Succeeded)
                return FailedSmokeUx6Y(explicitSelection, realReadable, cleanup);
            var sourceProject = editing.Project!;
            var sourceArea = sourceProject.Areas[0];
            var sourceEnvironment = sourceArea.Environment;
            var sourceWorld = sourceArea.World;
            var draft = ChangedRuntimeOperationsDraftUx6Y(
                editing.CreateRuntimeOperationsDraftUx6Y(sourceArea.Id, true)!);
            var freshPreview = editing.PreviewRuntimeOperationsUx6Y(draft);
            var weatherRuntime = EvaluateWeatherRuntimeUx6Y(sourceArea.Id,
                BuildWeatherCandidateUx6Y(sourceEnvironment.Weather, draft.Weather));
            var streamingRuntime = EvaluateStreamingRuntimeUx6Y(
                BuildLoadingCandidateUx6Y(sourceWorld.Loading, draft.Loading));
            var applied = freshPreview.Succeeded && editing.ApplyApprovedRuntimeOperationsUx6Y(draft).Succeeded;
            var finalProject = editing.Project!;
            var finalArea = finalProject.Areas.Single(item => item.Id == sourceArea.Id);
            var canonicalCommands = applied && finalArea.Environment.Weather.DeterministicSeed ==
                                    draft.Weather.DeterministicSeed &&
                                    finalArea.World.Loading.CacheBudgetMegabytes == draft.Loading.CacheBudgetMegabytes &&
                                    editing.History!.State.Revision == new ProjectRevision(2);
            var environmentNeighbors = applied && EnvironmentNeighborsUx6Y(sourceEnvironment, finalArea.Environment);
            var worldNeighbors = applied && WorldNeighborsUx6Y(sourceWorld, finalArea.World);
            var projectNeighbors = applied && ReferenceEquals(sourceProject.Module, finalProject.Module) &&
                                   sourceProject.Blueprints.SequenceEqual(finalProject.Blueprints) &&
                                   sourceProject.Dialogues.SequenceEqual(finalProject.Dialogues) &&
                                   sourceProject.Quests.SequenceEqual(finalProject.Quests) &&
                                   sourceProject.Areas.Where(item => item.Id != sourceArea.Id)
                                       .SequenceEqual(finalProject.Areas.Where(item => item.Id != sourceArea.Id));
            var finalJson = applied ? ProjectJsonSerializer.Serialize(finalProject) : string.Empty;
            var undo = applied && editing.UndoRuntimeOperationsUx6Y().Succeeded &&
                       ProjectJsonSerializer.Serialize(editing.Project!) == ProjectJsonSerializer.Serialize(sourceProject);
            var redo = undo && editing.RedoRuntimeOperationsUx6Y().Succeeded &&
                       ProjectJsonSerializer.Serialize(editing.Project!) == finalJson;
            var saved = redo && editing.SaveRuntimeOperationsUx6Y().Succeeded;
            reopened = new StudioSessionController();
            var persistence = saved && reopened.Open(root).Succeeded && reopened.Project is not null &&
                              ProjectJsonSerializer.Serialize(reopened.Project) == finalJson;
            var human = !ContainsUuidUx6Y(string.Join(' ', freshPreview.Diff
                .Concat(freshPreview.WeatherResults).Concat(freshPreview.LoadingResults))) &&
                        freshPreview.WeatherResults.Contains(Ux6YBoundary) &&
                        freshPreview.LoadingResults.Contains(Ux6YBoundary);
            editing.Close();
            reopened.Close();
            cleanup = DeleteTemporaryUx6Y(root);
            var succeeded = explicitSelection && realReadable && invalidConfiguration && weatherDryRun &&
                            loadingDryRun && weatherRuntime.Resolved && weatherRuntime.TypedEvent &&
                            weatherRuntime.AuthorityPolicy && streamingRuntime.Resolved &&
                            streamingRuntime.CacheAndProgress && streamingRuntime.Benchmark &&
                            streamingRuntime.CancellationPolicy && staleSource && staleApproval &&
                            canonicalCommands && undo && redo && persistence && environmentNeighbors &&
                            worldNeighbors && projectNeighbors && human && freshPreview.RuntimeClaimsBounded && cleanup;
            return new(succeeded, explicitSelection, realReadable, invalidConfiguration, weatherDryRun,
                loadingDryRun, weatherRuntime.Resolved, weatherRuntime.TypedEvent,
                weatherRuntime.AuthorityPolicy, streamingRuntime.Resolved,
                streamingRuntime.CacheAndProgress, streamingRuntime.Benchmark,
                streamingRuntime.CancellationPolicy, staleSource, staleApproval, canonicalCommands,
                undo && redo, persistence, environmentNeighbors, worldNeighbors, projectNeighbors,
                human, freshPreview.RuntimeClaimsBounded, cleanup);
        }
        catch
        {
            return FailedSmokeUx6Y(false, false, cleanup);
        }
        finally
        {
            editing?.Close();
            reopened?.Close();
            if (!string.IsNullOrWhiteSpace(root)) cleanup = DeleteTemporaryUx6Y(root);
            if (!cleanup) Godot.GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_Y_CLEANUP_FAILED");
        }
    }

    internal static StudioUx6YDraft ChangedRuntimeOperationsDraftUx6Y(StudioUx6YDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var weather = draft.Weather;
        var loading = draft.Loading;
        var states = draft.SourceEnvironment.Weather.States;
        var initial = states.FirstOrDefault(item => item.Id != weather.InitialStateId)?.Id ?? weather.InitialStateId;
        var resources = loading.RequiredResources.Count == 0
            ? new[] { "areas/runtime-preview/terrain.bin", "areas/runtime-preview/entities.bin" }
            : loading.RequiredResources.ToArray();
        return draft with
        {
            Weather = weather with
            {
                InitialStateId = initial,
                ServerAuthoritative = true,
                DeterministicSeed = weather.DeterministicSeed == 2608 ? 2609 : 2608,
                PersistState = true,
                ReplicateState = true,
            },
            Loading = loading with
            {
                AsyncStreaming = true,
                PrefetchDependencies = true,
                CancellationEnabled = true,
                CacheBudgetMegabytes = loading.CacheBudgetMegabytes == 768 ? 1024 : 768,
                MaximumConcurrentLoads = 4,
                ChunkRadius = 3,
                TargetLoadMilliseconds = 1500,
                RequiredResources = resources,
            },
        };
    }

    private bool TryGetCurrentUx6Y(StudioUx6YDraft draft, out ProjectCommandHistory? history,
        out ProjectDefinition? project, out StudioUx6YPreview? failure)
    {
        history = History;
        project = Project;
        failure = null;
        if (history is null || project is null || project.Areas.All(item => item.Id != draft.AreaId))
        {
            failure = FailedPreviewUx6Y("Ouvrez un module et choisissez explicitement une zone.",
                new(Ux6YSelectionCode, "areas", "Sélection explicite requise."));
            return false;
        }
        if (draft.BaseRevision != history.State.Revision ||
            !string.Equals(draft.SourceProjectFingerprint, ProjectFingerprintUx6Y(project), StringComparison.Ordinal))
        {
            failure = FailedPreviewUx6Y("Le projet a changé · rechargez les valeurs de la zone.",
                new(Ux6YSourceCode, $"areas/{draft.AreaId}/runtime", "La révision ou la source ne correspond plus."));
            return false;
        }
        return true;
    }

    private static bool TryBuildCandidatesUx6Y(StudioUx6YDraft draft,
        out AreaEnvironmentProfile? environment, out AreaWorldProfile? world,
        out StudioUx6YDiagnostic? diagnostic)
    {
        environment = null;
        world = null;
        diagnostic = null;
        try
        {
            var weather = BuildWeatherCandidateUx6Y(draft.SourceEnvironment.Weather, draft.Weather);
            environment = new AreaEnvironmentProfile(draft.SourceEnvironment.SchemaVersion,
                draft.SourceEnvironment.Classification, draft.SourceEnvironment.Tags,
                draft.SourceEnvironment.Lighting, draft.SourceEnvironment.Fog, draft.SourceEnvironment.Audio,
                draft.SourceEnvironment.Rendering, draft.SourceEnvironment.Sky, draft.SourceEnvironment.Wind,
                weather, draft.SourceEnvironment.Water, draft.SourceEnvironment.Reverb,
                draft.SourceEnvironment.Authoring, draft.SourceEnvironment.RequireAuthoringRuntimeMetadata);
            var loading = BuildLoadingCandidateUx6Y(draft.SourceWorld.Loading, draft.Loading);
            world = new AreaWorldProfile(draft.SourceWorld.Id, draft.SourceWorld.SchemaVersion,
                draft.SourceWorld.Terrain, draft.SourceWorld.Biomes, draft.SourceWorld.Features,
                draft.SourceWorld.VolumetricNavigation, draft.SourceWorld.FlightRoutes, loading,
                draft.SourceWorld.Provenance);
            return true;
        }
        catch (ArgumentException exception)
        {
            diagnostic = new(Ux6YProfileCode, $"areas/{draft.AreaId}/runtime", exception.Message);
            environment = null;
            world = null;
            return false;
        }
    }

    private static WeatherProfile BuildWeatherCandidateUx6Y(WeatherProfile source,
        StudioUx6YWeatherDraft draft) => new(source.SchemaVersion, draft.InitialStateId, source.States,
        draft.ServerAuthoritative, draft.DeterministicSeed, draft.PersistState, draft.ReplicateState);

    private static AreaLoadingProfile BuildLoadingCandidateUx6Y(AreaLoadingProfile source,
        StudioUx6YLoadingDraft draft) => new(source.Id, source.SchemaVersion, draft.AsyncStreaming,
        draft.PrefetchDependencies, draft.CancellationEnabled, draft.CacheBudgetMegabytes,
        draft.MaximumConcurrentLoads, draft.ChunkRadius, draft.TargetLoadMilliseconds,
        draft.RequiredResources, Ux6YProvenance);

    private static (IReadOnlyList<string> Results, bool Resolved, bool TypedEvent, bool AuthorityPolicy)
        EvaluateWeatherRuntimeUx6Y(AreaId areaId, WeatherProfile profile)
    {
        var initial = WeatherRuntimeStateMachine.CreateInitial(profile);
        var current = WeatherRuntimeStateMachine.Resolve(profile, initial);
        var denied = WeatherRuntimeStateMachine.Advance(profile, initial,
            Math.Max(1, current.MaximumDurationSeconds), hasAuthority: false);
        var advanced = WeatherRuntimeStateMachine.Advance(profile, initial,
            Math.Max(1, current.MaximumDurationSeconds) + 1, hasAuthority: true);
        WeatherForgeScriptEvent? typed = null;
        if (advanced.Transition is not null)
            typed = WeatherForgeScriptDispatchService.Build(profile, advanced.State, advanced.Transition);
        var authority = !profile.ServerAuthoritative || denied.AuthorityDenied;
        var next = WeatherRuntimeStateMachine.Resolve(profile, advanced.State);
        var results = new List<string>
        {
            $"État initial : {HumanUx6Y(current.Name)} · {HumanWeatherKindUx6Y(current.Kind)}.",
            advanced.Transition is null
                ? "Transition : cycle stable à état unique."
                : $"Transition déterministe : {HumanUx6Y(current.Name)} → {HumanUx6Y(next.Name)} · fusion {advanced.Transition.BlendSeconds:0.##} s.",
            $"Autorité : {(profile.ServerAuthoritative ? "serveur requise et refus client vérifié" : "locale autorisée")}.",
            $"Persistance/réplication : {(profile.PersistState ? "snapshot demandé" : "snapshot désactivé")} · {(profile.ReplicateState ? "enveloppe réseau demandée" : "réplication désactivée")}.",
            typed is null
                ? "Événement typé : aucun changement d’état dans ce profil à état unique."
                : $"Événement typé : {typed.EventName} {typed.ApiVersion} · révision serveur {typed.ServerRevision} · effet {HumanUx6Y(typed.GameplayEffect)}.",
            $"Zone : {HumanUx6Y(areaId.ToString())} · seed {profile.DeterministicSeed}.",
            Ux6YBoundary,
        };
        return (results, true, typed is not null || profile.States.Count == 1, authority);
    }

    private static (IReadOnlyList<string> Results, bool Resolved, bool CacheAndProgress, bool Benchmark,
        bool CancellationPolicy) EvaluateStreamingRuntimeUx6Y(AreaLoadingProfile profile)
    {
        var payloads = new Dictionary<string, AreaChunkPayload>(StringComparer.Ordinal);
        var shared = "areas/runtime-preview/shared-dependency.bin";
        foreach (var (resource, index) in profile.RequiredResources.Select((item, index) => (item, index)))
        {
            payloads[resource] = new(resource, new byte[64 + index],
                profile.PrefetchDependencies && index == 0 ? [shared] : []);
        }
        if (profile.PrefetchDependencies && profile.RequiredResources.Count > 0)
            payloads[shared] = new(shared, new byte[32], []);
        var loader = new Ux6YMemoryChunkLoader(payloads);
        var coordinator = new AreaStreamingCoordinator(loader);
        var progress = new List<AreaStreamingProgress>();
        var first = coordinator.LoadAsync(profile, new Ux6YInlineProgress(progress.Add))
            .GetAwaiter().GetResult();
        var second = coordinator.LoadAsync(profile).GetAwaiter().GetResult();
        var gate = AreaStreamingBenchmarkGate.Evaluate(profile, first);
        var expectedResources = profile.RequiredResources.Count +
                                (profile.PrefetchDependencies && profile.RequiredResources.Count > 0 ? 1 : 0);
        var resolved = first.Resources.Count == expectedResources;
        var cacheProgress = expectedResources == 0 ||
                            (progress.Count > 0 && Math.Abs(progress[^1].Fraction - 1) < .001 &&
                             second.CacheHits == expectedResources && loader.Calls.Count == expectedResources);
        var cancellation = ResolveCancellationPolicyUx6Y(profile, payloads);
        return (new[]
        {
            $"Streaming : {(profile.AsyncStreaming ? $"asynchrone · {profile.MaximumConcurrentLoads} chargements" : "séquentiel")} · rayon {profile.ChunkRadius}.",
            $"Ressources : {first.Resources.Count} résolue(s) · {first.LoadedBytes} octets simulés · dépendances {(profile.PrefetchDependencies ? "préchargées" : "ignorées")}.",
            $"Cache : budget {profile.CacheBudgetMegabytes} Mio · second passage {second.CacheHits} hit(s).",
            $"Progression : {(progress.Count == 0 ? "aucune ressource requise" : $"{progress[^1].CompletedResources}/{progress[^1].TotalResources}")}.",
            $"Benchmark : {first.Elapsed.TotalMilliseconds:0.###} ms / cible {profile.TargetLoadMilliseconds} ms · {(gate.Passed ? "PASS" : gate.Diagnostics[0].Code)}.",
            $"Annulation : {(profile.CancellationEnabled ? "token annulé propagé" : "token appelant ignoré par politique")}.",
            Ux6YBoundary,
        }, resolved, cacheProgress, gate.Passed, cancellation);
    }

    private static bool ResolveCancellationPolicyUx6Y(AreaLoadingProfile profile,
        IReadOnlyDictionary<string, AreaChunkPayload> payloads)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            _ = new AreaStreamingCoordinator(new Ux6YMemoryChunkLoader(payloads))
                .LoadAsync(profile, cancellationToken: cancellation.Token).GetAwaiter().GetResult();
            return !profile.CancellationEnabled;
        }
        catch (OperationCanceledException)
        {
            return profile.CancellationEnabled;
        }
    }

    private static IReadOnlyList<string> BuildDiffUx6Y(WeatherProfile sourceWeather,
        WeatherProfile candidateWeather, AreaLoadingProfile sourceLoading, AreaLoadingProfile candidateLoading)
    {
        var diff = new List<string>();
        if (sourceWeather.InitialStateId != candidateWeather.InitialStateId)
        {
            var state = candidateWeather.States.Single(item => item.Id == candidateWeather.InitialStateId);
            diff.Add($"Météo initiale : {HumanUx6Y(state.Name)} ({HumanWeatherKindUx6Y(state.Kind)}).");
        }
        if (sourceWeather.ServerAuthoritative != candidateWeather.ServerAuthoritative ||
            sourceWeather.DeterministicSeed != candidateWeather.DeterministicSeed)
            diff.Add($"Météo runtime : {(candidateWeather.ServerAuthoritative ? "serveur autoritaire" : "autorité locale")} · seed {candidateWeather.DeterministicSeed}.");
        if (sourceWeather.PersistState != candidateWeather.PersistState ||
            sourceWeather.ReplicateState != candidateWeather.ReplicateState)
            diff.Add($"État météo : persistance {(candidateWeather.PersistState ? "active" : "inactive")} · réplication {(candidateWeather.ReplicateState ? "active" : "inactive")}.");
        if (sourceLoading.AsyncStreaming != candidateLoading.AsyncStreaming ||
            sourceLoading.PrefetchDependencies != candidateLoading.PrefetchDependencies ||
            sourceLoading.CancellationEnabled != candidateLoading.CancellationEnabled)
            diff.Add($"Streaming : {(candidateLoading.AsyncStreaming ? "asynchrone" : "séquentiel")} · dépendances {(candidateLoading.PrefetchDependencies ? "préchargées" : "à la demande")} · annulation {(candidateLoading.CancellationEnabled ? "active" : "désactivée")}.");
        if (sourceLoading.CacheBudgetMegabytes != candidateLoading.CacheBudgetMegabytes ||
            sourceLoading.MaximumConcurrentLoads != candidateLoading.MaximumConcurrentLoads ||
            sourceLoading.ChunkRadius != candidateLoading.ChunkRadius ||
            sourceLoading.TargetLoadMilliseconds != candidateLoading.TargetLoadMilliseconds)
            diff.Add($"Budget streaming : {candidateLoading.CacheBudgetMegabytes} Mio · {candidateLoading.MaximumConcurrentLoads} parallèle(s) · rayon {candidateLoading.ChunkRadius} · cible {candidateLoading.TargetLoadMilliseconds} ms.");
        if (!sourceLoading.RequiredResources.SequenceEqual(candidateLoading.RequiredResources))
            diff.Add($"Ressources requises : {candidateLoading.RequiredResources.Count} entrée(s) sûres et ordonnées.");
        return diff;
    }

    private static bool EnvironmentNeighborsUx6Y(AreaEnvironmentProfile source, AreaEnvironmentProfile candidate) =>
        source.Classification == candidate.Classification && source.Tags.SequenceEqual(candidate.Tags) &&
        ReferenceEquals(source.Lighting, candidate.Lighting) && ReferenceEquals(source.Fog, candidate.Fog) &&
        ReferenceEquals(source.Audio, candidate.Audio) && ReferenceEquals(source.Rendering, candidate.Rendering) &&
        ReferenceEquals(source.Sky, candidate.Sky) && ReferenceEquals(source.Wind, candidate.Wind) &&
        ReferenceEquals(source.Water, candidate.Water) && ReferenceEquals(source.Reverb, candidate.Reverb) &&
        ReferenceEquals(source.Authoring, candidate.Authoring) &&
        source.RequireAuthoringRuntimeMetadata == candidate.RequireAuthoringRuntimeMetadata &&
        source.Weather.States.SequenceEqual(candidate.Weather.States);

    private static bool WorldNeighborsUx6Y(AreaWorldProfile source, AreaWorldProfile candidate) =>
        source.Id == candidate.Id && source.SchemaVersion == candidate.SchemaVersion &&
        ReferenceEquals(source.Terrain, candidate.Terrain) && source.Biomes.SequenceEqual(candidate.Biomes) &&
        source.Features.SequenceEqual(candidate.Features) &&
        ReferenceEquals(source.VolumetricNavigation, candidate.VolumetricNavigation) &&
        source.FlightRoutes.SequenceEqual(candidate.FlightRoutes) && source.Provenance == candidate.Provenance;

    private static ProjectDefinition BuildEvidenceProjectUx6Y(ProjectDefinition source)
    {
        var area = source.Areas[0];
        var weather = area.Environment.Weather;
        if (weather.States.Count == 1)
        {
            var rain = new WeatherStateProfile(Guid.Parse("6f560000-0000-4000-8000-000000000002"),
                "Pluie de preuve", WeatherKind.Rain, .55f, .85f, .7f, false, string.Empty,
                "weather.rain", "surface.wet", 2, 4, 1);
            weather = new WeatherProfile(weather.SchemaVersion, weather.InitialStateId,
                weather.States.Append(rain).ToArray(), true, weather.DeterministicSeed, true, true);
        }
        var environment = new AreaEnvironmentProfile(area.Environment.SchemaVersion,
            area.Environment.Classification, area.Environment.Tags, area.Environment.Lighting,
            area.Environment.Fog, area.Environment.Audio, area.Environment.Rendering, area.Environment.Sky,
            area.Environment.Wind, weather, area.Environment.Water, area.Environment.Reverb,
            area.Environment.Authoring, area.Environment.RequireAuthoringRuntimeMetadata);
        var resources = area.World.Loading.RequiredResources.Count == 0
            ? new[] { "areas/runtime-preview/terrain.bin", "areas/runtime-preview/entities.bin" }
            : area.World.Loading.RequiredResources;
        var loading = new AreaLoadingProfile(area.World.Loading.Id, area.World.Loading.SchemaVersion,
            area.World.Loading.AsyncStreaming, area.World.Loading.PrefetchDependencies,
            area.World.Loading.CancellationEnabled, area.World.Loading.CacheBudgetMegabytes,
            area.World.Loading.MaximumConcurrentLoads, area.World.Loading.ChunkRadius,
            area.World.Loading.TargetLoadMilliseconds, resources, "evidence:ux6-y");
        var world = new AreaWorldProfile(area.World.Id, area.World.SchemaVersion, area.World.Terrain,
            area.World.Biomes, area.World.Features, area.World.VolumetricNavigation, area.World.FlightRoutes,
            loading, area.World.Provenance);
        var replacement = area.WithEnvironment(environment).WithWorld(world);
        return new(source.Id, source.Title, source.Module, source.Blueprints,
            source.Areas.Select(item => item.Id == area.Id ? replacement : item).ToArray(),
            source.Dialogues, source.Quests);
    }

    private static StudioUx6YDraft FreezeDraftUx6Y(StudioUx6YDraft draft) => draft with
    {
        Loading = draft.Loading with { RequiredResources = draft.Loading.RequiredResources.ToArray() },
    };

    private static string ProjectFingerprintUx6Y(ProjectDefinition project) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))));

    private static string CandidateFingerprintUx6Y(StudioUx6YDraft draft)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteUx6Y(writer, draft.SourceProjectFingerprint);
        writer.Write(draft.BaseRevision.Value);
        WriteUx6Y(writer, draft.AreaId.ToString());
        writer.Write(draft.Weather.InitialStateId.ToByteArray());
        writer.Write(draft.Weather.ServerAuthoritative);
        writer.Write(draft.Weather.DeterministicSeed);
        writer.Write(draft.Weather.PersistState);
        writer.Write(draft.Weather.ReplicateState);
        writer.Write(draft.Loading.AsyncStreaming);
        writer.Write(draft.Loading.PrefetchDependencies);
        writer.Write(draft.Loading.CancellationEnabled);
        writer.Write(draft.Loading.CacheBudgetMegabytes);
        writer.Write(draft.Loading.MaximumConcurrentLoads);
        writer.Write(draft.Loading.ChunkRadius);
        writer.Write(draft.Loading.TargetLoadMilliseconds);
        writer.Write(draft.Loading.RequiredResources.Count);
        foreach (var resource in draft.Loading.RequiredResources) WriteUx6Y(writer, resource);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteUx6Y(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static StudioUx6YPreview FailedPreviewUx6Y(string message, StudioUx6YDiagnostic diagnostic) =>
        new(false, false, false, false, message, [], [diagnostic], [], [], false, false, true);

    private static StudioUx6YPreview FailedCommandUx6Y(CommandResult result, string path)
    {
        var diagnostic = result.Diagnostics.FirstOrDefault();
        return FailedPreviewUx6Y(HumanCommandFailureUx6Y(result), new(
            diagnostic?.Code ?? Ux6YProfileCode, path,
            diagnostic?.Message ?? "La commande runtime a été refusée."));
    }

    private static string HumanCommandFailureUx6Y(CommandResult result) =>
        result.Diagnostics.FirstOrDefault() is { } diagnostic
            ? $"{diagnostic.Code} · {diagnostic.Message}"
            : "La commande runtime a été refusée.";

    private static string HumanWeatherKindUx6Y(WeatherKind kind) => kind switch
    {
        WeatherKind.Clear => "clair",
        WeatherKind.Overcast => "couvert",
        WeatherKind.Rain => "pluie",
        WeatherKind.Storm => "orage",
        WeatherKind.Snow => "neige",
        WeatherKind.Fog => "brume",
        _ => HumanUx6Y(kind.ToString()),
    };

    private static string HumanUx6Y(string value) => Regex.Replace(value ?? string.Empty,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        "identifiant protégé").Replace('_', ' ').Trim();

    private static bool ContainsUuidUx6Y(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    internal static bool DeleteTemporaryUx6Y(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath());
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux6-y-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static StudioUx6YSmokeResult FailedSmokeUx6Y(bool explicitSelection, bool realReadable,
        bool cleanup) => new(false, explicitSelection, realReadable, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false, false, false,
        false, true, cleanup);

    private sealed class Ux6YMemoryChunkLoader(IReadOnlyDictionary<string, AreaChunkPayload> payloads)
        : IAreaChunkLoader
    {
        public List<string> Calls { get; } = [];

        public Task<AreaChunkPayload> LoadAsync(string resourcePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (Calls) Calls.Add(resourcePath);
            if (payloads.TryGetValue(resourcePath, out var payload)) return Task.FromResult(payload);
            return Task.FromResult(new AreaChunkPayload(resourcePath, new byte[16], []));
        }
    }

    private sealed class Ux6YInlineProgress(Action<AreaStreamingProgress> report) : IProgress<AreaStreamingProgress>
    {
        public void Report(AreaStreamingProgress value) => report(value);
    }
}
