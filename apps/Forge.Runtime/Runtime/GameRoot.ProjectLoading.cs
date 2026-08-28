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
    private ProjectDefinition LoadProject(string? snapshotPath, string? modulePackagePath, string? workspacePath)
    {
        var sourceCount = (snapshotPath is null ? 0 : 1) + (modulePackagePath is null ? 0 : 1) +
            (workspacePath is null ? 0 : 1);
        if (sourceCount > 1)
        {
            throw new InvalidOperationException(
                "A runtime launch cannot combine a workspace, project snapshot and module package.");
        }

        if (workspacePath is not null)
        {
            if (!Path.IsPathFullyQualified(workspacePath) || !Directory.Exists(workspacePath))
            {
                throw new InvalidOperationException("The runtime workspace path must be an existing absolute directory.");
            }

            var openedWorkspace = ProjectSession.Open(workspacePath);
            if (!openedWorkspace.Succeeded || openedWorkspace.Session is null)
            {
                throw new InvalidOperationException(
                    "The requested Forge workspace could not be loaded: " +
                    string.Join("; ", openedWorkspace.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code} ({diagnostic.Path}): {diagnostic.Message}")));
            }

            var workspaceProject = openedWorkspace.Session.Project;
            _moduleContentRoot = openedWorkspace.Session.RootDirectory;
            SetUnpackagedModuleIdentity(workspaceProject);
            GD.Print($"OPENRPG_FORGE_RUNTIME_WORKSPACE_LOADED format={ProjectSession.CurrentProjectFormatVersion} " +
                     $"slug={workspaceProject.Module.Slug} external=true");
            return workspaceProject;
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
            _moduleContentRoot = Path.GetDirectoryName(snapshotPath);
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

        _modelLibrary = new GodotAssetModelLibrary(_moduleContentRoot, loaded.Manifests);

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_ASSET_MANIFESTS_PASS manifests={loaded.Manifests.Count} " +
            $"chunks={resolvedChunks} platform={platform}");
    }

    private static string HashFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static AreaDefinition ResolveStartArea(ProjectDefinition project, EntityId? requestedSpawnId)
    {
        if (requestedSpawnId is null)
            return project.Areas.Single(area => area.Id == project.Module.EntryAreaId);
        return project.Areas.SingleOrDefault(area => area.Entities.Any(entity => entity.Id == requestedSpawnId))
            ?? throw new InvalidOperationException($"Requested spawn '{requestedSpawnId}' has no owning area.");
    }

    private static EntityId ResolvePlayerSpawnId(ProjectDefinition project, string areaId)
    {
        var parsedAreaId = AreaId.Parse(areaId);
        var area = project.Areas.SingleOrDefault(candidate => candidate.Id == parsedAreaId)
            ?? throw new InvalidOperationException($"Saved area '{areaId}' does not exist in the loaded module.");
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var spawns = area.Entities
            .Where(entity => blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
                             blueprint.Kind == BlueprintKind.PlayerSpawn)
            .ToArray();
        return spawns.Length == 1
            ? spawns[0].Id
            : throw new InvalidOperationException(
                $"Saved area '{areaId}' must expose exactly one player spawn; found {spawns.Length}.");
    }

    private void ApplyPlaytestLaunch(ProjectDefinition project, PlaytestLaunchManifest? manifest)
    {
        _playtestQuestVariables.Clear();
        if (manifest is null) return;
        var restoredQuestStates = new List<RuntimeQuestStateSnapshot>();
        foreach (var injection in manifest.QuestStates)
        {
            var quest = project.Quests.SingleOrDefault(item =>
                string.Equals(item.Id.ToString(), injection.QuestId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Playtest quest '{injection.QuestId}' does not exist.");
            var stateId = QuestStateId.Parse(injection.StateId);
            if (quest.States.All(item => item.Id != stateId))
                throw new InvalidOperationException(
                    $"Playtest quest state '{injection.StateId}' does not exist in '{injection.QuestId}'.");
            var acceptedVariable = injection.Variables.SingleOrDefault(variable =>
                string.Equals(variable.Key, "quest.accepted", StringComparison.Ordinal));
            var accepted = string.IsNullOrEmpty(acceptedVariable.Key) ||
                !bool.TryParse(acceptedVariable.Value, out var parsedAcceptance) || parsedAcceptance;
            restoredQuestStates.Add(new RuntimeQuestStateSnapshot(quest.Id, stateId, accepted));
            foreach (var variable in injection.Variables)
                _playtestQuestVariables[$"{quest.Id}:{variable.Key}"] = variable.Value;
        }
        var targetKind = Enum.Parse<PlaytestTargetKind>(manifest.TargetKind, ignoreCase: true);
        QuestId? trackedQuestId = null;
        if (targetKind == PlaytestTargetKind.SelectedQuest)
        {
            var targetId = QuestId.Parse(manifest.TargetId!);
            var quest = project.Quests.Single(item => item.Id == targetId);
            var injection = manifest.QuestStates.Single(item =>
                string.Equals(item.QuestId, manifest.TargetId, StringComparison.OrdinalIgnoreCase));
            var stateId = QuestStateId.Parse(injection.StateId);
            if (!quest.States.Single(state => state.Id == stateId).IsTerminal &&
                restoredQuestStates.Single(state => state.QuestId == targetId).IsAccepted)
                trackedQuestId = targetId;
        }
        else if (targetKind == PlaytestTargetKind.Dialogue)
        {
            var targetId = DialogueId.Parse(manifest.TargetId!);
            _activeDialogue = project.Dialogues.Single(item => item.Id == targetId);
            _activeDialogueEntryNodeId = _activeDialogue.EntryNodeId;
        }
        _questTracker?.Restore(restoredQuestStates, trackedQuestId);
        SyncTrackedQuestDefinition();
        GD.Print($"OPENRPG_FORGE_RUNTIME_PLAY_FROM_HERE_PASS mode={manifest.StartMode.ToLowerInvariant()} " +
                 $"profile={manifest.TestProfile} area={manifest.AreaId} quests={manifest.QuestStates.Count} " +
                 $"variables={_playtestQuestVariables.Count} target={targetKind.ToString().ToLowerInvariant()} " +
                 $"headless={manifest.Headless.ToString().ToLowerInvariant()}");
    }

    private EntityInstance BuildProject(ProjectDefinition project, EntityId? requestedSpawnId)
    {
        _loadedProject = project;
        _livingWorldTriggerOccupancy.Clear();
        _livingWorldTriggerDiagnostics.Clear();
        _livingWorldTriggerFoundationStates.Clear();
        _livingWorldNarrativeState = TriggerNarrativeRuntimeState.Empty;
        _livingWorldPlayerEntity = null;
        _narrativeParticipants.Clear();
        _npc = null;
        _guideName = "Guide";
        _activeDialogueParticipant = null;
        _activeDialogue = null;
        _activeDialogueEntryNodeId = null;
        BuildDialogueAuthoringAdapters(project.Dialogues);
        BuildDialogueExperienceAdapters(project.Dialogues);
        BuildDialogueProductionAdapters(project.Dialogues);
        BuildQuestAuthoringAdapters(project.Quests);
        BuildQuestExperienceAdapters(project.Quests);
        BuildQuestQualityAdapters(project.Quests);
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        _questTracker = new RuntimeQuestStateTracker(project.Quests);
        _trackedQuestDefinition = null;
        foreach (var quest in project.Quests.Where(quest => quest.Operations is not null))
        {
            var questOperations = quest.Operations!;
            var notification = QuestOperationsRuntimeResolver.ResolveNotification(quest);
            GD.Print($"OPENRPG_FORGE_RUNTIME_QUEST_OPERATIONS_LOADED profile={questOperations.Id:D} " +
                     $"priority={questOperations.Journal.Priority} repeat={questOperations.Repeat.Enabled.ToString().ToLowerInvariant()} " +
                     $"share={questOperations.Share.Mode.ToString().ToLowerInvariant()} " +
                     $"exclusive={questOperations.ExclusiveChoices.Count} notification={notification.GroupKey}");
        }
        var spawnCandidates = project.Areas
            .SelectMany(area => area.Entities.Select(entity => (Area: area, Entity: entity)))
            .Where(candidate =>
                blueprints[candidate.Entity.BlueprintId].Kind == BlueprintKind.PlayerSpawn)
            .ToArray();
        var selectedSpawn = requestedSpawnId is null
            ? spawnCandidates.SingleOrDefault(candidate => candidate.Area.Id == project.Module.EntryAreaId)
            : spawnCandidates.SingleOrDefault(candidate => candidate.Entity.Id == requestedSpawnId);

        if (selectedSpawn.Entity is null)
        {
            throw new InvalidOperationException(
                requestedSpawnId is null
                    ? "The project does not contain a player spawn."
                    : $"The requested player spawn '{requestedSpawnId}' does not exist.");
        }

        _activeArea = selectedSpawn.Area;
        _inventory.SetAuthoredBaseline(RuntimeAuthoredInventoryResolver.Resolve(
            project,
            selectedSpawn.Area.Id,
            selectedSpawn.Entity.Id));
        BuildAreaCollisionShell(selectedSpawn.Area);
        foreach (var entity in selectedSpawn.Area.Entities)
        {
            var resolvedTile = ModularTilePlacementResolver.Resolve(selectedSpawn.Area, entity);
            var effectiveEntity = resolvedTile.Entity;
            var blueprint = blueprints[effectiveEntity.BlueprintId];
            if (blueprint.Kind == BlueprintKind.PlayerSpawn && entity.Id != selectedSpawn.Entity.Id)
            {
                continue;
            }

            var previousChildCount = GetChildCount();
            BuildEntity(effectiveEntity, blueprint);
            if (GetChildCount() > previousChildCount && GetChild(previousChildCount) is Node3D node)
            {
                ApplyTileMirror(node, resolvedTile.Mirror);
                _entityNodes[entity.Id.Value] = node;
                var clickTrigger = EntityClickTriggerSettings.Read(effectiveEntity.Overrides);
                if (clickTrigger.Enabled)
                {
                    _clickInteractions[node] = clickTrigger;
                }
            }
            _builtEntityCount++;
        }

        if (_player is null)
        {
            throw new InvalidOperationException("The selected area did not reconstruct a player spawn.");
        }

        _currentAreaId = selectedSpawn.Area.Id.ToString();

        GD.Print($"OPENRPG_FORGE_RUNTIME_MODELS_PASS manifests={_modelLibrary?.Count ?? 0} " +
                 $"instances={_runtimeModelInstances}");

        return selectedSpawn.Entity;
    }

    private void BuildAreaCollisionShell(AreaDefinition area)
    {
        _interiorWalkableBounds = null;
        _activeAreaHorizontalBounds = null;
        _interiorFloorTop = 0;
        if (area.Kind != AreaKind.Interior)
        {
            BuildExteriorBoundaryShell(area);
            return;
        }

        var floorTiles = area.Entities
            .Where(entity => entity.Tags.Contains("floor", StringComparer.Ordinal))
            .ToArray();
        var xCoordinates = floorTiles.Select(entity => entity.Transform.Position.X).Distinct().Order().ToArray();
        var zCoordinates = floorTiles.Select(entity => entity.Transform.Position.Z).Distinct().Order().ToArray();
        if (floorTiles.Length < 4 || xCoordinates.Length < 2 || zCoordinates.Length < 2) return;

        var stepX = MinimumPositiveStep(xCoordinates);
        var stepZ = MinimumPositiveStep(zCoordinates);
        var minimumX = xCoordinates[0] - (stepX * .5f);
        var maximumX = xCoordinates[^1] + (stepX * .5f);
        var minimumZ = zCoordinates[0] - (stepZ * .5f);
        var maximumZ = zCoordinates[^1] + (stepZ * .5f);
        _interiorFloorTop = (float)floorTiles.Average(entity => entity.Transform.Position.Y) + .05f;
        _interiorWalkableBounds = new Rect2(minimumX, minimumZ, maximumX - minimumX, maximumZ - minimumZ);
        _activeAreaHorizontalBounds = _interiorWalkableBounds;

        const float floorThickness = .2f;
        const float wallThickness = .6f;
        const float wallHeight = 4.2f;
        var centerX = (minimumX + maximumX) * .5f;
        var centerZ = (minimumZ + maximumZ) * .5f;
        AddRuntimeCollisionBox("InteriorFloorShell",
            new Vector3(centerX, _interiorFloorTop - (floorThickness * .5f), centerZ),
            new Vector3(maximumX - minimumX, floorThickness, maximumZ - minimumZ));
        AddRuntimeCollisionBox("InteriorWallNorth",
            new Vector3(centerX, _interiorFloorTop + (wallHeight * .5f), minimumZ),
            new Vector3(maximumX - minimumX, wallHeight, wallThickness));
        AddRuntimeCollisionBox("InteriorWallSouth",
            new Vector3(centerX, _interiorFloorTop + (wallHeight * .5f), maximumZ),
            new Vector3(maximumX - minimumX, wallHeight, wallThickness));
        AddRuntimeCollisionBox("InteriorWallWest",
            new Vector3(minimumX, _interiorFloorTop + (wallHeight * .5f), centerZ),
            new Vector3(wallThickness, wallHeight, maximumZ - minimumZ));
        AddRuntimeCollisionBox("InteriorWallEast",
            new Vector3(maximumX, _interiorFloorTop + (wallHeight * .5f), centerZ),
            new Vector3(wallThickness, wallHeight, maximumZ - minimumZ));
        GD.Print($"OPENRPG_FORGE_INTERIOR_COLLISION_SHELL_READY area={area.Slug} " +
                 $"bounds={minimumX:0.###},{minimumZ:0.###},{maximumX:0.###},{maximumZ:0.###} " +
                 $"floor_y={_interiorFloorTop:0.###} barriers=4");

        static float MinimumPositiveStep(IReadOnlyList<float> values)
        {
            var step = float.PositiveInfinity;
            for (var index = 1; index < values.Count; index++)
            {
                var candidate = values[index] - values[index - 1];
                if (candidate > .001f) step = MathF.Min(step, candidate);
            }
            return float.IsFinite(step) ? step : 1;
        }
    }

    private void BuildExteriorBoundaryShell(AreaDefinition area)
    {
        var halfWidth = area.Dimensions.Width * .5f;
        var halfDepth = area.Dimensions.Depth * .5f;
        const float inset = 1.25f;
        const float wallThickness = 2.5f;
        const float overheadClearance = 48f;
        const float undergroundClearance = 18f;
        var minimumTerrainHeight = area.World.Terrain.Heights.Count == 0
            ? -4f
            : area.World.Terrain.Heights.Min();
        var maximumTerrainHeight = area.World.Terrain.Heights.Count == 0
            ? 8f
            : area.World.Terrain.Heights.Max();
        var wallBottom = minimumTerrainHeight - undergroundClearance;
        var wallTop = maximumTerrainHeight + overheadClearance;
        var wallHeight = wallTop - wallBottom;
        var wallCenterY = wallBottom + (wallHeight * .5f);
        var minimumX = -halfWidth + inset;
        var maximumX = halfWidth - inset;
        var minimumZ = -halfDepth + inset;
        var maximumZ = halfDepth - inset;
        _activeAreaHorizontalBounds = new Rect2(minimumX, minimumZ,
            maximumX - minimumX, maximumZ - minimumZ);

        AddRuntimeCollisionBox("ExteriorBoundaryNorth",
            new Vector3(0, wallCenterY, minimumZ),
            new Vector3(area.Dimensions.Width, wallHeight, wallThickness));
        AddRuntimeCollisionBox("ExteriorBoundarySouth",
            new Vector3(0, wallCenterY, maximumZ),
            new Vector3(area.Dimensions.Width, wallHeight, wallThickness));
        AddRuntimeCollisionBox("ExteriorBoundaryWest",
            new Vector3(minimumX, wallCenterY, 0),
            new Vector3(wallThickness, wallHeight, area.Dimensions.Depth));
        AddRuntimeCollisionBox("ExteriorBoundaryEast",
            new Vector3(maximumX, wallCenterY, 0),
            new Vector3(wallThickness, wallHeight, area.Dimensions.Depth));
        AddRuntimeCollisionBox("ExteriorSafetyFloor",
            new Vector3(0, minimumTerrainHeight - 8f, 0),
            new Vector3(area.Dimensions.Width, 1f, area.Dimensions.Depth));
        GD.Print($"OPENRPG_FORGE_EXTERIOR_BOUNDARY_READY area={area.Slug} " +
                 $"bounds={minimumX:0.###},{minimumZ:0.###},{maximumX:0.###},{maximumZ:0.###} " +
                 $"terrain_y={minimumTerrainHeight:0.###}..{maximumTerrainHeight:0.###} barriers=4 safety_floor=1");
    }

    private void AddRuntimeCollisionBox(string name, Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D { Name = name, Position = position };
        body.AddChild(new CollisionShape3D
        {
            Name = $"{name}Shape",
            Shape = new BoxShape3D { Size = size },
        });
        AddChild(body);
        _collisionCount++;
    }

    private static void ApplyTileMirror(Node3D node, ModularTileMirrorMode mirror)
    {
        node.Scale = mirror switch
        {
            ModularTileMirrorMode.X => new Vector3(-node.Scale.X, node.Scale.Y, node.Scale.Z),
            ModularTileMirrorMode.Z => new Vector3(node.Scale.X, node.Scale.Y, -node.Scale.Z),
            _ => node.Scale,
        };
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


}
