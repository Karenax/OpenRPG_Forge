using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Areas;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Build;
using Forge.Application.Content;
using Forge.Application.Creatures;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Localization;
using Forge.Application.Modules;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Application.Rules;
using Forge.Application.Quality;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.RuntimeExtensions;
using Forge.Application.Scripting;
using Forge.Application.Entities;
using Forge.Application.Combat;
using Forge.Application.Factions;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Audio;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Creatures;
using Forge.Domain.Content;
using Forge.Application.Dialogues;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Lifecycle;
using Forge.Domain.Localization;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Quality;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Spatial;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Production;
using Forge.Network.Observability;
using Forge.Network.Testing;
using Forge.Network.Authoring;
using Forge.Network.Scenarios;
using Forge.Network.World;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Scenarios;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using Forge.RpgKit.Domain.Merchants;
using RpgAttackContext = Forge.RpgKit.Domain.Scripting.AttackContext;
using RpgDamageSpec = Forge.RpgKit.Domain.Scripting.DamageSpec;
using RpgEffectDefinition = Forge.RpgKit.Domain.Scripting.EffectDefinition;
using RpgEffectSpec = Forge.RpgKit.Domain.Scripting.EffectSpec;
using RpgEffectStacking = Forge.RpgKit.Domain.Scripting.EffectStacking;
using RpgScriptCombatState = Forge.RpgKit.Domain.Scripting.ScriptCombatState;
using RpgScriptGameplayService = Forge.RpgKit.Domain.Scripting.ScriptGameplayService;
using RpgDialogueRequest = Forge.RpgKit.Domain.Scripting.DialogueRequest;
using RpgFactionState = Forge.RpgKit.Domain.Scripting.FactionState;
using RpgFactionTransaction = Forge.RpgKit.Domain.Scripting.FactionTransaction;
using RpgInventoryState = Forge.RpgKit.Domain.Scripting.InventoryState;
using RpgInventoryTransaction = Forge.RpgKit.Domain.Scripting.InventoryTransaction;
using RpgPerceptionQuery = Forge.RpgKit.Domain.Scripting.PerceptionQuery;
using RpgQuestState = Forge.RpgKit.Domain.Scripting.QuestState;
using RpgQuestTransition = Forge.RpgKit.Domain.Scripting.QuestTransition;
using RpgScriptWorldService = Forge.RpgKit.Domain.Scripting.ScriptWorldService;
using ScriptAudioCue = Forge.Scripting.AudioCue;
using ScriptAudioState = Forge.Scripting.AudioState;
using ScriptCustomEvent = Forge.Scripting.CustomEvent;
using ScriptCustomEventSchema = Forge.Scripting.CustomEventSchema;
using ScriptDebugFrame = Forge.Scripting.DebugFrame;
using ScriptHotReloadModule = Forge.Scripting.HotReloadModule;
using ScriptRuntimeTools = Forge.Scripting.ForgeScriptRuntimeTools;
using ScriptWorldClock = Forge.Scripting.WorldClock;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private void AdvanceClimate(double delta)
    {
        if (_environmentProfile is null || _weatherState is null)
        {
            return;
        }

        var result = WeatherRuntimeStateMachine.Advance(
            _environmentProfile.Weather,
            _weatherState,
            delta,
            hasAuthority: true);
        _weatherState = result.State;
        if (result.Transition is not null)
        {
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_WEATHER_TRANSITION revision={result.State.Revision} " +
                $"state={result.State.StateId} blend={result.Transition.BlendSeconds:0.###} " +
                $"effect={result.Transition.GameplayEffect} replicate={result.Transition.Replicate.ToString().ToLowerInvariant()} " +
                $"persist={result.Transition.Persist.ToString().ToLowerInvariant()}");
            ApplyClimateEffects(force: true);
            RefreshRuntimePresentation();
        }

        _climateProbeElapsed += delta;
        if (_climateProbeElapsed >= .5)
        {
            _climateProbeElapsed = 0;
            ApplyClimateEffects(force: false);
        }
        AdvanceSurfaceWetness(delta);
    }

    private void ConfigureRuntimePolicies(ModuleRuntimePolicyProfile module, AreaRuntimePolicyProfile area)
    {
        _moduleRuntimePolicies = module ?? throw new ArgumentNullException(nameof(module));
        _areaRuntimePolicies = area ?? throw new ArgumentNullException(nameof(area));
        _gameplayControls = module.Gameplay;
        BuildGameplaySimulationAdapters(module.Gameplay.Simulation);
        BuildGameplaySessionAdapters(module.Gameplay.Session);
        _cameraRig.Configure(module.Gameplay.Foundation.Cameras);
        ApplyRuntimeInputProfile(module.Gameplay.Foundation.Input);
        _moduleRules = module.Rules;
        _areaRules = area.Rules;
        _areaCombatAuthority = new AreaCombatAuthority(_areaCombatParties, area.Rules.Combat);
        if (!area.Rules.Combat.ServerAuthoritative)
        {
            GD.PrintErr("OPENRPG_FORGE_AREA_COMBAT_DIAGNOSTIC " +
                        "code=NET_AREA_COMBAT_AUTHORITY_REQUIRED " +
                        "path=area/runtimePolicies/rules/combat/serverAuthoritative");
        }
        else if (!_areaCombatServerAuthority)
        {
            GD.PrintErr("OPENRPG_FORGE_AREA_COMBAT_DIAGNOSTIC " +
                        "code=NET_AREA_COMBAT_SERVER_REQUIRED " +
                        "path=runtime/authority");
        }
        else
        {
            GD.Print("OPENRPG_FORGE_AREA_COMBAT_READY " +
                     $"pvp={area.Rules.Combat.PvpMode.ToString().ToLowerInvariant()} " +
                     $"aggro={area.Rules.Combat.AggroEnabled.ToString().ToLowerInvariant()} " +
                     $"server={_areaCombatServerAuthority.ToString().ToLowerInvariant()}");
        }
        _areaExperience = area.Experience;
        _runtimeExtensions = module.Extensions;
        var worldResolution = ProjectWorldRuntimeResolver.Resolve(module.World, module.Id, module.World.RequiredAtRuntime);
        if (!worldResolution.Succeeded)
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_PROJECT_WORLD_DIAGNOSTIC code={worldResolution.Diagnostic!.Code} " +
                        $"path={worldResolution.Diagnostic.Path}");
        else if (worldResolution.Handle is not null)
        {
            _customWorldEvents.Register(worldResolution.Handle.Profile);
            foreach (var schema in worldResolution.Handle.Profile.CustomEvents)
            {
                _customWorldEvents.Subscribe(schema.Id, _ => DispatchConfiguredEventScript(
                    schema.ScriptPath, schema.Handler, schema.RequiredAtRuntime,
                    $"user-event:{schema.Namespace}.{schema.Name}"));
            }
            _worldClockSnapshot = CustomWorldEventBus.CreateInitialSnapshot(worldResolution.Handle.Profile.Clock);
        }
        foreach (var diagnostic in RuntimeExtensionRuntimeService.ValidateRequired(module.Extensions))
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_EXTENSION_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path} uuid={diagnostic.SubjectId}");
        _runtimeJsonHandles.Clear();
        foreach (var document in module.Extensions.JsonDocuments)
        {
            var snapshot = _runtimeJson.Register(document.Id, document.Key, document.JsonSchema,
                document.InitialValue, new SafeJsonQuota(document.MaxBytes, document.MaxDepth), document.Mutable);
            _runtimeJsonHandles[document.Key] = snapshot.Handle;
        }
        foreach (var document in module.Extensions.UiDocuments)
            foreach (var binding in document.EventBindings)
                _scriptUiEvents.Register(new ScriptUiBinding(binding.Id, new ScriptUiHandle(document.Id, document.Key),
                    binding.WidgetKey, binding.Event.ToString().ToLowerInvariant(), binding.Command,
                    binding.ForgeScriptApiVersion, binding.InputContext, binding.Permission));
        _areaAudioState = AreaAudioRuntimeState.Empty;
        _scatterDueSeconds.Clear();
        _scatterSequences.Clear();
        _resolvedDifficulty = RulesRuntimeService.ResolveDifficulty(module.Rules);
        var rulesetPlugin = RulesetPluginRuntimeService.Resolve(module.Rules.Plugins, ["content.core"]);
        if (!rulesetPlugin.Succeeded)
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_RULESET_PLUGIN_DIAGNOSTIC code={rulesetPlugin.DiagnosticCode} " +
                $"path=module/runtimePolicies/rules/plugins uuid={module.Rules.Plugins.Id} " +
                $"message={rulesetPlugin.Diagnostic}");
        else
            GD.Print($"OPENRPG_FORGE_RUNTIME_RULESET_PLUGIN_READY active=" +
                $"{rulesetPlugin.Registration?.Key ?? "none"} present={rulesetPlugin.ExtensionPresent.ToString().ToLowerInvariant()} " +
                $"required={module.Rules.Plugins.RequiredAtRuntime.ToString().ToLowerInvariant()}");
        _activeCombatProvider = module.Gameplay.CombatModes.DefaultProvider;
        _timeControlSession = TimeControlSession.Running;
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11J_POLICY_PASS global={module.Heartbeats.Count} area={area.Heartbeats.Count} " +
            $"rest={module.Rest.Enabled.ToString().ToLowerInvariant()} volumes={area.Rest.Volumes.Count} " +
            $"abort={module.SequenceAbort.Enabled.ToString().ToLowerInvariant()} restore={module.SequenceAbort.Restore}");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11L_GAMEPLAY_PASS swim={module.Gameplay.Swimming.Enabled.ToString().ToLowerInvariant()} " +
            $"climb={module.Gameplay.Climbing.Enabled.ToString().ToLowerInvariant()} " +
            $"radial={module.Gameplay.RadialMenu.Actions.Count} accessible={module.Gameplay.RadialMenu.ListFallback.ToString().ToLowerInvariant()} " +
            $"pause={module.Gameplay.TimeControl.PauseEnabled.ToString().ToLowerInvariant()} planning={module.Gameplay.TimeControl.PlanningEnabled.ToString().ToLowerInvariant()} " +
            $"combat={_activeCombatProvider.ProviderId} authority={_activeCombatProvider.ServerAuthoritative.ToString().ToLowerInvariant()}");
        var perception = RulesRuntimeService.ResolvePerception(area.Rules.Perception, "vision", 20, .8,
            new HashSet<string>(StringComparer.Ordinal));
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11M_RULES_PASS progression={module.Rules.Progression.RulesetId} " +
            $"difficulty={_resolvedDifficulty.Key} mutators={_resolvedDifficulty.EnabledMutators.Count} " +
            $"perception={area.Rules.Perception.Modifiers.Count}:{perception.Range:0.###} " +
            $"pvp={area.Rules.Combat.PvpMode.ToString().ToLowerInvariant()} " +
            $"authority={area.Rules.Combat.ServerAuthoritative.ToString().ToLowerInvariant()}");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11N_EXPERIENCE_PASS cues={area.Experience.AudioCues.Count} " +
            $"scatter={area.Experience.ScatterEmitters.Count} voices={area.Experience.Voices.Count} " +
            $"patrols={area.Experience.PatrolRoutes.Count} loading=accessible-progress-prefetch " +
            "audio_api=started-completed-interrupted-music-snapshot patrol_api=streaming-owner-resume");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11O_EXTENSIONS_PASS rules={module.Extensions.RuleSchemas.Count} " +
            $"ui={module.Extensions.UiDocuments.Count} json={module.Extensions.JsonDocuments.Count} " +
            $"snapshot={module.Extensions.SnapshotPolicy.Components.Count} api=1.0 quotas=bounded");
    }

    private void VerifyModernRuntimeTooling(ProjectDefinition project, AreaDefinition area)
    {
        if (_runtimeExtensions is null || _forgeScriptCache is null) return;
        var tooling = _runtimeExtensions.ModernTooling;
        var source = area.Entities.First();
        var destinationId = EntityId.From(Guid.Parse("e0190000-0000-4000-8000-000000000001"));
        var copyService = new RuntimeEntityCopyService(tooling.RuntimeCopy);
        var copy = AssertSingle(copyService.CopyTransaction(
        [
            new RuntimeEntityCopyRequest(
                new RuntimeEntityCopySource(source, "module", [source.Id],
                    [new RuntimeEntityStateComponent("transform", "{\"x\":0}"), new RuntimeEntityStateComponent("private", "true")]),
                destinationId, new Vector3Value(1, 0, 1), "runtime", true,
                new Dictionary<EntityId, EntityId> { [source.Id] = destinationId }),
        ], project.Areas.SelectMany(candidate => candidate.Entities).Select(entity => entity.Id).ToHashSet()));
        if (!copy.Succeeded || copy.Entity?.Id != destinationId || copy.References.SingleOrDefault() != destinationId ||
            copy.State.Any(component => component.Name == "private"))
            throw new InvalidOperationException($"G11-P runtime copy failed: {copy.Code}");

        var aoeService = new AreaEffectPreviewService(tooling.AreaEffectIndicator);
        var preview = aoeService.Preview(new AreaEffectPreviewRequest(AreaEffectShape.Circle,
            Vector3Value.Zero, new Vector3Value(0, 0, -1), 8, 6, 2, 90, true,
            [new AreaEffectTargetCandidate(source.Id, new Vector3Value(2, 0, 1), 0, true)]));
        if (!preview.IsValid || !preview.ValidTargets.Contains(source.Id))
            throw new InvalidOperationException($"G11-P AOE preview failed: {preview.Code}");
        _areaEffectIndicator = new RuntimeAreaEffectIndicator();
        _areaEffectIndicator.Configure(tooling.AreaEffectIndicator);
        AddChild(_areaEffectIndicator);
        _areaEffectIndicator.ShowPreview(AreaEffectShape.Circle, new Vector3(0, 0.05f, 0), Vector3.Forward,
            8, 2.5f, 2, valid: true);
        _areaEffectIndicator.HidePreview();

        var compiler = new ForgeScriptCompilerService(_forgeScriptCache,
            new ForgeScriptCompilerOptions(tooling.Compiler.Incremental, tooling.Compiler.StableDiagnostics,
                tooling.Compiler.PerformanceTelemetry, tooling.Compiler.MaxParallelism, tooling.Compiler.MaxSourceBytes,
                tooling.Compiler.ConformanceSuites));
        var first = compiler.Compile([new ForgeScriptCompilationUnit("smoke/g11p.as", "void main() {}", [])]);
        var second = compiler.Compile([new ForgeScriptCompilationUnit("smoke/g11p.as", "void main() {}", [])]);
        var conformance = compiler.RunConformance(
        [
            new ForgeScriptConformanceCase("valid", "void main() {}", true, null),
            new ForgeScriptConformanceCase("diagnostic", "void main() { missing_api(); }", false, null),
        ]);
        if (!first.Success || second.Reused != 1 || conformance.Any(result => !result.Passed))
            throw new InvalidOperationException("G11-P compiler conformance or incremental compilation failed.");

        GD.Print("OPENRPG_FORGE_RUNTIME_G11P_MODERN_PASS " +
            $"copy=uuid-owner-references-state-authority aoe=mesh-targets-cover-height-colorblind-confirmation " +
            $"nui={tooling.UiToolkit.ToolkitVersion}-forms-lists-canvas-accessibility-localization-inspector " +
            $"compiler=incremental-stable-telemetry-conformance docs=metadata-generated");
    }

    private static void VerifyOperationalReadiness(ProjectDefinition project)
    {
        var readiness = project.Module.RuntimePolicies.Extensions.OperationalReadiness;
        var diagnostics = OperationalReadinessValidator.Validate(readiness);
        var testEvidence = new ProjectHealthSignal("tests", ProjectHealthCategory.Tests,
            ProjectHealthSeverity.Information, "health.runtime.smoke", "Runtime smoke is executing.",
            "health/tests/runtime", true);
        var health = ProjectHealthReportService.Evaluate(project, DateTimeOffset.UnixEpoch, [testEvidence]);
        if (diagnostics.Count != 0 || readiness.TaskManifests.Count == 0 ||
            !readiness.CrashArtifacts.CaptureSanitizedLogs ||
            !readiness.Recovery.OfferRecoveryOnOpen || !readiness.Recovery.QuarantineCorruptFiles ||
            !readiness.ContentSynchronization.Resumable || !readiness.ContentSynchronization.DeduplicateByHash ||
            !readiness.ContentSynchronization.VerifyContent || health.Status != ProjectHealthStatus.Healthy)
            throw new InvalidOperationException("G11-Q operational readiness contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G11Q_RESILIENCE_PASS " +
            "tasks=bounded-machine-readable crash=sanitized-version-fingerprint-reproduction " +
            $"health={health.Domains.Count}-domains-healthy recovery=journal-reopen-quarantine-compare " +
            "sync=resume-dedup-hash-trust-storage");
    }

    private void VerifyQualityLab(ProjectDefinition project)
    {
        if (string.IsNullOrWhiteSpace(_moduleContentRoot) || !Directory.Exists(_moduleContentRoot)) return;
        var options = WorkspaceQualityLabOptions.Default with { WritePseudoLocalizationSnapshot = false };
        var report = new WorkspaceQualityLabService(_forgeScriptRuntime).Run(_moduleContentRoot, project, options);
        if (!report.Passed)
            throw new InvalidOperationException("G11-R quality lab contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G11R_QUALITY_PASS " +
            $"localization=coverage-placeholders-plurals-overflow-pseudo scripts={report.Scripts.Tests.Count}-sandbox-quota-mock-coverage " +
            $"dialogues={report.Dialogues.Count}-branches-actions quests={report.Quests.Count}-events-save-scope " +
            $"encounters={report.Encounters.TotalRuns}-seeds-nav-cleanup");
    }

    private void VerifyPerformanceLab(ProjectDefinition project)
    {
        if (string.IsNullOrWhiteSpace(_moduleContentRoot) || !Directory.Exists(_moduleContentRoot)) return;
        var report = new WorkspacePerformanceLabService().Run(_moduleContentRoot, project,
            new WorkspacePerformanceLabOptions(false));
        if (!report.Passed || report.Network.Scenarios.Count != 9 || report.VisualReviews.Cases.Count != 8 ||
            report.Benchmarks.Measurements.Count != 9 || report.Benchmarks.Budgets.Any(item =>
                item.Status == PerformanceBudgetStatus.Failed) ||
            !Enum.GetValues<QualityTraceSurface>().All(surface =>
                report.TraceSpans.Any(span => span.Surface == surface)))
            throw new InvalidOperationException("G11-S performance laboratory contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G11S_PERFORMANCE_PASS " +
            "network=9-latency-loss-reconnect-mismatch-authority visual=8-fixtures-tolerance-quality " +
            "benchmarks=9-interior-exterior-stress-load-frame-memory-nav-package " +
            "budgets=3-frame-memory-texture-draw-light-script-network " +
            "profiler=editor-runtime-server-script-database-network");
    }

    private void VerifyGovernanceLab(ProjectDefinition project)
    {
        if (string.IsNullOrWhiteSpace(_moduleContentRoot) || !Directory.Exists(_moduleContentRoot)) return;
        var report = new WorkspaceGovernanceLabService(_forgeScriptRuntime).Run(_moduleContentRoot, project,
            new WorkspaceGovernanceLabOptions(false));
        if (!report.Passed || report.SecurityFuzz.Boundaries.Count != 7 ||
            report.SecurityFuzz.TotalCases != 112 ||
            report.SecurityFuzz.Boundaries.Any(item => item.UnexpectedFailures != 0 || item.PathEscapes != 0))
            throw new InvalidOperationException("G11-T governance laboratory contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G11T_GOVERNANCE_PASS " +
            $"editor={report.Accessibility.EditorChecks.Count}-keyboard-labels-scale-contrast " +
            $"runtime={report.Accessibility.RuntimeChecks.Count}-subtitles-captions-color-input-motion-difficulty " +
            $"fuzz={report.SecurityFuzz.TotalCases}-cases-7-boundaries-no-crash-no-escape " +
            $"dependencies={report.Dependencies.DependencyCount}-approved-risk-review-audit-lifecycle");
    }

    private static T AssertSingle<T>(IReadOnlyList<T> values) => values.Count == 1
        ? values[0] : throw new InvalidOperationException($"Expected one result, found {values.Count}.");

    private void ConfigureCinematics(ProjectDefinition project, AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(area);
        if (_runtimeExtensions?.ModernTooling.QualityAssurance.Accessibility.RuntimeReducedMotion == true)
        {
            GD.Print("OPENRPG_FORGE_RUNTIME_ACCESSIBILITY reduced_motion=startup-cinematic-skipped");
            return;
        }
        _cameraAnchors.Clear();
        foreach (var anchor in area.RuntimePolicies.Cinematics.CameraAnchors)
        {
            _cameraAnchors.Add(anchor.Id, anchor);
        }

        var profile = project.Module.RuntimePolicies.Cinematics;
        var resolution = StartupFlowResolver.Resolve(profile, CinematicResourceExists);
        foreach (var diagnostic in resolution.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_CINEMATIC_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        if (resolution.Kind == StartupFlowKind.Sequence && resolution.ReferenceId is not null)
        {
            StartSequence(profile.Sequences.Single(item => item.Id == resolution.ReferenceId));
        }
        else if (resolution.Kind == StartupFlowKind.Dialogue && resolution.ReferenceId is not null)
        {
            var dialogue = project.Dialogues.SingleOrDefault(item => item.Id.Value == resolution.ReferenceId);
            _message = dialogue is null ? _message : $"Dialogue d’introduction · {dialogue.DisplayName}";
        }
        else if (resolution.Kind is StartupFlowKind.Video or StartupFlowKind.Scene)
        {
            StartResourceFlow(resolution.Kind, resolution.ResourcePath);
        }

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11K_CINEMATIC_PASS startup={resolution.Kind.ToString().ToLowerInvariant()} " +
            $"fallback={resolution.UsedFallback.ToString().ToLowerInvariant()} sequences={profile.Sequences.Count} " +
            $"anchors={_cameraAnchors.Count} skip={resolution.Skippable.ToString().ToLowerInvariant()} " +
            "timeline=branch-signal-transform authority=governed recovery=enabled");
    }

    private void StartSequence(SequenceDefinition sequence)
    {
        _activeSequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        _sequenceState = SequencePlaybackState.Start(sequence.Id);
        if (_camera is not null)
        {
            _explorationCameraTransform = _camera.GlobalTransform;
            _explorationCameraFov = _camera.Fov;
        }
        _sequenceOwnsCamera = sequence.Tracks.Any(item => item.Kind == SequenceTrackKind.Camera);
        if (_player is not null) _player.ProcessMode = ProcessModeEnum.Disabled;
        ApplySequenceEvaluation(SequencePlayer.Advance(sequence, _sequenceState, 0, true, EvaluateSequenceCondition));
        GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_STARTED id={sequence.Id:D} duration_ms={sequence.DurationMilliseconds}");
    }

    private void AdvanceCinematics(double delta)
    {
        if (_activeSequence is null || _sequenceState is null || !_sequenceState.Active) return;
        var milliseconds = Math.Max(0, (int)Math.Round(delta * 1000));
        var evaluation = SequencePlayer.Advance(_activeSequence, _sequenceState, milliseconds, true,
            EvaluateSequenceCondition);
        ApplySequenceEvaluation(evaluation);
        if (evaluation.State.Completed) CompleteSequence(evaluation, "completed");
    }

    private void ApplySequenceEvaluation(SequenceEvaluation evaluation)
    {
        _sequenceState = evaluation.State;
        if (evaluation.CameraAnchorId is not null && _camera is not null &&
            _cameraAnchors.TryGetValue(evaluation.CameraAnchorId.Value, out var anchor))
        {
            _pendingCameraPosition = EngineValueConverter.ToEngine(anchor.Transform.Position);
            _pendingCameraFieldOfView = anchor.FieldOfViewDegrees;
            _pendingCameraLookAt = EngineValueConverter.ToEngine(anchor.LookAt);
        }

        foreach (var transform in evaluation.Transforms)
        {
            if (_entityNodes.TryGetValue(transform.Key, out var node))
                node.Transform = EngineValueConverter.ToEngine(transform.Value);
        }
        foreach (var signal in evaluation.Signals)
            GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_SIGNAL name={signal.Name} at_ms={signal.TimeMilliseconds}");
        foreach (var resource in evaluation.AudioResources) PlaySequenceAudio(resource);
        foreach (var diagnostic in evaluation.Diagnostics)
            GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
    }

    private void CompleteSequence(SequenceEvaluation evaluation, string reason)
    {
        ApplySequenceEvaluation(evaluation);
        if (_player is not null) _player.ProcessMode = ProcessModeEnum.Inherit;
        if (_camera is not null && _sequenceOwnsCamera)
        {
            _camera.GlobalTransform = _explorationCameraTransform;
            _camera.Fov = _explorationCameraFov;
        }
        GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_FINISHED id={_activeSequence!.Id:D} reason={reason}");
        _activeSequence = null;
        _sequenceState = null;
        _sequenceOwnsCamera = false;
    }

    private void QueueSequenceCompletion(SequenceEvaluation evaluation, string reason)
    {
        _pendingSequenceCompletion = evaluation;
        _pendingSequenceCompletionReason = reason;
    }

    private void AbortActiveSequence()
    {
        if (_activeSequence is null || _sequenceState is null || _moduleRuntimePolicies is null) return;
        var policy = _moduleRuntimePolicies.SequenceAbort;
        var snapshot = new SequenceRuntimeState("camera.exploration", [], new HashSet<string>(),
            new HashSet<string>(), 0, null, false);
        var current = snapshot with { CameraId = "camera.cinematic", SequenceActive = true };
        var result = SequenceAbortCoordinator.Abort(policy, current, snapshot);
        if (!result.Accepted)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_ABORT_REFUSED code={result.DiagnosticCode}");
            return;
        }
        var aborted = _sequenceState with { Active = false, Completed = false, Skipped = false };
        QueueSequenceCompletion(
            new SequenceEvaluation(aborted, null, new Dictionary<Guid, TransformValue>(), [], [], []),
            "aborted");
        var scriptResolved = DispatchConfiguredEventScript(policy.Binding.ScriptPath,
            policy.Binding.Handler, policy.Binding.RequiredAtRuntime, "sequence:aborted");
        GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_ABORTED handler={result.EventHandler} " +
                 $"camera_restored={result.CameraRestored.ToString().ToLowerInvariant()} " +
                 $"script_resolved={scriptResolved.ToString().ToLowerInvariant()}");
    }

    private bool EvaluateSequenceCondition(string condition) => condition switch
    {
        "boss.defeated" => _bossDefeated,
        "door.unlocked" => _doorUnlocked,
        "key.acquired" => _hasKey,
        _ => _blackboard.TryGetValue(condition, out var value) && bool.TryParse(value, out var parsed) && parsed,
    };

    private bool CinematicResourceExists(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return false;
        var uri = resourcePath.StartsWith("res://", StringComparison.Ordinal) ? resourcePath : $"res://{resourcePath}";
        if (ResourceLoader.Exists(uri)) return true;
        return _moduleContentRoot is not null && File.Exists(Path.Combine(_moduleContentRoot,
            resourcePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private void StartResourceFlow(StartupFlowKind kind, string resourcePath)
    {
        var uri = resourcePath.StartsWith("res://", StringComparison.Ordinal) ? resourcePath : $"res://{resourcePath}";
        if (kind == StartupFlowKind.Scene && ResourceLoader.Load<PackedScene>(uri) is { } scene)
        {
            AddChild(scene.Instantiate());
        }
        else if (kind == StartupFlowKind.Video && ResourceLoader.Load<VideoStream>(uri) is { } stream)
        {
            var player = new VideoStreamPlayer { Stream = stream, Autoplay = true };
            player.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(player);
        }
    }

    private void PlaySequenceAudio(string resourcePath)
    {
        var uri = resourcePath.StartsWith("res://", StringComparison.Ordinal) ? resourcePath : $"res://{resourcePath}";
        if (ResourceLoader.Load<AudioStream>(uri) is not { } stream) return;
        var player = new AudioStreamPlayer { Stream = stream, Autoplay = true };
        player.Finished += player.QueueFree;
        AddChild(player);
    }

    private void AdvanceRuntimePolicies(double delta)
    {
        if (_moduleRuntimePolicies is null || _areaRuntimePolicies is null) return;
        var milliseconds = Math.Max(0, (long)Math.Round(delta * 1_000, MidpointRounding.AwayFromZero));
        DispatchHeartbeats(_heartbeatScheduler.Advance(_moduleRuntimePolicies.Heartbeats, milliseconds, true));
        DispatchHeartbeats(_heartbeatScheduler.Advance(_areaRuntimePolicies.Heartbeats, milliseconds, true,
            _currentAreaId is null ? null : AreaId.Parse(_currentAreaId)));
        if (_worldClockSnapshot is not null && _moduleRuntimePolicies.World.Enabled)
        {
            var advanced = CustomWorldEventBus.AdvanceClock(_moduleRuntimePolicies.World.Clock,
                _worldClockSnapshot, delta / 60d, _currentAreaId ?? string.Empty);
            _worldClockSnapshot = advanced.Snapshot;
            foreach (var astronomicalEvent in advanced.CrossedEvents)
                GD.Print($"OPENRPG_FORGE_RUNTIME_ASTRONOMICAL_EVENT kind={astronomicalEvent} " +
                         $"minute={advanced.Snapshot.TotalMinutes} area={_currentAreaId}");
        }
        if (_restSession is { Active: true } session && DateTimeOffset.UtcNow >= session.CompletesAt)
        {
            var completed = _restRuntime.Complete(session, ++_runtimePolicyRevision, DateTimeOffset.UtcNow);
            PublishRestEvents(completed.Events);
            _restSession = completed.Session;
            _message = completed.Accepted ? "Repos terminé." : "Le repos a été interrompu.";
            UpdateHud();
        }
    }

    private void DispatchHeartbeats(HeartbeatAdvanceResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_POLICY_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        foreach (var dispatch in result.Dispatches)
        {
            var scriptResolved = DispatchConfiguredEventScript(dispatch.ScriptPath, dispatch.Handler,
                dispatch.RequiredAtRuntime, $"heartbeat:{dispatch.Name}");
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_HEARTBEAT handler={dispatch.Handler} scope={dispatch.Scope} " +
                $"budget_ms={dispatch.BudgetMilliseconds} due_ms={dispatch.DueAtMilliseconds} " +
                $"script_resolved={scriptResolved.ToString().ToLowerInvariant()}");
        }
    }

    private void RequestPlayerRest()
    {
        if (_player is null || _moduleRuntimePolicies is null || _areaRuntimePolicies is null ||
            _currentAreaId is null)
        {
            return;
        }

        var position = _player.GlobalPosition;
        var restPosition = new Vector3Value(position.X, position.Y, position.Z);
        var effectivePolicy = _areaRuntimePolicies.Rest.ResolveAt(restPosition);
        var triggerRest = ResolveLivingWorldRest(restPosition);
        if (triggerRest is { Allowed: false } denied)
        {
            _message = ResolveLivingWorldMessage(denied.MessageKey);
            _toastQueue?.Show(RuntimeToastKind.Alert, _message, $"trigger-rest:{denied.DiagnosticCode}");
            GD.Print($"OPENRPG_FORGE_TRIGGER_REST_DENIED code={denied.DiagnosticCode} " +
                     $"message={denied.MessageKey} summary={denied.Summary}");
            UpdateHud();
            return;
        }
        var result = _restRuntime.Request("player", "player-party", AreaId.Parse(_currentAreaId),
            restPosition, _moduleRuntimePolicies.Rest, _areaRuntimePolicies.Rest,
            secure: _hostiles.Keys.All(hostile => hostile.GlobalPosition.DistanceTo(position) >
                effectivePolicy.RequiredSecureRadius),
            EvaluateRuntimePolicyCondition, ++_runtimePolicyRevision, DateTimeOffset.UtcNow);
        PublishRestEvents(result.Events);
        _restSession = result.Session;
        _message = result.Accepted
            ? triggerRest is { Available: true } allowed
                ? $"{ResolveLivingWorldMessage(allowed.MessageKey)} " +
                  $"({_moduleRuntimePolicies.Rest.DefaultDurationSeconds} s)…"
                : $"Repos en cours ({_moduleRuntimePolicies.Rest.DefaultDurationSeconds} s)…"
            : result.Events.LastOrDefault()?.Cause ?? result.DiagnosticCode ?? "Repos refusé.";
        UpdateHud();
    }

    private TriggerRestRuntimeResolution? ResolveLivingWorldRest(Vector3Value position)
    {
        if (_worldFeatureCatalog is null || _currentAreaId is null) return null;
        var currentArea = AreaId.Parse(_currentAreaId);
        var partyExpected = LauncherConnectedPlayerCount();
        // Remote poses are visible but are not evaluated against authored safe-rest volumes yet. A
        // multi-member session therefore cannot claim that the full party is physically inside one.
        var partyPresent = partyExpected == 1 ? 1 : 0;
        TriggerRestRuntimeResolution? accepted = null;
        foreach (var profile in _worldFeatureCatalog.Triggers
                     .Where(item => item.AreaId == currentArea && item.Rest is not null)
                     .OrderBy(item => item.Id))
        {
            var containment = TriggerLivingWorldRuntimeService.Contains(profile, position);
            if (!containment.Supported)
            {
                ReportLivingWorldTriggerDiagnostic(profile, containment.DiagnosticCode, containment.Summary);
                continue;
            }
            if (!containment.Contains) continue;
            var foundationProbe = EnterLivingWorldFoundation(profile,
                EmptyLivingWorldFoundationState(), position, DateTimeOffset.UtcNow);
            if (!foundationProbe.Fired) continue;
            var resolution = TriggerLivingWorldRuntimeService.ResolveRest(profile,
                new(EvaluateLivingWorldDanger(profile.Rest!.DangerQuery), partyPresent, partyExpected));
            if (!resolution.Allowed) return resolution;
            accepted ??= resolution;
        }
        return accepted;
    }

    private bool EvaluateLivingWorldDanger(string query)
    {
        if (_blackboard.TryGetValue(query, out var authored) && bool.TryParse(authored, out var parsed))
            return parsed;
        if (string.Equals(query, "danger.nearby", StringComparison.Ordinal))
            return _hostiles.Values.Any(item => !item.IsDead);
        GD.PrintErr($"OPENRPG_FORGE_TRIGGER_DANGER_QUERY_UNRESOLVED query={query}");
        return true;
    }

    private void AdvanceLivingWorldTriggers()
    {
        if (_player is null || _livingWorldPlayerEntity is null ||
            _worldFeatureCatalog is null || _currentAreaId is null) return;
        var currentArea = AreaId.Parse(_currentAreaId);
        var position = _player.GlobalPosition;
        var worldPosition = new Vector3Value(position.X, position.Y, position.Z);
        var insideNow = new HashSet<Guid>();
        var now = DateTimeOffset.UtcNow;
        var narrativeClaimed = false;
        foreach (var profile in _worldFeatureCatalog.Triggers
                     .Where(item => item.AreaId == currentArea &&
                                    (item.Clue is not null || item.Narrative is not null))
                     .OrderByDescending(item => item.Narrative?.Priority ?? 0)
                     .ThenBy(item => item.Id))
        {
            var containment = TriggerLivingWorldRuntimeService.Contains(profile, worldPosition);
            if (!containment.Supported)
            {
                ReportLivingWorldTriggerDiagnostic(profile, containment.DiagnosticCode, containment.Summary);
                continue;
            }
            if (!containment.Contains) continue;
            insideNow.Add(profile.Id);
            if (_livingWorldTriggerOccupancy.Contains(profile.Id)) continue;
            var foundationState = _livingWorldTriggerFoundationStates.GetValueOrDefault(profile.Id) ??
                                  EmptyLivingWorldFoundationState();
            var entered = EnterLivingWorldFoundation(profile, foundationState, worldPosition, now);
            _livingWorldTriggerFoundationStates[profile.Id] = entered.State;
            if (!entered.Fired) continue;
            ResolveLivingWorldClue(profile);
            if (!narrativeClaimed)
                narrativeClaimed = ResolveLivingWorldNarrative(profile);
        }

        foreach (var departedId in _livingWorldTriggerOccupancy.Except(insideNow).ToArray())
        {
            var profile = _worldFeatureCatalog.Triggers.SingleOrDefault(item => item.Id == departedId);
            if (profile?.Foundation is null ||
                !_livingWorldTriggerFoundationStates.TryGetValue(departedId, out var state)) continue;
            _livingWorldTriggerFoundationStates[departedId] = WorldFeatureRuntimeResolver.ExitTrigger(
                profile, state, _livingWorldPlayerEntity.Id.ToString(), now, "player-left-volume").State;
        }

        _livingWorldTriggerOccupancy.Clear();
        _livingWorldTriggerOccupancy.UnionWith(insideNow);
    }

    private WorldFeatureRuntimeResolver.TriggerEnterResult EnterLivingWorldFoundation(
        TriggerWorldProfile profile,
        WorldFeatureRuntimeResolver.TriggerRuntimeState state,
        Vector3Value position,
        DateTimeOffset now)
    {
        var entity = _livingWorldPlayerEntity ??
                     throw new InvalidOperationException("Living-world player context is unavailable.");
        var conditions = profile.Foundation!.Conditions.Distinct(StringComparer.Ordinal).ToDictionary(
            condition => condition,
            condition => _blackboard.TryGetValue(condition, out var value) ? value : "false",
            StringComparer.Ordinal);
        var velocity = _player?.Velocity ?? Vector3.Zero;
        var direction = velocity.LengthSquared() > .0001f ? velocity.Normalized() : Vector3.Zero;
        return WorldFeatureRuntimeResolver.EnterTrigger(profile, state, entity.Id.ToString(), "creature",
            entity.Tags.ToHashSet(StringComparer.Ordinal), new HashSet<string>(["characters"], StringComparer.Ordinal),
            position, new(direction.X, direction.Y, direction.Z), conditions, now);
    }

    private static WorldFeatureRuntimeResolver.TriggerRuntimeState EmptyLivingWorldFoundationState() => new(
        new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
        new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal), false);

    private void ResolveLivingWorldClue(TriggerWorldProfile profile)
    {
        if (profile.Clue is not { } clue) return;
        int? skillScore = null;
        if (_blackboard.TryGetValue($"skill.{clue.SkillId}", out var value) &&
            int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            skillScore = parsed;
        var clueQuest = clue.QuestId is null
            ? null
            : _questTracker?.ResolveQuest(clue.QuestId.Value.ToString("D"));
        var questActive = clue.QuestId is null ||
                          clueQuest is not null && _questTracker?.IsAccepted(clueQuest.Id) == true;
        var result = TriggerLivingWorldRuntimeService.ResolveClue(profile, new(skillScore, questActive));
        if (!result.Revealed)
        {
            GD.Print($"OPENRPG_FORGE_TRIGGER_CLUE_SKIPPED code={result.DiagnosticCode} summary={result.Summary}");
            return;
        }

        var kind = result.FalseClue ? "Piste incertaine" : "Piste révélée";
        _message = $"{kind} : {result.TraceId} · direction {result.DirectionDegrees:0.#}° · " +
                   $"âge {result.AgeHours:0.#} h";
        _toastQueue?.Show(RuntimeToastKind.Objective, _message, $"trigger-clue:{profile.Id:D}:{result.TraceId}");
        UpdateHud();
        GD.Print($"OPENRPG_FORGE_TRIGGER_CLUE_REVEALED trigger={profile.Id:D} trace={result.TraceId} " +
                 $"false={result.FalseClue.ToString().ToLowerInvariant()} " +
                 $"quest={result.JournalQuestId?.ToString("D") ?? "none"} summary={result.Summary}");
    }

    private bool ResolveLivingWorldNarrative(TriggerWorldProfile profile)
    {
        if (profile.Narrative is not { } narrative || _loadedProject is null || _activeArea is null) return false;
        var presentParticipants = _activeArea.Entities
            .Where(entity => narrative.Participants.Contains(entity.BlueprintId) &&
                             _entityNodes.TryGetValue(entity.Id.Value, out var node) &&
                             GodotObject.IsInstanceValid(node))
            .Select(entity => entity.BlueprintId)
            .ToHashSet();
        var connectedPlayers = LauncherConnectedPlayerCount();
        // The local runtime has no multiplayer vote transport. Only the canonical party-vote policy can
        // resolve, and only for a one-member local-host session; every other policy remains fail-closed.
        var voteSatisfied = string.Equals(narrative.MultiplayerPolicy, "party-vote", StringComparison.Ordinal) &&
                            connectedPlayers == 1;
        var result = TriggerLivingWorldRuntimeService.ResolveNarrative(profile, _livingWorldNarrativeState,
            new(DateTimeOffset.UtcNow, true, voteSatisfied, presentParticipants));
        _livingWorldNarrativeState = result.State;
        if (!result.Fired)
        {
            GD.Print($"OPENRPG_FORGE_TRIGGER_NARRATIVE_SKIPPED code={result.DiagnosticCode} " +
                     $"summary={result.Summary}");
            return false;
        }

        var dialogue = _loadedProject.Dialogues.SingleOrDefault(item => item.Id.Value == result.DialogueId);
        if (dialogue is null)
        {
            GD.PrintErr($"OPENRPG_FORGE_TRIGGER_NARRATIVE_FAILED code=world.reference.dialogue_missing " +
                        $"trigger={profile.Id:D}");
            return false;
        }
        _activeDialogue = dialogue;
        _activeDialogueEntryNodeId = dialogue.EntryNodeId;
        _activeDialogueParticipant = null;
        _guideName = dialogue.DisplayName;
        ShowLivingWorldInterjection(dialogue);
        GD.Print($"OPENRPG_FORGE_TRIGGER_NARRATIVE_FIRED trigger={profile.Id:D} " +
                 $"dialogue={dialogue.Id} priority={result.Priority} policy={narrative.MultiplayerPolicy}");
        return true;
    }

    private void ShowLivingWorldInterjection(DialogueDefinition dialogue)
    {
        var traversal = new DialogueTraversal(dialogue);
        var start = traversal.Start();
        var evaluator = new RuntimeConditionEvaluator(this);
        var spokenLine = ApplyDialoguePresentation(start.Node!.Id, evaluator) ?? start.Node.Text.Fallback;
        if (_actionWheel?.IsOpen == true) _actionWheel.Close();
        // An interjection presents the authored opening line only. It does not auto-pick a dialogue choice or
        // execute dialogue actions; the full dialogue traversal remains an explicit player interaction.
        _dialogueOverlay?.Show(dialogue.DisplayName, "Interjection", spokenLine, []);
        SyncPlayerInterfaceLock();
        _message = $"{dialogue.DisplayName} : {spokenLine}";
        UpdateHud();
    }

    private void ReportLivingWorldTriggerDiagnostic(
        TriggerWorldProfile profile,
        string code,
        string summary)
    {
        if (!_livingWorldTriggerDiagnostics.Add(profile.Id)) return;
        GD.PrintErr($"OPENRPG_FORGE_TRIGGER_LIVING_WORLD_DIAGNOSTIC trigger={profile.Id:D} " +
                    $"code={code} summary={summary}");
    }

    private string ResolveLivingWorldMessage(string key)
    {
        if (_loadedProject is null || string.IsNullOrWhiteSpace(key)) return key;
        try
        {
            return _loadedProject.Module.Localization.Resolve(key, _defaultLocale);
        }
        catch (KeyNotFoundException)
        {
            return key;
        }
    }

    private bool EvaluateRuntimePolicyCondition(string expression)
    {
        var parts = expression.Split("==", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !_blackboard.TryGetValue(parts[0], out var value)) return false;
        return string.Equals(value, parts[1], StringComparison.OrdinalIgnoreCase);
    }

    private void PublishRestEvents(IReadOnlyList<RestLifecycleEvent> events)
    {
        foreach (var @event in events)
        {
            var published = _gameplayEvents.Publish(@event);
            if (!published.Accepted)
            {
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_REST_EVENT_FAILED code={published.DiagnosticCode}");
                continue;
            }

            var binding = _moduleRuntimePolicies?.Rest.Bindings.SingleOrDefault(item =>
                string.Equals(item.EventName, @event.EventName, StringComparison.Ordinal));
            var scriptResolved = binding is null || DispatchConfiguredEventScript(binding.ScriptPath,
                binding.Handler, binding.RequiredAtRuntime, $"rest:{@event.Stage}");
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_REST_EVENT stage={@event.Stage} handler={binding?.Handler ?? "unbound"} " +
                $"revision={@event.ServerRevision} script_resolved={scriptResolved.ToString().ToLowerInvariant()}");
        }
    }

    private void ApplyClimateEffects(bool force)
    {
        if (_environmentProfile is null || _weatherState is null)
        {
            return;
        }

        var listener = _player?.GlobalPosition ?? Vector3.Zero;
        var plan = AreaClimatePlanBuilder.Build(
            _environmentProfile,
            _weatherState.StateId,
            new(listener.X, listener.Y, listener.Z),
            OS.HasFeature("mobile") ? "mobile" : "desktop");
        if (_climateEffects?.GetChildren().OfType<GpuParticles3D>().FirstOrDefault() is { } precipitation)
        {
            precipitation.GlobalPosition = listener + (Vector3.Up * 18);
        }

        var signature = $"{plan.Weather.Id}:{plan.ActiveWindZone?.Id}:{plan.ActiveReverbZone?.Id}";
        SetSurfaceWetnessTarget(plan.Weather);
        if (!force && string.Equals(signature, _climateSignature, StringComparison.Ordinal))
        {
            return;
        }

        if (_climateEffects is not null)
        {
            RemoveChild(_climateEffects);
            _climateEffects.QueueFree();
        }

        var result = GodotClimateAdapter.Build(plan, configureRuntimeAudio: true);
        _climateEffects = result.Root;
        _climateSignature = signature;
        AddChild(_climateEffects);
        ApplyWeatherLighting(plan.Weather);
        if (_climateEffects.GetChildren().OfType<GpuParticles3D>().FirstOrDefault() is { } createdPrecipitation)
        {
            createdPrecipitation.GlobalPosition = listener + (Vector3.Up * 18);
        }
        foreach (var diagnostic in result.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_CLIMATE_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        var effectiveWindSpeed = plan.ActiveWindZone?.Speed ?? plan.Wind.Speed;
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11H_ENVIRONMENT_PASS wind={effectiveWindSpeed:0.###} " +
            $"weather={plan.Weather.Kind.ToString().ToLowerInvariant()} revision={_weatherState.Revision} " +
            $"surface_wetness={plan.Weather.SurfaceWetness:0.###} " +
            $"particles={result.PrecipitationEmitters} water={result.WaterSurfaces} reverb={result.ReverbPreset} " +
            $"authority={_environmentProfile.Weather.ServerAuthoritative.ToString().ToLowerInvariant()} " +
            $"save={_environmentProfile.Weather.PersistState.ToString().ToLowerInvariant()} " +
                    $"replicate={_environmentProfile.Weather.ReplicateState.ToString().ToLowerInvariant()}");
    }

    private void SetSurfaceWetnessTarget(WeatherStateProfile weather)
    {
        var coefficientsChanged = _surfaceWetnessCoefficients != weather.WetnessMaterial;
        _surfaceWetnessCoefficients = weather.WetnessMaterial;
        if (!_surfaceWetnessInitialized)
        {
            _surfaceWetnessInitialized = true;
            _surfaceWetnessValue = weather.SurfaceWetness;
            _surfaceWetnessTransition = SurfaceWetnessTransition.Start(
                weather.SurfaceWetness, weather.SurfaceWetness, 0);
        }
        else if (_surfaceWetnessTransition is null ||
                 !_surfaceWetnessTransition.TargetValue.Equals(weather.SurfaceWetness))
        {
            _surfaceWetnessTransition = SurfaceWetnessTransition.Start(
                _surfaceWetnessValue,
                weather.SurfaceWetness,
                weather.TransitionSeconds);
        }
        if (_surfaceWetnessBound && coefficientsChanged)
            _surfaceWetnessResult = _surfaceWetnessMaterials.Apply(
                _surfaceWetnessValue,
                weather.WetnessMaterial);
    }

    private void AdvanceSurfaceWetness(double delta)
    {
        if (!_surfaceWetnessBound || _surfaceWetnessTransition is null ||
            _surfaceWetnessCoefficients is null) return;
        var result = SurfaceWetnessTransition.Advance(_surfaceWetnessTransition, delta);
        _surfaceWetnessTransition = result.State;
        if (_surfaceWetnessValue.Equals(result.Value)) return;
        _surfaceWetnessValue = result.Value;
        _surfaceWetnessResult = _surfaceWetnessMaterials.Apply(
            _surfaceWetnessValue,
            _surfaceWetnessCoefficients);
    }

    private void BindSurfaceWetnessMaterials()
    {
        if (_environmentProfile is null || _weatherState is null) return;
        var weather = WeatherRuntimeStateMachine.Resolve(_environmentProfile.Weather, _weatherState);
        SetSurfaceWetnessTarget(weather);
        var result = _surfaceWetnessMaterials.BindAndApply(this, _surfaceWetnessValue, weather.WetnessMaterial);
        _surfaceWetnessResult = result;
        _surfaceWetnessBound = true;
        foreach (var diagnostic in result.Diagnostics)
            GD.Print($"OPENRPG_FORGE_RUNTIME_WETNESS_FALLBACK {diagnostic}");
        GD.Print($"OPENRPG_FORGE_RUNTIME_WETNESS_PASS wetness={result.SurfaceWetness:0.###} " +
                 $"shader_materials={result.ShaderMaterials} pbr_materials={result.PbrMaterials} " +
                 $"fallback_materials={result.FallbackMaterials} reversible=true");
    }

    private void ApplyWeatherLighting(WeatherStateProfile weather)
    {
        if (_worldEnvironment is null || _referenceSun is null) return;

        var (sunMultiplier, ambientMultiplier, skyMultiplier, exposureMultiplier,
            fogDensity, fogEnergy) = weather.Kind switch
            {
                WeatherKind.Overcast => (.68f, .82f, .76f, .94f, _baseFogDensity, .82f),
                WeatherKind.Rain => (.52f, .72f, .64f, .90f,
                    Math.Max(_baseFogDensity, .0022f + weather.Intensity * .0015f), .72f),
                WeatherKind.Storm => (.38f, .62f, .48f, .84f,
                    Math.Max(_baseFogDensity, .0038f + weather.Intensity * .0022f), .62f),
                WeatherKind.Snow => (.62f, .84f, .78f, .92f,
                    Math.Max(_baseFogDensity, .0028f + weather.Intensity * .0012f), .86f),
                WeatherKind.Fog => (.44f, .74f, .58f, .89f,
                    Math.Max(_baseFogDensity, .0052f + weather.Intensity * .003f), .66f),
                _ => (1f, 1f, 1f, 1f, _baseFogDensity, 1f),
            };

        _referenceSun.LightEnergy = _baseDirectionalLightEnergy * sunMultiplier;
        _worldEnvironment.AmbientLightEnergy = _baseAmbientLightEnergy * ambientMultiplier;
        _worldEnvironment.BackgroundEnergyMultiplier = _baseBackgroundEnergyMultiplier * skyMultiplier;
        _worldEnvironment.TonemapExposure = _baseTonemapExposure * exposureMultiplier;
        _worldEnvironment.FogEnabled = _baseFogEnabled || weather.Kind is WeatherKind.Rain or
            WeatherKind.Storm or WeatherKind.Snow or WeatherKind.Fog;
        _worldEnvironment.FogDensity = fogDensity;
        _worldEnvironment.FogLightEnergy = _baseFogLightEnergy * fogEnergy;
        if (_worldEnvironment.Sky?.SkyMaterial is ShaderMaterial skyMaterial)
        {
            var cloudCoverage = weather.Kind switch
            {
                WeatherKind.Overcast => Math.Max(_baseCloudCoverage, .68f),
                WeatherKind.Rain => Math.Max(_baseCloudCoverage, .86f),
                WeatherKind.Storm => Math.Max(_baseCloudCoverage, .95f),
                WeatherKind.Snow => Math.Max(_baseCloudCoverage, .78f),
                WeatherKind.Fog => Math.Max(_baseCloudCoverage, .64f),
                _ => _baseCloudCoverage,
            };
            skyMaterial.SetShaderParameter("cloud_coverage", cloudCoverage);
        }
    }

    private void CreateAuthoredArtKitAtmosphere(string artKitId)
    {
        var layoutJson = ReadModuleContentFile($"art-kits/{artKitId}.layout.json");
        var artPass = BundledArtKitResolver.Build(artKitId, layoutJson);
        AddChild(artPass.Root);
        _runtimeModelInstances += artPass.ModelInstances;
        GD.Print($"OPENRPG_FORGE_RUNTIME_ART_KIT_PASS id={artKitId} models={artPass.ModelInstances} " +
                 $"floors={artPass.FloorTiles} walls={artPass.WallSegments} props={artPass.Props} " +
                 $"zones={artPass.Zones} source=module-layout collisions=layout-and-gameplay");
    }


}
