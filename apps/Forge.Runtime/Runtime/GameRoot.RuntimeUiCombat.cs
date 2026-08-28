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
    private void CreateHud()
    {
        _combatUiPolicy = RuntimeUiMotionPolicy.Create(
            _runtimeExtensions?.ModernTooling.QualityAssurance.Accessibility,
            OS.GetCmdlineUserArgs());
        InitializeMapExploration();
        _runtimePresentation = BuildRuntimePresentation();
        _hud = new RuntimeHud
        {
            ModuleTitle = _moduleTitle,
            AreaTitle = _activeArea?.DisplayName ?? _moduleTitle,
            InteractionLabel = string.IsNullOrWhiteSpace(_guideName) ? "Interagir" : $"Parler à {_guideName}",
            CombatEnabled = _playerCombatant is not null,
            Accessibility = _runtimeExtensions?.ModernTooling.QualityAssurance.Accessibility,
            PartyHud = _worldFeatureCatalog?.Companions.FirstOrDefault()?.Experience?.Hud,
            Presentation = _runtimePresentation,
            MotionPolicy = _combatUiPolicy,
        };
        _hud.PauseRequested += ToggleTacticalPause;
        _hud.JournalRequested += () => ToggleRpgMenu(RuntimeRpgMenuPage.Journal);
        AddChild(_hud);
        _toastQueue = new RuntimeToastQueue { MotionPolicy = _combatUiPolicy };
        AddChild(_toastQueue);
        _actionBar = new RuntimeActionBar
        {
            CombatEnabled = _playerCombatant is not null,
            MotionPolicy = _combatUiPolicy,
        };
        if (_playerCombatant?.Profile.Abilities.FirstOrDefault() is { } primaryAbility)
            _actionBar.ConfigurePrimaryAbility(primaryAbility);
        _actionBar.PrimaryAbilityReady += HandlePrimaryAbilityReady;
        _actionBar.CommandRequested += HandleActionBarCommand;
        AddChild(_actionBar);
        if (_camera is not null)
        {
            _combatFeedbackLayer = new RuntimeCombatFeedbackLayer(_camera, _combatUiPolicy);
            AddChild(_combatFeedbackLayer);
            _combatImpactPool = new RuntimeCombatImpactPool(_combatUiPolicy);
            AddChild(_combatImpactPool);
            RegisterCombatFeedbackTargets();
        }
        _moduleMap = new RuntimeModuleMap { MotionPolicy = _combatUiPolicy };
        _moduleMap.Configure(_runtimePresentation.Map, _player!);
        _moduleMap.GlobalMapVisibilityChanged += _ => SyncPlayerInterfaceLock();
        _moduleMap.InventoryRequested += () => ShowRpgMenu(RuntimeRpgMenuPage.Inventory);
        _moduleMap.CharacterRequested += () => ShowRpgMenu(RuntimeRpgMenuPage.Character);
        _moduleMap.JournalRequested += () => ShowRpgMenu(RuntimeRpgMenuPage.Journal);
        AddChild(_moduleMap);
        _rpgMenu = new RuntimeRpgMenu
        {
            ModuleTitle = _moduleTitle,
            AreaTitle = _activeArea?.DisplayName ?? _moduleTitle,
            Presentation = _runtimePresentation,
        };
        _rpgMenu.MapRequested += OpenModuleMapFromMenu;
        _rpgMenu.Closed += SyncPlayerInterfaceLock;
        AddChild(_rpgMenu);
        _rpgMenu.UpdateContext(_runtimePresentation.Journal.ActiveObjective,
            _runtimePresentation.Journal.ActiveEntry?.Details);
        _dialogueOverlay = new RuntimeDialogueOverlay();
        _dialogueOverlay.ChoiceSelected += HandleDialogueOverlayChoiceSelected;
        _dialogueOverlay.Closed += HandleDialogueOverlayClosed;
        AddChild(_dialogueOverlay);
        _dialogueVoicePlayer = new RuntimeDialogueVoicePlayer();
        AddChild(_dialogueVoicePlayer);
        _actionWheel = new RuntimeActionWheel();
        if (_gameplayControls is not null) _actionWheel.Configure(_gameplayControls);
        _actionWheel.SelectionAccepted += command => _actionBar?.NotifyCommandAccepted(command);
        _actionWheel.CommandRequested += RequestGameplayCommand;
        _actionWheel.WheelVisibilityChanged += _ => SyncPlayerInterfaceLock();
        AddChild(_actionWheel);
        if (_runtimeExtensions is not null && _runtimeExtensions.UiDocuments.Count > 0)
        {
            _declarativeUi = new RuntimeDeclarativeUi();
            _declarativeUi.Configure(_runtimeExtensions, _runtimeJson, _scriptUiEvents, _runtimeJsonHandles);
            _declarativeUi.EventRaised += HandleScriptableUiEvent;
            AddChild(_declarativeUi);
        }
        UpdateTacticalHud();
    }

    private void InitializeMapExploration()
    {
        if (_activeArea is null || _player is null || !_activeArea.Authoring.Map.Enabled)
        {
            _mapExploration = null;
            _mapExplorationAreaId = null;
            _mapExplorationCell = null;
            return;
        }

        if (_mapExploration is null ||
            !string.Equals(_mapExplorationAreaId, _activeArea.Id.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            _mapExploration = new AreaMapExplorationState(
                _activeArea.Authoring.Map.ExplorationScope,
                _livingWorldPlayerEntity?.Id.ToString() ?? "player:local",
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<Guid>());
        }

        var normalized = AreaExperienceAuthoringScriptService.Normalize(_activeArea, _mapExploration);
        _mapExploration = normalized.State;
        ReportMapExplorationDiagnostics(normalized.Diagnostics);
        _mapExplorationAreaId = _activeArea.Id.ToString();
        if (!TryCurrentMapCell(_activeArea, _mapExploration.ActiveFloor, _player.GlobalPosition, out var cell))
        {
            _mapExplorationCell = null;
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_MAP_EXPLORATION_IGNORED " +
                        $"code={AreaMapExplorationGrid.CellOutOfScopeCode} floor={_mapExploration.ActiveFloor}");
            return;
        }
        _mapExploration = AreaExperienceAuthoringScriptService.Reveal(
            _activeArea,
            _mapExploration,
            _mapExploration.ActiveFloor,
            _player.GlobalPosition.X,
            _player.GlobalPosition.Z);
        _mapExplorationCell = cell;
    }

    private void AdvanceMapExploration()
    {
        if (_activeArea is null || _player is null || _mapExploration is null) return;
        if (!TryCurrentMapCell(_activeArea, _mapExploration.ActiveFloor, _player.GlobalPosition, out var cell))
            return;
        if (string.Equals(cell, _mapExplorationCell, StringComparison.Ordinal)) return;
        _mapExploration = AreaExperienceAuthoringScriptService.Reveal(
            _activeArea,
            _mapExploration,
            _mapExploration.ActiveFloor,
            _player.GlobalPosition.X,
            _player.GlobalPosition.Z);
        _mapExplorationCell = cell;
        RefreshRuntimePresentation();
    }

    internal void SetActiveMapFloor(int floor)
    {
        if (_activeArea is null || _mapExploration is null)
            throw new InvalidOperationException("Map exploration is not active.");
        if (floor < 0 || floor >= _activeArea.Authoring.Map.FloorCount)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (_mapExploration.ActiveFloor == floor) return;

        _mapExploration = _mapExploration with { ActiveFloor = floor };
        _mapExplorationCell = null;
        if (_player is not null && TryCurrentMapCell(_activeArea, floor, _player.GlobalPosition, out var cell))
        {
            _mapExploration = AreaExperienceAuthoringScriptService.Reveal(
                _activeArea,
                _mapExploration,
                floor,
                _player.GlobalPosition.X,
                _player.GlobalPosition.Z);
            _mapExplorationCell = cell;
        }
        RefreshRuntimePresentation();
    }

    private RuntimeModulePresentation BuildRuntimePresentation()
    {
        if (_loadedProject is null || _activeArea is null)
            throw new InvalidOperationException("The runtime presentation requires a loaded project and active area.");
        var context = new RuntimeModulePresentationContext(
            PlayerEntityId: _livingWorldPlayerEntity?.Id,
            PartyBlueprintIds: _worldFeatureCatalog?.Companions
                .Select(companion => companion.BlueprintId)
                .Distinct()
                .ToArray(),
            RuntimeInventory: _inventory.Snapshot(),
            RuntimePlayerResources: BuildLivePlayerResourceSnapshots(),
            RuntimeProgressionTracks: BuildLiveProgressionTrackSnapshots(),
            WeatherStateId: _weatherState?.StateId,
            QuestStates: _questTracker?.Snapshot(),
            TrackedQuestId: _questTracker?.TrackedQuestId,
            Exploration: _mapExploration);
        var presentation = RuntimeModulePresentationProjector.Project(_loadedProject, _activeArea.Id, context);
        return presentation with
        {
            Hero = ResolvePresentationPortrait(presentation.Hero),
            Party = presentation.Party.Select(ResolvePresentationPortrait).ToArray(),
        };
    }

    private IReadOnlyList<RuntimeActorResourceSnapshot> BuildLivePlayerResourceSnapshots()
    {
        if (_playerCombatant is null) return [];
        var authoredResources = ResolveRuntimePlayerBlueprint()?.CreatureFoundation?.Resources ?? [];
        var healthId = authoredResources.FirstOrDefault(resource =>
            string.Equals(resource.ResourceId, "health", StringComparison.OrdinalIgnoreCase))?.ResourceId ?? "health";
        var snapshots = new List<RuntimeActorResourceSnapshot>
        {
            new(healthId, _playerCombatant.Health, _playerCombatant.Profile.Stats.MaximumHealth),
        };
        if (_playerCombatant.Profile.Stats.MaximumResource > 0)
        {
            var resourceId = authoredResources.FirstOrDefault(resource =>
                !string.Equals(resource.ResourceId, healthId, StringComparison.OrdinalIgnoreCase))?.ResourceId ??
                "resource";
            snapshots.Add(new RuntimeActorResourceSnapshot(resourceId, _playerCombatant.Resource,
                _playerCombatant.Profile.Stats.MaximumResource));
        }

        return snapshots.OrderBy(snapshot => snapshot.Id, StringComparer.Ordinal).ToArray();
    }

    private IReadOnlyList<RuntimeProgressionTrackSnapshot> BuildLiveProgressionTrackSnapshots()
    {
        if (_moduleRules is null) return [];
        var authoredTrack = ResolveRuntimePlayerBlueprint()?.CreatureProgression?.Tracks.FirstOrDefault();
        var maximumLevel = Math.Max(
            _progressionLevel,
            authoredTrack?.MaximumLevel ?? _moduleRules.Progression.Curve.Max(point => point.Level));
        return
        [
            new RuntimeProgressionTrackSnapshot(
                authoredTrack?.TrackId ?? _moduleRules.Progression.RulesetId,
                authoredTrack?.DisplayName ?? "Progression",
                _progressionLevel,
                maximumLevel,
                _progressionExperience),
        ];
    }

    private BlueprintDefinition? ResolveRuntimePlayerBlueprint()
    {
        if (_loadedProject is null) return null;
        if (_livingWorldPlayerEntity is not null)
        {
            return _loadedProject.Blueprints.SingleOrDefault(blueprint =>
                blueprint.Id == _livingWorldPlayerEntity.BlueprintId);
        }

        return _loadedProject.Blueprints.FirstOrDefault(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn);
    }

    private RuntimeModulePresentation.Actor ResolvePresentationPortrait(RuntimeModulePresentation.Actor actor)
    {
        var resolved = RuntimeModuleAssetPathResolver.ResolveImage(_moduleContentRoot, actor.PortraitPath);
        return actor with { PortraitPath = resolved };
    }

    private void RefreshRuntimePresentation()
    {
        if (_loadedProject is null || _activeArea is null) return;
        _runtimePresentation = BuildRuntimePresentation();
        _moduleMap?.UpdatePresentation(_runtimePresentation.Map);
        _rpgMenu?.UpdatePresentation(_runtimePresentation);
    }

    private static bool TryCurrentMapCell(AreaDefinition area, int floor, Vector3 position, out string cell)
    {
        var map = area.Authoring.Map;
        var grid = new AreaMapExplorationGrid(area.Dimensions, map.FloorCount, map.ExplorationCellSize);
        if (grid.TryResolve(floor, position.X, position.Z, out var resolved))
        {
            cell = resolved.Key;
            return true;
        }

        cell = string.Empty;
        return false;
    }

    private static void ReportMapExplorationDiagnostics(IReadOnlyList<DomainDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_MAP_EXPLORATION_DIAGNOSTIC " +
                        $"code={diagnostic.Code} path={diagnostic.Path}");
    }

    private void ApplyModuleWindowTitle()
    {
        var title = $"OpenRPG Forge — {_moduleTitle}";
        GetWindow().Title = title;
        DisplayServer.WindowSetTitle(title);
    }

    private void RegisterCombatFeedbackTargets()
    {
        if (_combatFeedbackLayer is null) return;
        foreach (var pair in _hostiles)
        {
            var node = pair.Key;
            var combatant = pair.Value;
            var worldLabel = node.GetNodeOrNull<Label3D>(RuntimeWorldLabelNodeName);
            var anchorHeight = worldLabel?.Position.Y ?? (node == _boss ? 2.2f : 1.35f);
            worldLabel?.QueueFree();
            _combatFeedbackLayer.Register(
                node,
                combatant.Id,
                _hostileNames[node],
                combatant.Health,
                combatant.Profile.Stats.MaximumHealth,
                node == _boss,
                anchorHeight);
        }
    }

    private void HandleActionBarCommand(string command)
    {
        switch (command)
        {
            case "action_wheel":
                _actionWheel?.Toggle();
                break;
            case "quick_load":
                LoadRuntimeSave(_quickSavePath!);
                UpdateHud();
                break;
            case "module_map":
                ToggleModuleMap();
                break;
            case "inventory":
                ToggleRpgMenu(RuntimeRpgMenuPage.Inventory);
                break;
            case "character":
                ToggleRpgMenu(RuntimeRpgMenuPage.Character);
                break;
            case "journal":
                ToggleRpgMenu(RuntimeRpgMenuPage.Journal);
                break;
            case "tactical_pause":
                ToggleTacticalPause();
                break;
            default:
                RequestGameplayCommand(command);
                break;
        }
    }

    private void HandlePrimaryAbilityReady()
    {
        var ability = _playerCombatant?.Profile.Abilities.FirstOrDefault();
        if (ability is null || ability.CooldownSeconds <= 0) return;
        _toastQueue?.Show(RuntimeToastKind.Combat, $"{ability.DisplayName} est prête.",
            $"combat-ready:{ability.Id}");
        if (_player is not null)
            _combatImpactPool?.PlayAudio(CombatFeedbackAudioKind.AbilityReady, _player.GlobalPosition);
    }

    private void HandleScriptableUiEvent(ScriptUiEvent @event)
    {
        _message = $"Interface {@event.Document.Key} : {@event.Command}";
        GD.Print($"OPENRPG_FORGE_RUNTIME_UI_EVENT_PASS document={@event.Document.Key} widget={@event.WidgetKey} " +
            $"command={@event.Command} sequence={@event.Sequence}");
        UpdateHud();
    }

    private void InteractWithNearest()
    {
        var candidates = new Node3D?[] { _flyingMount, _keyItem, _door }
            .Where(node => node is not null && GodotObject.IsInstanceValid(node))
            .Cast<Node3D>()
            .Concat(_narrativeParticipants.Keys.Where(GodotObject.IsInstanceValid))
            .Concat(_portalNodes.Keys.Where(GodotObject.IsInstanceValid))
            .Concat(_corpseLoot.Keys.Where(GodotObject.IsInstanceValid))
            .Distinct()
            .OrderBy(node => _player!.GlobalPosition.DistanceTo(node.GlobalPosition))
            .ToArray();
        var nearest = candidates.FirstOrDefault();

        var interactionDistance = _gameplayControls?.Foundation.Interaction.FocusDistance ?? InteractionDistance;
        if (nearest is null || !IsNear(nearest, interactionDistance))
        {
            _message = "Rien à portée d’interaction.";
            UpdateHud();
            return;
        }

        InteractWith(nearest);
    }

    private bool TryInteractWithWorldPointer(Vector2 pointerPosition)
    {
        if (_camera is null || _player is null || _portalTransitionInProgress ||
            _moduleMap?.GlobalMapVisible == true || _rpgMenu?.IsOpen == true || _dialogueOverlay?.IsOpen == true)
            return false;

        var origin = _camera.ProjectRayOrigin(pointerPosition);
        var query = PhysicsRayQueryParameters3D.Create(origin,
            origin + (_camera.ProjectRayNormal(pointerPosition) * 250));
        query.Exclude = [_player.GetRid()];
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (!hit.TryGetValue("collider", out var collider) ||
            collider.AsGodotObject() is not Node colliderNode)
            return false;

        Node? current = colliderNode;
        Node3D? interactiveNode = null;
        while (current is not null)
        {
            if (current is Node3D node3D &&
                (_portalNodes.ContainsKey(node3D) || node3D == _flyingMount ||
                 _clickInteractions.ContainsKey(node3D) || _narrativeParticipants.ContainsKey(node3D) ||
                 node3D == _keyItem || node3D == _door || _corpseLoot.ContainsKey(node3D)))
            {
                interactiveNode = node3D;
                break;
            }
            current = current.GetParent();
        }
        if (interactiveNode is null) return false;

        var interactionDistance = _clickInteractions.TryGetValue(interactiveNode, out var clickTrigger)
            ? clickTrigger.MaximumDistanceMeters
            : _gameplayControls?.Foundation.Interaction.FocusDistance ?? InteractionDistance;
        if (!IsNear(interactiveNode, interactionDistance))
        {
            if (clickTrigger is not null)
            {
                _message = $"Approchez-vous de l'objet pour l'utiliser ({interactionDistance:0.#} m maximum).";
                UpdateHud();
                return true;
            }
            _message = interactiveNode switch
            {
                _ when interactiveNode == _flyingMount => "Approchez-vous de Brumaille pour le monter.",
                _ when _narrativeParticipants.TryGetValue(interactiveNode, out var participant) =>
                    $"Approchez-vous de {participant.DisplayName} pour lui parler.",
                _ when interactiveNode == _keyItem => $"Approchez-vous de {_questItemDisplayName} pour la prendre.",
                _ when interactiveNode == _door => $"Approchez-vous de {_gateDisplayName} pour l’ouvrir.",
                _ when _corpseLoot.ContainsKey(interactiveNode) => "Approchez-vous de la dépouille pour la fouiller.",
                _ => "Approchez-vous du passage pour l’emprunter."
            };
            UpdateHud();
            return true;
        }

        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        if (clickTrigger is not null && interactiveNode != _flyingMount &&
            !_portalNodes.ContainsKey(interactiveNode))
        {
            ExecuteClickInteraction(interactiveNode, clickTrigger);
            return true;
        }
        InteractWith(interactiveNode);
        return true;
    }

    private void ExecuteClickInteraction(Node3D source, EntityClickTriggerSettings settings)
    {
        switch (settings.Action)
        {
            case EntityClickTriggerAction.ShowMessage:
                _message = settings.Message;
                break;
            case EntityClickTriggerAction.StartDialogue:
                if (!Guid.TryParse(settings.TargetId, out var dialogueId) || _loadedProject is null ||
                    _loadedProject.Dialogues.FirstOrDefault(dialogue => dialogue.Id.Value == dialogueId) is not { } dialogue)
                {
                    _message = "Le dialogue lié à cet objet n'est plus disponible.";
                    break;
                }
                _activeDialogue = dialogue;
                _activeDialogueEntryNodeId = dialogue.EntryNodeId;
                _activeDialogueTraversal = new DialogueTraversal(dialogue, dialogue.EntryNodeId);
                _activeDialogueTraversalDialogueId = dialogue.Id;
                _activeDialogueParticipant = source;
                _guideName = dialogue.DisplayName;
                InteractWithGuide();
                break;
            case EntityClickTriggerAction.ToggleVisibility:
                if (!Guid.TryParse(settings.TargetId, out var entityId) ||
                    !_entityNodes.TryGetValue(entityId, out var target) ||
                    !GodotObject.IsInstanceValid(target))
                {
                    _message = "L'objet cible de ce déclencheur n'est plus disponible.";
                    break;
                }
                var visible = !target.Visible;
                target.Visible = visible;
                foreach (var node in target.FindChildren("*", "CollisionShape3D", recursive: true, owned: false))
                {
                    if (node is CollisionShape3D collision) collision.Disabled = !visible;
                }
                _message = $"{target.Name} est maintenant {(visible ? "visible" : "masqué")}.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), settings.Action,
                    "Unknown click interaction action.");
        }
        UpdateHud();
        GD.Print($"OPENRPG_FORGE_ENTITY_CLICK_TRIGGER action={EntityClickTriggerSettings.ActionKey(settings.Action)} " +
                 $"source={source.Name} target={settings.TargetId}");
    }

    private void InteractWith(Node3D target)
    {
        if (target == _flyingMount && _player is not null)
        {
            if (_flyingMount.IsMounted)
            {
                _message = _flyingMount.TryDismount()
                    ? "Vous descendez de Brumaille."
                    : "Descendez près du sol avec Ctrl avant de quitter la selle.";
            }
            else
            {
                _flyingMount.Mount(_player);
                _message = "Brumaille prend son envol · ZQSD/WASD diriger · Espace monter · Ctrl descendre · E quitter la selle.";
            }
            SyncPlayerInterfaceLock();
            UpdateHud();
            return;
        }

        if (_portalNodes.TryGetValue(target, out var portal))
        {
            _ = TransitionThroughPortalAsync(portal);
            return;
        }

        if (_corpseLoot.Remove(target, out var contents))
        {
            if (_corpseCreatureEvents.Remove(target, out var creatureEvents))
            {
                var accessed = CreatureEventRuntimeResolver.InventoryAccessed(creatureEvents, target.Name,
                    "player", "corpse-loot", contents.Count == 0 ? "empty" : "accepted");
                if (accessed is not null)
                {
                    _creatureEventInventoryDispatchCount++;
                    DispatchCreatureEvent(accessed);
                }
                foreach (var item in contents)
                {
                    var transferred = CreatureEventRuntimeResolver.ItemTransferred(creatureEvents, target.Name,
                        "player", item.ItemSlug, item.Quantity, "corpse-loot", "accepted");
                    if (transferred is null) continue;
                    _creatureEventInventoryDispatchCount++;
                    DispatchCreatureEvent(transferred);
                }
            }
            GrantLoot(contents);
            _message = contents.Count == 0
                ? "La dépouille ne contient plus rien."
                : $"Vous fouillez la dépouille · {contents.Count} type(s) d’objet récupéré(s).";
            if (target == _bossCorpse) _bossCorpse = null;
            target.QueueFree();
            UpdateHud();
            return;
        }

        if (_narrativeParticipants.TryGetValue(target, out var narrativeParticipant))
        {
            if (TryActivateNarrativeDialogue(target, narrativeParticipant))
                InteractWithGuide();
            UpdateHud();
            return;
        }

        if (target == _keyItem && !_hasKey)
        {
            if (_keyItemBlueprint?.ItemAdvanced is { } advanced)
            {
                var context = AdvancedItemContext(advanced, 2026);
                var requirements = ItemAdvancedRuntimeResolver.ResolveRequirements(advanced, context);
                if (!requirements.Allowed)
                {
                    _message = $"Objet inutilisable : {string.Join(", ", requirements.Missing)}.";
                    UpdateHud();
                    return;
                }
                var reservation = ItemUniqueRegistryResolver.Reserve(advanced, _uniqueItemRegistry, "player:local",
                    "party:local", "season:2026", DateTimeOffset.UtcNow, true);
                if (!reservation.Accepted)
                {
                    _message = $"Objet unique indisponible ({reservation.Code}).";
                    UpdateHud();
                    return;
                }
                _uniqueItemRegistry = reservation.State;
                _keyItemUniqueToken = reservation.Reservation?.Token;
                if (_keyItemUniqueToken is { } token)
                    _uniqueItemRegistry = ItemUniqueRegistryResolver.Commit(_uniqueItemRegistry, token, true);
                _keyItemEconomicValue = ItemAdvancedRuntimeResolver.ResolveEconomicValue(advanced, context);
                _keyItemGeneratedAffixes = advanced.Generator?.Enabled == true
                    ? ItemAdvancedRuntimeResolver.Generate(advanced, context).Affixes.Count : 0;
                var identified = ItemAdvancedRuntimeResolver.ResolveIdentifiedProperties(advanced,
                    advanced.Identification.Difficulty);
                GD.Print($"OPENRPG_FORGE_RUNTIME_ITEM_PICKUP profile={advanced.Id:D} value={_keyItemEconomicValue:0.##} " +
                         $"identified={identified.Count} affixes={_keyItemGeneratedAffixes} " +
                         $"unique_revision={_uniqueItemRegistry.Revision}");
            }
            _hasKey = true;
            GrantInventoryItem(_questItemSlug, 1);
            _message = $"Vous obtenez {_questItemDisplayName}.";
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
                    ? $"{_gateDisplayName} reste fermée. La Clé de braise se trouve dans le reliquaire latéral, avant cette porte."
                    : "La quête n’autorise pas encore le passage.";
            }

            UpdateHud();
            return;
        }

        if (target == _keyItem && !_hasKey)
        {
            _hasKey = true;
            GrantInventoryItem(_questItemSlug, 1);
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

    private async Task TransitionThroughPortalAsync(PortalLink portal)
    {
        if (_portalTransitionInProgress || _loadedProject is null || _activeArea is null || _lifecycle is null)
            return;
        _portalTransitionInProgress = true;
        try
        {
            var travel = PortalLinkRuntimeService.Resolve(_loadedProject, _activeArea.Id, portal.Id,
                Vector3Value.Zero, 0);
            var destination = _loadedProject.Areas.Single(area => area.Id == travel.DestinationAreaId);
            EnsureLifecycle(_lifecycle.Transition(destination.Id, null, portal.Id,
                RuntimeLifecycleCause.PortalTransition), "portal_transition");
            _message = $"Voyage vers {destination.DisplayName}…";
            UpdateHud();
            s_pendingPortalSpawn = travel.Reversed ? portal.SourceEntityId : portal.DestinationEntityId;
            s_pendingPortalLabel = destination.DisplayName;
            s_pendingPortalRuntimeState = BuildRuntimeSave(
                destination.Id.ToString(),
                new RuntimeVector3(
                    travel.DestinationTransform.Position.X,
                    travel.DestinationTransform.Position.Y,
                    travel.DestinationTransform.Position.Z));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var error = GetTree().ReloadCurrentScene();
            if (error != Error.Ok)
            {
                s_pendingPortalSpawn = null;
                s_pendingPortalLabel = null;
                s_pendingPortalRuntimeState = null;
                throw new InvalidOperationException($"Scene reload failed during portal travel: {error}.");
            }
        }
        catch
        {
            _portalTransitionInProgress = false;
            throw;
        }
    }

    private async Task RunInnUxSmokeTestAsync(ProjectDefinition project)
    {
        var activeArea = _activeArea ?? throw new InvalidOperationException(
            "The inn UX smoke test requires an active area.");
        if (s_innUxSmokeStep == 1)
        {
            AssertSmoke(activeArea.Slug == "clairval",
                $"Clicking the inn door loaded '{activeArea.Slug}' instead of Clairval.");
            AssertSmoke(_inventory.Snapshot().SingleOrDefault(item =>
                            string.Equals(item.Slug, "portal-proof-token", StringComparison.Ordinal))?.Quantity == 2 &&
                        Math.Abs(_progressionExperience - 321) <= .001 &&
                        _runtimeAreaStates.Values.Any(state =>
                            !string.Equals(state.AreaId, activeArea.Id.ToString(), StringComparison.OrdinalIgnoreCase) &&
                            state.Weather is not null),
                "The portal transition lost inventory, progression or the source area's weather state.");
            s_innUxSmokeStep = 0;
            GD.Print("OPENRPG_FORGE_INN_DOOR_CLICK_PASS source=ember-inn destination=clairval " +
                     "input=left-mouse ui_button=absent transition=runtime-reload " +
                     "state=inventory-progression-weather-preserved");
            GetTree().Quit(0);
            return;
        }

        AssertSmoke(s_innUxSmokeStep == 0 && activeArea.Slug == "ember-inn" &&
                    project.Module.EntryAreaId == activeArea.Id,
            "The inn UX smoke test must begin at the module entry inside the Ember Inn.");
        var player = _player ?? throw new InvalidOperationException("The inn UX smoke test has no player.");
        var camera = _camera ?? throw new InvalidOperationException("The inn UX smoke test has no camera.");
        var start = player.GlobalPosition;
        for (var frame = 0; frame < 20; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _cameraRig.ResetMotionDiagnostics();
        Input.ActionPress("move_right");
        try
        {
            for (var frame = 0; frame < 60; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        finally
        {
            Input.ActionRelease("move_right");
        }
        for (var frame = 0; frame < 8; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var travelled = new Vector2(player.GlobalPosition.X - start.X, player.GlobalPosition.Z - start.Z).Length();
        AssertSmoke(travelled >= 3,
            $"The inn camera motion test moved only {travelled:0.###} metres.");
        AssertSmoke(_cameraRig.MaximumVerticalFocusStep <= .04f,
            $"The camera focus still bounces vertically while walking " +
            $"(vertical={_cameraRig.MaximumVerticalFocusStep:0.###}).");
        GD.Print($"OPENRPG_FORGE_CAMERA_MOTION_STABLE_PASS area=ember-inn distance={travelled:0.###} " +
                 $"max_focus_step={_cameraRig.MaximumFocusStep:0.###} " +
                 $"max_step_delta={_cameraRig.MaximumFocusStepDelta:0.###} " +
                 $"max_vertical_step={_cameraRig.MaximumVerticalFocusStep:0.###} " +
                 "focus=physics-interpolated-render-smoothed wall_avoidance=enabled");

        player.GlobalPosition = start;
        player.Velocity = Vector3.Zero;
        for (var frame = 0; frame < 4; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        _cameraRig.Snap(camera, player.GlobalPosition, ConstrainCameraPosition);
        AssertSmoke(_hud?.InteractionPromptVisible == false,
            "The obsolete central inn exit prompt is still visible.");

        GrantInventoryItem("portal-proof-token", 2);
        _progressionExperience = 321;

        var portalNode = _portalNodes.Keys.Single();
        var clickTarget = portalNode.GetNode<Area3D>("PortalClickTarget");
        AssertSmoke(!camera.IsPositionBehind(clickTarget.GlobalPosition),
            "The authored inn door click target is behind the initial camera.");
        var pointer = camera.UnprojectPosition(clickTarget.GlobalPosition);
        s_innUxSmokeStep = 1;
        var clicked = TryInteractWithWorldPointer(pointer);
        AssertSmoke(clicked && _portalTransitionInProgress,
            "A left click on the authored inn door did not begin the portal transition.");
    }

    private async Task RunAreaStateTransitionSmokeTestAsync(ProjectDefinition project)
    {
        var activeArea = _activeArea ?? throw new InvalidOperationException(
            "The area-state smoke test requires an active area.");
        if (s_areaStateSmokeStep == 1)
        {
            AssertSmoke(activeArea.Id != project.Module.EntryAreaId,
                "The area-state smoke test did not load the portal destination.");
            var canonicalGeneratedWorld =
                $"world/areas/{activeArea.Id.Value:N}/generated-world.tscn";
            if (activeArea.World.Loading.RequiredResources.Contains(
                    canonicalGeneratedWorld, StringComparer.Ordinal))
            {
                AssertSmoke(_areaWorldRoot?.GetNodeOrNull<Node3D>("PromotedGeneratedWorld") is not null,
                    "The destination area's promoted WorldGeneration scene was not instantiated.");
            }
            var sourceState = _runtimeAreaStates.Values.SingleOrDefault(state =>
                string.Equals(state.AreaId, project.Module.EntryAreaId.ToString(),
                    StringComparison.OrdinalIgnoreCase));
            AssertSmoke(_inventory.Snapshot().SingleOrDefault(item =>
                            string.Equals(item.Slug, "portal-proof-token", StringComparison.Ordinal))?.Quantity == 2 &&
                        Math.Abs(_progressionExperience - 321) <= .001 &&
                        _hasKey &&
                        sourceState?.Weather is not null,
                "The portal reload lost global state or the source area's weather snapshot.");
            AssertSmoke(_quickSavePath is not null && SaveRuntime(_quickSavePath),
                "The destination-area checkpoint could not be written.");
            s_areaStateSmokeStep = 0;
            GD.Print($"OPENRPG_FORGE_RUNTIME_AREA_STATE_SMOKE_PASS source={project.Module.EntryAreaId} " +
                     $"destination={activeArea.Id} inventory=2 progression={_progressionExperience:0} " +
                     $"quests={_questTracker?.Snapshot().Count ?? 0} variables={_blackboard.Snapshot().Count} " +
                     $"weather_revision={sourceState!.Weather!.Revision} areas={_runtimeAreaStates.Count}");
            GetTree().Quit(0);
            return;
        }

        AssertSmoke(s_areaStateSmokeStep == 0 && activeArea.Id == project.Module.EntryAreaId,
            "The area-state smoke test must begin in the module entry area.");
        var portal = activeArea.Portals.SingleOrDefault() ?? throw new InvalidOperationException(
            "The area-state smoke fixture requires one entry-area portal.");
        GrantInventoryItem("portal-proof-token", 2);
        _progressionExperience = 321;
        _hasKey = true;
        s_areaStateSmokeStep = 1;
        await TransitionThroughPortalAsync(portal);
    }

    private void RunAreaSaveResumeSmokeTest(ProjectDefinition project)
    {
        var activeArea = _activeArea ?? throw new InvalidOperationException(
            "The area-save resume smoke test requires an active area.");
        var sourceState = _runtimeAreaStates.Values.SingleOrDefault(state =>
            string.Equals(state.AreaId, project.Module.EntryAreaId.ToString(), StringComparison.OrdinalIgnoreCase));
        AssertSmoke(_loadedSave && activeArea.Id != project.Module.EntryAreaId &&
                    _inventory.Snapshot().SingleOrDefault(item =>
                        string.Equals(item.Slug, "portal-proof-token", StringComparison.Ordinal))?.Quantity == 2 &&
                    Math.Abs(_progressionExperience - 321) <= .001 &&
                    _hasKey && sourceState?.Weather is not null && _runtimeAreaStates.Count >= 2,
            "Loading a destination-area checkpoint lost the selected area or persistent session state.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_AREA_SAVE_RESUME_PASS source={project.Module.EntryAreaId} " +
                 $"destination={activeArea.Id} inventory=2 progression={_progressionExperience:0} " +
                 $"weather_revision={sourceState!.Weather!.Revision} areas={_runtimeAreaStates.Count}");
        GetTree().Quit(0);
    }



    private void AttackNearestHostile()
    {
        var nearest = _hostiles.Keys
            .Where(node => GodotObject.IsInstanceValid(node) && !_hostiles[node].IsDead)
            .OrderBy(node => _player!.GlobalPosition.DistanceTo(node.GlobalPosition))
            .FirstOrDefault();

        if (nearest is null || !IsNear(nearest, AttackDistance))
        {
            _message = "Aucun ennemi à portée.";
            _toastQueue?.Show(RuntimeToastKind.Alert, "Aucune cible hostile à portée.", "combat:no-target");
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
        var nowSeconds = Time.GetTicksMsec() / 1000d;
        if (target.IsDead)
        {
            RejectCombatAction("Cette cible est déjà vaincue.", "combat:target-dead");
            return;
        }
        if (!AuthorizeAreaCombatAttack(_playerCombatant, target))
        {
            return;
        }
        if (_playerCombatant.Resource < ability.ResourceCost)
        {
            RejectCombatAction(
                $"Ressource insuffisante pour {ability.DisplayName} : {ability.ResourceCost} requise, " +
                $"{_playerCombatant.Resource} disponible.",
                $"combat:resource:{ability.Id}");
            return;
        }
        var cooldownRemaining = _playerCombatant.CooldownRemaining(ability, nowSeconds);
        if (cooldownRemaining > 0)
        {
            RejectCombatAction($"{ability.DisplayName} est en recharge ({cooldownRemaining:0.0} s).",
                $"combat:cooldown:{ability.Id}");
            return;
        }
        var damageTransactionId = Guid.NewGuid();
        if (_creatureEventProfiles.TryGetValue(hostile, out var preDamageEvents))
        {
            var requested = ability.Effects.Where(item => item.Kind == AbilityEffectKind.Damage)
                .Sum(item => item.Magnitude);
            var beforeDamage = CreatureEventRuntimeResolver.BeforeDamage(preDamageEvents, damageTransactionId,
                hostile.Name, _playerCombatant.Id, requested, "physical", ability.Slug);
            if (beforeDamage is not null)
            {
                _creatureEventBeforeDamageDispatchCount++;
                DispatchCreatureEvent(beforeDamage);
            }
        }
        _player?.PlayAttackAnimation();
        var result = CombatResolver.Cast(
            _playerCombatant,
            target,
            ability.Id,
            nowSeconds,
            damageMultiplier: _resolvedDifficulty?.PlayerDamageMultiplier ?? 1);
        _lastCombatFeedback = CombatFeedbackPresentationService.Create(
            damageTransactionId, _playerCombatant, target, result, nowSeconds);
        _actionBar?.NotifyCastAccepted();
        PresentCombatFeedback(hostile, target, _lastCombatFeedback);
        if (hostile == _boss) ReportFactionWitness("attack.ash-warden");
        if (_creatureEventProfiles.TryGetValue(hostile, out var creatureEvents))
        {
            var damageApplied = CreatureEventRuntimeResolver.DamageApplied(creatureEvents, damageTransactionId,
                hostile.Name, _playerCombatant.Id, result.Damage, result.Damage, target.Health,
                "physical", ability.Slug);
            if (damageApplied is not null)
            {
                _creatureEventDamageAppliedDispatchCount++;
                DispatchCreatureEvent(damageApplied);
            }
            var combatBoundary = CreatureEventRuntimeResolver.CombatBoundary(creatureEvents,
                CreatureCombatBoundaryMode.CombatTickCompleted, hostile.Name, ++_creatureCombatBoundarySequence,
                _activeCombatProvider?.ProviderId ?? "realtime");
            if (combatBoundary is not null)
            {
                _creatureEventCombatDispatchCount++;
                DispatchCreatureEvent(combatBoundary);
            }
        }
        var health = target.Health;
        var hostileName = _hostileNames[hostile];
        _message = $"{hostileName} subit {result.Damage} dégât(s) ({health} PV).";
        var reactionDuration = PlayHostileCombatReaction(hostile, result.TargetDied);
        if (_characterAnimations.TryGetValue(hostile, out var hitStopAnimation))
            _combatImpactPool?.ApplyHitStop(hitStopAnimation.Player);

        if (hostile == _boss && health <= Math.Max(1, target.Profile.Stats.MaximumHealth / 2) && !_bossPhaseTwo)
        {
            _bossPhaseTwo = true;
            if (_creatureEventProfiles.TryGetValue(hostile, out var bossEvents) &&
                bossEvents.Overrides.TryGetValue("runtime.phase_two.custom_event", out var phaseTwoEvent) &&
                bossEvents.CustomEvents.FirstOrDefault(item =>
                    string.Equals(item.QualifiedName, phaseTwoEvent, StringComparison.Ordinal)) is { } customSchema)
            {
                var custom = CreatureEventRuntimeResolver.Custom(bossEvents, customSchema.SchemaId,
                    new Dictionary<string, string>
                    {
                        ["count"] = "3",
                        ["source"] = _hostileBlueprints[hostile].Slug,
                    });
                _creatureEventCustomDispatchCount++;
                DispatchCreatureEvent(custom);
            }
            ExecuteForgeScript(_bossPhaseTwoScript, hostile);
            GD.Print("OPENRPG_FORGE_BOSS_PHASE_TWO");
        }

        if (result.TargetDied)
        {
            var blueprint = _hostileBlueprints[hostile];
            _hostiles.Remove(hostile);
            _hostileNames.Remove(hostile);
            _hostileBlueprints.Remove(hostile);
            var loot = CreatureLootRuntimeResolver.ResolveDeath(
                blueprint.CreatureLoot,
                result.Loot,
                StableLootRoll(target.Id),
                "player",
                "player-party",
                DateTimeOffset.UtcNow,
                blueprint.CreatureLoot?.RequiredAtRuntime == true);
            if (!loot.Succeeded)
                throw new InvalidOperationException(string.Join("; ", loot.Diagnostics.Select(item =>
                    $"{item.Code} at {item.Path}: {item.Message}")));
            GrantLoot(loot.Grants);
            if (blueprint.CreatureEvents is not null)
            {
                var died = CreatureEventRuntimeResolver.EntityDied(blueprint.CreatureEvents, hostile.Name,
                    _playerCombatant.Id, [_playerCombatant.Id], ability.Slug,
                    $"{hostile.GlobalPosition.X:0.###},{hostile.GlobalPosition.Y:0.###},{hostile.GlobalPosition.Z:0.###}",
                    result.Loot.Select(item => item.ItemSlug).ToArray(), loot.Corpse.Mode.ToString());
                if (died is not null)
                {
                    _creatureEventDeathDispatchCount++;
                    DispatchCreatureEvent(died);
                }
            }
            if (loot.Corpse.Created)
            {
                var corpse = CreateLootCorpse(hostile, hostileName, loot.Corpse);
                if (blueprint.CreatureEvents is not null) _corpseCreatureEvents[corpse] = blueprint.CreatureEvents;
                if (hostile == _boss) _bossCorpse = corpse;
            }

            _characterAnimations.Remove(hostile);
            RemoveCreatureRoleBehavior(hostile);
            RemoveCreatureEvents(hostile);
            if (reactionDuration > 0)
            {
                var timer = GetTree().CreateTimer(reactionDuration);
                timer.Timeout += () =>
                {
                    ReleaseCombatFeedbackTarget(hostile);
                    if (GodotObject.IsInstanceValid(hostile)) hostile.QueueFree();
                };
            }
            else
            {
                ReleaseCombatFeedbackTarget(hostile);
                hostile.QueueFree();
            }

            if (hostile == _boss)
            {
                _bossDefeated = true;
                TransitionFactionDiplomacy();
                ExecuteForgeScript(_bossDeathScript, hostile);
            }
            else
            {
                _message = $"{hostileName} est vaincue.";
            }

            AwardProgression(hostile == _boss ? 200 : 75, "combat");
        }

        UpdateHud();
    }

    private bool AuthorizeAreaCombatAttack(CombatantState attacker, CombatantState target)
    {
        var path = _activeArea is null
            ? "area/runtimePolicies/rules/combat"
            : $"areas/{_activeArea.Id}/runtimePolicies/rules/combat";
        if (_areaCombatAuthority is null ||
            !Guid.TryParse(attacker.Id, out var attackerId) ||
            !Guid.TryParse(target.Id, out var targetId))
        {
            const string code = "NET_AREA_COMBAT_CONTEXT_INVALID";
            GD.PrintErr($"OPENRPG_FORGE_AREA_COMBAT_REJECTED code={code} path={path}");
            RejectCombatAction("Le contexte d’autorité du combat est indisponible.",
                "combat:authority:context-invalid");
            return false;
        }

        var decision = _areaCombatAuthority.AuthorizeDamage(
            attackerId,
            targetId,
            targetIsPlayer: false,
            worldRevision: checked(++_runtimePolicyRevision),
            serverAuthorityGranted: _areaCombatServerAuthority,
            aggroInitiation: true);
        if (!decision.Accepted)
        {
            var message = decision.Code switch
            {
                "NET_AREA_COMBAT_SERVER_REQUIRED" =>
                    "Cette action de combat exige l’autorité du serveur.",
                "NET_AREA_COMBAT_AUTHORITY_REQUIRED" =>
                    "La politique de combat de cette zone n’autorise pas une décision locale.",
                "NET_AREA_AGGRO_DISABLED" =>
                    "Cette zone interdit l’initiation d’un combat hostile.",
                _ => "La politique de combat de cette zone refuse cette action.",
            };
            GD.PrintErr($"OPENRPG_FORGE_AREA_COMBAT_REJECTED code={decision.Code} path={path} " +
                        $"revision={decision.Revision}");
            RejectCombatAction(message, $"combat:authority:{decision.Code.ToLowerInvariant()}");
            return false;
        }

        GD.Print($"OPENRPG_FORGE_AREA_COMBAT_AUTHORITY_PASS code={decision.Code} path={path} " +
                 $"aggro=true server={_areaCombatServerAuthority.ToString().ToLowerInvariant()} " +
                 $"revision={decision.Revision}");
        return true;
    }

    private void RejectCombatAction(string message, string deduplicationKey)
    {
        _message = message;
        _toastQueue?.Show(RuntimeToastKind.Alert, message, deduplicationKey);
        UpdateHud();
    }

    private void PresentCombatFeedback(
        Node3D hostile,
        CombatantState target,
        CombatFeedbackPresentation presentation)
    {
        var targetChanged = _currentCombatTarget != hostile;
        _currentCombatTarget = hostile;
        _combatFeedbackLayer?.Present(presentation, hostile);
        if (presentation.Amounts.FirstOrDefault() is { } primaryAmount)
            _combatFeedbackLayer?.Flash(hostile, primaryAmount.Kind);
        if (_camera is not null)
            _combatImpactPool?.Present(presentation, hostile, _camera.GlobalPosition);
        if (presentation.Amounts.Any(item => item.Kind == CombatFeedbackAmountKind.Damage))
            _cameraRig.AddImpulse(_combatUiPolicy.CameraImpulseMeters, _combatUiPolicy.CameraImpulseSeconds);
        PlayCombatFeedbackAudio(presentation, hostile);
        var displayName = _hostileNames.GetValueOrDefault(hostile, hostile.Name);
        if (targetChanged)
        {
            var position = hostile.GlobalPosition;
            _moduleMap?.ShowPing(new Vector3Value(position.X, position.Y, position.Z),
                RuntimeMapPingKind.Combat, $"Cible de combat : {displayName}");
        }
        _hud?.UpdateTargetStatus(
            displayName,
            target.Health,
            target.Profile.Stats.MaximumHealth,
            hostile == _boss,
            presentation.TargetDied);
    }

    private void PlayCombatFeedbackAudio(CombatFeedbackPresentation presentation, Node3D target)
    {
        if (_combatImpactPool is null) return;
        var position = target.GlobalPosition + Vector3.Up;
        var authoredCue = _areaExperience?.AudioCues.FirstOrDefault(item =>
            string.Equals(item.CueId, presentation.VisualCueKey, StringComparison.Ordinal));
        if (authoredCue is not null && _areaExperience is not null)
        {
            var resolved = AreaExperienceRuntimeService.PlayCue(
                _areaExperience, _areaAudioState, authoredCue.CueId, DateTimeOffset.UtcNow);
            _areaAudioState = resolved.State;
            var stream = LoadAudioStream(resolved.Playback.Resource);
            if (stream is not null)
            {
                var playbackId = resolved.Playback.PlaybackId;
                _combatImpactPool.PlayAudioStream(stream, position, resolved.Playback.Bus,
                    Math.Clamp(resolved.Playback.Volume, .001f, 1), () =>
                    {
                        _areaAudioState = AreaExperienceRuntimeService.Complete(
                            _areaAudioState, playbackId, DateTimeOffset.UtcNow);
                    }, cueOverride: true);
                GD.Print($"OPENRPG_FORGE_RUNTIME_COMBAT_AUDIO cue={presentation.VisualCueKey} source=area");
                return;
            }
            _areaAudioState = AreaExperienceRuntimeService.Complete(
                _areaAudioState, resolved.Playback.PlaybackId, DateTimeOffset.UtcNow);
        }

        var kind = presentation.Amounts.Any(item => item.Kind == CombatFeedbackAmountKind.Damage)
            ? CombatFeedbackAudioKind.ImpactLight
            : CombatFeedbackAudioKind.Healing;
        _combatImpactPool.PlayAudio(kind, position);
        GD.Print($"OPENRPG_FORGE_RUNTIME_COMBAT_AUDIO cue={presentation.VisualCueKey} source=builtin kind={kind}");
    }

    private void ReleaseCombatFeedbackTarget(Node3D hostile)
    {
        _combatFeedbackLayer?.Unregister(hostile);
        if (_currentCombatTarget != hostile) return;
        ClearCombatTargetFocus();
    }

    private void ClearCombatTargetFocus()
    {
        _currentCombatTarget = null;
        _combatFeedbackLayer?.ClearTarget();
        _hud?.ClearTarget();
    }

    private void GrantLoot(IEnumerable<LootGrant> grants)
    {
        foreach (var grant in grants)
        {
            GrantInventoryItem(grant.ItemSlug, grant.Quantity);
            _toastQueue?.Show(RuntimeToastKind.Loot,
                $"Butin : {grant.ItemSlug} ×{grant.Quantity}", $"loot:{grant.ItemSlug}");
            GD.Print($"OPENRPG_FORGE_LOOT_GRANTED item={grant.ItemSlug} quantity={grant.Quantity} " +
                $"owner={grant.OwnerId} instance={grant.InstanceKey} cause={grant.Cause}");
        }
    }

    private void GrantInventoryItem(string slug, int quantity) => _inventory.Grant(slug, quantity);

    private bool HasInventoryItem(string slug) => _inventory.HasItem(slug);

    private Node3D CreateLootCorpse(Node3D source, string hostileName, CreatureCorpseSnapshot snapshot)
    {
        var corpse = new Node3D
        {
            Name = $"Corpse_{SafeNodeName(hostileName)}",
            GlobalTransform = source.GlobalTransform,
        };
        AddChild(corpse);
        corpse.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, .18f, 0),
            RotationDegrees = new Vector3(0, 0, 90),
            Mesh = new CapsuleMesh { Radius = .28f, Height = 1.35f },
            MaterialOverride = CreateMaterial(new Color("4d3545")),
        });
        CreateLabel(corpse, $"DÉPOUILLE · {hostileName}\n[E] Fouiller", new Color("ffd2a6"), .65f);
        _corpseLoot[corpse] = snapshot.Contents;
        GD.Print($"OPENRPG_FORGE_CORPSE_CREATED mode={snapshot.Mode} items={snapshot.Contents.Count} " +
            $"ownership={snapshot.OwnershipPolicy} resurrection={snapshot.AllowResurrection}");
        if (snapshot.ExpiresAt is { } expiresAt)
        {
            var delay = Math.Max(.05, (expiresAt - DateTimeOffset.UtcNow).TotalSeconds);
            var timer = GetTree().CreateTimer(delay);
            timer.Timeout += () =>
            {
                _corpseLoot.Remove(corpse);
                _corpseCreatureEvents.Remove(corpse);
                if (corpse == _bossCorpse) _bossCorpse = null;
                if (GodotObject.IsInstanceValid(corpse)) corpse.QueueFree();
            };
        }
        return corpse;
    }

    private static double StableLootRoll(string combatantId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combatantId));
        return BitConverter.ToUInt32(bytes, 0) / (double)uint.MaxValue;
    }

    private double PlayHostileCombatReaction(Node3D hostile, bool died)
    {
        if (!_characterAnimations.TryGetValue(hostile, out var animation)) return 0;
        var clip = died ? animation.DeathClip : animation.HitClip;
        if (clip is null || !GodotAssetAnimationController.Play(animation, clip, .06, restart: true)) return 0;
        if (died)
        {
            _combatDeathAnimationCount++;
        }
        else
        {
            _combatHitAnimationCount++;
        }

        return Math.Max(.05, GodotAssetAnimationController.DurationSeconds(animation, clip));
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


}
