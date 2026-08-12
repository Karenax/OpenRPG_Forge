#if TOOLS
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeScriptEditorWindow : Window
{
    private static readonly ForgeScriptEventContract DamageEvent = new(
        Guid.Parse("12000000-0000-4000-8000-000000000010"), 1, "forge:events",
        "creature.damaged", "int", true);
    private Func<ProjectSession?>? _getSession;
    private Action<Window>? _brandWindow;
    private ItemList? _sources;
    private CodeEdit? _source;
    private ItemList? _diagnostics;
    private ItemList? _bindings;
    private ItemList? _capabilities;
    private Label? _identity;
    private Label? _status;
    private Label? _console;
    private ItemList? _authoringTools;
    private ItemList? _gameplayApis;
    private Label? _gameplayHeading;
    private Button? _apply;
    private Button? _undo;
    private Button? _redo;
    private OptionButton? _policyPreset;
    private Label? _policySummary;
    private Button? _policyApply;
    private Button? _policyUndo;
    private Button? _policyRedo;
    private ForgeScriptSourceHistory? _history;
    private ForgeScriptExecutionPolicyHistory _policyHistory = new(ForgeScriptExecutionPolicy.Default);
    private ForgeScriptPermissionsWindow? _permissionsWindow;
    private ForgeScriptDiagnostic[] _compileDiagnostics = [];
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
    private string _captureMarker = "OPENRPG_FORGE_SCRIPT_EDITOR_UI_CAPTURE";

    internal int DiagnosticCount => _diagnostics?.ItemCount ?? 0;
    internal int BindingCount => _history?.Document.EventBindings.Count ?? 0;
    internal int CapabilityCount => _capabilities?.ItemCount ?? 0;
    internal bool LastCompileSucceeded { get; private set; }
    internal bool LastSandboxSucceeded { get; private set; }
    internal bool ModulePermissionsSmokeSucceeded { get; private set; }
    internal bool P1AuthoringSmokeSucceeded { get; private set; }
    internal bool P1WorldApiSmokeSucceeded { get; private set; }
    internal bool P1RuntimeToolsSmokeSucceeded { get; private set; }
    internal bool P1QualitySmokeSucceeded { get; private set; }
    internal bool P1InteropSmokeSucceeded { get; private set; }

    public void Initialize(Func<ProjectSession?> getSession, Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "ForgeScript · OpenRPG Forge";
        MinSize = new Vector2I(1680, 1000);
        Size = new Vector2I(1680, 1000);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        CloseRequested += Hide;
        BuildUi();
        _permissionsWindow = new ForgeScriptPermissionsWindow();
        _permissionsWindow.Initialize(getSession, brandWindow);
        AddChild(_permissionsWindow);
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r101-script-interop-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r100-script-quality-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r099-script-runtime-tools-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r098-script-world-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r097-script-p1-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--script-editor-ui-capture=", StringComparison.Ordinal));
        var capturePrefix = captureArgument?.StartsWith("--r101-script-interop-ui-capture=", StringComparison.Ordinal) == true
            ? "--r101-script-interop-ui-capture="
            : captureArgument?.StartsWith("--r100-script-quality-ui-capture=", StringComparison.Ordinal) == true
                ? "--r100-script-quality-ui-capture="
            : captureArgument?.StartsWith("--r099-script-runtime-tools-ui-capture=", StringComparison.Ordinal) == true
                ? "--r099-script-runtime-tools-ui-capture="
            : captureArgument?.StartsWith("--r098-script-world-ui-capture=", StringComparison.Ordinal) == true
                ? "--r098-script-world-ui-capture="
                : captureArgument?.StartsWith("--r097-script-p1-ui-capture=", StringComparison.Ordinal) == true
                    ? "--r097-script-p1-ui-capture="
                    : "--script-editor-ui-capture=";
        if (captureArgument is not null && captureArgument.Length > capturePrefix.Length)
        {
            _capturePath = captureArgument[capturePrefix.Length..];
            if (capturePrefix.StartsWith("--r101", StringComparison.Ordinal))
                _captureMarker = "OPENRPG_FORGE_R101_SCRIPT_INTEROP_UI_CAPTURE";
            else if (capturePrefix.StartsWith("--r100", StringComparison.Ordinal))
                _captureMarker = "OPENRPG_FORGE_R100_SCRIPT_QUALITY_UI_CAPTURE";
            else if (capturePrefix.StartsWith("--r099", StringComparison.Ordinal))
                _captureMarker = "OPENRPG_FORGE_R099_SCRIPT_RUNTIME_TOOLS_UI_CAPTURE";
            else if (capturePrefix.StartsWith("--r098", StringComparison.Ordinal))
                _captureMarker = "OPENRPG_FORGE_R098_SCRIPT_WORLD_UI_CAPTURE";
            else if (capturePrefix.StartsWith("--r097", StringComparison.Ordinal))
                _captureMarker = "OPENRPG_FORGE_R097_SCRIPT_P1_UI_CAPTURE";
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            Open();
            if (_captureMarker.Contains("R101", StringComparison.Ordinal))
            {
                Compile();
                RunInteropPreview();
            }
            else if (_captureMarker.Contains("R100", StringComparison.Ordinal))
            {
                Compile();
                RunQualityPreview();
            }
            else
            {
                if (_captureMarker.Contains("R099", StringComparison.Ordinal)) RunRuntimeToolsPreview();
                else if (_captureMarker.Contains("R098", StringComparison.Ordinal)) RunWorldApiPreview();
                else RunP1Preview();
                Compile();
            }
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"{_captureMarker}_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        LoadWorkspace();
        Exclusive = true;
        PopupCentered(new Vector2I(1680, 1000));
        _brandWindow?.Invoke(this);
    }

    internal bool RunForSmokeTest()
    {
        LoadSample();
        Preview();
        Compile();
        RunSandbox();
        PreviewPolicy();
        ApplyPolicy();
        UndoPolicy();
        RedoPolicy();
        var handlers = ForgeScriptSourceValidator.ResolveHandlers(_history!.Document, "creature.combat",
            "creature.damaged", ForgeScriptEventAuthority.Server, condition => condition == "is_enraged");
        var denied = ForgeScriptCapabilityManifest.ValidateSource("void main() { file_open(\"x\"); }");
        ModulePermissionsSmokeSucceeded = _permissionsWindow is not null && _permissionsWindow.RunForSmokeTest();
        return LastCompileSucceeded && LastSandboxSucceeded && handlers.Count == 2 && BindingCount == 2 &&
               CapabilityCount == 6 && denied.Count == 1 && _history.Preview(Command()).Succeeded &&
               _policyHistory.Revision == 3 && ForgeScriptExecutionPolicyValidator.Validate(_policyHistory.Policy).Count == 0 &&
               ModulePermissionsSmokeSucceeded;
    }

    internal bool RunP1ForSmokeTest()
    {
        LoadSample();
        RunP1Preview();
        var document = _history!.Document;
        var adapter = new ForgeScriptP1RuntimeAdapter();
        var resolution = adapter.Resolve(document, true);
        var handle = resolution.Handle!;
        var runtime = adapter.Build(handle, document, EditorContext(),
            new ScriptCombatState(new Dictionary<string, double> { ["warden"] = 40 }, [], 0,
                new Dictionary<string, double> { ["warden"] = 2 }));
        var analysis = adapter.Analyze(handle, runtime,
            document.Source.IndexOf("combat_attack", StringComparison.Ordinal) + "combat_attack".Length);
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(20_000, 1000, 32, 8));
        var optional = adapter.Resolve(null, false);
        var required = adapter.Resolve(null, true);
        P1AuthoringSmokeSucceeded = resolution.Available && analysis.Completions.Count > 0 &&
                                    analysis.SyntaxSpans.Count > 0 &&
                                    execution.Status == ForgeScriptExecutionStatus.Completed &&
                                    execution.Events.Any(item => item.Kind == "action.queued") &&
                                    execution.Events.Any(item => item.Kind == "combat.attack") &&
                                    execution.Events.Any(item => item.Kind == "effect.applied") &&
                                    optional.OptionalFallback &&
                                    required.DiagnosticCode == ForgeScriptP1RuntimeAdapter.RequiredMissingCode &&
                                    _authoringTools is { ItemCount: >= 5 } &&
                                    _gameplayApis is { ItemCount: >= 5 };
        return P1AuthoringSmokeSucceeded;
    }

    internal bool RunWorldApiForSmokeTest()
    {
        LoadSample();
        RunWorldApiPreview();
        var document = _history!.Document;
        var adapter = new ForgeScriptWorldApiRuntimeAdapter();
        var resolution = adapter.Resolve(document, true);
        var handle = resolution.Handle!;
        var runtime = adapter.Build(handle, document, WorldInventory(), WorldQuests(), WorldFactions());
        var transfer = adapter.Transfer(handle, runtime,
            new(Guid.Parse("c7980000-0000-4000-8000-000000000020"), "server", "loot",
                "chest", "hero", "crypt-key", 1));
        runtime = adapter.TransitionQuest(handle, transfer.Runtime, "crypt",
            new QuestTransition(Guid.Parse("c7980000-0000-4000-8000-000000000021"), "server",
                "active", "completed", "find-key", 1, "found"));
        var dialogue = adapter.StartDialogue(handle, runtime,
            new DialogueRequest(Guid.Parse("c7980000-0000-4000-8000-000000000022"), "server",
                "graph:intro", new Dictionary<string, string>
                {
                    ["speaker"] = "warden",
                    ["listener"] = "hero",
                }, new Dictionary<string, string> { ["quest"] = "crypt" }));
        runtime = adapter.CompleteDialogue(handle, dialogue.Runtime, dialogue.Session.Id, "accepted");
        runtime = adapter.ApplyFaction(handle, runtime,
            new FactionTransaction(Guid.Parse("c7980000-0000-4000-8000-000000000023"), "server",
                "hero", "wardens", 25, true, "module", "rescued"));
        var perceived = adapter.Perceive(handle, runtime,
            new PerceptionQuery("warden", "hero", "sight", 2, 10, true, .8, 98));
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));
        var optional = adapter.Resolve(null, false);
        var required = adapter.Resolve(null, true);
        P1WorldApiSmokeSucceeded = resolution.Available && transfer.Result.Committed &&
                                   runtime.Inventory.Items["hero"]["crypt-key"] == 1 &&
                                   runtime.Quests["crypt"].Status == "completed" &&
                                   runtime.Dialogues[dialogue.Session.Id].State == "completed" &&
                                   ScriptWorldService.QueryFaction(runtime.Factions, "hero", "wardens", "module")
                                       .Member && perceived.Perceived &&
                                   execution.Status == ForgeScriptExecutionStatus.Completed &&
                                   execution.Events.Any(item => item.Kind == "inventory.transfer") &&
                                   execution.Events.Any(item => item.Kind == "quest.transition") &&
                                   execution.Events.Any(item => item.Kind == "dialogue.started") &&
                                   execution.Events.Any(item => item.Kind == "faction.reputation") &&
                                   execution.Events.Any(item => item.Kind == "perception.query") &&
                                   optional.OptionalFallback &&
                                   required.DiagnosticCode == ForgeScriptWorldApiRuntimeAdapter.RequiredMissingCode &&
                                   _gameplayApis is { ItemCount: 5 };
        return P1WorldApiSmokeSucceeded;
    }

    internal bool RunRuntimeToolsForSmokeTest()
    {
        LoadSample();
        RunRuntimeToolsPreview();
        var document = _history!.Document;
        var adapter = new ForgeScriptRuntimeToolAdapter();
        var resolution = adapter.Resolve(document, true);
        var handle = resolution.Handle!;
        var runtime = adapter.Build(handle, document,
            new AudioState(new Dictionary<string, AudioCue>(), "camera", "default", []),
            new WorldClock(DateTimeOffset.UnixEpoch, TimeSpan.Zero, 2, 0), RuntimeToolModule());
        runtime = adapter.Play(handle, runtime,
            new("music:dawn", "music", .8, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
                true, "exploration"));
        runtime = adapter.SetListener(handle, runtime, "hero", "combat", "player");
        runtime = adapter.Schedule(handle, runtime,
            Guid.Parse("c7990000-0000-4000-8000-000000000020"), TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3), "OnTick", "daily");
        runtime = adapter.Advance(handle, runtime, TimeSpan.FromSeconds(5));
        var deliveries = adapter.Publish(handle, runtime,
            new("forge.damage", 1,
                new Dictionary<string, Type> { ["amount"] = typeof(int), ["target"] = typeof(Guid) }),
            ["audio", "quest"], new("forge.damage", 1,
                new Dictionary<string, object?>
                {
                    ["amount"] = 4,
                    ["target"] = Guid.Parse("c7990000-0000-4000-8000-000000000021"),
                }, "server", Guid.Parse("c7990000-0000-4000-8000-000000000022")));
        var candidate = runtime.ActiveModule with
        {
            Version = 2,
            SourceHash = "sha256:new",
            Dependencies = new HashSet<string> { "quest" },
        };
        var reloaded = adapter.Reload(handle, runtime, candidate, true, (_, _) => true);
        runtime = reloaded.State;
        runtime = adapter.Break(handle, runtime, "debug:r099", "server-admin",
            [new("OnDamage", "scripts/events.as", 42, new Dictionary<string, object?> { ["amount"] = 4 })],
            new Dictionary<string, object?> { ["hero.hp"] = 36 },
            new Dictionary<string, string> { ["hero"] = "entity:hero" }, ["forge.damage"],
            new HashSet<string> { "scripts/events.as:42" }, true);
        runtime = adapter.Step(handle, runtime, "over");
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));
        var optional = adapter.Resolve(null, false);
        var required = adapter.Resolve(null, true);
        P1RuntimeToolsSmokeSucceeded = resolution.Available && runtime.Audio.Playing.Count == 1 &&
                                       runtime.Audio.ListenerPolicy == "player" &&
                                       ForgeScriptRuntimeTools.Due(runtime.Clock, runtime.Timers).Count == 1 &&
                                       deliveries.Select(item => item.Subscriber).SequenceEqual(["audio", "quest"]) &&
                                       reloaded.Result.Applied && runtime.Debugger is
                                       { Remote: true, State: "paused" } &&
                                       execution.Status == ForgeScriptExecutionStatus.Completed &&
                                       execution.Events.Any(item => item.Kind == "audio.started") &&
                                       execution.Events.Any(item => item.Kind == "time.timer.scheduled") &&
                                       execution.Events.Any(item => item.Kind == "event.published") &&
                                       execution.Events.Any(item => item.Kind == "hot-reload.requested") &&
                                       execution.Events.Any(item => item.Kind == "debug.step") &&
                                       optional.OptionalFallback &&
                                       required.DiagnosticCode == ForgeScriptRuntimeToolAdapter.RequiredMissingCode &&
                                       _gameplayApis is { ItemCount: 5 };
        return P1RuntimeToolsSmokeSucceeded;
    }

    internal bool RunQualityForSmokeTest()
    {
        LoadSample();
        RunQualityPreview();
        var document = _history!.Document with { EventBindings = [] };
        var profile = ForgeScriptQualityProfile.Default;
        var adapter = new ForgeScriptQualityRuntimeAdapter();
        var resolution = adapter.Resolve(profile, document, true);
        var handle = resolution.Handle!;
        var runtime = adapter.Build(handle, profile, document);
        var testResults = adapter.RunTests(handle, runtime,
        [
            new("author-preview", new("fixture", profile.UnitTests.DeterministicSeed,
                    new Dictionary<string, object?> { ["ready"] = true },
                    new Dictionary<string, object?> { ["clock"] = "mock" }),
                _ => { }, world => world.Events.Add("quality.tested"),
                [new("ready", world => (bool)world.State["ready"]!, "world not ready")]),
        ]);
        var profiler = adapter.Profile(handle, runtime,
        [
            new("events.as", "OnHeartbeat", 12_000, 9 * 1024 * 1024,
                TimeSpan.FromMilliseconds(125), ["OnHeartbeat", "FindAll"]),
        ]);
        var documentation = adapter.GenerateDocumentation(handle, runtime, ForgeScriptApiCatalog.Registrations);
        var lint = adapter.Analyze(handle, runtime,
            new("scripts/events.as", "OnHeartbeat FindAll();\nUnsafeQuery();\nDeprecatedApi();\nServerOnly();\ntarget.Use();",
                "client", new HashSet<string> { "target" }));
        runtime.StateService.RegisterPersistent(new("score", 1, typeof(int), (_, json) => json.GetInt32()));
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
            Id = Guid.Parse("c7100000-0000-4000-8000-000000000220"),
            Source = "void main() { while (true) { int value = 1; } }",
        };
        var abusiveHandle = adapter.Resolve(profile, abusiveDocument, true).Handle!;
        var abusive = adapter.Execute(abusiveHandle, adapter.Build(abusiveHandle, profile, abusiveDocument),
            new NativeForgeScriptRuntime(), new ForgeScriptQuotas(10, 1000, 32, 8));
        var recovered = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));
        var history = new ForgeScriptQualityHistory(profile);
        var changed = profile with { Provenance = "toolset:r100-reviewed" };
        var preview = history.Preview(new(Guid.NewGuid(), 0, changed, "toolset preview"));
        var applied = history.Apply(new(Guid.NewGuid(), 0, changed, "toolset apply"));
        var undone = history.Undo();
        var redone = history.Redo();
        var invalid = history.Preview(new(Guid.NewGuid(), history.Revision,
            changed with { Documentation = changed.Documentation with { OutputRelativePath = "../bad.md" } },
            "toolset invalid"));
        var optional = adapter.Resolve(null, null, false);
        var required = adapter.Resolve(null, document, true);
        P1QualitySmokeSucceeded = resolution.Available && AssertQualityUi() &&
                                  testResults.Single().Passed && profiler.Alerts.Count == 3 &&
                                  profiler.CallGraph.ContainsKey("events.as::OnHeartbeat") &&
                                  documentation.McpResources.Count > 0 && lint.Count == 5 &&
                                  (int)restored["score"]! == 7 && !serialized.Contains("vm-pointer", StringComparison.Ordinal) &&
                                  execution.Status == ForgeScriptExecutionStatus.Completed &&
                                  abusive.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                                  recovered.Status == ForgeScriptExecutionStatus.Completed &&
                                  preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                                  redone.Profile.Provenance == "toolset:r100-reviewed" && !invalid.Succeeded &&
                                  optional.OptionalFallback &&
                                  required.DiagnosticCode == ForgeScriptQualityRuntimeAdapter.RequiredMissingCode;
        return P1QualitySmokeSucceeded;
    }

    internal bool RunInteropForSmokeTest()
    {
        LoadSample();
        RunInteropPreview();
        var document = _history!.Document with { EventBindings = [] };
        var profile = ForgeScriptInteropProfile.Default;
        var adapter = new ForgeScriptInteropRuntimeAdapter();
        var resolution = adapter.Resolve(profile, document, true);
        var handle = resolution.Handle!;
        var runtime = adapter.Build(handle, profile, document);
        var nominal = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime());
        var abusiveDocument = document with
        {
            Id = Guid.Parse("c7110000-0000-4000-8000-000000000220"),
            Source = "void main() { while (true) { int value = 1; } }",
        };
        var abusiveHandle = adapter.Resolve(profile, abusiveDocument, true).Handle!;
        var abusive = adapter.Execute(abusiveHandle, adapter.Build(abusiveHandle, profile, abusiveDocument),
            new NativeForgeScriptRuntime(), new ForgeScriptQuotas(10, 1000, 32, 8));
        var recovered = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime());
        var history = new ForgeScriptInteropHistory(profile);
        var changed = profile with { Provenance = "toolset:r101-reviewed", MaximumSources = 1024 };
        var preview = history.Preview(new(Guid.NewGuid(), 0, changed, "toolset preview"));
        var applied = history.Apply(new(Guid.NewGuid(), 0, changed, "toolset apply"));
        var rejected = history.Preview(new(Guid.NewGuid(), history.Revision,
            changed with { EventBinding = "" }, "toolset reject"));
        var undone = history.Undo();
        var redone = history.Redo();
        var sourceHistory = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), []);
        var sourceChanged = document with { Provenance = "toolset:r101-source" };
        var sourcePreview = sourceHistory.Preview(new(Guid.NewGuid(), 0, sourceChanged, "interop preview"));
        var sourceApplied = sourceHistory.Apply(new(Guid.NewGuid(), 0, sourceChanged, "interop apply"));
        var sourceUndone = sourceHistory.Undo();
        var sourceRedone = sourceHistory.Redo();
        var optional = adapter.Resolve(null, null, false);
        var required = adapter.Resolve(null, document, true);
        P1InteropSmokeSucceeded = resolution.Available && nominal.Status == ForgeScriptExecutionStatus.Completed &&
                                  abusive.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                                  recovered.Status == ForgeScriptExecutionStatus.Completed &&
                                  preview.Succeeded && applied.Succeeded && !rejected.Succeeded &&
                                  undone.Succeeded && redone.Succeeded &&
                                  sourcePreview.Succeeded && sourceApplied.Succeeded &&
                                  sourceUndone.Succeeded && sourceRedone.Succeeded &&
                                  sourceRedone.Document.Provenance == "toolset:r101-source" &&
                                  optional.OptionalFallback &&
                                  required.DiagnosticCode == ForgeScriptInteropRuntimeAdapter.RequiredMissingCode &&
                                  _authoringTools is { ItemCount: 6 } && _gameplayApis is { ItemCount: 5 } &&
                                  _diagnostics is { ItemCount: > 0 };
        return P1InteropSmokeSucceeded;
    }

    private bool AssertQualityUi() => _authoringTools is { ItemCount: 5 } &&
                                      _gameplayApis is { ItemCount: 5 } &&
                                      _diagnostics is { ItemCount: > 0 };

    private void RunP1Preview()
    {
        if (_history is null || _authoringTools is null || _gameplayApis is null) return;
        if (_gameplayHeading is not null) _gameplayHeading.Text = "ACTIONS · COMBAT · EFFETS";
        var context = EditorContext();
        var source = _source?.Text ?? _history.Document.Source;
        var cursor = Math.Min(source.Length,
            Math.Max(0, source.IndexOf("combat_attack", StringComparison.Ordinal) + "combat_attack".Length));
        var analysis = ForgeScriptAuthoringService.Analyze(source, cursor, context);
        var condition = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Compare, ">", "bool",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "health", "int"),
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "0", "int"));
        var action = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Call, "say", "void",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "\"Cible valide\"", "string"));
        var wizard = ForgeScriptAuthoringService.BuildWizard(new(
            Guid.Parse("c7970000-0000-4000-8000-000000000010"), 1, "toolset:r097",
            "NotifyValidTarget", [condition], [action]));

        var at = DateTimeOffset.UnixEpoch;
        var queued = ForgeScriptAuthoringService.Enqueue(new([], 0),
            new ScriptTask(Guid.Parse("c7970000-0000-4000-8000-000000000011"), "hero", 100,
                ScriptNetworkAuthority.Server, "attack", new Dictionary<string, object> { ["target"] = "warden" },
                ScriptTaskStatus.Queued, at, null, null), ScriptNetworkAuthority.Server);
        var started = ForgeScriptAuthoringService.StartNext(queued.Queue, at.AddSeconds(1));
        var completed = ForgeScriptAuthoringService.Complete(started.Queue, started.Queue.Tasks[0].Id,
            "hit", at.AddSeconds(2));
        var combat = ScriptGameplayService.Attack(
            new ScriptCombatState(new Dictionary<string, double> { ["warden"] = 40 }, [], 0,
                new Dictionary<string, double> { ["warden"] = 2 }),
            new AttackContext(Guid.Parse("c7970000-0000-4000-8000-000000000012"), "hero", "warden",
                "ash-slash", 1, 0, 97, "server", "toolset:r097"),
            new DamageSpec(8, "fire", new HashSet<string> { "burning" }, false, 0.5));
        var definition = new EffectDefinition("effect:burning", 1, "toolset:r097", "damage-over-time",
            EffectStacking.RefreshDuration, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), 2,
            new HashSet<string> { "fire" }, true);
        var effects = ScriptGameplayService.ApplyEffect([], definition,
            new EffectSpec(Guid.Parse("c7970000-0000-4000-8000-000000000013"), definition.Id,
                "hero", "warden", 1, null, at));

        _authoringTools.Clear();
        _authoringTools.AddItem($"Coloration syntaxique · {analysis.SyntaxSpans.Count} token(s)");
        _authoringTools.AddItem($"Complétion · {analysis.Completions.Count} proposition(s)");
        _authoringTools.AddItem($"Signature · {analysis.SignatureHelp ?? "API contextuelle prête"}");
        _authoringTools.AddItem($"Symboles & docs · {analysis.DocumentSymbols.Count} symbole(s)");
        _authoringTools.AddItem($"Formatage · {analysis.FormattedSource.Length} caractères normalisés");
        _authoringTools.AddItem($"Wizard typé · {(wizard.Succeeded ? "source relisible" : "refusé")}");
        _gameplayApis.Clear();
        _gameplayApis.AddItem($"Action différée · {completed.Queue.Tasks[0].Status} · priorité 100");
        _gameplayApis.AddItem($"Combat · {combat.AppliedDamage:0.##} dégâts · seed 97 · serveur");
        _gameplayApis.AddItem($"Effet · {effects.Effects[0].Definition.Stacking} · période 2 s");
        _gameplayApis.AddItem("API · action_enqueue / combat_attack / effect_apply / effect_dispel");
        _gameplayApis.AddItem("Sandbox · handles opaques · quotas · aucun accès externe");
        _authoringTools.SetMeta("wizard_source", wizard.ReviewedSource);
        _gameplayApis.SetMeta("task_revision", completed.Queue.Revision);
        _gameplayApis.SetMeta("combat_revision", combat.State.Revision);
        _gameplayApis.SetMeta("effect_count", effects.Effects.Count);
    }

    private void RunWorldApiPreview()
    {
        if (_history is null || _gameplayApis is null) return;
        if (_gameplayHeading is not null)
            _gameplayHeading.Text = "INVENTAIRE · QUÊTES · DIALOGUES · FACTIONS · PERCEPTION";
        var inventory = ScriptWorldService.Transfer(WorldInventory(),
            new(Guid.Parse("c7980000-0000-4000-8000-000000000030"), "server", "loot",
                "chest", "hero", "crypt-key", 1));
        var inventoryQuery = ScriptWorldService.QueryInventory(inventory.State, "hero", "crypt-key");
        var quest = ScriptWorldService.TransitionQuest(WorldQuests()["crypt"],
            new(Guid.Parse("c7980000-0000-4000-8000-000000000031"), "server",
                "active", "completed", "find-key", 1, "found"));
        var dialogue = ScriptWorldService.StartDialogue(new(
            Guid.Parse("c7980000-0000-4000-8000-000000000032"), "server", "graph:intro",
            new Dictionary<string, string> { ["speaker"] = "warden", ["listener"] = "hero" },
            new Dictionary<string, string> { ["quest"] = "crypt" }));
        var faction = ScriptWorldService.ApplyFaction(WorldFactions(),
            new(Guid.Parse("c7980000-0000-4000-8000-000000000033"), "server",
                "hero", "wardens", 25, true, "module", "rescued"));
        var factionQuery = ScriptWorldService.QueryFaction(faction, "hero", "wardens", "module");
        var perception = ScriptWorldService.Perceive(
            new("warden", "hero", "sight", 2, 10, true, .8, 98));
        _gameplayApis.Clear();
        _gameplayApis.AddItem($"Inventaire · clé {inventoryQuery.Quantity} · capacité {inventoryQuery.UsedCapacity}/{inventoryQuery.MaximumCapacity} · rollback sûr");
        _gameplayApis.AddItem($"Quête · {quest.QuestId} · {quest.Status} · objectif {quest.Objectives["find-key"]} · scope {quest.ScopeId}");
        _gameplayApis.AddItem($"Dialogue · {dialogue.GraphId} · participants {dialogue.Participants.Count} · variables {dialogue.Variables.Count}");
        _gameplayApis.AddItem($"Faction · wardens {factionQuery.Relationship:+#;-#;0} · membre {factionQuery.Member} · module");
        _gameplayApis.AddItem($"Perception · sight · confiance {perception.Confidence:0.000} · seed 98 · LOS");
        _gameplayApis.SetMeta("inventory_revision", inventory.State.Revision);
        _gameplayApis.SetMeta("quest_revision", quest.Revision);
        _gameplayApis.SetMeta("faction_revision", faction.Revision);
        if (_captureMarker.Contains("R098", StringComparison.Ordinal) && _source is not null)
        {
            var sourceLines = _source.Text.Split('\n');
            var inventoryLine = Array.FindIndex(sourceLines,
                line => line.Contains("inventory_transfer", StringComparison.Ordinal));
            if (inventoryLine >= 0)
            {
                _source.SetCaretLine(inventoryLine);
                _source.ScrollVertical = Math.Max(0, inventoryLine - 3);
            }
        }
    }

    private void RunRuntimeToolsPreview()
    {
        if (_history is null || _gameplayApis is null) return;
        if (_gameplayHeading is not null)
            _gameplayHeading.Text = "AUDIO · TEMPS · ÉVÉNEMENTS · HOT RELOAD · DÉBOGUEUR";
        var audio = ForgeScriptRuntimeTools.Play(
            new(new Dictionary<string, AudioCue>(), "camera", "default", []),
            new("music:dawn", "music", .8, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2),
                true, "exploration"));
        audio = ForgeScriptRuntimeTools.SetListener(audio, "hero", "combat", "player");
        var clock = ForgeScriptRuntimeTools.Advance(
            new(DateTimeOffset.UnixEpoch, TimeSpan.Zero, 2, 0), TimeSpan.FromSeconds(5));
        var timer = ForgeScriptRuntimeTools.Schedule(clock,
            Guid.Parse("c7990000-0000-4000-8000-000000000030"), TimeSpan.Zero,
            TimeSpan.FromSeconds(3), "OnTick", "daily");
        var events = new ForgeScriptRuntimeTools();
        events.Register(new("forge.damage", 1, new Dictionary<string, Type> { ["amount"] = typeof(int) }));
        events.Subscribe("forge.damage", "audio");
        events.Subscribe("forge.damage", "quest");
        var deliveries = events.Publish(new("forge.damage", 1,
            new Dictionary<string, object?> { ["amount"] = 4 }, "server",
            Guid.Parse("c7990000-0000-4000-8000-000000000031")));
        var active = RuntimeToolModule();
        var reload = ForgeScriptRuntimeTools.Reload(active,
            active with { Version = 2, SourceHash = "sha256:new", Dependencies = new HashSet<string> { "quest" } },
            true, (_, _) => true);
        var debug = ForgeScriptRuntimeTools.Break("debug:r099", "server-admin",
            [new("OnDamage", "scripts/events.as", 42, new Dictionary<string, object?> { ["amount"] = 4 })],
            new Dictionary<string, object?> { ["hero.hp"] = 36 },
            new Dictionary<string, string> { ["hero"] = "entity:hero" }, ["forge.damage"],
            new HashSet<string> { "scripts/events.as:42" }, true);
        _gameplayApis.Clear();
        _gameplayApis.AddItem($"Audio · {audio.Playing.Keys.Single()} · bus music · snapshot {audio.Snapshot} · {audio.ListenerPolicy}");
        _gameplayApis.AddItem($"Temps · {clock.SimulationTime.TotalSeconds:0} s · calendrier {ForgeScriptRuntimeTools.CalendarTime(clock):HH:mm:ss} · timer {timer.Schedule}");
        _gameplayApis.AddItem($"Événement · forge.damage@1 · {deliveries.Count} abonnés · trace corrélée");
        _gameplayApis.AddItem($"Hot reload · v{reload.Active.Version} · {reload.Strategy} · invalidations {reload.Invalidated.Count}");
        _gameplayApis.AddItem($"Débogueur · {debug.State} · distant {debug.Remote} · breakpoint {debug.Breakpoints.Single()}");
        _gameplayApis.SetMeta("audio_revision", audio.Revision);
        _gameplayApis.SetMeta("clock_revision", clock.Revision);
        _gameplayApis.SetMeta("debug_session", debug.SessionId);
        if (_captureMarker.Contains("R099", StringComparison.Ordinal) && _source is not null)
        {
            var sourceLines = _source.Text.Split('\n');
            var audioLine = Array.FindIndex(sourceLines,
                line => line.Contains("audio_emitter", StringComparison.Ordinal));
            if (audioLine >= 0)
            {
                _source.SetCaretLine(audioLine);
                _source.ScrollVertical = Math.Max(0, audioLine - 3);
            }
        }
    }

    private void RunQualityPreview()
    {
        if (_history is null || _authoringTools is null || _gameplayApis is null) return;
        _authoringTools.CustomMinimumSize = new Vector2(0, 110);
        _gameplayApis.CustomMinimumSize = new Vector2(0, 110);
        if (_bindings is not null) _bindings.CustomMinimumSize = new Vector2(0, 50);
        if (_diagnostics is not null) _diagnostics.CustomMinimumSize = new Vector2(0, 85);
        if (_gameplayHeading is not null)
            _gameplayHeading.Text = "TESTS · PROFILAGE · DOCUMENTATION · ANALYSE · ÉTAT";
        var profile = ForgeScriptQualityProfile.Default;
        var test = ForgeScriptQualityService.Run(new("author-preview",
            new("fixture", profile.UnitTests.DeterministicSeed,
                new Dictionary<string, object?> { ["ready"] = true },
                new Dictionary<string, object?> { ["clock"] = "mock" }),
            _ => { }, world => world.Events.Add("quality.tested"),
            [new("ready", world => (bool)world.State["ready"]!, "world not ready")]));
        var profiler = ForgeScriptQualityService.Profile(
            [new("events.as", "OnHeartbeat", 12_000, 9 * 1024 * 1024,
                TimeSpan.FromMilliseconds(125), ["OnHeartbeat", "FindAll"])],
            profile.Profiler.InstructionThreshold, profile.Profiler.AllocationThresholdBytes,
            TimeSpan.FromMilliseconds(profile.Profiler.CpuThresholdMilliseconds));
        var documentation = ForgeScriptQualityService.GenerateDocumentation(ForgeScriptApiCatalog.Registrations, "1.0");
        var lint = ForgeScriptQualityService.Analyze(new("scripts/events.as",
            "OnHeartbeat FindAll();\nUnsafeQuery();\nDeprecatedApi();\nServerOnly();\ntarget.Use();",
            "client", new HashSet<string> { "target" }), profile.StaticAnalysis);
        var state = new ForgeScriptQualityService(profile.State.MaximumBytes);
        state.RegisterPersistent(new("score", 1, typeof(int), (_, json) => json.GetInt32()));
        var stateJson = state.SerializeState(new Dictionary<string, object?> { ["score"] = 7, ["vm-pointer"] = "0xBAD" });

        _authoringTools.Clear();
        _authoringTools.AddItem($"Tests unitaires · {(test.Passed ? "PASS" : "ÉCHEC")} · seed {test.Seed} · services mockés");
        _authoringTools.AddItem($"Profilage · {profiler.TotalInstructions:N0} instructions · {profiler.TotalAllocatedBytes / 1024:N0} Kio · {profiler.TotalCpu.TotalMilliseconds:0} ms");
        _authoringTools.AddItem($"Documentation · API {documentation.ApiVersion} · {documentation.McpResources.Count} ressources MCP");
        _authoringTools.AddItem($"Analyse statique · {lint.Count} diagnostics localisés · 5 règles versionnées");
        _authoringTools.AddItem($"État script · score enregistré · {stateJson.Length} octets · pointeurs VM exclus");
        _gameplayApis.Clear();
        _gameplayApis.AddItem("TestWorld · fixtures · arrange/act/assert · rapport CI déterministe");
        _gameplayApis.AddItem($"Hotspots · {profiler.Hotspots[0].Function} · {profiler.Alerts.Count} alertes de seuil");
        _gameplayApis.AddItem("Référence générée · exemples · server/client/kit · ressource MCP");
        _gameplayApis.AddItem("Lints · heartbeat · query · null · deprecated · autorité");
        _gameplayApis.AddItem("Persistance · composants enregistrés · migration · validation · quota");
        ShowDiagnostics(lint.Select(item => new ForgeScriptSourceDiagnostic(item.Code, item.Path,
            _history.Document.Id, item.Severity, item.Message)).ToArray());
        _authoringTools.SetMeta("quality_profile", profile.Id.ToString("D"));
        _gameplayApis.SetMeta("test_seed", test.Seed);
        _gameplayApis.SetMeta("profile_alerts", profiler.Alerts.Count);
        _gameplayApis.SetMeta("state_bytes", stateJson.Length);
    }

    private void RunInteropPreview()
    {
        if (_history is null || _authoringTools is null || _gameplayApis is null) return;
        _authoringTools.CustomMinimumSize = new Vector2(0, 130);
        _gameplayApis.CustomMinimumSize = new Vector2(0, 110);
        if (_bindings is not null) _bindings.CustomMinimumSize = new Vector2(0, 50);
        if (_diagnostics is not null) _diagnostics.CustomMinimumSize = new Vector2(0, 75);
        if (_gameplayHeading is not null)
            _gameplayHeading.Text = "INTEROP ÉDITEUR · MCP · TRANSACTIONS";
        var profile = ForgeScriptInteropProfile.Default;
        var history = new ForgeScriptInteropHistory(profile);
        var changed = profile with { Provenance = "toolset:r101-preview", MaximumSources = 1024 };
        var preview = history.Preview(new(Guid.Parse("c7110000-0000-4000-8000-000000000230"), 0,
            changed, "author preview"));
        var applied = history.Apply(new(Guid.Parse("c7110000-0000-4000-8000-000000000231"), 0,
            changed, "author apply"));
        var undone = history.Undo();
        var redone = history.Redo();
        var invalid = history.Preview(new(Guid.Parse("c7110000-0000-4000-8000-000000000232"),
            history.Revision, changed with { EventBinding = "" }, "author reject"));

        _authoringTools.Clear();
        _authoringTools.AddItem("get_api · API 1.0.0 · signatures et outils versionnés");
        _authoringTools.AddItem("create/update source · UUID · schéma · provenance · révision");
        _authoringTools.AddItem("compile + diagnostics · chemins et UUID cliquables");
        _authoringTools.AddItem("run_tests · sandbox sous quotas · source inchangée");
        _authoringTools.AddItem("assign_event · handler typé · preview avant application");
        _authoringTools.AddItem("undo/redo · plans source et binding · conflits refusés");
        _gameplayApis.Clear();
        _gameplayApis.AddItem($"Configuration · {profile.Id:D} · schéma v{profile.SchemaVersion} · {profile.Provenance}");
        _gameplayApis.AddItem($"Transaction · preview {preview.Succeeded} · apply {applied.Succeeded} · révision {applied.Revision}");
        _gameplayApis.AddItem($"Historique · undo {undone.Succeeded} · redo {redone.Succeeded} · révision {redone.Revision}");
        _gameplayApis.AddItem($"Validation · rejet {(!invalid.Succeeded ? "PASS" : "ÉCHEC")} · {invalid.Diagnostics.First().Code}");
        _gameplayApis.AddItem($"Quotas · {profile.MaximumSources} sources · {profile.MaximumSourceBytes / 1024} Kio · sandbox {profile.TestInstructionLimit:N0}");
        ShowDiagnostics(invalid.Diagnostics.Select(item => new ForgeScriptSourceDiagnostic(
            item.Code, item.Path, item.SubjectId, item.Severity, item.Message)).ToArray());
        _authoringTools.SetMeta("interop_profile", profile.Id.ToString("D"));
        _gameplayApis.SetMeta("interop_revision", redone.Revision);
        _gameplayApis.SetMeta("interop_rejected", !invalid.Succeeded);
    }

    private static InventoryState WorldInventory() =>
        new(new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["chest"] = new Dictionary<string, int> { ["crypt-key"] = 1 },
            ["hero"] = new Dictionary<string, int>(),
        }, new Dictionary<string, int> { ["hero"] = 8 }, 0);

    private static IReadOnlyDictionary<string, QuestState> WorldQuests() =>
        new Dictionary<string, QuestState>
        {
            ["crypt"] = new("crypt", "party:1", "active",
                new Dictionary<string, int> { ["find-key"] = 0 }, [], 0),
        };

    private static FactionState WorldFactions() =>
        new(new Dictionary<string, IReadOnlySet<string>>(), new Dictionary<string, int>(), 0);

    private static HotReloadModule RuntimeToolModule() =>
        new("module", 1, new Dictionary<string, string> { ["score"] = "int" },
            new HashSet<string> { "dialogue" }, "sha256:old");

    private void InsertWizard()
    {
        var action = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Call, "say", "void",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "\"Action générée\"", "string"));
        var wizard = ForgeScriptAuthoringService.BuildWizard(new(Guid.NewGuid(), 1, "toolset:script-wizard",
            "GeneratedAction", [], [action]));
        if (!wizard.Succeeded) return;
        _source!.Text = _source.Text.TrimEnd() + System.Environment.NewLine + System.Environment.NewLine +
                        wizard.ReviewedSource + System.Environment.NewLine;
        _apply!.Disabled = true;
        RunP1Preview();
        SetStatus("✓ Wizard inséré dans la source · prévisualisez avant enregistrement", false);
    }

    private void FormatSource()
    {
        var analysis = ForgeScriptAuthoringService.Analyze(_source!.Text, _source.Text.Length, EditorContext());
        _source.Text = analysis.FormattedSource;
        _apply!.Disabled = true;
        RunP1Preview();
        SetStatus("✓ Formatage déterministe appliqué · prévisualisez avant enregistrement", false);
    }

    private static ForgeScriptEditorContext EditorContext()
    {
        var symbols = ForgeScriptApiCatalog.Registrations.Select(item => new ForgeScriptSymbol(
            item.Name, "function", item.Signature.Split(' ', 2)[0], item.Summary, item.Signature,
            item.Availability.Order(StringComparer.Ordinal).ToArray())).ToArray();
        return new(symbols, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["main"] = "void main() {\n}\n",
            ["handler"] = "void Handler(const int &in e) {\n}\n",
            ["ifvalid"] = "if (health(target) > 0) {\n}\n",
        }, new HashSet<string>(["bool", "const", "double", "else", "float", "if", "int", "return", "string", "void", "while"],
            StringComparer.Ordinal));
    }

    private void LoadWorkspace()
    {
        _sources!.Clear();
        var session = _getSession?.Invoke();
        if (session is null)
        {
            SetPolicy(ForgeScriptExecutionPolicy.Default);
            LoadSample();
            SetStatus("Aucun projet ouvert · exemple en lecture locale", warning: true);
            return;
        }
        LoadPolicy(session);
        var scripts = Path.Combine(session.RootDirectory, "scripts");
        var paths = Directory.Exists(scripts)
            ? Directory.GetFiles(scripts, "*.as", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray()
            : [];
        foreach (var path in paths) _sources.AddItem(Path.GetRelativePath(session.RootDirectory, path).Replace('\\', '/'));
        if (paths.Length == 0)
        {
            LoadSample();
            _sources.AddItem("＋ Nouvelle source · scripts/events.as");
            _sources.SetItemTooltip(0, "Créer la source canonique scripts/events.as via une transaction prévisualisée.");
            SetStatus("État vide · aucune source .as canonique ; l’exemple n’est pas encore enregistré", warning: true);
            return;
        }
        try
        {
            var relative = Path.GetRelativePath(session.RootDirectory, paths[0]).Replace('\\', '/');
            var document = new ForgeScriptSourceStore(session.RootDirectory).Load(relative);
            SetDocument(document);
            _sources.Select(0);
            SetStatus($"✓ Source chargée · {relative}", warning: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException)
        {
            LoadSample();
            ShowDiagnostics([new("script.source.load_failed", "script/source", null, "error", exception.Message)]);
            SetStatus("Source invalide · consultez le diagnostic cliquable", warning: true);
        }
    }

    private void LoadSample()
    {
        var first = Binding("OnDamaged", 100, "is_enraged");
        var second = Binding("LogDamage", 20, null);
        SetDocument(new ForgeScriptSourceDocument(
            Guid.Parse("12000000-0000-4000-8000-000000000001"), 1, "forge:script-editor",
            "scripts/events.as",
            "void OnDamaged(const int &in e)\n{\n    int observed = e;\n    say(\"Cible touchee\");\n}\n\n" +
            "void LogDamage(const int &in e)\n{\n    int observed = e;\n    variable(\"combat.last_damage\", \"received\");\n}\n\n" +
            "void main()\n{\n    AreaRef area = current_area();\n    EntityRef hero = find_entity(area, \"hero\");\n" +
            "    Vec3 goal = vec3(6, 0, 4);\n    local_set_int(hero, \"quest.stage\", 2);\n" +
            "    MovementTaskRef task = move_to(hero, goal, \"run\");\n    cancel_movement(task);\n" +
            "    ScriptTaskRef attack = action_enqueue(hero, \"attack\", 100);\n" +
            "    combat_attack(hero, hero, \"ash-slash\", 8.0f, 97);\n" +
            "    EffectHandle burning = effect_apply(hero, hero, \"burning\", 2.0f, 10.0f);\n" +
            "    action_complete(attack, \"hit\");\n    effect_dispel(hero, \"fire\");\n" +
            "    release_effect(burning);\n" +
            "    EntityRef chest = spawn(\"ash-chest\");\n" +
            "    InventoryResultRef loot = inventory_transfer(chest, hero, \"crypt-key\", 1);\n" +
            "    QuestResultRef questResult = quest_transition(\"crypt\", \"party:1\", \"active\", \"completed\", \"find-key\", 1, \"found\");\n" +
            "    DialogueSessionRef talk = dialogue_start(\"graph:intro\", hero, hero);\n" +
            "    dialogue_complete(talk, \"accepted\");\n" +
            "    FactionResultRef standing = faction_reputation(hero, \"wardens\", 25, true, \"module\", \"rescued\");\n" +
            "    PerceptionResultRef seen = perception_query(hero, hero, \"sight\", 2.0f, 10.0f, true, 0.8f, 98);\n" +
            "    AudioEmitterRef music = audio_emitter(\"music:dawn\");\n" +
            "    audio_start(music);\n    audio_snapshot(\"combat\");\n    audio_listener(hero, \"player\");\n" +
            "    WorldClockRef clock = world_clock();\n" +
            "    TimerRef timer = timer_schedule(1000, 5000, \"OnTick\", \"daily\");\n" +
            "    event_subscribe(\"forge.damage\", 1, \"OnDamage\");\n" +
            "    CustomEventRef evt = custom_event(\"forge.damage\", 1, \"amount=4\");\n" +
            "    event_publish(evt);\n" +
            "    HotReloadRef reload = hot_reload_request(\"module\", 2, \"sha256:new\");\n" +
            "    DebugSessionRef debug = debug_break(\"session:1\", \"local-author\");\n" +
            "    debug_step(debug, \"over\");\n" +
            "    say(\"ForgeScript pret\");\n}\n",
            "1.0.0", ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], [first, second]));
    }

    private void SetDocument(ForgeScriptSourceDocument document)
    {
        _history = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), [DamageEvent]);
        PopulateDocument();
    }

    private void LoadPolicy(ProjectSession session)
    {
        try
        {
            var root = Path.Combine(session.RootDirectory, ".forge");
            SetPolicy(File.Exists(Path.Combine(root, "forge-script-policy.json"))
                ? new ForgeScriptExecutionPolicyStore(root).Load()
                : ForgeScriptExecutionPolicy.Default);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or System.Text.Json.JsonException)
        {
            SetPolicy(ForgeScriptExecutionPolicy.Default);
            ShowDiagnostics([new("script.policy.load_failed", "script/policy", null, "error", exception.Message)]);
        }
    }

    private void SetPolicy(ForgeScriptExecutionPolicy policy)
    {
        _policyHistory = new(policy);
        if (_policyPreset is not null) _policyPreset.Selected = 1;
        PopulatePolicy();
    }

    private ForgeScriptExecutionPolicy SelectedPolicy() => (_policyPreset?.Selected ?? 1) switch
    {
        0 => _policyHistory.Policy with
        {
            HandlerInstructionLimit = 5_000,
            TickInstructionLimit = 25_000,
            ModuleInstructionLimit = 500_000,
            ContextMemoryLimitBytes = 4 * 1024 * 1024,
            ModuleMemoryLimitBytes = 64L * 1024 * 1024
        },
        2 => _policyHistory.Policy with
        {
            HandlerInstructionLimit = 25_000,
            TickInstructionLimit = 125_000,
            ModuleInstructionLimit = 2_500_000,
            ContextMemoryLimitBytes = 16 * 1024 * 1024,
            ModuleMemoryLimitBytes = 256L * 1024 * 1024
        },
        _ => _policyHistory.Policy with
        {
            HandlerInstructionLimit = 10_000,
            TickInstructionLimit = 50_000,
            ModuleInstructionLimit = 1_000_000,
            ContextMemoryLimitBytes = 8 * 1024 * 1024,
            ModuleMemoryLimitBytes = 128L * 1024 * 1024
        },
    };

    private ForgeScriptExecutionPolicyCommand PolicyCommand() => new(Guid.NewGuid(), _policyHistory.Revision,
        SelectedPolicy(), "script-editor-policy");

    private void PreviewPolicy()
    {
        var result = _policyHistory.Preview(PolicyCommand());
        _policyApply!.Disabled = !result.Succeeded;
        SetStatus(result.Succeeded ? $"✓ Politique prévisualisée · {result.SemanticDiff}" :
            "Politique de sandbox refusée · aucun changement", !result.Succeeded);
    }

    private void ApplyPolicy()
    {
        var result = _policyHistory.Apply(PolicyCommand());
        if (!result.Succeeded) return;
        PersistPolicy();
        PopulatePolicy();
        SetStatus($"✓ Politique de sandbox appliquée · révision {result.Revision}", false);
    }

    private void UndoPolicy()
    {
        var result = _policyHistory.Undo();
        if (!result.Succeeded) return;
        PersistPolicy();
        PopulatePolicy();
        SetStatus($"✓ Politique annulée · révision {result.Revision}", false);
    }

    private void RedoPolicy()
    {
        var result = _policyHistory.Redo();
        if (!result.Succeeded) return;
        PersistPolicy();
        PopulatePolicy();
        SetStatus($"✓ Politique rétablie · révision {result.Revision}", false);
    }

    private void PersistPolicy()
    {
        var session = _getSession?.Invoke();
        if (session is not null)
            new ForgeScriptExecutionPolicyStore(Path.Combine(session.RootDirectory, ".forge")).Save(_policyHistory.Policy);
    }

    private void PopulatePolicy()
    {
        if (_policySummary is null) return;
        var policy = _policyHistory.Policy;
        _policySummary.Text = $"{policy.HandlerInstructionLimit:N0} handler · {policy.TickInstructionLimit:N0} tick\n" +
                              $"{policy.ContextMemoryLimitBytes / 1_048_576} Mio contexte · seed {policy.DeterministicSeed}\n" +
                              $"tick {policy.SimulationTick} · {policy.Authority} · API {policy.ApiVersion}";
        _policyUndo!.Disabled = !_policyHistory.CanUndo;
        _policyRedo!.Disabled = !_policyHistory.CanRedo;
        _policyApply!.Disabled = true;
    }

    private void PopulateDocument()
    {
        var document = _history!.Document;
        _source!.Text = document.Source;
        _identity!.Text = $"UUID · {document.Id:D}\nSchéma · v{document.SchemaVersion}\n" +
                          $"Langage · {ForgeScriptSourceDocument.Language} {ForgeScriptSourceDocument.DialectVersion}\n" +
                          $"API · {document.ApiVersion}\nSource · {document.RelativePath}\n" +
                          $"Distribution · {document.Distribution}\nProvenance · {document.Provenance}";
        _capabilities!.Clear();
        foreach (var capability in ForgeScriptCapabilityManifest.Capabilities)
        {
            _capabilities.AddItem($"{capability.DisplayName} · {capability.ApiNames.Count} API");
            _capabilities.SetItemTooltip(_capabilities.ItemCount - 1,
                $"{string.Join(" · ", capability.ApiNames)}\nAutorité {capability.Authority}\nQuota {capability.Quota}");
        }
        _bindings!.Clear();
        foreach (var binding in document.EventBindings.OrderByDescending(item => item.Priority).ThenBy(item => item.Id))
        {
            _bindings.AddItem($"P{binding.Priority} · {binding.EventName} · " +
                              $"{binding.Handler}({binding.PayloadType}) · {binding.Authority}");
            _bindings.SetItemTooltip(_bindings.ItemCount - 1,
                $"UUID {binding.Id:D}\nComposant {binding.ComponentId}\n" +
                $"Entrée {ForgeScriptSourceValidator.EntryDeclaration(binding.Handler, binding.PayloadType)}\n" +
                $"Condition {binding.EnableCondition ?? "toujours"}\nProvenance {binding.Provenance}");
        }
        _undo!.Disabled = !_history.CanUndo;
        _redo!.Disabled = !_history.CanRedo;
        _apply!.Disabled = true;
        ShowDiagnostics([]);
        RunP1Preview();
    }

    private ForgeScriptEventBinding Binding(string handler, int priority, string? condition) => new(
        Guid.NewGuid(), 1, "forge:script-editor", "creature.combat", DamageEvent.EventName, handler,
        DamageEvent.PayloadType, priority, condition, ForgeScriptEventAuthority.Server, true);

    private ForgeScriptSourceCommand Command() => new(Guid.NewGuid(), _history!.Revision,
        _history.Document with { Source = _source!.Text }, "script-editor");

    private void Preview()
    {
        var result = _history!.Preview(Command());
        ShowDiagnostics(result.Diagnostics);
        _apply!.Disabled = !result.Succeeded;
        _console!.Text = result.Succeeded
            ? $"PRÉVISUALISATION\n{result.SemanticDiff}\nAucun fichier écrit."
            : "PRÉVISUALISATION REFUSÉE\nCorrigez les diagnostics avant l’enregistrement.";
        SetStatus(result.Succeeded ? "✓ Transaction source valide · prête à enregistrer" :
            "Transaction refusée · aucune mutation partielle", !result.Succeeded);
    }

    private void Apply()
    {
        var result = _history!.Apply(Command());
        if (!result.Succeeded)
        {
            ShowDiagnostics(result.Diagnostics);
            return;
        }
        var session = _getSession?.Invoke();
        if (session is null)
        {
            SetStatus("Aucun projet ouvert · enregistrement indisponible", true);
            return;
        }
        new ForgeScriptSourceStore(session.RootDirectory).Save(result.Document);
        PopulateDocument();
        SetStatus($"✓ Source enregistrée atomiquement · révision {result.Revision}", false);
    }

    private void Undo()
    {
        var result = _history!.Undo();
        if (result.Succeeded) PersistHistory(result, "Annulation appliquée");
    }

    private void Redo()
    {
        var result = _history!.Redo();
        if (result.Succeeded) PersistHistory(result, "Rétablissement appliqué");
    }

    private void PersistHistory(ForgeScriptSourceMutationResult result, string message)
    {
        var session = _getSession?.Invoke();
        if (session is not null) new ForgeScriptSourceStore(session.RootDirectory).Save(result.Document);
        PopulateDocument();
        SetStatus($"✓ {message} · révision {result.Revision}", false);
    }

    private void Compile()
    {
        var runtime = new NativeForgeScriptRuntime();
        var result = runtime.Compile(_source!.Text);
        _compileDiagnostics = result.Diagnostics.ToArray();
        LastCompileSucceeded = result.Success;
        ShowDiagnostics(result.Diagnostics.Select(item => new ForgeScriptSourceDiagnostic("script.compile",
            $"{_history!.Document.RelativePath}:{Math.Max(1, item.Line)}:{Math.Max(1, item.Column)}",
            _history.Document.Id, item.Severity, item.Message)).ToArray());
        _console!.Text = result.Success
            ? $"COMPILATION RÉUSSIE\nVM {runtime.VirtualMachineVersion} · API {runtime.ApiVersion} · " +
              $"bytecode {result.Artifact!.Bytecode.Length} octets\nCache : source + API + compilateur"
            : $"COMPILATION REFUSÉE\n{result.Diagnostics.Count} diagnostic(s) structuré(s)";
        SetStatus(result.Success ? "✓ Compilation terminée · bytecode jetable prêt" :
            "Compilation impossible · source canonique conservée", !result.Success);
    }

    private void RunSandbox()
    {
        var runtime = new NativeForgeScriptRuntime();
        var compiled = runtime.Compile(_source!.Text);
        if (!compiled.Success || compiled.Artifact is null)
        {
            LastSandboxSucceeded = false;
            Compile();
            return;
        }
        var policy = _policyHistory.Policy;
        var executed = runtime.Execute(compiled.Artifact, new ForgeScriptQuotas(policy.HandlerInstructionLimit,
            policy.TimeoutMilliseconds, policy.RecursionLimit, policy.SpawnLimit, policy.ContextMemoryLimitBytes,
            policy.DeterministicSeed, policy.SimulationTick, policy.SimulationTimeMilliseconds));
        LastSandboxSucceeded = executed.Status == ForgeScriptExecutionStatus.Completed;
        _console!.Text = $"TEST SANDBOX · {executed.Status}\n" +
                         $"Quotas : {policy.HandlerInstructionLimit:N0} instructions · " +
                         $"{policy.ContextMemoryLimitBytes / 1_048_576} Mio · seed {policy.DeterministicSeed} · tick {policy.SimulationTick}\n" +
                         $"Télémétrie : {executed.Telemetry?.Instructions ?? 0} instructions · " +
                         $"pic {executed.Telemetry?.PeakMemoryBytes ?? 0:N0} octets\n" +
                         string.Join("\n", executed.Events.Select(item => $"{item.Kind} · {item.Value}"));
        SetStatus(LastSandboxSucceeded ? "✓ Test sandbox terminé sans mutation du projet" :
            "Test interrompu proprement par le sandbox", !LastSandboxSucceeded);
    }

    private void ShowDiagnostics(IReadOnlyList<ForgeScriptSourceDiagnostic> diagnostics)
    {
        _diagnostics!.Clear();
        foreach (var diagnostic in diagnostics)
        {
            _diagnostics.AddItem($"{diagnostic.Severity.ToUpperInvariant()} · {diagnostic.Code} · " +
                                 $"{diagnostic.Path} — {diagnostic.Message}");
            _diagnostics.SetItemTooltip(_diagnostics.ItemCount - 1,
                $"UUID {diagnostic.SubjectId?.ToString("D") ?? "document"}\nChemin {diagnostic.Path}");
        }
        if (diagnostics.Count == 0)
        {
            _diagnostics.AddItem("✓ Aucun diagnostic · source prête");
            _diagnostics.SetItemSelectable(0, false);
        }
    }

    private void JumpToDiagnostic(long index)
    {
        if (index < 0 || index >= _compileDiagnostics.Length) return;
        _source!.SetCaretLine(Math.Max(0, _compileDiagnostics[index].Line - 1));
        _source.GrabFocus();
    }

    private void SetStatus(string text, bool warning)
    {
        _status!.Text = text;
        _status.AddThemeColorOverride("font_color", warning ? new Color("ffc66d") : new Color("83d6ae"));
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("0d0b17", "62458a"));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "FORGESCRIPT · SOURCES, API SÛRE & SANDBOX" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Créer la logique du monde avec handles opaques, API versionnée et tests sous quotas avant enregistrement.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);
        var left = Panel(body, 250);
        left.AddChild(Section("SOURCES CANONIQUES"));
        _sources = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 125),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        left.AddChild(_sources);
        left.AddChild(Section("IDENTITÉ & GOUVERNANCE"));
        _identity = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _identity.AddThemeColorOverride("font_color", new Color("bdaed4"));
        left.AddChild(_identity);
        left.AddChild(Section("BUDGETS & DÉTERMINISME"));
        _policyPreset = new OptionButton();
        _policyPreset.AddItem("Strict · 5k / 4 Mio");
        _policyPreset.AddItem("Standard · 10k / 8 Mio");
        _policyPreset.AddItem("Large · 25k / 16 Mio");
        _policyPreset.Selected = 1;
        _policyPreset.ItemSelected += _ => _policyApply!.Disabled = true;
        left.AddChild(_policyPreset);
        _policySummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _policySummary.AddThemeColorOverride("font_color", new Color("9dddc1"));
        left.AddChild(_policySummary);
        var policyPreview = Button(left, "Prévisualiser politique", PreviewPolicy);
        policyPreview.CustomMinimumSize = new Vector2(0, 30);
        _policyApply = Button(left, "Appliquer politique", ApplyPolicy, accent: true);
        _policyApply.CustomMinimumSize = new Vector2(0, 30);
        var policyHistory = new HBoxContainer();
        left.AddChild(policyHistory);
        _policyUndo = Button(policyHistory, "Annuler", UndoPolicy);
        _policyUndo.CustomMinimumSize = new Vector2(90, 28);
        _policyRedo = Button(policyHistory, "Rétablir", RedoPolicy);
        _policyRedo.CustomMinimumSize = new Vector2(90, 28);
        PopulatePolicy();

        var center = Panel(body, 690, expand: true);
        var editorHeader = new HBoxContainer();
        editorHeader.AddChild(Section("ÉDITEUR C-LIKE STATIQUEMENT TYPÉ"));
        center.AddChild(editorHeader);
        _source = new CodeEdit
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 480),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        var highlighter = new CodeHighlighter();
        foreach (var keyword in EditorContext().Keywords)
            highlighter.AddKeywordColor(keyword, new Color("c792ea"));
        highlighter.SymbolColor = new Color("89ddff");
        highlighter.NumberColor = new Color("f78c6c");
        highlighter.FunctionColor = new Color("82aaff");
        _source.SyntaxHighlighter = highlighter;
        _source.TextChanged += () => _apply!.Disabled = true;
        center.AddChild(_source);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        center.AddChild(actions);
        Button(actions, "Prévisualiser", Preview);
        Button(actions, "Formater", FormatSource);
        Button(actions, "Wizard", InsertWizard);
        Button(actions, "Compiler", Compile);
        Button(actions, "Tester sandbox", RunSandbox, accent: true);
        Button(actions, "Permissions module", () => _permissionsWindow?.Open());
        _apply = Button(actions, "Enregistrer", Apply, accent: true);
        _apply.Disabled = true;
        _undo = Button(actions, "Annuler", Undo);
        _redo = Button(actions, "Rétablir", Redo);
        _console = new Label
        {
            Text = "CONSOLE\nCompilez ou testez la source ; aucun fichier n’est écrit par ces actions.",
            CustomMinimumSize = new Vector2(0, 105),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _console.AddThemeColorOverride("font_color", new Color("9dddc1"));
        center.AddChild(_console);

        var right = Panel(body, 430);
        right.AddChild(Section("API VERSIONNÉE · P0 + P1"));
        var capabilityHelp = new Label
        {
            Text = "Aucun fichier · réseau · processus · moteur · .NET\nHandles générationnels · RNG et horloge déterministes",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        capabilityHelp.AddThemeColorOverride("font_color", new Color("83d6ae"));
        right.AddChild(capabilityHelp);
        _capabilities = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 95),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        right.AddChild(_capabilities);
        right.AddChild(Section("OUTILS D'AUTEUR P1"));
        _authoringTools = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 150),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        right.AddChild(_authoringTools);
        _gameplayHeading = Section("ACTIONS · COMBAT · EFFETS");
        right.AddChild(_gameplayHeading);
        _gameplayApis = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 145),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        right.AddChild(_gameplayApis);
        right.AddChild(Section("ÉVÉNEMENTS PAR OBJET"));
        var eventHelp = new Label
        {
            Text = "Multi-handlers · priorité · condition · autorité explicite\nEntrée : void Handler(const Payload &in e)",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        eventHelp.AddThemeColorOverride("font_color", new Color("bdaed4"));
        right.AddChild(eventHelp);
        _bindings = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 80),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        right.AddChild(_bindings);
        right.AddChild(Section("DIAGNOSTICS CLIQUABLES"));
        _diagnostics = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _diagnostics.ItemSelected += JumpToDiagnostic;
        right.AddChild(_diagnostics);
        _status = new Label { Text = "Initialisation…", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_status);
    }

    private static VBoxContainer Panel(Container parent, float width, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
        };
        panel.AddThemeStyleboxOverride("panel", Card("151020", "47365f"));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 12);
        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);
        return content;
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color("ffb276"));
        return label;
    }

    private static Button Button(Container parent, string text, Action action, bool accent = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(90, 34) };
        if (accent) button.AddThemeStyleboxOverride("normal", Card("d84f32", "ff9a73"));
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static StyleBoxFlat Card(string background, string border) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
    };
}
#endif
