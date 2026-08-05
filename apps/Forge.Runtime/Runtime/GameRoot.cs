using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Lifecycle;
using Forge.Application.Sequences;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Spatial;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot : Node3D
{
    private const string ProjectPath = "res://content/crypt-of-ashes/project.json";
    private const string ProjectSnapshotArgument = "--project-snapshot";
    private const string ModulePackageArgument = "--module-package";
    private const string SpawnIdArgument = "--spawn-id";
    private const string SaveFileArgument = "--save-file";
    private const string LoadSaveArgument = "--load-save";
    private const float InteractionDistance = 3.0f;
    private const float AttackDistance = 3.5f;

    private readonly Dictionary<Node3D, CombatantState> _hostiles = [];
    private readonly Dictionary<Node3D, string> _hostileNames = [];
    private ProjectBlackboard _blackboard = new([], allowUndeclared: true);
    private readonly HashSet<string> _inventory = new(StringComparer.Ordinal);
    private readonly NativeForgeScriptRuntime _forgeScriptRuntime = new();
    private readonly List<RuntimeLifecycleEvent> _lifecycleEvents = [];
    private readonly HeartbeatScheduler _heartbeatScheduler = new();
    private readonly RestRuntimeCoordinator _restRuntime = new();
    private readonly ProjectGameplayEventBus _gameplayEvents = new();
    private readonly Dictionary<Guid, Node3D> _entityNodes = [];
    private readonly Dictionary<Guid, CameraAnchorDefinition> _cameraAnchors = [];

    private PlayerController? _player;
    private CombatantState? _playerCombatant;
    private Camera3D? _camera;
    private RuntimeHud? _hud;
    private Node3D? _npc;
    private Node3D? _keyItem;
    private Node3D? _door;
    private Node3D? _boss;
    private Node3D? _exit;
    private DialogueDefinition? _elyraDialogue;
    private QuestStateMachine? _questMachine;
    private QuestDefinition? _questDefinition;
    private EntityId? _doorEntityId;
    private ForgeScriptArtifact? _bossPhaseTwoScript;
    private ForgeScriptArtifact? _bossDeathScript;
    private PrototypeQuestState _questState;
    private bool _hasKey;
    private bool _doorUnlocked;
    private bool _bossPhaseTwo;
    private bool _bossDefeated;
    private bool _escaped;
    private int _builtEntityCount;
    private int _collisionCount;
    private int _spawnedAshSpiritCount;
    private int _ashNovaCueCount;
    private string _message = "Explorez la crypte.";
    private string? _moduleContentRoot;
    private string? _moduleId;
    private string? _moduleContentSha256;
    private string? _currentAreaId;
    private string? _quickSavePath;
    private string _moduleTitle = "OpenRPG Forge";
    private string _moduleSlug = string.Empty;
    private bool _isCryptPrototype;
    private bool _loadedSave;
    private RuntimeLifecycleCoordinator? _lifecycle;
    private ForgeScriptArtifactCache? _forgeScriptCache;
    private AreaRenderPlan? _renderPlan;
    private AreaEnvironmentProfile? _environmentProfile;
    private WeatherRuntimeSnapshot? _weatherState;
    private Node3D? _climateEffects;
    private string? _climateSignature;
    private double _climateProbeElapsed;
    private Node3D? _areaWorldRoot;
    private ModuleRuntimePolicyProfile? _moduleRuntimePolicies;
    private AreaRuntimePolicyProfile? _areaRuntimePolicies;
    private RestSession? _restSession;
    private long _runtimePolicyRevision;
    private SequenceDefinition? _activeSequence;
    private SequencePlaybackState? _sequenceState;
    private Transform3D _explorationCameraTransform;
    private float _explorationCameraFov;
    private bool _sequenceOwnsCamera;

    public override void _Ready()
    {
        var startupStarted = Stopwatch.GetTimestamp();
        var userArguments = OS.GetCmdlineUserArgs();
        var smokeTest = userArguments.Contains("--smoke-test", StringComparer.Ordinal);
        var genericSmokeTest = userArguments.Contains("--generic-smoke-test", StringComparer.Ordinal);
        var rpgKitSmokeTest = userArguments.Contains("--rpg-kit-smoke-test", StringComparer.Ordinal);
        var saveSmokeCreate = userArguments.Contains("--save-smoke-create", StringComparer.Ordinal);
        var saveSmokeResume = userArguments.Contains("--save-smoke-resume", StringComparer.Ordinal);
        var automatedTest = smokeTest || genericSmokeTest || rpgKitSmokeTest || saveSmokeCreate || saveSmokeResume;

        try
        {
            RegisterInputActions();
            var snapshotPath = GetArgumentValue(userArguments, ProjectSnapshotArgument);
            var modulePackagePath = GetArgumentValue(userArguments, ModulePackageArgument);
            var saveFilePath = GetArgumentValue(userArguments, SaveFileArgument);
            var loadSavePath = GetArgumentValue(userArguments, LoadSaveArgument);
            _quickSavePath = saveFilePath ?? loadSavePath ??
                ProjectSettings.GlobalizePath("user://openrpg-forge/saves/quick.orpsave");
            _forgeScriptCache = new ForgeScriptArtifactCache(
                _forgeScriptRuntime,
                ProjectSettings.GlobalizePath("user://openrpg-forge/cache/forgescript"));
            var requestedSpawnId = ParseOptionalEntityId(
                GetArgumentValue(userArguments, SpawnIdArgument));
            var project = LoadProject(snapshotPath, modulePackagePath);
            ValidateAssetPackageManifests();
            var lifecycleCause = loadSavePath is null
                ? RuntimeLifecycleCause.Startup
                : RuntimeLifecycleCause.SaveResume;
            _lifecycleEvents.Clear();
            _lifecycle = new RuntimeLifecycleCoordinator(observers: [HandleLifecycleEvent]);
            EnsureLifecycle(
                _lifecycle.LoadProject(project, RuntimeAuthority.SinglePlayer, lifecycleCause),
                "project_loaded");
            _blackboard = new ProjectBlackboard(project.Module.Variables, allowUndeclared: true);
            _moduleTitle = project.Module.DisplayName;
            _moduleSlug = project.Module.Slug;
            _isCryptPrototype = string.Equals(_moduleSlug, "crypt-of-ashes", StringComparison.Ordinal);
            _message = $"Explorez {_moduleTitle}.";
            var entryArea = project.Areas.Single(area => area.Id == project.Module.EntryAreaId);
            ConfigureRuntimePolicies(project.Module.RuntimePolicies, entryArea.RuntimePolicies);
            ConfigureWorld(entryArea.Environment, entryArea.World, _isCryptPrototype);
            var selectedSpawn = BuildProject(project, requestedSpawnId);
            EnsureLifecycle(
                _lifecycle.EnterInitialArea(
                    AreaId.Parse(_currentAreaId!),
                    selectedSpawn.Id,
                    lifecycleCause),
                "area_entered");
            if (loadSavePath is not null)
            {
                if (!LoadRuntimeSave(loadSavePath))
                {
                    throw new InvalidOperationException("The requested runtime save could not be loaded.");
                }
            }

            CreateHud();
            ConfigureCinematics(project, entryArea);
            UpdateHud();
            EnsureLifecycle(_lifecycle.StartWorld(lifecycleCause), "world_started");
            AssertLifecycleStartupContract();
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_PERF startup_ms={Stopwatch.GetElapsedTime(startupStarted).TotalMilliseconds:0.###} " +
                $"nodes={CountTreeNodes(this)} entities={_builtEntityCount} collisions={_collisionCount}");

            if (snapshotPath is not null)
            {
                GD.Print($"OPENRPG_FORGE_RUNTIME_SNAPSHOT_LOADED file={Path.GetFileName(snapshotPath)}");
            }

            GD.Print($"OPENRPG_FORGE_RUNTIME_SPAWN_SELECTED id={selectedSpawn.Id}");
            GD.Print($"OPENRPG_FORGE_RUNTIME_PROJECT_LOADED entities={_builtEntityCount} collisions={_collisionCount}");

            if (rpgKitSmokeTest)
            {
                RunRpgKitHeadlessSmokeTest(project);
            }
            else if (genericSmokeTest)
            {
                RunGenericHeadlessSmokeTest();
            }
            else if (saveSmokeCreate)
            {
                RunSaveCreateSmokeTest();
            }
            else if (saveSmokeResume)
            {
                RunSaveResumeSmokeTest();
            }
            else if (smokeTest)
            {
                RunHeadlessSmokeTest();
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_STARTUP_FAILED {exception}");
            if (automatedTest)
            {
                GetTree().Quit(1);
            }
            else
            {
                ShowStartupFailure(exception);
            }
        }
    }

    public override void _Process(double delta)
    {
        AdvanceClimate(delta);
        AdvanceRuntimePolicies(delta);
        AdvanceCinematics(delta);

        if (_player is null)
        {
            return;
        }

        if (_camera is not null && !_sequenceOwnsCamera)
        {
            _camera.GlobalPosition = _player.GlobalPosition + new Vector3(13, 15, 17);
            _camera.LookAt(_player.GlobalPosition + (Vector3.Up * 0.8f), Vector3.Up);
        }

        if (Input.IsActionJustPressed("interact"))
        {
            InteractWithNearest();
        }

        if (Input.IsActionJustPressed("attack"))
        {
            AttackNearestHostile();
        }

        if (Input.IsActionJustPressed("quick_save"))
        {
            SaveRuntime(_quickSavePath!);
        }

        if (Input.IsActionJustPressed("quick_load"))
        {
            LoadRuntimeSave(_quickSavePath!);
            UpdateHud();
        }

        if (Input.IsActionJustPressed("rest"))
        {
            RequestPlayerRest();
        }

        if (Input.IsActionJustPressed("skip_sequence") && _activeSequence is not null && _sequenceState is not null)
        {
            if (_activeSequence.Skippable)
            {
                CompleteSequence(SequencePlayer.Skip(_activeSequence, _sequenceState), "skipped");
            }
            else
            {
                AbortActiveSequence();
            }
        }

        if (_isCryptPrototype && !_escaped && _bossDefeated && IsNear(_exit, 2.5f))
        {
            _escaped = true;
            _message = "La Crypte des Cendres est libérée. Module terminé !";
            UpdateHud();
            GD.Print("OPENRPG_FORGE_PROTOTYPE_COMPLETED");
        }
    }

    private ProjectDefinition LoadProject(string? snapshotPath, string? modulePackagePath)
    {
        if (snapshotPath is not null && modulePackagePath is not null)
        {
            throw new InvalidOperationException(
                "A runtime launch cannot combine a project snapshot and a module package.");
        }

        if (modulePackagePath is not null)
        {
            if (!Path.IsPathFullyQualified(modulePackagePath) ||
                !string.Equals(Path.GetExtension(modulePackagePath), ".orpmod", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The module package path must be an absolute .orpmod path.");
            }

            var cacheRoot = ProjectSettings.GlobalizePath("user://openrpg-forge/module-cache");
            var loaded = ModulePackageLoader.Load(modulePackagePath, cacheRoot);
            _moduleContentRoot = Path.Combine(loaded.CachePath, "content");
            _moduleId = loaded.Manifest.ModuleId.ToString();
            _moduleContentSha256 = loaded.Manifest.ContentSha256;
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_MODULE_LOADED slug={loaded.Manifest.Slug} " +
                $"cache_hit={loaded.CacheHit.ToString().ToLowerInvariant()}");
            return loaded.Project;
        }

        if (snapshotPath is not null)
        {
            if (!Path.IsPathFullyQualified(snapshotPath) ||
                !string.Equals(Path.GetExtension(snapshotPath), ".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The playtest snapshot path must be an absolute JSON file path.");
            }

            var snapshotProject = ProjectJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            SetUnpackagedModuleIdentity(snapshotProject);
            return snapshotProject;
        }

        var projectFilePath = ProjectSettings.GlobalizePath(ProjectPath);
        var workspaceRoot = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new InvalidOperationException($"Project workspace could not be resolved from '{ProjectPath}'.");
        }

        var opened = ProjectSession.Open(workspaceRoot);
        if (!opened.Succeeded)
        {
            throw new InvalidOperationException(
                "The bundled Forge workspace could not be loaded: " +
                string.Join("; ", opened.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code} ({diagnostic.Path}): {diagnostic.Message}")));
        }

        var project = opened.Session!.Project;
        _moduleContentRoot = opened.Session.RootDirectory;
        SetUnpackagedModuleIdentity(project);
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_WORKSPACE_LOADED format={ProjectSession.CurrentProjectFormatVersion} " +
            $"slug={project.Module.Slug}");
        return project;
    }

    private void ValidateAssetPackageManifests()
    {
        if (string.IsNullOrWhiteSpace(_moduleContentRoot))
        {
            return;
        }

        var loaded = AssetPackageManifestStore.Load(_moduleContentRoot);
        if (!loaded.Succeeded)
        {
            throw new InvalidOperationException(
                $"Asset package manifests could not be loaded: {loaded.Diagnostics[0].Code} " +
                $"({loaded.Diagnostics[0].Path}).");
        }

        var platform = OS.GetName() switch
        {
            "Windows" => AssetTargetPlatform.Windows,
            "Linux" => AssetTargetPlatform.Linux,
            "macOS" => AssetTargetPlatform.MacOS,
            "Android" => AssetTargetPlatform.Android,
            "iOS" => AssetTargetPlatform.IOS,
            _ => AssetTargetPlatform.Any,
        };
        var resolvedChunks = 0;
        foreach (var manifest in loaded.Manifests)
        {
            var available = manifest.Chunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Path = Path.Combine(
                        _moduleContentRoot,
                        chunk.RelativePath.Replace('/', Path.DirectorySeparatorChar)),
                })
                .Where(item => File.Exists(item.Path))
                .ToDictionary(
                    item => item.Chunk.RelativePath,
                    item => HashFileSha256(item.Path),
                    StringComparer.Ordinal);
            var resolution = AssetPackageManifestResolver.Resolve(manifest, platform, available);
            if (!resolution.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Asset package manifest '{manifest.Id}' is invalid at runtime: " +
                    $"{resolution.Diagnostics[0].Code} ({resolution.Diagnostics[0].Path}).");
            }

            resolvedChunks += resolution.Chunks.Count;
        }

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_ASSET_MANIFESTS_PASS manifests={loaded.Manifests.Count} " +
            $"chunks={resolvedChunks} platform={platform}");
    }

    private static string HashFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private EntityInstance BuildProject(ProjectDefinition project, EntityId? requestedSpawnId)
    {
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        _elyraDialogue = _isCryptPrototype
            ? project.Dialogues.SingleOrDefault(dialogue => dialogue.Slug == "elyra-crypt-of-ashes")
            : null;
        var quest = _isCryptPrototype
            ? project.Quests.SingleOrDefault(candidate => candidate.Slug == "crypt-of-ashes")
            : null;
        _questDefinition = quest;
        _questMachine = quest is null ? null : new QuestStateMachine(quest);
        var spawnCandidates = project.Areas
            .SelectMany(area => area.Entities.Select(entity => (Area: area, Entity: entity)))
            .Where(candidate =>
                blueprints[candidate.Entity.BlueprintId].Kind == BlueprintKind.PlayerSpawn)
            .ToArray();
        var selectedSpawn = requestedSpawnId is null
            ? spawnCandidates.FirstOrDefault()
            : spawnCandidates.SingleOrDefault(candidate => candidate.Entity.Id == requestedSpawnId);

        if (selectedSpawn.Entity is null)
        {
            throw new InvalidOperationException(
                requestedSpawnId is null
                    ? "The project does not contain a player spawn."
                    : $"The requested player spawn '{requestedSpawnId}' does not exist.");
        }

        foreach (var entity in selectedSpawn.Area.Entities)
        {
            var blueprint = blueprints[entity.BlueprintId];
            if (blueprint.Kind == BlueprintKind.PlayerSpawn && entity.Id != selectedSpawn.Entity.Id)
            {
                continue;
            }

            var previousChildCount = GetChildCount();
            BuildEntity(entity, blueprint);
            if (GetChildCount() > previousChildCount && GetChild(previousChildCount) is Node3D node)
            {
                _entityNodes[entity.Id.Value] = node;
            }
            _builtEntityCount++;
        }

        if (_player is null)
        {
            throw new InvalidOperationException("The selected area did not reconstruct a player spawn.");
        }

        _currentAreaId = selectedSpawn.Area.Id.ToString();

        return selectedSpawn.Entity;
    }

    private void SetUnpackagedModuleIdentity(ProjectDefinition project)
    {
        _moduleId = project.Module.Id.ToString();
        _moduleContentSha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))))
            .ToLowerInvariant();
    }

    private void ShowStartupFailure(Exception exception)
    {
        var layer = new CanvasLayer { Layer = 100 };
        AddChild(layer);

        var background = new ColorRect { Color = new Color("0b0915") };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 48);
        }

        layer.AddChild(margin);
        var content = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        content.AddThemeConstantOverride("separation", 18);
        margin.AddChild(content);

        var title = new Label { Text = "OpenRPG Forge n'a pas pu démarrer le module" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        content.AddChild(title);

        var explanation = new Label
        {
            Text = "La scène 3D n'a pas été construite. Le diagnostic ci-dessous permet de corriger le projet sans afficher une fenêtre vide.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        explanation.AddThemeFontSizeOverride("font_size", 17);
        explanation.AddThemeColorOverride("font_color", new Color("d8cce8"));
        content.AddChild(explanation);

        var diagnostic = new Label
        {
            Text = exception.Message,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        diagnostic.AddThemeColorOverride("font_color", new Color("ff806d"));
        content.AddChild(diagnostic);

        var close = new Button
        {
            Text = "Fermer",
            CustomMinimumSize = new Vector2(160, 44),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        close.Pressed += () => GetTree().Quit();
        content.AddChild(close);
    }

    private static string? GetArgumentValue(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], name, StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                throw new InvalidOperationException($"The runtime argument '{name}' requires a value.");
            }

            return arguments[index + 1];
        }

        return null;
    }

    private static EntityId? ParseOptionalEntityId(string? value) =>
        value is null ? null : EntityId.Parse(value);

    private void HandleLifecycleEvent(RuntimeLifecycleEvent @event)
    {
        _lifecycleEvents.Add(@event);
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_LIFECYCLE sequence={@event.Sequence} event={@event.Kind} " +
            $"binding={@event.Binding} authority={@event.Authority} " +
            $"previous={@event.PreviousAreaId?.ToString() ?? "none"} " +
            $"current={@event.CurrentAreaId?.ToString() ?? "none"} " +
            $"actor={@event.ActorEntityId?.ToString() ?? "none"} " +
            $"transition={@event.TransitionId?.ToString() ?? "none"} cause={@event.Cause} " +
            $"schema={@event.SchemaVersion} api={@event.ForgeScriptApiVersion} " +
            $"provenance={@event.Provenance}");
    }

    private static void EnsureLifecycle(RuntimeLifecycleDispatchResult result, string stage)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Runtime lifecycle stage '{stage}' failed: " +
            string.Join("; ", result.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}:{diagnostic.Message} [{diagnostic.Path}]")));
    }

    private void AssertLifecycleStartupContract()
    {
        var expectedOrder = new[]
        {
            RuntimeLifecycleEventKind.ProjectLoaded,
            RuntimeLifecycleEventKind.AreaEntered,
            RuntimeLifecycleEventKind.WorldStarted,
        };
        if (!_lifecycleEvents.Select(@event => @event.Kind).SequenceEqual(expectedOrder))
        {
            throw new InvalidOperationException("Runtime lifecycle startup order is not deterministic.");
        }

        GD.Print(
            "OPENRPG_FORGE_RUNTIME_LIFECYCLE_PASS " +
            "events=3 order=ProjectLoaded>AreaEntered>WorldStarted authority=SinglePlayer");
    }

    private void BuildEntity(EntityInstance entity, BlueprintDefinition blueprint)
    {
        switch (blueprint.Kind)
        {
            case BlueprintKind.Placeable:
                BuildPlaceable(entity, blueprint);
                break;
            case BlueprintKind.Door:
                _door = CreateBox(entity, blueprint.DisplayName, new Color("8f5738"));
                _doorEntityId = entity.Id;
                break;
            case BlueprintKind.Creature:
                BuildCreature(entity, blueprint);
                break;
            case BlueprintKind.PlayerSpawn:
                _player = CreatePlayer(entity, blueprint);
                break;
            case BlueprintKind.Item:
                _keyItem = CreateItem(entity, blueprint.DisplayName);
                break;
            case BlueprintKind.Trigger:
                _exit = CreateExit(entity);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(blueprint), blueprint.Kind, "Unsupported blueprint kind.");
        }
    }

    private void BuildPlaceable(EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (entity.Tags.Contains("pillar", StringComparer.Ordinal))
        {
            CreatePillar(entity, blueprint.DisplayName);
            return;
        }

        var isFloor = entity.Tags.Contains("floor", StringComparer.Ordinal);
        CreateBox(
            entity,
            blueprint.DisplayName,
            isFloor ? new Color("282638") : new Color("443b51"));
    }

    private void BuildCreature(EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (entity.Tags.Contains("npc", StringComparer.Ordinal))
        {
            _npc = CreateCreature(entity, blueprint.DisplayName, new Color("4ec9b0"));
            return;
        }

        var isBoss = entity.Tags.Contains("boss", StringComparer.Ordinal);
        var creature = CreateCreature(
            entity,
            blueprint.DisplayName,
            isBoss ? new Color("d34f4f") : new Color("d68a3a"));
        var profile = blueprint.Combat
            ?? throw new InvalidOperationException($"Hostile blueprint '{blueprint.Slug}' has no combat profile.");
        _hostiles.Add(creature, new CombatantState(entity.Id.ToString(), profile));
        _hostileNames.Add(creature, blueprint.DisplayName);

        if (isBoss)
        {
            _boss = creature;
            _bossPhaseTwoScript = CompileEntityScript(entity, "phaseTwoScript");
            _bossDeathScript = CompileEntityScript(entity, "deathScript");
        }
    }

    private PlayerController CreatePlayer(EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (blueprint.Combat is not null)
        {
            _playerCombatant = new CombatantState(entity.Id.ToString(), blueprint.Combat);
        }

        var player = new PlayerController { Name = "Player" };
        AddChild(player);
        player.GlobalPosition = EngineValueConverter.ToEngine(entity.Transform.Position);

        var mesh = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.45f, Height = 1.8f },
            MaterialOverride = CreateMaterial(new Color("74b9ff")),
        };
        player.AddChild(mesh);

        player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.45f, Height = 1.8f },
        });
        CreateLabel(player, blueprint.Combat is null ? "AVATAR" : "HÉROS", new Color("dff3ff"), 1.5f);
        return player;
    }

    private Node3D CreateBox(EntityInstance entity, string displayName, Color color)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);

        var body = new StaticBody3D();
        root.AddChild(body);
        body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One },
            MaterialOverride = CreateMaterial(color),
        });
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = Vector3.One },
        });
        _collisionCount++;
        return root;
    }

    private Node3D CreatePillar(EntityInstance entity, string displayName)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);

        var body = new StaticBody3D();
        root.AddChild(body);
        body.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1 },
            MaterialOverride = CreateMaterial(new Color("665565")),
        });
        body.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = 0.5f, Height = 1 },
        });
        _collisionCount++;
        return root;
    }

    private Node3D CreateCreature(EntityInstance entity, string displayName, Color color)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.5f, Height = 1.7f },
            MaterialOverride = CreateMaterial(color),
        });
        CreateLabel(root, displayName, Colors.White, 1.35f);
        return root;
    }

    private Node3D CreateItem(EntityInstance entity, string displayName)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f },
            MaterialOverride = CreateMaterial(new Color("ffca55"), emission: true),
        });
        CreateLabel(root, displayName, new Color("ffe6a0"), 0.9f);
        return root;
    }

    private Node3D CreateExit(EntityInstance entity)
    {
        var root = new Node3D
        {
            Name = "CryptExit",
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 0.1f },
            MaterialOverride = CreateMaterial(new Color("55efc4"), emission: true),
        });
        return root;
    }

    private void ConfigureWorld(AreaEnvironmentProfile environmentProfile, AreaWorldProfile worldProfile,
        bool includeCryptAtmosphere)
    {
        ArgumentNullException.ThrowIfNull(environmentProfile);
        ArgumentNullException.ThrowIfNull(worldProfile);
        var dynamicLights = includeCryptAtmosphere ? 8 : 2;
        var shadowLights = 2;
        _renderPlan = AreaRenderPlanBuilder.Build(
            environmentProfile,
            OS.HasFeature("mobile") ? "mobile" : "desktop",
            dynamicLights,
            shadowLights);
        var engineEnvironment = GodotEnvironmentAdapter.Build(_renderPlan);
        RenderingServer.SetDefaultClearColor(new Color(environmentProfile.Sky.GroundColor));
        AddChild(new WorldEnvironment
        {
            Name = "ForgeWorldEnvironment",
            Environment = engineEnvironment.Environment,
        });
        GetViewport().PositionalShadowAtlasSize = engineEnvironment.ShadowAtlasSize;

        var sun = new DirectionalLight3D
        {
            Name = "ForgeReferenceSun",
            RotationDegrees = new Vector3(-58, -32, 0),
            LightColor = engineEnvironment.DirectionalLightColor,
            LightEnergy = engineEnvironment.DirectionalLightEnergy,
            ShadowEnabled = environmentProfile.Lighting.Shadows,
        };
        AddChild(sun);

        AddChild(new OmniLight3D
        {
            Position = new Vector3(0, 5, -7),
            OmniRange = 18,
            LightColor = new Color("ff7043"),
            LightEnergy = 4,
            ShadowEnabled = true,
        });

        foreach (var diagnostic in engineEnvironment.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_RENDER_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_RENDER_PROFILE_PASS pbr={environmentProfile.Rendering.MaterialWorkflow.ToString().ToLowerInvariant()} " +
            $"tone={environmentProfile.Rendering.ToneMapping.ToString().ToLowerInvariant()} hdr={environmentProfile.Rendering.HdrOutput.ToString().ToLowerInvariant()} " +
            $"perpixel={environmentProfile.Rendering.PerPixelLighting.ToString().ToLowerInvariant()} sky={environmentProfile.Sky.Mode.ToString().ToLowerInvariant()} " +
            $"lights={dynamicLights}/{_renderPlan.LightBudget.MaximumDynamicLights} shadows={shadowLights}/{_renderPlan.LightBudget.MaximumShadowCastingLights}");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_RENDER_BACKEND method={RenderingServer.GetCurrentRenderingMethod()} " +
            $"driver={RenderingServer.GetCurrentRenderingDriverName()}");
        _environmentProfile = environmentProfile;
        _weatherState = WeatherRuntimeStateMachine.CreateInitial(environmentProfile.Weather);
        ApplyClimateEffects(force: true);
        ConfigureAreaWorld(worldProfile);

        if (includeCryptAtmosphere)
        {
            CreateFinalCryptAtmosphere();
        }

        _camera = new Camera3D
        {
            Position = new Vector3(13, 15, 17),
            Current = true,
            Fov = 55,
        };
        AddChild(_camera);
        _camera.LookAt(Vector3.Zero, Vector3.Up);
    }

    private void ConfigureAreaWorld(AreaWorldProfile profile)
    {
        var started = Stopwatch.GetTimestamp();
        var terrain = GodotTerrainAdapter.Build(profile);
        _areaWorldRoot = terrain.Root;
        AddChild(_areaWorldRoot);
        foreach (var diagnostic in terrain.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_TERRAIN_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        AreaStreamingResult? streaming = null;
        if (profile.Loading.RequiredResources.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(_moduleContentRoot))
            {
                throw new InvalidOperationException("Area streaming resources require a module content root.");
            }

            var coordinator = new AreaStreamingCoordinator(new RuntimeAreaChunkLoader(_moduleContentRoot));
            streaming = coordinator.LoadAsync(profile.Loading).GetAwaiter().GetResult();
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11I_TERRAIN_PASS provider={profile.Terrain.Provider.ToString().ToLowerInvariant()} " +
            $"vertices={terrain.Vertices} triangles={terrain.Triangles} collisions={terrain.CollisionShapes} " +
            $"features={terrain.FeatureSegments} volumes={terrain.DebugVolumes} routes={terrain.FlightRouteSegments} " +
            $"streamed={streaming?.Resources.Count ?? 0} cache_hits={streaming?.CacheHits ?? 0} " +
            $"elapsed_ms={elapsed.TotalMilliseconds:0.###} target_ms={profile.Loading.TargetLoadMilliseconds}");
    }

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
        }

        _climateProbeElapsed += delta;
        if (_climateProbeElapsed >= .5)
        {
            _climateProbeElapsed = 0;
            ApplyClimateEffects(force: false);
        }
    }

    private void ConfigureRuntimePolicies(ModuleRuntimePolicyProfile module, AreaRuntimePolicyProfile area)
    {
        _moduleRuntimePolicies = module ?? throw new ArgumentNullException(nameof(module));
        _areaRuntimePolicies = area ?? throw new ArgumentNullException(nameof(area));
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11J_POLICY_PASS global={module.Heartbeats.Count} area={area.Heartbeats.Count} " +
            $"rest={module.Rest.Enabled.ToString().ToLowerInvariant()} volumes={area.Rest.Volumes.Count} " +
            $"abort={module.SequenceAbort.Enabled.ToString().ToLowerInvariant()} restore={module.SequenceAbort.Restore}");
    }

    private void ConfigureCinematics(ProjectDefinition project, AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(area);
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
            _camera.GlobalPosition = EngineValueConverter.ToEngine(anchor.Transform.Position);
            _camera.Fov = anchor.FieldOfViewDegrees;
            _camera.LookAt(EngineValueConverter.ToEngine(anchor.LookAt), Vector3.Up);
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
        CompleteSequence(new SequenceEvaluation(aborted, null, new Dictionary<Guid, TransformValue>(), [], [], []),
            "aborted");
        GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_ABORTED handler={result.EventHandler} camera_restored={result.CameraRestored.ToString().ToLowerInvariant()}");
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
        if (_restSession is { Active: true } session && DateTimeOffset.UtcNow >= session.CompletesAt)
        {
            var completed = _restRuntime.Complete(session, ++_runtimePolicyRevision, DateTimeOffset.UtcNow);
            PublishRestEvents(completed.Events);
            _restSession = completed.Session;
            _message = completed.Accepted ? "Repos terminé." : "Le repos a été interrompu.";
            UpdateHud();
        }
    }

    private static void DispatchHeartbeats(HeartbeatAdvanceResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_POLICY_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        foreach (var dispatch in result.Dispatches)
        {
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_HEARTBEAT handler={dispatch.Handler} scope={dispatch.Scope} " +
                $"budget_ms={dispatch.BudgetMilliseconds} due_ms={dispatch.DueAtMilliseconds}");
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
        var result = _restRuntime.Request("player", "player-party", AreaId.Parse(_currentAreaId),
            restPosition, _moduleRuntimePolicies.Rest, _areaRuntimePolicies.Rest,
            secure: _hostiles.Keys.All(hostile => hostile.GlobalPosition.DistanceTo(position) >
                effectivePolicy.RequiredSecureRadius),
            EvaluateRuntimePolicyCondition, ++_runtimePolicyRevision, DateTimeOffset.UtcNow);
        PublishRestEvents(result.Events);
        _restSession = result.Session;
        _message = result.Accepted
            ? $"Repos en cours ({_moduleRuntimePolicies.Rest.DefaultDurationSeconds} s)…"
            : result.Events.LastOrDefault()?.Cause ?? result.DiagnosticCode ?? "Repos refusé.";
        UpdateHud();
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
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_REST_EVENT stage={@event.Stage} handler={binding?.Handler ?? "unbound"} " +
                $"revision={@event.ServerRevision}");
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
            $"particles={result.PrecipitationEmitters} water={result.WaterSurfaces} reverb={result.ReverbPreset} " +
            $"authority={_environmentProfile.Weather.ServerAuthoritative.ToString().ToLowerInvariant()} " +
            $"save={_environmentProfile.Weather.PersistState.ToString().ToLowerInvariant()} " +
            $"replicate={_environmentProfile.Weather.ReplicateState.ToString().ToLowerInvariant()}");
    }

    private void CreateFinalCryptAtmosphere()
    {
        var torchPositions = new[]
        {
            new Vector3(-7.8f, 0, 8),
            new Vector3(7.8f, 0, 8),
            new Vector3(-7.8f, 0, 1),
            new Vector3(7.8f, 0, 1),
            new Vector3(-7.8f, 0, -7),
            new Vector3(7.8f, 0, -7),
        };
        foreach (var position in torchPositions)
        {
            var torch = new Node3D { Name = "EmberBrazier", Position = position };
            AddChild(torch);
            torch.AddChild(new MeshInstance3D
            {
                Position = new Vector3(0, 0.35f, 0),
                Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.3f, Height = 0.7f },
                MaterialOverride = CreateMaterial(new Color("4a3448")),
            });
            torch.AddChild(new MeshInstance3D
            {
                Position = new Vector3(0, 0.86f, 0),
                Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f },
                MaterialOverride = CreateMaterial(new Color("ff7043"), emission: true),
            });
            torch.AddChild(new OmniLight3D
            {
                Position = new Vector3(0, 1.05f, 0),
                OmniRange = 7,
                LightColor = new Color("ff8a50"),
                LightEnergy = 1.7f,
                ShadowEnabled = false,
            });
        }

        var rune = new MeshInstance3D
        {
            Name = "AshRune",
            Position = new Vector3(0, 0.03f, -8.5f),
            Mesh = new CylinderMesh { TopRadius = 2.2f, BottomRadius = 2.2f, Height = 0.05f },
            MaterialOverride = CreateMaterial(new Color("8e3828"), emission: true),
        };
        AddChild(rune);

        CreateDecorativeBeam(new Vector3(0, 3.15f, 4.5f), new Vector3(17, 0.35f, 0.45f));
        CreateDecorativeBeam(new Vector3(0, 3.15f, -4.5f), new Vector3(17, 0.35f, 0.45f));
    }

    private void CreateDecorativeBeam(Vector3 position, Vector3 size)
    {
        AddChild(new MeshInstance3D
        {
            Name = "CryptVaultBeam",
            Position = position,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = CreateMaterial(new Color("372f45")),
        });
    }

    private void CreateHud()
    {
        _hud = new RuntimeHud { ModuleTitle = _moduleTitle, CombatEnabled = _playerCombatant is not null };
        AddChild(_hud);
    }

    private void InteractWithNearest()
    {
        var candidates = new[] { _npc, _keyItem, _door }
            .Where(node => node is not null && GodotObject.IsInstanceValid(node))
            .Cast<Node3D>()
            .OrderBy(node => _player!.GlobalPosition.DistanceTo(node.GlobalPosition))
            .ToArray();
        var nearest = candidates.FirstOrDefault();

        if (nearest is null || !IsNear(nearest, InteractionDistance))
        {
            _message = "Rien à portée d’interaction.";
            UpdateHud();
            return;
        }

        InteractWith(nearest);
    }

    private void InteractWith(Node3D target)
    {
        if (target == _npc)
        {
            InteractWithElyra();
            UpdateHud();
            return;
        }

        if (target == _keyItem && !_hasKey)
        {
            _hasKey = true;
            _inventory.Add("ash-key");
            _message = "Vous obtenez la Clé de braise.";
            _keyItem.QueueFree();
            UpdateHud();
            return;
        }

        if (target == _door && !_doorUnlocked)
        {
            var transition = GetAvailableQuestTransitions()
                .FirstOrDefault(candidate => candidate.Actions.Any(action =>
                    action.Kind == QuestActionKind.UnlockEntity &&
                    string.Equals(action.Key, _doorEntityId?.ToString(), StringComparison.Ordinal)));
            if (transition is not null)
            {
                AdvanceQuest(transition);
            }
            else
            {
                _message = !_hasKey
                    ? "La porte est scellée. Une clé ardente semble nécessaire."
                    : "La quête n’autorise pas encore le passage.";
            }

            UpdateHud();
            return;
        }

        if (target == _npc)
        {
            if (_questState == PrototypeQuestState.NotStarted)
            {
                _questState = PrototypeQuestState.Active;
                _message = "Elyra : Retrouvez la clé de braise et terrassez le Gardien.";
            }
            else
            {
                _message = _bossDefeated
                    ? "Elyra : La cendre retombe enfin."
                    : "Elyra : La clé brille dans l’aile est de la crypte.";
            }
        }
        else if (target == _keyItem && !_hasKey)
        {
            _hasKey = true;
            _message = "Vous obtenez la Clé de braise.";
            _keyItem.QueueFree();
        }
        else if (target == _door && !_doorUnlocked)
        {
            if (!_hasKey)
            {
                _message = "La porte est scellée. Une clé ardente semble nécessaire.";
            }
            else
            {
                _doorUnlocked = true;
                _message = "La Porte des Cendres se déverrouille.";
                _door.QueueFree();
            }
        }

        UpdateHud();
    }

    private void AttackNearestHostile()
    {
        var nearest = _hostiles.Keys
            .Where(GodotObject.IsInstanceValid)
            .OrderBy(node => _player!.GlobalPosition.DistanceTo(node.GlobalPosition))
            .FirstOrDefault();

        if (nearest is null || !IsNear(nearest, AttackDistance))
        {
            _message = "Aucun ennemi à portée.";
            UpdateHud();
            return;
        }

        DamageHostile(nearest);
    }

    private void DamageHostile(Node3D hostile)
    {
        if (!_hostiles.TryGetValue(hostile, out var target) || _playerCombatant is null)
        {
            return;
        }

        var ability = _playerCombatant.Profile.Abilities.FirstOrDefault()
            ?? throw new InvalidOperationException("The player combat profile has no attack ability.");
        var result = CombatResolver.Cast(
            _playerCombatant,
            target,
            ability.Id,
            Time.GetTicksMsec() / 1000d);
        var health = target.Health;
        var hostileName = _hostileNames[hostile];
        _message = $"{hostileName} subit {result.Damage} dégât(s) ({health} PV).";

        if (hostile == _boss && health <= Math.Max(1, target.Profile.Stats.MaximumHealth / 2) && !_bossPhaseTwo)
        {
            _bossPhaseTwo = true;
            ExecuteForgeScript(_bossPhaseTwoScript, hostile);
            GD.Print("OPENRPG_FORGE_BOSS_PHASE_TWO");
        }

        if (result.TargetDied)
        {
            _hostiles.Remove(hostile);
            _hostileNames.Remove(hostile);
            foreach (var drop in result.Loot)
            {
                _inventory.Add(drop.ItemSlug);
                GD.Print($"OPENRPG_FORGE_LOOT_GRANTED item={drop.ItemSlug} quantity={drop.Quantity}");
            }

            hostile.QueueFree();

            if (hostile == _boss)
            {
                _bossDefeated = true;
                ExecuteForgeScript(_bossDeathScript, hostile);
            }
            else
            {
                _message = $"{hostileName} est vaincue.";
            }
        }

        UpdateHud();
    }

    private void SpawnAshSpirit(Vector3 position)
    {
        _spawnedAshSpiritCount++;
        var spirit = new Node3D { Name = "AshSpirit", Position = position };
        AddChild(spirit);
        spirit.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f },
            MaterialOverride = CreateMaterial(new Color("ff7675"), emission: true),
        });
    }

    private ForgeScriptArtifact CompileEntityScript(EntityInstance entity, string propertyKey)
    {
        var relativePath = entity.Overrides.Properties
            .SingleOrDefault(property => string.Equals(property.Key, propertyKey, StringComparison.Ordinal))
            ?.Value;
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidOperationException(
                $"Entity '{entity.Id}' requires a safe relative ForgeScript path in '{propertyKey}'.");
        }

        var resourcePath = $"res://content/crypt-of-ashes/{relativePath.Replace('\\', '/')}";
        var source = _moduleContentRoot is null
            ? Godot.FileAccess.GetFileAsString(resourcePath)
            : ReadCachedModuleScript(relativePath);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException($"ForgeScript source '{resourcePath}' could not be loaded.");
        }

        GD.Print($"OPENRPG_FORGE_SCRIPT_COMPILE_START path={relativePath}");
        var cachedCompilation = _forgeScriptCache?.Compile(source)
            ?? throw new InvalidOperationException("ForgeScript cache is not initialized.");
        var compilation = cachedCompilation.Compilation;
        if (!compilation.Success || compilation.Artifact is null)
        {
            throw new InvalidOperationException(
                $"ForgeScript '{resourcePath}' failed to compile: " +
                string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        GD.Print(
            $"OPENRPG_FORGE_FORGESCRIPT_CACHE status={(cachedCompilation.CacheHit ? "hit" : "miss")} " +
            $"key={cachedCompilation.CacheKey} script={relativePath} " +
            $"schema={cachedCompilation.Metadata!.SchemaVersion}");
        GD.Print($"OPENRPG_FORGE_SCRIPT_COMPILE_PASS path={relativePath}");
        return compilation.Artifact;
    }

    private string ReadCachedModuleScript(string relativePath)
    {
        var root = Path.GetFullPath(_moduleContentRoot!);
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path))
        {
            throw new InvalidOperationException($"Cached ForgeScript '{relativePath}' is unavailable.");
        }

        return File.ReadAllText(path);
    }

    private void ExecuteForgeScript(ForgeScriptArtifact? artifact, Node3D origin)
    {
        if (artifact is null)
        {
            _message = "Le script de gameplay requis est absent.";
            GD.PrintErr("OPENRPG_FORGE_SCRIPT_FAILED missing_artifact");
            return;
        }

        var result = _forgeScriptRuntime.Execute(
            artifact,
            new ForgeScriptQuotas(
                instructionLimit: 2_000,
                timeoutMilliseconds: 50,
                recursionLimit: 16,
                spawnCap: 8));
        if (result.Status != ForgeScriptExecutionStatus.Completed)
        {
            _message = "Un script de gameplay a été interrompu ; la partie continue.";
            GD.PrintErr(
                "OPENRPG_FORGE_SCRIPT_FAILED " +
                string.Join(";", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return;
        }

        var spawnIndex = 0;
        foreach (var scriptEvent in result.Events)
        {
            switch (scriptEvent.Kind)
            {
                case "variable":
                    var variable = scriptEvent.Value.Split('=', 2);
                    if (variable.Length == 2)
                    {
                        SetProjectVariable(variable[0], variable[1]);
                    }

                    break;
                case "say":
                    _message = scriptEvent.Value;
                    break;
                case "cast":
                    if (scriptEvent.Value.EndsWith(":ash-nova", StringComparison.Ordinal))
                    {
                        _ashNovaCueCount++;
                    }

                    GD.Print($"OPENRPG_FORGE_ABILITY_CUE value={scriptEvent.Value}");
                    break;
                case "spawn":
                    var blueprint = scriptEvent.Value.Split(':', 2)[0];
                    if (string.Equals(blueprint, "ash-spirit", StringComparison.Ordinal))
                    {
                        var side = spawnIndex++ % 2 == 0 ? -2 : 2;
                        SpawnAshSpirit(origin.GlobalPosition + new Vector3(side, 0, 0));
                    }

                    break;
                case "quest":
                    var questTransition = GetAvailableQuestTransitions().FirstOrDefault();
                    if (questTransition is not null)
                    {
                        AdvanceQuest(questTransition);
                    }

                    break;
                case "unlock":
                    UnlockEntity(scriptEvent.Value);
                    break;
            }
        }

        GD.Print($"OPENRPG_FORGE_SCRIPT_EXECUTED events={result.Events.Count}");
    }

    private void UpdateHud()
    {
        if (_hud is null)
        {
            return;
        }

        if (!_isCryptPrototype)
        {
            _hud.UpdateState(
                "explorer le module",
                _message,
                $"Module : {_moduleSlug} | Entités : {_builtEntityCount} | Combat : {(_playerCombatant is null ? "désactivé" : "actif")}");
            return;
        }

        var objective = _questState switch
        {
            PrototypeQuestState.NotStarted => "parler à Elyra [E]",
            PrototypeQuestState.Active when !_hasKey => "trouver la Clé de braise",
            PrototypeQuestState.Active when !_doorUnlocked => "ouvrir la Porte des Cendres",
            PrototypeQuestState.Active => "vaincre le Gardien des Cendres [ESPACE]",
            PrototypeQuestState.Completed => "rejoindre la sortie lumineuse",
            _ => "explorer",
        };
        if (_questMachine is not null)
        {
            objective = _questMachine.CurrentState.Journal.Fallback;
        }

        var bossCombatant = _boss is not null && _hostiles.TryGetValue(_boss, out var combatant)
            ? combatant
            : null;
        var bossHealth = bossCombatant?.Health ?? 0;
        var bossMaximumHealth = bossCombatant?.Profile.Stats.MaximumHealth ?? 0;
        var state = $"Quête : {_questState} | Clé : {(_hasKey ? "oui" : "non")} | Boss : {bossHealth}/{bossMaximumHealth}";
        _hud.UpdateState(objective, _message, state);
    }

    private void InteractWithElyra()
    {
        if (_elyraDialogue is null)
        {
            _message = "Elyra reste silencieuse : aucun dialogue n’est lié à ce module.";
            return;
        }

        var traversal = new DialogueTraversal(_elyraDialogue);
        var start = traversal.Start();
        var evaluator = new RuntimeConditionEvaluator(this);
        var choice = traversal.GetAvailableChoices(evaluator).FirstOrDefault();
        if (choice is null)
        {
            _message = $"Elyra : {start.Node!.Text.Fallback}";
            return;
        }

        var step = traversal.Choose(choice.Id, evaluator);
        ApplyDialogueActions(step.Actions);
        _message = $"Elyra : {step.Node?.Text.Fallback ?? start.Node!.Text.Fallback}";
    }

    private void ApplyDialogueActions(IReadOnlyList<DialogueAction> actions)
    {
        foreach (var action in actions)
        {
            switch (action.Kind)
            {
                case DialogueActionKind.SetVariable:
                    SetProjectVariable(action.Key, action.Value);
                    break;
                case DialogueActionKind.StartQuest:
                case DialogueActionKind.AdvanceQuest:
                case DialogueActionKind.CompleteQuest:
                    var transition = GetAvailableQuestTransitions().FirstOrDefault();
                    if (transition is not null)
                    {
                        AdvanceQuest(transition);
                    }

                    break;
                case DialogueActionKind.GiveItem:
                    _inventory.Add(action.Key);
                    break;
                case DialogueActionKind.UnlockEntity:
                    UnlockEntity(action.Key);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unsupported dialogue action.");
            }
        }
    }

    private IReadOnlyList<QuestTransition> GetAvailableQuestTransitions() =>
        _questMachine?.GetAvailableTransitions(new RuntimeConditionEvaluator(this)) ?? [];

    private void AdvanceQuest(QuestTransition transition)
    {
        if (_questMachine is null)
        {
            return;
        }

        var result = _questMachine.Advance(transition.Id, new RuntimeConditionEvaluator(this));
        _questState = result.Completed ? PrototypeQuestState.Completed : PrototypeQuestState.Active;
        foreach (var action in result.Actions)
        {
            switch (action.Kind)
            {
                case QuestActionKind.SetVariable:
                    SetProjectVariable(action.Key, action.Value);
                    break;
                case QuestActionKind.GiveItem:
                    _inventory.Add(action.Key);
                    break;
                case QuestActionKind.UnlockEntity:
                    UnlockEntity(action.Key);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unsupported quest action.");
            }
        }

        _message = result.State.Journal.Fallback;
    }

    private void UnlockEntity(string entityId)
    {
        if (_doorEntityId is null || !string.Equals(entityId, _doorEntityId.ToString(), StringComparison.Ordinal))
        {
            return;
        }

        _doorUnlocked = true;
        if (_door is not null && GodotObject.IsInstanceValid(_door))
        {
            _door.QueueFree();
        }

        _message = "La Porte des Cendres se déverrouille.";
    }

    private void SetProjectVariable(string key, string value)
    {
        if (_blackboard.TrySet(key, value, out var error))
        {
            return;
        }

        _message = $"La variable de projet '{key}' a refusé une valeur incompatible.";
        GD.PrintErr($"OPENRPG_FORGE_RUNTIME_VARIABLE_REJECTED key={key} error={error}");
    }

    private bool SaveRuntime(string path)
    {
        if (_player is null || _playerCombatant is null || _moduleId is null ||
            _moduleContentSha256 is null || _currentAreaId is null)
        {
            GD.PrintErr("OPENRPG_FORGE_RUNTIME_SAVE_FAILED runtime_not_ready");
            return false;
        }

        var position = _player.GlobalPosition;
        var save = new RuntimeSaveDocument(
            RuntimeSaveDocument.CurrentFormatVersion,
            _moduleId,
            _moduleContentSha256,
            _currentAreaId,
            DateTimeOffset.UtcNow,
            new RuntimeVector3(position.X, position.Y, position.Z),
            new RuntimeCombatantSave(
                _playerCombatant.Id,
                _playerCombatant.Health,
                _playerCombatant.Resource),
            _inventory.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            _blackboard.Snapshot().OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new RuntimeKeyValue(item.Key, item.Value))
                .ToArray(),
            _questDefinition?.Id.ToString(),
            _questMachine?.CurrentState.Id.ToString(),
            _hostiles.Values
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new RuntimeCombatantSave(item.Id, item.Health, item.Resource))
                .ToArray(),
            new RuntimeProgressFlags(
                _hasKey,
                _doorUnlocked,
                _bossPhaseTwo,
                _bossDefeated,
                _escaped),
            _environmentProfile?.Weather.PersistState == true && _weatherState is not null
                ? new RuntimeWeatherSave(
                    _weatherState.SchemaVersion,
                    _weatherState.StateId.ToString(),
                    _weatherState.Revision,
                    _weatherState.ServerRevision,
                    _weatherState.ElapsedSeconds)
                : null);
        var result = RuntimeSaveStore.Save(path, save);
        if (!result.Succeeded)
        {
            _message = "Échec de la sauvegarde de partie.";
            GD.PrintErr(
                "OPENRPG_FORGE_RUNTIME_SAVE_FAILED " +
                string.Join(";", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
            return false;
        }

        _message = $"Partie sauvegardée — {Path.GetFileName(path)}";
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_WRITTEN format={RuntimeSaveDocument.CurrentFormatVersion} " +
            $"file={Path.GetFileName(path)} quest={_questMachine?.CurrentState.Id}");
        UpdateHud();
        return true;
    }

    private bool LoadRuntimeSave(string path)
    {
        if (_moduleId is null || _moduleContentSha256 is null)
        {
            GD.PrintErr("OPENRPG_FORGE_RUNTIME_LOAD_FAILED runtime_not_ready");
            return false;
        }

        var result = RuntimeSaveStore.Load(path, _moduleId, _moduleContentSha256);
        if (!result.Succeeded || result.Save is null)
        {
            _message = "Cette sauvegarde est absente, invalide ou incompatible avec le module.";
            GD.PrintErr(
                "OPENRPG_FORGE_RUNTIME_LOAD_FAILED " +
                string.Join(";", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
            return false;
        }

        try
        {
            ApplyRuntimeSave(result.Save);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            _message = "La sauvegarde référence un état de jeu indisponible.";
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_LOAD_FAILED save.state_invalid:{exception.Message}");
            return false;
        }

        _loadedSave = true;
        _message = $"Partie reprise — {Path.GetFileName(path)}";
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_LOADED format={result.Save.FormatVersion} " +
            $"file={Path.GetFileName(path)} quest={result.Save.QuestStateId}");
        return true;
    }

    private void ApplyRuntimeSave(RuntimeSaveDocument save)
    {
        if (_player is null || _playerCombatant is null ||
            !string.Equals(save.AreaId, _currentAreaId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The save area does not match the loaded runtime area.");
        }

        _player.GlobalPosition = new Vector3(
            (float)save.PlayerPosition.X,
            (float)save.PlayerPosition.Y,
            (float)save.PlayerPosition.Z);
        _playerCombatant.Restore(save.Player.Health, save.Player.Resource);
        _inventory.Clear();
        foreach (var item in save.Inventory)
        {
            _inventory.Add(item);
        }

        _blackboard = new ProjectBlackboard(_blackboard.Definitions, allowUndeclared: true);
        var variableDiagnostics = _blackboard.Load(save.Variables.ToDictionary(
            variable => variable.Key,
            variable => variable.Value,
            StringComparer.Ordinal));
        if (variableDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", variableDiagnostics));
        }

        if (_questDefinition is not null && _questMachine is not null && save.QuestStateId is not null)
        {
            if (!string.Equals(save.QuestId, _questDefinition.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The saved quest does not match the loaded module quest.");
            }

            _questMachine.Restore(QuestStateId.Parse(save.QuestStateId));
            _questState = _questMachine.Completed
                ? PrototypeQuestState.Completed
                : _questMachine.CurrentState.Id == _questDefinition.InitialStateId
                    ? PrototypeQuestState.NotStarted
                    : PrototypeQuestState.Active;
        }

        if (save.Weather is not null)
        {
            if (_environmentProfile is null ||
                !_environmentProfile.Weather.States.Any(state =>
                    string.Equals(state.Id.ToString(), save.Weather.StateId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("The saved weather state is unavailable in the current area.");
            }

            _weatherState = new WeatherRuntimeSnapshot(
                save.Weather.SchemaVersion,
                Guid.Parse(save.Weather.StateId),
                save.Weather.Revision,
                save.Weather.ServerRevision,
                save.Weather.ElapsedSeconds);
            ApplyClimateEffects(force: true);
        }

        var hostileSaves = save.Hostiles.ToDictionary(item => item.EntityId, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _hostiles.ToArray())
        {
            if (hostileSaves.TryGetValue(pair.Value.Id, out var hostileSave))
            {
                pair.Value.Restore(hostileSave.Health, hostileSave.Resource);
                continue;
            }

            _hostiles.Remove(pair.Key);
            _hostileNames.Remove(pair.Key);
            pair.Key.QueueFree();
        }

        if (hostileSaves.Keys.Any(id => _hostiles.Values.All(item =>
                !string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException("A saved hostile is no longer available in the current scene.");
        }

        _hasKey = save.Progress.HasKey;
        _doorUnlocked = save.Progress.DoorUnlocked;
        _bossPhaseTwo = save.Progress.BossPhaseTwo;
        _bossDefeated = save.Progress.BossDefeated;
        _escaped = save.Progress.Escaped;
        if (_hasKey && _keyItem is not null && GodotObject.IsInstanceValid(_keyItem))
        {
            _keyItem.QueueFree();
        }

        if (_doorUnlocked && _door is not null && GodotObject.IsInstanceValid(_door))
        {
            _door.QueueFree();
        }
    }

    private bool IsNear(Node3D? target, float distance) =>
        _player is not null &&
        target is not null &&
        GodotObject.IsInstanceValid(target) &&
        _player.GlobalPosition.DistanceTo(target.GlobalPosition) <= distance;

    private void RunSaveCreateSmokeTest()
    {
        AssertSmoke(_quickSavePath is not null, "The save smoke test requires --save-file.");
        AssertSmoke(_npc is not null && _keyItem is not null && _door is not null && _boss is not null,
            "The save smoke scenario is incomplete.");
        InteractWith(_npc!);
        InteractWith(_keyItem!);
        InteractWith(_door!);
        DamageHostile(_boss!);
        _player!.GlobalPosition = new Vector3(2, 0.9f, 1);
        AssertSmoke(SaveRuntime(_quickSavePath!), "The runtime checkpoint was not written.");
        AssertSmoke(_questMachine?.CurrentState.DisplayName == "Gardien des Cendres",
            "The saved quest did not reach the boss objective.");
        AssertSmoke(_hostiles[_boss!].Health == 3, "The saved boss health is not the expected checkpoint.");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_PASS file={Path.GetFileName(_quickSavePath)} " +
            $"quest={_questMachine!.CurrentState.Id} boss_health={_hostiles[_boss!].Health}");
        GetTree().Quit(0);
    }

    private void RunSaveResumeSmokeTest()
    {
        AssertSmoke(_loadedSave, "The save resume smoke test did not load a checkpoint.");
        AssertSmoke(_hasKey && _doorUnlocked, "Inventory or unlocked door state was not restored.");
        AssertSmoke(_questMachine?.CurrentState.DisplayName == "Gardien des Cendres",
            "Quest state was not restored.");
        AssertSmoke(_boss is not null && _hostiles.TryGetValue(_boss, out var boss) && boss.Health == 3,
            "Boss combat state was not restored.");
        AssertSmoke(_player?.GlobalPosition.IsEqualApprox(new Vector3(2, 0.9f, 1)) == true,
            "Player position was not restored.");
        while (_hostiles.TryGetValue(_boss!, out var combatant) && combatant.Health > 0)
        {
            DamageHostile(_boss!);
        }

        AssertSmoke(_bossDefeated && _questMachine?.Completed == true,
            "The restored game could not be completed.");
        AssertSmoke(SaveRuntime(_quickSavePath!), "The completed resumed game was not saved.");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_RESUME_PASS file={Path.GetFileName(_quickSavePath)} " +
            "quest=completed boss=defeated");
        GetTree().Quit(0);
    }

    private void RunHeadlessSmokeTest()
    {
        var scriptRuntime = new NativeForgeScriptRuntime();
        var scriptCompilation = scriptRuntime.Compile(
            "void main() { variable(\"smoke\", \"pass\"); say(\"ForgeScript prêt\"); }");
        AssertSmoke(scriptCompilation.Success, "ForgeScript native compilation failed.");
        var scriptResult = scriptRuntime.Execute(scriptCompilation.Artifact!);
        AssertSmoke(scriptResult.Status == ForgeScriptExecutionStatus.Completed, "ForgeScript execution failed.");
        AssertSmoke(scriptResult.Events.Count == 2, "ForgeScript API events were not emitted.");
        GD.Print(
            $"OPENRPG_FORGE_FORGESCRIPT_SMOKE_PASS vm={scriptRuntime.VirtualMachineVersion} " +
            $"api={scriptRuntime.ApiVersion} events={scriptResult.Events.Count}");

        AssertSmoke(_builtEntityCount == 16, "Expected 16 reconstructed entities.");
        AssertSmoke(_collisionCount == 10, "Expected 10 static collision shapes.");
        AssertSmoke(_player is not null, "Player was not reconstructed.");
        AssertSmoke(_npc is not null, "NPC was not reconstructed.");
        AssertSmoke(_keyItem is not null, "Key item was not reconstructed.");
        AssertSmoke(_door is not null, "Door was not reconstructed.");
        AssertSmoke(_boss is not null, "Boss was not reconstructed.");

        InteractWith(_npc!);
        AssertSmoke(_questState == PrototypeQuestState.Active, "NPC interaction did not start the quest.");
        AssertSmoke(
            _questMachine?.CurrentState.DisplayName == "Clé de braise",
            "NPC interaction did not start the data-driven quest.");
        InteractWith(_keyItem!);
        AssertSmoke(_hasKey, "Key interaction did not update inventory state.");
        InteractWith(_door!);
        AssertSmoke(_doorUnlocked, "Door interaction did not unlock the door.");

        while (_hostiles.TryGetValue(_boss!, out var combatant) && combatant.Health > 0)
        {
            DamageHostile(_boss!);
        }

        AssertSmoke(_bossPhaseTwo, "Boss phase two did not trigger.");
        AssertSmoke(_spawnedAshSpiritCount == 2, "ForgeScript did not spawn two ash spirits.");
        AssertSmoke(_ashNovaCueCount == 1, "ForgeScript did not emit the ash nova cue.");
        AssertSmoke(_bossDefeated, "Boss was not defeated.");
        AssertSmoke(_inventory.Contains("warden-ember"), "Data-driven boss loot was not granted.");
        AssertSmoke(_questState == PrototypeQuestState.Completed, "Quest was not completed.");
        AssertSmoke(_questMachine?.Completed == true, "Data-driven quest was not completed.");

        GD.Print("OPENRPG_FORGE_RUNTIME_SMOKE_PASS quest=completed door=unlocked boss=defeated");
        GetTree().Quit(0);
    }

    private void RunGenericHeadlessSmokeTest()
    {
        AssertSmoke(!_isCryptPrototype, "The generic smoke test cannot use the Crypt prototype module.");
        AssertSmoke(!string.IsNullOrWhiteSpace(_moduleSlug), "The generic module slug was not loaded.");
        AssertSmoke(_builtEntityCount >= 2, "Expected at least the generated floor and player spawn.");
        AssertSmoke(_collisionCount >= 1, "Expected at least one generated floor collision.");
        AssertSmoke(_player is not null, "The generic player spawn was not reconstructed.");
        AssertSmoke(_playerCombatant is null, "A Core starter module must not require an RPG combat profile.");
        AssertSmoke(_npc is null && _keyItem is null && _door is null && _boss is null && _exit is null,
            "The generic starter unexpectedly reconstructed Crypt-specific gameplay actors.");
        AssertSmoke(FindChild("AshRune", recursive: true, owned: false) is null,
            "The generic starter unexpectedly loaded Crypt-specific atmosphere.");
        InteractWithNearest();
        AssertSmoke(string.Equals(_message, "Rien à portée d’interaction.", StringComparison.Ordinal),
            "The generic interaction path did not complete cleanly without a target.");

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_GENERIC_SMOKE_PASS slug={_moduleSlug} " +
            $"entities={_builtEntityCount} collisions={_collisionCount} combat=false interaction=empty");
        GetTree().Quit(0);
    }

    private void RunRpgKitHeadlessSmokeTest(ProjectDefinition project)
    {
        AssertSmoke(string.Equals(_moduleSlug, "la-veillee-des-eclats", StringComparison.Ordinal), "Unexpected RPG Kit smoke module.");
        AssertSmoke(_builtEntityCount == 8, "Expected eight authored entities.");
        AssertSmoke(_collisionCount >= 3, "Expected authored floor, placeable and door collisions.");
        AssertSmoke(_player is not null, "The authored player spawn was not reconstructed.");
        AssertSmoke(_hostiles.Count == 2, "Expected both authored combat profiles to be reconstructed.");
        AssertSmoke(project.Dialogues.Count == 1 && project.Dialogues.Single().Nodes.Count == 2, "Expected the authored dialogue graph.");
        AssertSmoke(project.Quests.Count == 1 && project.Quests.Single().States.Count == 3, "Expected the authored quest state machine.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G8_MODULE_PASS slug={_moduleSlug} entities={_builtEntityCount} collisions={_collisionCount} combat={_hostiles.Count} dialogues=1 quests=1");
        GetTree().Quit(0);
    }

    private static void AssertSmoke(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private StandardMaterial3D CreateMaterial(Color color, bool emission = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.72f,
            Metallic = 0.08f,
        };
        if (_renderPlan is not null)
        {
            GodotEnvironmentAdapter.ApplyPbrDefaults(material, _renderPlan);
        }

        if (emission)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 2.4f;
        }

        return material;
    }

    private static void CreateLabel(Node3D parent, string text, Color color, float height)
    {
        parent.AddChild(new Label3D
        {
            Text = text,
            Position = new Vector3(0, height, 0),
            Modulate = color,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 32,
            OutlineSize = 8,
        });
    }

    private static string SafeNodeName(string value) =>
        new(value.Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());

    private static int CountTreeNodes(Node root)
    {
        var count = 1;
        foreach (Node child in root.GetChildren())
        {
            count += CountTreeNodes(child);
        }

        return count;
    }

    private static void RegisterInputActions()
    {
        RegisterKeyAction("move_forward", Key.W, Key.Up);
        RegisterKeyAction("move_back", Key.S, Key.Down);
        RegisterKeyAction("move_left", Key.A, Key.Q, Key.Left);
        RegisterKeyAction("move_right", Key.D, Key.Right);
        RegisterKeyAction("interact", Key.E);
        RegisterKeyAction("attack", Key.Space);
        RegisterKeyAction("quick_save", Key.F5);
        RegisterKeyAction("quick_load", Key.F9);
        RegisterKeyAction("rest", Key.R);
        RegisterKeyAction("skip_sequence", Key.Escape);
    }

    private static void RegisterKeyAction(StringName action, params Key[] keys)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }

        foreach (var key in keys)
        {
            var inputEvent = new InputEventKey { PhysicalKeycode = key };
            if (!InputMap.ActionHasEvent(action, inputEvent))
            {
                InputMap.ActionAddEvent(action, inputEvent);
            }
        }
    }

    private sealed class RuntimeAreaChunkLoader : IAreaChunkLoader
    {
        private readonly string _root;
        private readonly string _rootPrefix;

        internal RuntimeAreaChunkLoader(string root)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
            _root = Path.GetFullPath(root);
            _rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public async Task<AreaChunkPayload> LoadAsync(string resourcePath, CancellationToken cancellationToken)
        {
            var normalized = WeatherStateProfile.NormalizeOptionalResource(resourcePath);
            if (normalized.Length == 0)
            {
                throw new InvalidDataException("Area streaming resource path cannot be empty.");
            }

            var fullPath = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Required area resource '{normalized}' is unavailable inside the module.", fullPath);
            }

            var content = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            var dependencyFile = fullPath + ".deps";
            IReadOnlyList<string> dependencies = File.Exists(dependencyFile)
                ? (await File.ReadAllLinesAsync(dependencyFile, cancellationToken).ConfigureAwait(false))
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 && !line.StartsWith('#'))
                    .Select(WeatherStateProfile.NormalizeOptionalResource)
                    .ToArray()
                : [];
            return new AreaChunkPayload(normalized, content, dependencies);
        }
    }

    private sealed class RuntimeConditionEvaluator : IDialogueConditionEvaluator, IQuestConditionEvaluator
    {
        private readonly GameRoot _runtime;

        internal RuntimeConditionEvaluator(GameRoot runtime) => _runtime = runtime;

        public bool IsSatisfied(DialogueCondition condition) => condition.Kind switch
        {
            DialogueConditionKind.VariableEquals =>
                _runtime._blackboard.TryGetValue(condition.Key, out var value) && value == condition.ExpectedValue,
            DialogueConditionKind.HasItem => _runtime._inventory.Contains(condition.Key),
            DialogueConditionKind.QuestStateEquals =>
                string.Equals(
                    _runtime._questMachine?.CurrentState.Id.ToString(),
                    condition.ExpectedValue,
                    StringComparison.Ordinal) ||
                string.Equals(
                    _runtime._questMachine?.CurrentState.DisplayName,
                    condition.ExpectedValue,
                    StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

        public bool IsSatisfied(QuestCondition condition) => condition.Kind switch
        {
            QuestConditionKind.VariableEquals =>
                _runtime._blackboard.TryGetValue(condition.Key, out var value) && value == condition.ExpectedValue,
            QuestConditionKind.HasItem => _runtime._inventory.Contains(condition.Key),
            _ => false,
        };
    }

    private enum PrototypeQuestState
    {
        NotStarted,
        Active,
        Completed,
    }
}
