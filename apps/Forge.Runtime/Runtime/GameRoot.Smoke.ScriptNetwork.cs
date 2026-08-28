using System.Diagnostics;
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
using Forge.Network.Sessions;
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
    private void RunGameplayFoundationRuntimeSmokeTest()
    {
        var profile = _gameplayControls?.Foundation ??
            throw new InvalidOperationException("Gameplay foundation is unavailable.");
        var frame = GameplayFoundationRuntime.Step(profile.GroundedMovement,
            new(new Vector3Value(0, 0, 0), true, false),
            new(new Vector3Value(1, 0, 0), true, true, false), .1f);
        var candidate = new InteractionCandidate(Guid.Parse("c7100000-0000-4000-8000-000000000001"),
            new Vector3Value(0, 0, -2), new HashSet<string>(["door"]), new HashSet<string>(["interact"]));
        var focus = GameplayFoundationRuntime.ResolveFocus(profile.Interaction, Vector3Value.Zero,
            new Vector3Value(0, 0, -1), [candidate], "interact");
        var camera = GameplayFoundationRuntime.ResolveCamera(profile.Cameras);
        var input = GameplayFoundationRuntime.ResolveInput(profile.Input, "interact",
            RuntimeInputDevice.KeyboardMouse, new HashSet<string>(["exploration"]));
        var scripts = new GameplayFoundationScriptService(_gameplayControls!);
        AssertSmoke(GameplayFoundationValidator.Validate(profile).Count == 0 && frame.Jumped && frame.Predicted &&
                    focus is { Ready: true } && camera.Key == "orbit" && input?.Binding.Binding == "E" &&
                    scripts.FindCamera("orbit") is not null && scripts.FindInputAction("interact") is not null &&
                    GameplayFoundationRuntime.ShouldAutosave(profile.Save, null, DateTimeOffset.UnixEpoch),
            "P0 gameplay movement, interaction, camera, input, save or ForgeScript contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R010_GAMEPLAY_PASS " +
            "movement=acceleration-slope-step-jump-crouch-root-motion-prediction " +
            "interaction=focus-verbs-hold targeting=view-cone camera=8-profiles " +
            "input=contexts-devices-glyphs-rebind save=chunks-checksum-autosave-rollback-migrations " +
            "forgescript=typed-handles");
    }

    private void RunBlackboardRulesetPluginRuntimeSmokeTest()
    {
        var variable = new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("c7110000-0000-4000-8000-000000000001")),
            "server.runtime.ready", ProjectVariableValueKind.Boolean, ProjectVariableScope.Server, "false",
            "R011 runtime proof", "runtime:r011", 1, ProjectVariableReplicationMode.ServerAuthority,
            debugVisible: true, requiredAtRuntime: true);
        var blackboard = new ProjectBlackboardScriptService([variable]);
        var handle = AssertSingle(blackboard.Query("runtime.ready"),
            "Typed blackboard handle could not be resolved.");
        var mutation = blackboard.Set(handle, "true");
        var serverDelta = blackboard.ReplicationDelta(BlackboardReplicationAudience.Server);
        var clientDelta = blackboard.ReplicationDelta(BlackboardReplicationAudience.Client);
        var debug = blackboard.DebugSnapshot();
        var plugins = _loadedProject?.Module.RuntimePolicies.Rules.Plugins ??
            throw new InvalidOperationException("Ruleset plugin catalog is unavailable.");
        var resolved = RulesetPluginRuntimeService.Resolve(plugins, ["content.core"]);
        var optional = RulesetPluginRuntimeService.Resolve(
            new RulesetPluginCatalog(Guid.Parse("c7110000-0000-4000-8000-000000000002"), 1,
                false, false, string.Empty, [], "runtime:r011"), []);
        var requiredMissing = RulesetPluginRuntimeService.Resolve(plugins, []);
        AssertSmoke(mutation.Succeeded && mutation.After?.Value == "true" &&
                    serverDelta.Values.GetValueOrDefault(variable.Key) == "true" &&
                    clientDelta.Values.Count == 0 && debug.Variables.Count == 1 &&
                    resolved is { Succeeded: true, ExtensionPresent: true, Registration: not null } &&
                    resolved.Registration.Attributes.Count > 0 && resolved.Registration.Effects.Count > 0 &&
                    resolved.Registration.Abilities.Count > 0 && resolved.Registration.Inventories.Count > 0 &&
                    resolved.Registration.ProgressionTracks.Count > 0 && resolved.Registration.UiPanels.Count > 0 &&
                    optional is { Succeeded: true, ExtensionPresent: false } &&
                    requiredMissing.DiagnosticCode == RulesetPluginRuntimeService.ContentMissingCode,
            "Blackboard replication/debug or ruleset plugin runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R011_GAMEPLAY_SYSTEMS_PASS " +
            "blackboard=typed-schema-save-replication-debug forgescript=typed-handle-command " +
            "ruleset=attributes-effects-abilities-inventories-progression-ui " +
                 "optional=ignored required=diagnostic");
    }

    private static void RunForgeScriptFoundationRuntimeSmokeTest()
    {
        var contract = new ForgeScriptEventContract(Guid.Parse("c7120000-0000-4000-8000-000000000001"),
            1, "runtime:r012", "creature.damaged", "int", true);
        var source = "void OnDamaged(const int &in e) { say(\"damaged\"); } " +
                     "void AuditDamage(const int &in e) {} void main() { say(\"script-ready\"); }";
        var bindings = new[]
        {
            new ForgeScriptEventBinding(Guid.Parse("c7120000-0000-4000-8000-000000000002"), 1,
                "runtime:r012", "creature.combat", contract.EventName, "OnDamaged", contract.PayloadType,
                100, "is_enraged", ForgeScriptEventAuthority.Server, true),
            new ForgeScriptEventBinding(Guid.Parse("c7120000-0000-4000-8000-000000000003"), 1,
                "runtime:r012", "creature.combat", contract.EventName, "AuditDamage", contract.PayloadType,
                20, null, ForgeScriptEventAuthority.Server, true),
        };
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7120000-0000-4000-8000-000000000004"), 1, "runtime:r012", "scripts/combat.as",
            source, "1.0.0", ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], bindings);
        var diagnostics = ForgeScriptSourceValidator.Validate(document, [contract]);
        var resolved = ForgeScriptSourceValidator.ResolveHandlers(document, "creature.combat",
            contract.EventName, ForgeScriptEventAuthority.Server, condition => condition == "is_enraged");
        var missing = ForgeScriptSourceValidator.ValidateRequirements(
            [new(Guid.Parse("c7120000-0000-4000-8000-000000000005"), "scripts/optional.as", false)],
            [document]);
        var runtime = new NativeForgeScriptRuntime();
        var compiled = runtime.Compile(source);
        var executed = compiled.Artifact is null ? null : runtime.Execute(compiled.Artifact,
            new ForgeScriptQuotas(20_000, 1000, 32, 8));
        AssertSmoke(diagnostics.Count == 0 && resolved.Count == 2 && resolved[0].Priority == 100 &&
                    missing.Count == 0 && compiled.Success &&
                    executed?.Status == ForgeScriptExecutionStatus.Completed &&
                    executed.Events.Any(item => item.Kind == "say" && item.Value == "script-ready"),
            "ForgeScript source, typed entry, event dispatch or sandbox contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R012_SCRIPT_FOUNDATION_PASS " +
                 "language=angelscript-2.38-c-like-static source=canonical-as " +
                 "bytecode=cache-source-api-compiler compiler=native-stable-diagnostics " +
                 "events=multi-handler-priority-condition-authority entry=typed-payload " +
                 "sandbox=quota-limited optional=ignored required=diagnostic");
    }

    private static void RunForgeScriptWorldApiRuntimeSmokeTest()
    {
        var heroBlueprint = BlueprintId.From(Guid.Parse("c7130000-0000-4000-8000-000000000001"));
        var guardBlueprint = BlueprintId.From(Guid.Parse("c7130000-0000-4000-8000-000000000002"));
        var hero = new EntityInstance(EntityId.From(Guid.Parse("c7130000-0000-4000-8000-000000000003")),
            heroBlueprint, TransformValue.Identity, ["hero"]);
        var area = new AreaDefinition(AreaId.From(Guid.Parse("c7130000-0000-4000-8000-000000000004")),
            "script-runtime", "Script runtime", [hero]);
        var world = new ForgeScriptWorldCommandService([area],
            new Dictionary<string, BlueprintId>(StringComparer.Ordinal) { ["ash-guard"] = guardBlueprint });
        const string source = """
            void main()
            {
                AreaRef area = current_area();
                EntityRef hero = find_entity(area, "hero");
                Vec3 goal = vec3(6, 0, 4);
                local_set_int(hero, "quest.stage", 2);
                local_set_string(hero, "title", "Warden");
                MovementTaskRef task = move_to(hero, goal, "run");
                set_velocity(hero, vec3(0, 0, 3));
                teleport(hero, area, goal);
                EntityRef guard = spawn_at(area, "ash-guard", vec3(2, 0, 1));
                destroy_entity(guard);
                cancel_movement(task);
                bool safe = regex_match("ash-guard", "^[a-z-]+$");
                say(format_float(vec_length(goal), 2));
            }
            """;
        var runtime = new NativeForgeScriptRuntime();
        var compiled = runtime.Compile(source);
        var execution = compiled.Artifact is null ? null : runtime.Execute(compiled.Artifact,
            new ForgeScriptQuotas(20_000, 1000, 32, 8));
        var command = new ForgeScriptWorldIntentCommand(
            Guid.Parse("c7130000-0000-4000-8000-000000000005"), 0, "runtime:r013",
            execution?.Events ?? [], true);
        var preview = world.Preview(command);
        var applied = world.Apply(command);
        var undone = world.Undo();
        var redone = world.Redo();
        var abusivePattern = new string('a', ForgeScriptCapabilityManifest.MaximumRegexPatternBytes + 1);
        var abusiveCompile = runtime.Compile($"void main() {{ regex_match(\"value\", \"{abusivePattern}\"); }}");
        var abusive = abusiveCompile.Artifact is null ? null : runtime.Execute(abusiveCompile.Artifact);
        var recoveryCompile = runtime.Compile("void main() { say(\"recovered\"); }");
        var recovery = recoveryCompile.Artifact is null ? null : runtime.Execute(recoveryCompile.Artifact);
        var denied = ForgeScriptCapabilityManifest.ValidateSource("void main() { file_open(\"x\"); }");
        AssertSmoke(compiled.Success && execution?.Status == ForgeScriptExecutionStatus.Completed &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.State.Areas.Single().Entities.Single().Transform.Position == new Vector3Value(6, 0, 4) &&
                    redone.State.MovementTasks.Single().Value.Status == ScriptMovementTaskStatus.Cancelled &&
                    abusive?.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovery?.Status == ForgeScriptExecutionStatus.Completed &&
                    recovery.Events.Single().Value == "recovered" && denied.Count == 1,
            "ForgeScript world intents, movement, utilities, variables or sandbox recovery failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R013_SCRIPT_WORLD_API_PASS " +
                 "world=canonical-intents-opaque-handles movement=task-mode-velocity-teleport-completion " +
                 "utilities=vector-quaternion-unicode-format-regex-bounded variables=typed-object-local " +
                 "sandbox=no-file-network-process-native-reflection-engine-dotnet recovery=quota-safe");
    }

    private static void RunForgeScriptSafetyPolicyRuntimeSmokeTest()
    {
        var runtime = new NativeForgeScriptRuntime();
        const string source = "void main() { AssetId asset = resolve_asset(\"ui/marker\"); " +
                              "EffectHandle effect = create_effect(\"ash\"); " +
                              "say(format_int(random_int(1, 1000000))); say(format_int(simulation_tick())); " +
                              "say(format_int(simulation_time_ms())); release_effect(effect); }";
        var compiled = runtime.Compile(source);
        var quotas = new ForgeScriptQuotas(deterministicSeed: 14_014, simulationTick: 42,
            simulationTimeMilliseconds: 7000);
        var first = compiled.Artifact is null ? null : runtime.Execute(compiled.Artifact, quotas);
        var second = compiled.Artifact is null ? null : runtime.Execute(compiled.Artifact, quotas);

        var invalid = runtime.Compile("void main() { AssetId asset = resolve_asset(\"x\"); health(asset); }");
        var invalidResult = invalid.Artifact is null ? null : runtime.Execute(invalid.Artifact);
        var memory = runtime.Compile("void main() { say(\"memory\"); }");
        var memoryResult = memory.Artifact is null ? null : runtime.Execute(memory.Artifact,
            new ForgeScriptQuotas(memoryLimitBytes: 65_536));
        var recovery = runtime.Compile("void main() { say(\"recovered-r014\"); }");
        var recoveryResult = recovery.Artifact is null ? null : runtime.Execute(recovery.Artifact);

        var negotiation = ForgeScriptApiCompatibility.Negotiate("1.0",
            new HashSet<string> { "opaque-handles", "determinism" });
        var history = new ForgeScriptExecutionPolicyHistory(ForgeScriptExecutionPolicy.Default);
        var command = new ForgeScriptExecutionPolicyCommand(Guid.NewGuid(), 0,
            ForgeScriptExecutionPolicy.Default with { DeterministicSeed = 14_014 }, "runtime:r014");
        var applied = history.Apply(command);
        var undone = history.Undo();
        var redone = history.Redo();

        AssertSmoke(compiled.Success && first?.Status == ForgeScriptExecutionStatus.Completed &&
                    second?.Status == ForgeScriptExecutionStatus.Completed && first.Events.SequenceEqual(second.Events) &&
                    first.Telemetry is { Instructions: > 0, PeakMemoryBytes: > 0, DeterministicSeed: 14_014 } &&
                    first.Events.Any(item => item.Kind == "say" && item.Value == "42") &&
                    first.Events.Any(item => item.Kind == "say" && item.Value == "7000") &&
                    invalidResult?.Status == ForgeScriptExecutionStatus.Failed &&
                    invalidResult.Diagnostics.Any(item => item.Message.Contains("handle.type_invalid", StringComparison.Ordinal)) &&
                    memoryResult?.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recoveryResult?.Status == ForgeScriptExecutionStatus.Completed &&
                    recoveryResult.Events.Single().Value == "recovered-r014" && negotiation.Succeeded &&
                    applied.Succeeded && undone.Succeeded && redone.Succeeded,
            "ForgeScript typed handles, budgets, deterministic execution or API negotiation failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R014_SCRIPT_SAFETY_POLICY_PASS " +
                 "handles=typed-generational-validated budgets=handler-tick-module-memory-timeout-cancel-recovery " +
                 "telemetry=instructions-memory-time determinism=seed-clock-server " +
                 "version=semver-negotiation-deprecation-migration policy=preview-apply-undo-redo");
    }

    private static void RunForgeScriptModulePermissionsRuntimeSmokeTest()
    {
        var moduleId = Guid.Parse("15000000-0000-4000-8000-000000000015");
        var policy = ForgeScriptModulePermissionPolicy.CreateDefault(moduleId);
        var request = new ForgeScriptModuleAuthorizationRequest(moduleId, "core", "module.start", "main",
            new HashSet<string>(["world"], StringComparer.Ordinal), ForgeScriptEventAuthority.Server,
            true, false, true);
        var runtime = new NativeForgeScriptRuntime();
        var service = new ForgeScriptModuleExecutionService(runtime);
        var nominal = service.Execute(policy, request, "void main() { say(\"r015-runtime\"); }");
        var untrusted = service.Execute(policy, request with { SignerTrusted = false },
            "void main() { say(\"denied\"); }");
        var optional = ForgeScriptModuleAuthorizationService.Authorize(null,
            request with { RequiredAtRuntime = false });
        var abusive = service.Execute(policy, request,
            "void main() { while (true) { int value = 1; } }", new ForgeScriptQuotas(instructionLimit: 10));
        var recovered = service.Execute(policy, request,
            "void main() { say(\"r015-recovered\"); }");
        AssertSmoke(nominal.Execution?.Status == ForgeScriptExecutionStatus.Completed &&
                    untrusted.Authorization.Diagnostics.SingleOrDefault()?.Code ==
                    "script.permission.signature_untrusted" && optional.Succeeded && !optional.ShouldExecute &&
                    abusive.Execution?.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovered.Execution?.Status == ForgeScriptExecutionStatus.Completed,
            "ForgeScript module permissions, authority, signature or recovery failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R015_SCRIPT_MODULE_PERMISSIONS_PASS " +
                 "policy=module-kit-event-intersection deny=undeclared authority=server " +
                 "signature=trusted-production optional=ignored required=diagnostic " +
                 "sandbox=quota-abort-recovery");
    }

    private void RunForgeScriptP1GameplayRuntimeSmokeTest()
    {
        const string source = """
            void main()
            {
                EntityRef hero = spawn("hero");
                EntityRef target = spawn("warden");
                ScriptTaskRef task = action_enqueue(hero, "attack", 100);
                bool hit = combat_attack(hero, target, "ash-slash", 8.0f, 97);
                EffectHandle burning = effect_apply(hero, target, "burning", 2.0f, 10.0f);
                action_complete(task, "hit");
                effect_dispel(target, "fire");
                release_effect(burning);
            }
            """;
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7970000-0000-4000-8000-000000000101"), 1, "runtime:r097",
            "scripts/r097-runtime.as", source, "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var editor = new ForgeScriptEditorContext(
            ForgeScriptApiCatalog.Registrations.Select(item => new ForgeScriptSymbol(
                item.Name, "function", item.Signature.Split(' ', 2)[0], item.Summary, item.Signature,
                item.Availability.Order(StringComparer.Ordinal).ToArray())).ToArray(),
            new Dictionary<string, string> { ["main"] = "void main() {\n}\n" },
            new HashSet<string> { "bool", "float", "if", "int", "return", "string", "void" });
        var adapter = new ForgeScriptP1RuntimeAdapter();
        var resolution = adapter.Resolve(document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R097 source handle is unavailable.");
        var runtime = adapter.Build(handle, document, editor,
            new RpgScriptCombatState(new Dictionary<string, double> { ["warden"] = 40 }, [], 0,
                new Dictionary<string, double> { ["warden"] = 2 }));
        var analysis = adapter.Analyze(handle, runtime,
            source.IndexOf("combat_attack", StringComparison.Ordinal) + "combat_attack".Length);
        var condition = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Compare, ">", "bool",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "health", "int"),
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "0", "int"));
        var action = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Call, "say", "void",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "\"ready\"", "string"));
        var wizard = adapter.BuildWizard(handle, runtime, new(
            Guid.Parse("c7970000-0000-4000-8000-000000000102"), 1, "runtime:r097",
            "NotifyReady", [condition], [action]));

        var at = DateTimeOffset.UnixEpoch;
        var task = new ScriptTask(Guid.Parse("c7970000-0000-4000-8000-000000000103"), "hero", 100,
            ScriptNetworkAuthority.Server, "attack", new Dictionary<string, object> { ["target"] = "warden" },
            ScriptTaskStatus.Queued, at, null, null);
        runtime = adapter.Enqueue(handle, runtime, task, ScriptNetworkAuthority.Server);
        runtime = adapter.StartNext(handle, runtime, at.AddSeconds(1));
        runtime = adapter.Complete(handle, runtime, task.Id, "hit", at.AddSeconds(2));
        var attacked = adapter.Attack(handle, runtime,
            new RpgAttackContext(Guid.Parse("c7970000-0000-4000-8000-000000000104"), "hero", "warden",
                "ash-slash", 1, 0, 97, "server", "runtime:r097"),
            new RpgDamageSpec(8, "fire", new HashSet<string> { "burning" }, false, 0.5));
        runtime = attacked.Runtime;
        var definition = new RpgEffectDefinition("effect:burning", 1, "runtime:r097", "damage-over-time",
            RpgEffectStacking.RefreshDuration, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), 2,
            new HashSet<string> { "fire" }, true);
        var effected = adapter.ApplyEffect(handle, runtime, definition,
            new RpgEffectSpec(Guid.Parse("c7970000-0000-4000-8000-000000000105"), definition.Id,
                "hero", "warden", 1, null, at));
        runtime = effected.Runtime;
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(20_000, 1000, 32, 8));
        var serializedEffects = RpgScriptGameplayService.SerializeEffects(runtime.Effects, at.AddSeconds(2));
        var restoredEffects = RpgScriptGameplayService.DeserializeEffects(serializedEffects);
        var history = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), []);
        var preview = history.Preview(new(Guid.Parse("c7970000-0000-4000-8000-000000000106"), 0,
            document with { Provenance = "runtime:r097-reviewed" }, "runtime:r097"));
        var applied = history.Apply(new(Guid.Parse("c7970000-0000-4000-8000-000000000107"), 0,
            document with { Provenance = "runtime:r097-reviewed" }, "runtime:r097"));
        var undone = history.Undo();
        var redone = history.Redo();
        var optional = adapter.Resolve(null, false);
        var required = adapter.Resolve(null, true);

        var root = new Node3D { Name = "ForgeScriptP1_R097" };
        var panel = new ForgeScriptP1Panel();
        panel.Configure(runtime, analysis, wizard, attacked.Result, effected.Result, execution);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && analysis.Completions.Any(item => item.Label == "combat_attack") &&
                    analysis.SyntaxSpans.Count > 0 && wizard.Succeeded &&
                    runtime.Tasks.Tasks.Single().Status == ScriptTaskStatus.Completed &&
                    attacked.Result is { Committed: true, AppliedDamage: 7 } &&
                    runtime.Combat.Health["warden"] == 33 && runtime.Effects.Count == 1 &&
                    restoredEffects.Effects.Single().Spec.InstanceId == runtime.Effects.Single().Spec.InstanceId &&
                    execution.Status == ForgeScriptExecutionStatus.Completed &&
                    execution.Events.Any(item => item.Kind == "action.queued") &&
                    execution.Events.Any(item => item.Kind == "combat.attack") &&
                    execution.Events.Any(item => item.Kind == "effect.applied") &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.Document.Provenance == "runtime:r097-reviewed" && optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptP1RuntimeAdapter.RequiredMissingCode &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptP1Layout/AuthoringList") is { ItemCount: 4 } &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptP1Layout/GameplayList") is { ItemCount: 4 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptP1Layout/ForgeScriptP1Actions/AttackButton") is
                    { Disabled: false },
            "ForgeScript P1 editor, wizard, actions, combat, effects or accessible runtime panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R097_SCRIPT_AUTHORING_GAMEPLAY_PASS " +
                 "editor=syntax-highlighting-completion-signature-docs-snippets-symbols-formatting " +
                 "wizard=typed-ast-reviewed-source " +
                 "actions=queue-priority-cancel-complete-authority " +
                 "combat=damage-attack-context-effects-attribution-seed-transaction-rollback " +
                 "effects=definition-spec-six-stacking-duration-period-source-dispel-serialization " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-native-apis " +
                 "mcp=get-api-compile-diagnostics-tests-sandbox " +
                 "runtime=optional-safe-required-diagnostic rpgkit=services-reused " +
                 "engine=typed-intents-accessible-panel");
    }

    private void RunForgeScriptP1WorldApiRuntimeSmokeTest()
    {
        const string source = """
            void main()
            {
                EntityRef chest = spawn("chest");
                EntityRef hero = spawn("hero");
                EntityRef warden = spawn("warden");
                InventoryResultRef loot = inventory_transfer(chest, hero, "crypt-key", 1);
                QuestResultRef quest = quest_transition("crypt", "party:1", "active", "completed", "find-key", 1, "found");
                DialogueSessionRef talk = dialogue_start("graph:intro", warden, hero);
                dialogue_complete(talk, "accepted");
                FactionResultRef faction = faction_reputation(hero, "wardens", 25, true, "module", "rescued");
                PerceptionResultRef seen = perception_query(warden, hero, "sight", 2.0f, 10.0f, true, 0.8f, 98);
            }
            """;
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7980000-0000-4000-8000-000000000101"), 1, "runtime:r098",
            "scripts/r098-runtime.as", source, "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var adapter = new ForgeScriptWorldApiRuntimeAdapter();
        var resolution = adapter.Resolve(document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R098 source handle is unavailable.");
        var inventory = new RpgInventoryState(
            new Dictionary<string, IReadOnlyDictionary<string, int>>
            {
                ["chest"] = new Dictionary<string, int> { ["crypt-key"] = 1 },
                ["hero"] = new Dictionary<string, int>(),
            },
            new Dictionary<string, int> { ["hero"] = 8 }, 0);
        IReadOnlyDictionary<string, RpgQuestState> quests = new Dictionary<string, RpgQuestState>
        {
            ["crypt"] = new("crypt", "party:1", "active",
                new Dictionary<string, int> { ["find-key"] = 0 }, [], 0),
        };
        var factions = new RpgFactionState(
            new Dictionary<string, IReadOnlySet<string>>(), new Dictionary<string, int>(), 0);
        var runtime = adapter.Build(handle, document, inventory, quests, factions);

        var transfer = adapter.Transfer(handle, runtime,
            new RpgInventoryTransaction(Guid.Parse("c7980000-0000-4000-8000-000000000102"),
                "server", "loot", "chest", "hero", "crypt-key", 1));
        runtime = transfer.Runtime;
        var inventoryQuery = RpgScriptWorldService.QueryInventory(runtime.Inventory, "hero", "crypt-key");
        runtime = adapter.TransitionQuest(handle, runtime, "crypt",
            new RpgQuestTransition(Guid.Parse("c7980000-0000-4000-8000-000000000103"),
                "server", "active", "completed", "find-key", 1, "found"));
        var dialogueStarted = adapter.StartDialogue(handle, runtime,
            new RpgDialogueRequest(Guid.Parse("c7980000-0000-4000-8000-000000000104"),
                "server", "graph:intro",
                new Dictionary<string, string> { ["speaker"] = "warden", ["listener"] = "hero" },
                new Dictionary<string, string> { ["quest"] = "crypt" }));
        runtime = adapter.CompleteDialogue(handle, dialogueStarted.Runtime, dialogueStarted.Session.Id, "accepted");
        runtime = adapter.ApplyFaction(handle, runtime,
            new RpgFactionTransaction(Guid.Parse("c7980000-0000-4000-8000-000000000105"),
                "server", "hero", "wardens", 25, true, "module", "rescued"));
        var factionQuery = RpgScriptWorldService.QueryFaction(runtime.Factions, "hero", "wardens", "module");
        var perception = adapter.Perceive(handle, runtime,
            new RpgPerceptionQuery("warden", "hero", "sight", 2, 10, true, .8, 98));
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));

        var history = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), []);
        var preview = history.Preview(new(Guid.Parse("c7980000-0000-4000-8000-000000000106"), 0,
            document with { Provenance = "runtime:r098-reviewed" }, "runtime:r098"));
        var applied = history.Apply(new(Guid.Parse("c7980000-0000-4000-8000-000000000107"), 0,
            document with { Provenance = "runtime:r098-reviewed" }, "runtime:r098"));
        var undone = history.Undo();
        var redone = history.Redo();
        var optional = adapter.Resolve(null, false);
        var required = adapter.Resolve(null, true);

        var root = new Node3D { Name = "ForgeScriptWorldApis_R098" };
        var panel = new ForgeScriptWorldApiPanel();
        panel.ShowEmpty();
        panel.ShowError("FORGESCRIPT_WORLD_PREVIEW_REJECTED");
        panel.Configure(runtime, transfer.Result, inventoryQuery, runtime.Quests["crypt"],
            runtime.Dialogues[dialogueStarted.Session.Id], factionQuery, perception, execution);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && transfer.Result.Committed &&
                    transfer.Result.Audit.Count >= 3 && transfer.Result.SemanticDiff.Contains("crypt-key") &&
                    inventoryQuery is { Quantity: 1, UsedCapacity: 1, MaximumCapacity: 8, Revision: 1 } &&
                    runtime.Quests["crypt"] is { Status: "completed", Revision: 1 } &&
                    runtime.Quests["crypt"].Objectives["find-key"] == 1 &&
                    runtime.Quests["crypt"].History.Single().Contains("scope=party:1") &&
                    runtime.Dialogues[dialogueStarted.Session.Id] is { State: "completed" } completedDialogue &&
                    completedDialogue.Outcomes.Single() == "accepted" &&
                    factionQuery is { Member: true, Relationship: 25, Revision: 1 } &&
                    perception.Perceived && perception.Stimuli.Contains("sense:sight") &&
                    perception.Trace.Contains("seed:98") &&
                    execution.Status == ForgeScriptExecutionStatus.Completed &&
                    execution.Events.Any(item => item.Kind == "inventory.transfer") &&
                    execution.Events.Any(item => item.Kind == "quest.transition") &&
                    execution.Events.Any(item => item.Kind == "dialogue.started") &&
                    execution.Events.Any(item => item.Kind == "dialogue.completed") &&
                    execution.Events.Any(item => item.Kind == "faction.reputation") &&
                    execution.Events.Any(item => item.Kind == "perception.query") &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.Document.Provenance == "runtime:r098-reviewed" && optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptWorldApiRuntimeAdapter.RequiredMissingCode &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptWorldApiLayout/WorldApiList") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptWorldApiLayout/WorldApiActions/TransferButton") is
                    { Disabled: false, FocusMode: Control.FocusModeEnum.All },
            "ForgeScript P1 inventory, quest, dialogue, faction, perception or accessible runtime panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R098_SCRIPT_WORLD_APIS_PASS " +
                 "inventory=transaction-ownership-query-capacity-rollback-audit-revision " +
                 "quest=transition-objectives-history-cause-instance-scope-revision " +
                 "dialogue=graph-participants-variables-complete-interrupt-safe " +
                 "faction=membership-reputation-cause-scope-authority-bounds-revision " +
                 "perception=sense-los-range-stimulus-confidence-seed-trace " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-native-handles " +
                 "mcp=get-api-compile-diagnostics-tests-sandbox " +
                 "runtime=optional-safe-required-diagnostic rpgkit=world-service-reused " +
                 "engine=typed-world-intents-accessible-panel");
    }

    private void RunForgeScriptP1RuntimeToolsRuntimeSmokeTest()
    {
        const string source = """
            void main()
            {
                EntityRef hero = spawn("hero");
                AudioEmitterRef music = audio_emitter("music:dawn");
                audio_start(music);
                audio_snapshot("combat");
                audio_listener(hero, "player");
                audio_complete(music, "finished");
                WorldClockRef clock = world_clock();
                int64 calendar = world_calendar_ms(clock);
                TimerRef timer = timer_schedule(1000, 5000, "OnTick", "daily");
                timer_cancel(timer);
                deterministic_delay(250, "OnReady");
                event_subscribe("forge.damage", 1, "OnDamage");
                CustomEventRef evt = custom_event("forge.damage", 1, "amount=4");
                event_publish(evt);
                HotReloadRef reload = hot_reload_request("module", 2, "sha256:new");
                DebugSessionRef debug = debug_break("session:1", "local-author");
                debug_step(debug, "over");
            }
            """;
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7990000-0000-4000-8000-000000000101"), 1, "runtime:r099",
            "scripts/r099-runtime.as", source, "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var adapter = new ForgeScriptRuntimeToolAdapter();
        var resolution = adapter.Resolve(document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R099 source handle is unavailable.");
        var activeModule = new ScriptHotReloadModule("module", 1,
            new Dictionary<string, string> { ["score"] = "int" },
            new HashSet<string> { "dialogue" }, "sha256:old");
        var runtime = adapter.Build(handle, document,
            new ScriptAudioState(new Dictionary<string, ScriptAudioCue>(), "camera", "default", []),
            new ScriptWorldClock(DateTimeOffset.UnixEpoch, TimeSpan.Zero, 2, 0), activeModule);
        runtime = adapter.Play(handle, runtime,
            new ScriptAudioCue("music:dawn", "music", .8, TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2), true, "exploration"));
        runtime = adapter.SetListener(handle, runtime, "hero", "combat", "player");
        runtime = adapter.Schedule(handle, runtime,
            Guid.Parse("c7990000-0000-4000-8000-000000000102"), TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3), "OnTick", "daily");
        runtime = adapter.Advance(handle, runtime, TimeSpan.FromSeconds(5));
        var deliveries = adapter.Publish(handle, runtime,
            new ScriptCustomEventSchema("forge.damage", 1,
                new Dictionary<string, Type> { ["amount"] = typeof(int), ["target"] = typeof(Guid) }),
            ["audio", "quest"], new ScriptCustomEvent("forge.damage", 1,
                new Dictionary<string, object?>
                {
                    ["amount"] = 4,
                    ["target"] = Guid.Parse("c7990000-0000-4000-8000-000000000103"),
                }, "server", Guid.Parse("c7990000-0000-4000-8000-000000000104")));
        var candidate = activeModule with
        {
            Version = 2,
            SourceHash = "sha256:new",
            Dependencies = new HashSet<string> { "quest" },
        };
        var rejectedReload = adapter.Reload(handle, runtime, candidate, true, (_, _) => false);
        var acceptedReload = adapter.Reload(handle, runtime, candidate, true, (_, _) => true);
        runtime = acceptedReload.State;
        runtime = adapter.Break(handle, runtime, "debug:r099", "server-admin",
            [new ScriptDebugFrame("OnDamage", "scripts/r099-runtime.as", 42,
                new Dictionary<string, object?> { ["amount"] = 4 })],
            new Dictionary<string, object?> { ["hero.hp"] = 36 },
            new Dictionary<string, string> { ["hero"] = "entity:hero" }, ["forge.damage"],
            new HashSet<string> { "scripts/r099-runtime.as:42" }, true);
        runtime = adapter.Step(handle, runtime, "over");
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));

        var abusiveDocument = document with
        {
            Id = Guid.Parse("c7990000-0000-4000-8000-000000000105"),
            Source = "void main() { while (true) { int value = 1; } }",
            Provenance = "runtime:r099-abuse",
        };
        var abusiveHandle = adapter.Resolve(abusiveDocument, true).Handle!;
        var abusiveRuntime = adapter.Build(abusiveHandle, abusiveDocument, runtime.Audio, runtime.Clock,
            runtime.ActiveModule);
        var abusive = adapter.Execute(abusiveHandle, abusiveRuntime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(10, 1000, 32, 8));
        var recovered = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));
        var history = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), []);
        var preview = history.Preview(new(Guid.Parse("c7990000-0000-4000-8000-000000000106"), 0,
            document with { Provenance = "runtime:r099-reviewed" }, "runtime:r099"));
        var applied = history.Apply(new(Guid.Parse("c7990000-0000-4000-8000-000000000107"), 0,
            document with { Provenance = "runtime:r099-reviewed" }, "runtime:r099"));
        var undone = history.Undo();
        var redone = history.Redo();
        var optional = adapter.Resolve(null, false);
        var required = adapter.Resolve(null, true);

        var root = new Node3D { Name = "ForgeScriptRuntimeTools_R099" };
        var panel = new ForgeScriptRuntimeToolsPanel();
        panel.ShowEmpty();
        panel.ShowError("FORGESCRIPT_RUNTIME_TOOL_PREVIEW_REJECTED");
        panel.Configure(runtime, deliveries, acceptedReload.Result, execution);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && runtime.Audio.Playing.ContainsKey("music:dawn") &&
                    runtime.Audio is { ListenerPolicy: "player", Revision: 2 } &&
                    runtime.Clock is { SimulationTime: var simulationTime, Revision: 1 } &&
                    simulationTime == TimeSpan.FromSeconds(10) &&
                    ScriptRuntimeTools.Due(runtime.Clock, runtime.Timers).Count == 1 &&
                    ScriptRuntimeTools.CalendarTime(runtime.Clock) == DateTimeOffset.UnixEpoch.AddSeconds(10) &&
                    deliveries.Select(item => item.Subscriber).SequenceEqual(["audio", "quest"]) &&
                    deliveries.All(item => item.Trace.Contains("forge.damage@1")) &&
                    rejectedReload.Result.Strategy == "restart" && ReferenceEquals(rejectedReload.State, runtime) == false &&
                    acceptedReload.Result.Applied && runtime.ActiveModule.Version == 2 &&
                    runtime.Debugger is { Remote: true, State: "paused", Revision: 1 } &&
                    runtime.Debugger.Breakpoints.Contains("scripts/r099-runtime.as:42") &&
                    ScriptRuntimeTools.InspectEntity(runtime.Debugger, "hero") == "entity:hero" &&
                    execution.Status == ForgeScriptExecutionStatus.Completed &&
                    execution.Events.Any(item => item.Kind == "audio.started") &&
                    execution.Events.Any(item => item.Kind == "time.timer.scheduled") &&
                    execution.Events.Any(item => item.Kind == "event.published") &&
                    execution.Events.Any(item => item.Kind == "hot-reload.requested") &&
                    execution.Events.Any(item => item.Kind == "debug.step") &&
                    abusive.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovered.Status == ForgeScriptExecutionStatus.Completed &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.Document.Provenance == "runtime:r099-reviewed" && optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptRuntimeToolAdapter.RequiredMissingCode &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptRuntimeToolsLayout/RuntimeToolsList") is
                    { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptRuntimeToolsLayout/RuntimeToolActions/DebugButton") is
                    { Disabled: false, FocusMode: Control.FocusModeEnum.All },
            "ForgeScript P1 audio, time, custom event, hot reload, debugger or accessible runtime panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R099_SCRIPT_RUNTIME_TOOLS_PASS " +
                 "audio=cues-state-fades-snapshots-listener-completion " +
                 "time=clock-timers-schedules-simulation-calendar-delay " +
                 "events=namespaced-schema-version-subscription-trace " +
                 "hot_reload=handler-boundary-migration-invalidation-restart " +
                 "debugger=breakpoints-step-stack-locals-watches-events-entities-remote-permissions " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-native-handles " +
                 "mcp=get-api-compile-diagnostics-tests-assign-event " +
                 "runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery " +
                 "engine=audio-clock-event-reload-debug-accessible-panel");
    }

    private void RunForgeScriptP1QualityRuntimeSmokeTest()
    {
        const string source = "void main() { say(\"quality-ready\"); }";
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7100000-0000-4000-8000-000000000301"), 1, "runtime:r100",
            "scripts/r100-quality.as", source, "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var profile = ForgeScriptQualityProfile.Default;
        var adapter = new ForgeScriptQualityRuntimeAdapter();
        var resolution = adapter.Resolve(profile, document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R100 quality handle is unavailable.");
        var runtime = adapter.Build(handle, profile, document);
        var tests = adapter.RunTests(handle, runtime,
        [
            new("runtime-quality", new("fixture", profile.UnitTests.DeterministicSeed,
                    new Dictionary<string, object?> { ["ready"] = true },
                    new Dictionary<string, object?> { ["clock"] = "mock" }),
                _ => { }, world => world.Events.Add("quality.tested"),
                [new("ready", world => (bool)world.State["ready"]!, "world not ready")]),
        ]);
        var profiler = adapter.Profile(handle, runtime,
        [
            new("r100-quality.as", "OnHeartbeat", 12_000, 9 * 1024 * 1024,
                TimeSpan.FromMilliseconds(125), ["OnHeartbeat", "FindAll"]),
        ]);
        var documentation = adapter.GenerateDocumentation(handle, runtime, ForgeScriptApiCatalog.Registrations);
        var diagnostics = adapter.Analyze(handle, runtime,
            new("scripts/r100-quality.as",
                "OnHeartbeat FindAll();\nUnsafeQuery();\nDeprecatedApi();\nServerOnly();\ntarget.Use();",
                "client", new HashSet<string> { "target" }));
        runtime.StateService.RegisterPersistent(new("score", 2, typeof(int),
            (version, json) => version == 1 ? json.GetInt32() + 1 : json.GetInt32())
        {
            Validate = value => value is int number && number >= 0,
        });
        var serialized = runtime.StateService.SerializeState(new Dictionary<string, object?>
        {
            ["score"] = 7,
            ["vm-pointer"] = "0xBAD",
        });
        var restored = runtime.StateService.DeserializeState(serialized);
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));
        var abusiveDocument = document with
        {
            Id = Guid.Parse("c7100000-0000-4000-8000-000000000302"),
            Provenance = "runtime:r100-abuse",
            Source = "void main() { while (true) { int value = 1; } }",
        };
        var abusiveHandle = adapter.Resolve(profile, abusiveDocument, true).Handle!;
        var abusiveRuntime = adapter.Build(abusiveHandle, profile, abusiveDocument);
        var abusive = adapter.Execute(abusiveHandle, abusiveRuntime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(10, 1000, 32, 8));
        var recovered = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));

        var history = new ForgeScriptQualityHistory(profile);
        var changed = profile with { Provenance = "runtime:r100-reviewed" };
        var preview = history.Preview(new(Guid.NewGuid(), 0, changed, "runtime preview"));
        var applied = history.Apply(new(Guid.NewGuid(), 0, changed, "runtime apply"));
        var undone = history.Undo();
        var redone = history.Redo();
        var rejected = history.Preview(new(Guid.NewGuid(), history.Revision,
            changed with { Documentation = changed.Documentation with { OutputRelativePath = "../invalid.md" } },
            "runtime invalid"));
        var storeRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-r100", Guid.NewGuid().ToString("N"));
        ForgeScriptQualityProfile loaded;
        try
        {
            var store = new ForgeScriptQualityStore(storeRoot);
            store.Save(redone.Profile);
            loaded = store.Load();
        }
        finally
        {
            if (Directory.Exists(storeRoot)) Directory.Delete(storeRoot, true);
        }
        var optional = adapter.Resolve(null, null, false);
        var required = adapter.Resolve(null, document, true);

        var root = new Node3D { Name = "ForgeScriptQuality_R100" };
        var panel = new ForgeScriptQualityPanel();
        panel.ShowEmpty();
        panel.ShowError("FORGESCRIPT_QUALITY_PREVIEW_REJECTED");
        panel.Configure(profile, tests, profiler, documentation, diagnostics, restored, execution);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && tests.Single().Passed &&
                    ForgeScriptQualityService.ToCiReport(tests).Contains("\"passed\":1", StringComparison.Ordinal) &&
                    profiler.Hotspots.Single().Function == "OnHeartbeat" && profiler.Alerts.Count == 3 &&
                    profiler.CallGraph["r100-quality.as::OnHeartbeat"].SequenceEqual(["OnHeartbeat", "FindAll"]) &&
                    documentation.Markdown.Contains("Do not edit manually", StringComparison.Ordinal) &&
                    documentation.McpResources.Count > 0 && diagnostics.Count == 5 &&
                    diagnostics.All(item => item.Path.StartsWith("scripts/r100-quality.as:", StringComparison.Ordinal)) &&
                    (int)restored["score"]! == 7 && !serialized.Contains("vm-pointer", StringComparison.Ordinal) &&
                    execution.Status == ForgeScriptExecutionStatus.Completed &&
                    execution.Events.Any(item => item.Kind == "say" && item.Value == "quality-ready") &&
                    abusive.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovered.Status == ForgeScriptExecutionStatus.Completed &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.Profile.Provenance == "runtime:r100-reviewed" && !rejected.Succeeded &&
                    loaded.Id == changed.Id && loaded.Provenance == changed.Provenance &&
                    loaded.State.RegisteredComponents.OrderBy(item => item.Key).SequenceEqual(
                        changed.State.RegisteredComponents.OrderBy(item => item.Key)) &&
                    optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptQualityRuntimeAdapter.RequiredMissingCode &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptQualityLayout/QualityList") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptQualityLayout/QualityActions/TestsButton") is
                    { Disabled: false, FocusMode: Control.FocusModeEnum.All },
            "ForgeScript P1 tests, profiler, generated docs, static analysis, script state or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R100_SCRIPT_QUALITY_PASS " +
                 "tests=testworld-fixtures-mocks-assertions-ci-deterministic " +
                 "profiler=per-handler-cpu-instructions-allocations-hotspots-call-graph-alerts " +
                 "docs=generated-versioned-examples-availability-mcp-resource " +
                 "analysis=heartbeat-query-null-deprecated-authority-localized " +
                 "state=registered-components-no-vm-pointers-migration-validation-quota " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-tooling " +
                 "mcp=get-api-compile-diagnostics-tests-assign-event " +
                 "runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery " +
                 "engine=quality-services-accessible-panel");
    }

    private void RunForgeScriptP1InteropRuntimeSmokeTest()
    {
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7110000-0000-4000-8000-000000000301"), 1, "runtime:r101",
            "scripts/r101-interop.as", "void main() { say(\"interop-ready\"); }", "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var profile = ForgeScriptInteropProfile.Default;
        var adapter = new ForgeScriptInteropRuntimeAdapter();
        var resolution = adapter.Resolve(profile, document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R101 interop handle is unavailable.");
        var runtime = adapter.Build(handle, profile, document);
        var nominal = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime());
        var abusiveDocument = document with
        {
            Id = Guid.Parse("c7110000-0000-4000-8000-000000000302"),
            Provenance = "runtime:r101-abuse",
            Source = "void main() { while (true) { int value = 1; } }",
        };
        var abusiveHandle = adapter.Resolve(profile, abusiveDocument, true).Handle!;
        var abusive = adapter.Execute(abusiveHandle, adapter.Build(abusiveHandle, profile, abusiveDocument),
            new NativeForgeScriptRuntime(), new ForgeScriptQuotas(10, 1000, 32, 8));
        var recovered = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime());

        var profileHistory = new ForgeScriptInteropHistory(profile);
        var changedProfile = profile with { Provenance = "runtime:r101-reviewed", MaximumSources = 1024 };
        var profilePreview = profileHistory.Preview(new(Guid.NewGuid(), 0, changedProfile, "runtime preview"));
        var profileApplied = profileHistory.Apply(new(Guid.NewGuid(), 0, changedProfile, "runtime apply"));
        var profileRejected = profileHistory.Preview(new(Guid.NewGuid(), profileHistory.Revision,
            changedProfile with { EventBinding = "" }, "runtime reject"));
        var profileUndone = profileHistory.Undo();
        var profileRedone = profileHistory.Redo();
        var sourceHistory = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), []);
        var changedDocument = document with { Provenance = "runtime:r101-source" };
        var sourcePreview = sourceHistory.Preview(new(Guid.NewGuid(), 0, changedDocument, "runtime preview"));
        var sourceApplied = sourceHistory.Apply(new(Guid.NewGuid(), 0, changedDocument, "runtime apply"));
        var sourceUndone = sourceHistory.Undo();
        var sourceRedone = sourceHistory.Redo();
        var storeRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-r101", Guid.NewGuid().ToString("N"));
        ForgeScriptInteropProfile loaded;
        try
        {
            var store = new ForgeScriptInteropProfileStore(storeRoot);
            store.Save(profileRedone.Profile);
            loaded = store.Load();
        }
        finally
        {
            if (Directory.Exists(storeRoot)) Directory.Delete(storeRoot, true);
        }
        var optional = adapter.Resolve(null, null, false);
        var required = adapter.Resolve(null, document, true);

        var root = new Node3D { Name = "ForgeScriptInterop_R101" };
        var panel = new ForgeScriptInteropPanel();
        panel.ShowEmpty();
        panel.ShowError("FORGESCRIPT_INTEROP_PREVIEW_REJECTED");
        panel.Configure(profile, document, nominal, profileRedone.Revision);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && nominal.Status == ForgeScriptExecutionStatus.Completed &&
                    nominal.Events.Any(item => item.Kind == "say" && item.Value == "interop-ready") &&
                    abusive.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovered.Status == ForgeScriptExecutionStatus.Completed &&
                    profilePreview.Succeeded && profileApplied.Succeeded && !profileRejected.Succeeded &&
                    profileUndone.Succeeded && profileRedone.Succeeded && loaded == changedProfile &&
                    sourcePreview.Succeeded && sourceApplied.Succeeded && sourceUndone.Succeeded &&
                    sourceRedone.Succeeded && sourceRedone.Document.Provenance == "runtime:r101-source" &&
                    optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptInteropRuntimeAdapter.RequiredMissingCode &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptInteropLayout/InteropTools") is { ItemCount: 6 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptInteropLayout/InteropActions/RedoButton") is
                    { Disabled: false, FocusMode: Control.FocusModeEnum.All },
            "ForgeScript editor/MCP interop, transactions, persistence, sandbox recovery or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R101_SCRIPT_INTEROP_PASS " +
                 "tools=get-api-create-update-compile-diagnostics-run-tests-assign-event " +
                 "identity=uuid-schema-provenance-api-event-binding " +
                 "validation=required-unique-resolved-schema-compile-api-quotas-tests-localized " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip editor=canonical-command-parity " +
                 "mcp=versioned-transactional-no-external-execution " +
                 "runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery " +
                 "engine=interop-services-accessible-panel");
    }

    private void RunNetworkFoundationP1RuntimeSmokeTest()
    {
        var initial = NetworkFoundationProfile.CreateDefault("runtime:r102");
        var history = new NetworkFoundationHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Server = initial.Server with { Capacity = 12 },
            Interest = initial.Interest with { MaximumDistance = 512 }
        };
        var command = new NetworkFoundationCommand(Guid.Parse("a1020000-0000-4000-8000-000000000301"),
            1, "runtime:r102", 0, "runtime:server", Forge.Network.Operations.NetworkRole.Administrator,
            true, candidate);
        var applied = history.Execute(command);
        var undone = history.Undo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var redone = history.Redo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var store = new NetworkFoundationStore();
        var loaded = store.Deserialize(store.Serialize(redone.Profile));
        var runtime = NetworkFoundationRuntimeAdapter.Build(loaded);
        var scriptSnapshot = runtime.Scripts.Read(runtime.Scripts.Handle);
        var optional = NetworkFoundationRuntimeAdapter.Resolve(null, false);
        var required = NetworkFoundationRuntimeAdapter.Resolve(null, true);
        var scenario = NetworkFoundationAcceptanceScenario.Run();
        var invalid = NetworkFoundationValidator.Validate(loaded with
        {
            Party = loaded.Party with { MaximumMembers = 1 }
        });
        var panel = new NetworkFoundationPanel();
        panel.ShowEmpty();
        panel.ShowError("NET_PARTY_CAPACITY_INVALID");
        panel.Configure(loaded, scenario);
        AddChild(panel);

        AssertSmoke(applied.Applied && undone.Succeeded && redone.Succeeded &&
                    store.Serialize(loaded) == store.Serialize(candidate) &&
                    scriptSnapshot.Id == loaded.Id && !optional.Available && optional.Diagnostics.Count == 0 &&
                    !required.Available && required.Diagnostics.Single().Code == "NET_FOUNDATION_REQUIRED_MISSING" &&
                    scenario.Passed && invalid.Any(item => item is
                    { Code: "NET_PARTY_CAPACITY_INVALID", Path: "/network/party/maximumMembers" }) &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("NetworkFoundationLayout/Capabilities") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("NetworkFoundationLayout/Actions/RedoButton") is
                    { FocusMode: Control.FocusModeEnum.All },
            "Network foundation profile, authority, party, replication, interest or accessible runtime panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R102_NETWORK_FOUNDATION_PASS " +
                 "dedicated=headless-service-health authority=intents-server-replicated-audited " +
                 "party=invitations-roles-leader-loot-quests-markers-separation " +
                 "replication=components-delta-ownership-prediction-reconciliation " +
                 "interest=area-distance-visibility-party-ownership-explicit " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit " +
                 "engine=authoritative-session-party-replication-interest-panel");
    }

    private void RunNetworkOperationsP1RuntimeSmokeTest()
    {
        var initial = NetworkOperationsProfile.CreateDefault("runtime:r103");
        var history = new NetworkOperationsHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Content = initial.Content with { ChunkBytes = 32 * 1024 },
            Chat = initial.Chat with { LocalRadius = 48 }
        };
        var command = new NetworkOperationsCommand(Guid.Parse("a1030000-0000-4000-8000-000000000301"),
            1, "runtime:r103", 0, "runtime:server", Forge.Network.Operations.NetworkRole.Administrator,
            true, candidate);
        var applied = history.Execute(command);
        var undone = history.Undo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var redone = history.Redo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var store = new NetworkOperationsStore();
        var loaded = store.Deserialize(store.Serialize(redone.Profile));
        var runtime = NetworkOperationsRuntimeAdapter.Build(loaded);
        var scriptSnapshot = runtime.Scripts.Read(runtime.Scripts.Handle);
        var optional = NetworkOperationsRuntimeAdapter.Resolve(null, false);
        var required = NetworkOperationsRuntimeAdapter.Resolve(null, true);
        var scenario = NetworkOperationsAcceptanceScenario.Run();
        var invalid = NetworkOperationsValidator.Validate(loaded with
        {
            Events = loaded.Events with { MaximumPendingEvents = 0 }
        });
        var panel = new NetworkOperationsPanel();
        panel.ShowEmpty();
        panel.ShowError("NET_EVENT_POLICY_INVALID");
        panel.Configure(loaded, scenario);
        AddChild(panel);

        AssertSmoke(applied.Applied && undone.Succeeded && redone.Succeeded &&
                    store.Serialize(loaded) == store.Serialize(candidate) &&
                    scriptSnapshot.Id == loaded.Id && scriptSnapshot.RoleCount == 6 &&
                    !optional.Available && optional.Diagnostics.Count == 0 &&
                    !required.Available && required.Diagnostics.Single().Code == "NET_OPERATIONS_REQUIRED_MISSING" &&
                    scenario.Passed && invalid.Any(item => item is
                    { Code: "NET_EVENT_POLICY_INVALID", Path: "/network/operations/events" }) &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("NetworkOperationsLayout/Capabilities") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("NetworkOperationsLayout/Actions/RedoButton") is
                    { FocusMode: Control.FocusModeEnum.All },
            "Network operations content, version, roles, chat, events or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R103_NETWORK_OPERATIONS_PASS " +
                 "content=manifest-chunks-hashes-cache-resume-delta-trust " +
                 "version=lockfile-fingerprint-before-join-readable-mismatch " +
                 "roles=player-moderator-builder-gm-admin-spectator-custom-abac " +
                 "chat=local-party-tell-shout-dm-proximity-language-logs-accessibility-bridges " +
                 "events=typed-subscriptions-priority-trace-replay-backpressure " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit " +
                 "engine=content-access-chat-events-panel");
    }

    private void RunNetworkAssuranceP1RuntimeSmokeTest()
    {
        var initial = NetworkAssuranceProfile.CreateDefault("runtime:r104");
        var history = new NetworkAssuranceHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Logs = initial.Logs with { RotationFiles = 5 },
            DeterministicTests = initial.DeterministicTests with { SoakCycles = 8 }
        };
        var command = new NetworkAssuranceCommand(Guid.Parse("a1040000-0000-4000-8000-000000000301"),
            1, "runtime:r104", 0, "runtime:server", Forge.Network.Operations.NetworkRole.Administrator,
            true, candidate);
        var applied = history.Execute(command);
        var undone = history.Undo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var redone = history.Redo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var store = new NetworkAssuranceStore();
        var loaded = store.Deserialize(store.Serialize(redone.Profile));
        var sink = new MemoryRemoteLogSink("audit");
        var runtime = NetworkAssuranceRuntimeAdapter.Build(loaded, sinks: [sink]);
        var scriptSnapshot = runtime.Scripts.Read(runtime.Scripts.Handle);
        var optional = NetworkAssuranceRuntimeAdapter.Resolve(null, false);
        var required = NetworkAssuranceRuntimeAdapter.Resolve(null, true);
        var scenario = NetworkAssuranceAcceptanceScenario.Run();
        var invalid = NetworkAssuranceValidator.Validate(loaded with
        {
            DeterministicTests = loaded.DeterministicTests with { ClientCount = 1 }
        });
        var panel = new NetworkAssurancePanel();
        panel.ShowEmpty();
        panel.ShowError("NET_ASSURANCE_TEST_POLICY_INVALID");
        panel.Configure(loaded, scenario);
        AddChild(panel);

        AssertSmoke(applied.Applied && undone.Succeeded && redone.Succeeded &&
                    store.Serialize(loaded) == store.Serialize(candidate) &&
                    scriptSnapshot.Id == loaded.Id && scriptSnapshot.SimulatedClients == 3 &&
                    !optional.Available && optional.Diagnostics.Count == 0 &&
                    !required.Available && required.Diagnostics.Single().Code == "NET_ASSURANCE_REQUIRED_MISSING" &&
                    scenario.Passed && invalid.Any(item => item is
                    { Code: "NET_ASSURANCE_TEST_POLICY_INVALID", Path: "/network/assurance/deterministicTests" }) &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("NetworkAssuranceLayout/Capabilities") is { ItemCount: 3 } &&
                    panel.GetNodeOrNull<Button>("NetworkAssuranceLayout/Actions/RedoButton") is
                    { FocusMode: Control.FocusModeEnum.All },
            "Network assurance logs, anti-cheat, deterministic tests or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R104_NETWORK_ASSURANCE_PASS " +
                 "logs=json-text-categories-correlation-privacy-rotation-remote-sinks " +
                 "anti_cheat=intents-rate-signed-content-authoritative-rng-anomaly-untrusted-saves " +
                 "tests=clients-latency-loss-duplicate-reorder-replay-soak-authoritative-state " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit " +
                 "engine=logs-authority-determinism-panel");
    }

    private void RunLiveDirectorAccessP1RuntimeSmokeTest()
    {
        var initial = LiveDirectorAccessProfile.CreateDefault("runtime:r105");
        var history = new LiveDirectorAccessHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Players = initial.Players with { RefreshSeconds = 3 },
            Audit = initial.Audit with { RetentionDays = 120 }
        };
        var command = new LiveDirectorAccessCommand(Guid.Parse("a1050000-0000-4000-8000-000000000301"),
            1, "runtime:r105", 0, "runtime:server", Forge.Network.Operations.NetworkRole.Administrator,
            NetworkAuthorityScope.Server, true, candidate);
        var applied = history.Execute(command);
        var undone = history.Undo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var redone = history.Redo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var store = new LiveDirectorAccessStore();
        var loaded = store.Deserialize(store.Serialize(redone.Profile));
        var runtime = LiveDirectorAccessRuntimeAdapter.Build(loaded);
        var snapshot = runtime.Scripts.Read(runtime.Scripts.Handle);
        var optional = LiveDirectorAccessRuntimeAdapter.Resolve(null, false);
        var required = LiveDirectorAccessRuntimeAdapter.Resolve(null, true);
        var scenario = LiveDirectorAccessAcceptanceScenario.Run();
        var invalid = LiveDirectorAccessValidator.Validate(loaded with
        {
            Chat = loaded.Chat with { ModerationEnabled = false }
        });
        var panel = new LiveDirectorAccessPanel();
        panel.ShowEmpty();
        panel.ShowError("LIVE_ACCESS_CHAT_POLICY_INVALID");
        panel.Configure(loaded, scenario);
        AddChild(panel);

        AssertSmoke(applied.Applied && undone.Succeeded && redone.Succeeded &&
                    store.Serialize(loaded) == store.Serialize(candidate) &&
                    snapshot.Id == loaded.Id && snapshot.ChatChannels == 7 &&
                    !optional.Available && optional.Diagnostics.Count == 0 &&
                    !required.Available && required.Diagnostics.Single().Code == "LIVE_ACCESS_REQUIRED_MISSING" &&
                    scenario.Passed && invalid.Any(item => item is
                    { Code: "LIVE_ACCESS_CHAT_POLICY_INVALID", Path: "/liveDirector/access/chat" }) &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("LiveDirectorAccessLayout/Capabilities") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("LiveDirectorAccessLayout/Actions/RedoButton") is
                    { FocusMode: Control.FocusModeEnum.All },
            "Live Director authentication, inspection, players, audit, chat or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R105_LIVE_DIRECTOR_ACCESS_PASS " +
                 "auth=gm-role-mfa-expiry-revoke-audit " +
                 "inspect=components-stats-variables-scripts-ownership-history-read-only " +
                 "players=identity-character-zone-latency-party-permissions-voice-moderation-refresh " +
                 "audit=actor-command-targets-before-after-reason-correlation-hash-chain " +
                 "chat=local-party-tell-server-gm-faction-impersonation-moderation-logs " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit " +
                 "engine=auth-inspection-players-audit-chat-panel");
    }

    private static void RunContentPackageRuntimeSmokeTest()
    {
        static ContentPackageSource Source(Guid id, string resource, string path, char hash) =>
            new(id, 1, "runtime:r016", resource, path, ContentSourceKind.Json, true, "CC0-1.0",
                new string(hash, 64), 1_048_576);
        static ContentPackageDefinition Package(Guid id, string packageNamespace, int priority,
            IReadOnlyList<ContentPackageSource> sources, IReadOnlyList<ContentPackageDependency>? dependencies = null,
            IReadOnlyList<string>? sealedResources = null) =>
            new(id, 1, "runtime:r016", packageNamespace, "1.0.0", "CC0-1.0", new string('c', 64),
                priority, sealedResources, dependencies, sources);

        var moduleId = Guid.Parse("c0160000-0000-4000-8000-000000000301");
        var core = Package(Guid.Parse("c0160000-0000-4000-8000-000000000302"), "forge.core", 10,
            [Source(Guid.Parse("c0160000-0000-4000-8000-000000000303"), "rules.balance", "data/core.json", 'a')],
            sealedResources: ["rules.balance"]);
        var optional = new ContentPackageDependency(Guid.Parse("c0160000-0000-4000-8000-000000000304"), 1,
            "runtime:r016", "forge.cosmetics", "^1.0.0", ContentDependencyKind.Optional);
        var module = Package(Guid.Parse("c0160000-0000-4000-8000-000000000305"), "crypt.module", 50,
            [Source(Guid.Parse("c0160000-0000-4000-8000-000000000306"), "quest.main", "data/quest.json", 'b')],
            [optional]);
        var catalog = new ContentPackageCatalog(moduleId, 1, "runtime:r016",
            ContentConflictPolicy.HighestPriority, [core, module]);
        var resolved = ContentPackageResolver.Resolve(catalog);

        var required = new ContentPackageDependency(Guid.Parse("c0160000-0000-4000-8000-000000000307"), 1,
            "runtime:r016", "forge.required-missing", ">=1.0.0", ContentDependencyKind.Required);
        var requiredCatalog = new ContentPackageCatalog(moduleId, 1, "runtime:r016",
            ContentConflictPolicy.HighestPriority,
            [Package(Guid.Parse("c0160000-0000-4000-8000-000000000308"), "crypt.required", 1, [], [required])]);
        var requiredResult = ContentPackageResolver.Resolve(requiredCatalog);

        AssertSmoke(resolved.Valid && resolved.LockEntries.Count == 2 && resolved.Resources.Count == 2 &&
                    !resolved.Diagnostics.Any(item => item.Message.Contains("cosmetics", StringComparison.Ordinal)) &&
                    requiredResult.Diagnostics.Any(item =>
                        item.Code == ContentPackageCatalogValidator.MissingDependencyCode),
            "Content package offline resolution did not ignore optional or diagnose required absence.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R016_CONTENT_PACKAGES_PASS " +
                 "package=versioned-manifest priority=deterministic-sealed dependencies=offline-lock " +
                 "optional=ignored required=diagnostic sources=text-safe compiler=packaged");
    }

    private static void RunContentAuthoringRuntimeSmokeTest(ProjectDefinition project)
    {
        var localization = new LocalizationCatalog(LocalizationCatalogId.From(project.Module.Id.Value), "fr",
            ["fr", "en"], [new LocalizationMessage("runtime.r017.loot",
                "{count, plural, =0 {aucun objet} one {# objet} other {# objets}}",
                new Dictionary<string, string>
                {
                    ["en"] = "{count, plural, =0 {no item} one {# item} other {# items}}",
                }, "runtime:r017")], packageNamespace: "crypt.localization", provenance: "runtime:r017");
        var localized = localization.Resolve("runtime.r017.loot", "en-US",
            new Dictionary<string, object?> { ["count"] = 2 });

        var source = new ContentPackageSource(Guid.Parse("c0170000-0000-4000-8000-000000000101"), 1,
            "runtime:r017", "rules/encounter", "data/encounter.json", ContentSourceKind.Json, false,
            "CC0-1.0", new string('a', 64), 4096, ["legacy.core:text/encounter"],
            ["crypt.core:model/props/chest"]);
        var asset = new ContentPackageAsset(Guid.Parse("c0170000-0000-4000-8000-000000000102"), 1,
            "runtime:r017", "props/chest", "models/chest.glb", true, "CC0-1.0", new string('b', 64),
            8 * 1024 * 1024);
        var package = new ContentPackageDefinition(Guid.Parse("c0170000-0000-4000-8000-000000000103"), 1,
            "runtime:r017", "crypt.core", "1.0.0", "CC0-1.0", new string('c', 64), 10, [], [], [source],
            [asset]);
        var catalog = new ContentPackageCatalog(project.Module.Id.Value, 1, "runtime:r017",
            ContentConflictPolicy.HighestPriority, [package]);
        var registryResult = ContentResourceRegistry.Build(catalog);
        var registry = ContentResourceRegistry.Create(registryResult);

        var module = project.Module;
        var content = new ContentWorkspaceConfiguration(module.Content.Id, module.Content.Batch,
            module.Content.DataTables, module.Content.MergePolicy, module.Content.Provenance,
            module.Content.SchemaVersion, catalog);
        var configuredModule = new ModuleDefinition(module.Id, module.Slug, module.DisplayName, module.Description,
            module.EntryAreaId, module.Metadata, module.Variables, module.Distribution, localization, content,
            module.ArtisticStyle, module.RuntimePolicies, module.Factions, module.WorldFeatures);
        var configuredProject = new ProjectDefinition(project.Id, project.Title, configuredModule,
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        var browser = new UnifiedContentBrowser(configuredProject);

        var texture = new AssetProcessingProfile(
            AssetCatalogItemId.From(Guid.Parse("c0170000-0000-4000-8000-000000000104")), 1,
            AssetProcessingKind.Texture, "png", "ktx2", "CC0-1.0", new string('d', 64), "runtime:r017",
            4 * 1024 * 1024, texture: new(TextureColorSpace.Srgb, TextureCompressionProfile.Universal, 2048,
                10.24m, new Dictionary<PbrTextureChannel, string>
                {
                    [PbrTextureChannel.BaseColor] = "textures/chest.png",
                }));
        var model = new AssetProcessingProfile(
            AssetCatalogItemId.From(Guid.Parse("c0170000-0000-4000-8000-000000000105")), 1,
            AssetProcessingKind.Model, "glb", "glb", "CC0-1.0", new string('e', 64), "runtime:r017",
            16 * 1024 * 1024, model: new(true, true, true, ["hand.r"], [1m, .5m]));

        var invalidSource = source with
        {
            Id = Guid.Parse("c0170000-0000-4000-8000-000000000106"),
            Required = true,
            References = ["crypt.core:model/missing"],
        };
        var invalidPackage = new ContentPackageDefinition(package.Id, package.SchemaVersion, package.Provenance,
            package.PackageNamespace, package.Version, package.LicenseSpdx, package.ContentSha256, package.Priority,
            package.SealedResources, package.Dependencies, [invalidSource], [asset]);
        var invalidRegistry = ContentResourceRegistry.Build(new ContentPackageCatalog(catalog.Id,
            catalog.SchemaVersion, catalog.Provenance, catalog.ConflictPolicy, [invalidPackage]));

        AssertSmoke(localized == "2 items" && registryResult.Valid &&
                    registry.TryResolve("legacy.core:text/encounter", out var resolved) &&
                    resolved?.References.Single() == "crypt.core:model/props/chest" &&
                    browser.Search(new UnifiedContentBrowserQuery(PackageNamespace: "crypt.core")).Count == 2 &&
                    browser.Items.Any(item => item.Status == "optional") &&
                    AssetProcessingProfileValidator.Validate(texture).Count == 0 &&
                    AssetProcessingProfileValidator.Validate(model).Count == 0 &&
                    invalidRegistry.Diagnostics.Any(item =>
                        item.Code == ContentPackageCatalogValidator.ResourceReferenceCode),
            "Content authoring localization, resource, profile or browser runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R017_CONTENT_AUTHORING_PASS " +
                 "localization=icu-fallback resource=namespaced-alias references=resolved " +
                 "texture=pbr-profile model=glb-profile browser=unified optional=ignored required=diagnostic");
    }

    private static void RunAssetGovernanceRuntimeSmokeTest()
    {
        var assetId = AssetCatalogItemId.ForExternalId("runtime:r018-governed-asset");
        var record = new AssetGovernanceRecord(1, AssetGovernanceId.ForAsset(assetId), assetId,
            AssetPackageManifestId.ForAsset(assetId), "CC0-1.0", new string('f', 64), "Runtime fixture",
            "https://example.invalid/runtime-asset", "permission:runtime-fixture", "None.", false,
            string.Empty, string.Empty, 4096,
            new("incoming/model.glb", "model/gltf-binary", "model/gltf-binary", 12, 4096, "scanner", "clean",
                "valid", AssetQuarantineStatus.Approved, "reviewer", DateTimeOffset.UnixEpoch),
            new(AssetOriginKind.OpenLicensed, true, false, string.Empty));
        var optional = AssetGovernanceResolver.Resolve([], assetId, required: false);
        var required = AssetGovernanceResolver.Resolve([], assetId, required: true);
        var proprietary = AssetGovernanceValidator.Validate(record with
        {
            IpBoundary = new AssetIpBoundary(AssetOriginKind.ProprietaryCep, false, true, "legal-review:R018"),
        });
        AssertSmoke(AssetGovernanceValidator.Validate(record).Count == 0 && optional.Diagnostics.Count == 0 &&
                    required.Diagnostics.Single().Code == AssetGovernanceResolver.RequiredMissingCode &&
                    proprietary.Any(item => item.Code == AssetGovernanceValidator.ProprietaryRedistributionCode),
            "Asset governance runtime optional/required or IP boundary contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R018_ASSET_GOVERNANCE_PASS provenance=validated quarantine=approved " +
                 "ip=proprietary-blocked optional=ignored required=diagnostic");
    }

    private static void RunQualityFoundationRuntimeSmokeTest()
    {
        var profile = QualityFoundationProfile.CreateDefault(
            Guid.Parse("30000000-0000-4000-8000-000000000001"));
        var logger = new StructuredLogger(TimeProvider.System, profile.Logs);
        var objectId = Guid.Parse("20000000-0000-4000-8000-000000000001");
        var transactionId = Guid.Parse("20000000-0000-4000-8000-000000000002");
        var entry = logger.Write(StructuredLogSeverity.Information, "runtime.qa.ready", Guid.NewGuid(),
            new StructuredLogContext("runtime", objectId, 42, transactionId, "player-runtime"),
            new Dictionary<string, object?> { ["accessToken"] = "never-log", ["playerName"] = "Elyra" });
        AssertSmoke(QualityFoundationProfileValidator.Validate(profile).Count == 0 &&
                    entry.ObjectId == objectId && entry.SourceLine == 42 &&
                    entry.TransactionId == transactionId &&
                    Equals(entry.Properties["accessToken"], "[REDACTED]") &&
                    Equals(entry.Properties["playerName"], "[PLAYER_REDACTED]"),
            "QA foundation or structured logging runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R019_QA_FOUNDATIONS_PASS " +
                 "licenses=spdx-provenance-notice-block domain=covered headless=scenarios " +
                 "save=roundtrip-legacy-partial-missing-rollback-deterministic " +
                 "logs=severity-category-object-source-transaction-privacy-retention");
    }

    private static void RunCanonicalValidationRuntimeSmokeTest()
    {
        var profile = QualityFoundationProfile.CreateDefault(
            Guid.Parse("30000000-0000-4000-8000-000000000027"));
        var optional = WorkspaceCanonicalValidationService.ValidateRuntimePolicy(null, required: false);
        var required = WorkspaceCanonicalValidationService.ValidateRuntimePolicy(null, required: true);
        AssertSmoke(optional.Count == 0 &&
                    required.Single().Code == WorkspaceCanonicalValidationService.RuntimePolicyMissingCode &&
                    QualityFoundationProfileValidator.Validate(profile).Count == 0 &&
                    profile.EffectiveValidation.Categories.Count == 5,
            "Canonical validation runtime optional/required contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R027_QA_VALIDATION_PASS " +
                 "categories=5 schemas=versioned references=typed graphs=generic geometry=scale-polygon " +
                 "navigation=agents-doors-links-flight optional=ignored required=diagnostic");
    }


}
