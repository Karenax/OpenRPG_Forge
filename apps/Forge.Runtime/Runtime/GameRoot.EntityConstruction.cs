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
                _gateDisplayName = GetOverride(entity, "runtime.displayName") ?? blueprint.DisplayName;
                break;
            case BlueprintKind.Creature:
                BuildCreature(entity, blueprint);
                break;
            case BlueprintKind.PlayerSpawn:
                _player = CreatePlayer(entity, blueprint);
                break;
            case BlueprintKind.Item:
                _keyItem = CreateItem(entity, blueprint);
                break;
            case BlueprintKind.Trigger:
                var portal = _activeArea?.Portals.SingleOrDefault(candidate => candidate.SourceEntityId == entity.Id);
                var trigger = CreateExit(entity, portal is not null);
                if (portal is null) _exit = trigger;
                else _portalNodes.Add(trigger, portal);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(blueprint), blueprint.Kind, "Unsupported blueprint kind.");
        }
    }

    private void BuildPlaceable(EntityInstance entity, BlueprintDefinition blueprint)
    {
        var displayName = GetOverride(entity, "runtime.displayName") ?? blueprint.DisplayName;
        if (entity.Tags.Contains("pillar", StringComparer.Ordinal))
        {
            CreatePillar(entity, displayName);
            return;
        }

        var isFloor = entity.Tags.Contains("floor", StringComparer.Ordinal);
        CreateBox(
            entity,
            displayName,
            isFloor ? new Color("282638") : new Color("443b51"));
    }

    private void BuildCreature(EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (entity.Tags.Contains("flying-mount", StringComparer.Ordinal))
        {
            var mountDisplayName = GetOverride(entity, "runtime.displayName") ?? blueprint.DisplayName;
            _flyingMount = CreateFlyingMount(entity, mountDisplayName);
            return;
        }

        if (entity.Tags.Contains("npc", StringComparer.Ordinal) || HasRuntimeRole(entity, "guide"))
        {
            var guideDisplayName = GetOverride(entity, "runtime.displayName") ?? ProgressionDisplayName(blueprint);
            var narrativeParticipant = CreateCreature(
                entity, guideDisplayName, new Color("4ec9b0"), blueprint.CreaturePresentation);
            _narrativeParticipants.Add(narrativeParticipant, (blueprint.Id, guideDisplayName));
            if (_npc is null)
            {
                _npc = narrativeParticipant;
                _guideName = guideDisplayName;
            }
            ConfigureCreatureFoundation(narrativeParticipant, blueprint);
            ConfigureCreatureRoleBehavior(narrativeParticipant, blueprint);
            ConfigureCreatureEvents(narrativeParticipant, blueprint);
            return;
        }

        var isBoss = entity.Tags.Contains("boss", StringComparer.Ordinal) || HasRuntimeRole(entity, "boss");
        var hostileDisplayName = GetOverride(entity, "runtime.displayName") ?? ProgressionDisplayName(blueprint);
        var creature = CreateCreature(
            entity,
            hostileDisplayName,
            isBoss ? new Color("d34f4f") : new Color("d68a3a"),
            blueprint.CreaturePresentation);
        var profile = blueprint.Combat
            ?? throw new InvalidOperationException($"Hostile blueprint '{blueprint.Slug}' has no combat profile.");
        _hostiles.Add(creature, new CombatantState(entity.Id.ToString(), profile));
        _hostileNames.Add(creature, hostileDisplayName);
        _hostileBlueprints.Add(creature, blueprint);
        ConfigureCreatureFoundation(creature, blueprint);
        ConfigureCreatureRoleBehavior(creature, blueprint);
        ConfigureCreatureEvents(creature, blueprint);

        if (isBoss)
        {
            _boss = creature;
            _bossPhaseTwoScript = TryCompileEntityScript(entity, "phaseTwoScript");
            _bossDeathScript = TryCompileEntityScript(entity, "deathScript");
        }
    }

    private PlayerController CreatePlayer(EntityInstance entity, BlueprintDefinition blueprint)
    {
        _livingWorldPlayerEntity = entity;
        if (blueprint.Combat is not null)
        {
            _playerCombatant = new CombatantState(entity.Id.ToString(), blueprint.Combat);
        }

        var player = new PlayerController { Name = "Player" };
        AddChild(player);
        player.GlobalPosition = EngineValueConverter.ToEngine(entity.Transform.Position);
        player.ResetPhysicsInterpolation();
        player.ConfigureFallRecovery(player.GlobalPosition,
            _activeArea?.Kind == AreaKind.Interior ? 8 : 30);
        player.MovementCamera = _camera;
        if (_gameplayControls is not null) player.Configure(_gameplayControls);
        if (!TryAttachVisual(player, entity, applyPlayerVisualTransform: true))
        {
            player.AddChild(new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.45f, Height = 1.8f },
                MaterialOverride = CreateMaterial(PresentationColor(blueprint.CreaturePresentation, new Color("74b9ff"))),
            });
        }

        AddPresentationProxies(player, blueprint.CreaturePresentation);

        player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.45f, Height = 1.8f },
        });
        var playerLevel = ResolveProgression(blueprint).TotalLevel;
        var authoredDisplayName = GetOverride(entity, "runtime.displayName");
        CreateLabel(player, !string.IsNullOrWhiteSpace(authoredDisplayName)
                ? authoredDisplayName.ToUpperInvariant()
                : blueprint.Combat is null ? "AVATAR" : playerLevel > 0 ? $"HÉROS · NIV. {playerLevel}" : "HÉROS",
            new Color("dff3ff"), 1.5f, isLocalHero: true);
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
        if (_gameplayControls?.Climbing.Enabled == true && entity.Tags.Any(tag =>
                _gameplayControls.Climbing.SurfaceTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
        {
            body.SetMeta("forge_climb", true);
        }
        root.AddChild(body);
        var hidden = bool.TryParse(GetOverride(entity, "visual.hidden"), out var hide) && hide;
        var collisionProxy = hidden && entity.Tags.Contains("collision", StringComparer.Ordinal);
        if (collisionProxy) root.Scale = Vector3.One;
        var attachedVisual = !hidden && TryAttachVisual(body, entity);
        if (!attachedVisual && !hidden)
        {
            var effectiveColor = Color.FromString(GetOverride(entity, "visual.color") ?? string.Empty, color);
            var emission = bool.TryParse(GetOverride(entity, "visual.emission"), out var emit) && emit;
            var assisted = entity.Tags.Contains("assistant-generated", StringComparer.Ordinal);
            PrimitiveMesh mesh = GetOverride(entity, "visual.shape")?.ToLowerInvariant() switch
            {
                "sphere" => new SphereMesh { Radius = .5f, Height = 1 },
                "cylinder" => new CylinderMesh { TopRadius = .5f, BottomRadius = .5f, Height = 1 },
                "cone" => new CylinderMesh { TopRadius = .05f, BottomRadius = .55f, Height = 1 },
                _ => new BoxMesh { Size = Vector3.One },
            };
            if (assisted)
            {
                BuildAssistedLandmark(root, body, entity);
            }
            else
            {
                body.AddChild(new MeshInstance3D
                {
                    Mesh = mesh,
                    MaterialOverride = CreateMaterial(effectiveColor, emission),
                });
            }
        }
        if (entity.Tags.Contains("light", StringComparer.Ordinal))
        {
            var lightColor = Color.FromString(GetOverride(entity, "visual.color") ?? string.Empty,
                new Color("ffad67"));
            root.AddChild(new OmniLight3D
            {
                Position = new Vector3(0, 1.35f, 0),
                LightColor = lightColor,
                LightEnergy = 2.6f,
                OmniRange = 10,
                ShadowEnabled = false,
            });
        }
        // Interior areas already have one continuous floor shell. Rebuilding a collider for
        // every overlapping floor tile creates invisible seams that trap grounded movement.
        // Areas without a shell still keep their authored floor colliders.
        var isFloor = entity.Tags.Contains("floor", StringComparer.Ordinal);
        var usesInteriorFloorShell = isFloor && _interiorWalkableBounds is not null;
        var requiresFloorCollision = isFloor && !usesInteriorFloorShell;
        var isOpenDoorway = entity.Tags.Contains("doorway", StringComparer.Ordinal);
        if (!usesInteriorFloorShell &&
            (requiresFloorCollision ||
             (!isOpenDoorway && !entity.Tags.Contains("nonblocking", StringComparer.Ordinal))))
        {
            if (collisionProxy)
            {
                var authoredSize = EngineValueConverter.ToEngine(entity.Transform.Scale).Abs();
                body.AddChild(new CollisionShape3D
                {
                    Name = "AuthoredCollisionProxy",
                    Shape = new BoxShape3D { Size = authoredSize },
                });
            }
            else
            {
                var visual = attachedVisual ? body.GetNodeOrNull<Node3D>("AuthoredVisual") : null;
                body.AddChild(CreateVisualCollision(visual));
            }
            _collisionCount++;
        }
        return root;
    }

    private static CollisionShape3D CreateVisualCollision(Node3D? visual)
    {
        if (visual is null)
            return new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One } };

        var hasBounds = false;
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        Accumulate(visual, Transform3D.Identity);
        if (!hasBounds)
            return new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One } };

        var size = maximum - minimum;
        size = new Vector3(MathF.Max(size.X, .15f), MathF.Max(size.Y, .15f), MathF.Max(size.Z, .15f));
        return new CollisionShape3D
        {
            Position = (minimum + maximum) * .5f,
            Shape = new BoxShape3D { Size = size },
        };

        void Accumulate(Node node, Transform3D parentTransform)
        {
            var transform = node is Node3D node3D ? parentTransform * node3D.Transform : parentTransform;
            if (node is MeshInstance3D { Mesh: not null } mesh)
            {
                var bounds = mesh.GetAabb();
                for (var x = 0; x <= 1; x++)
                    for (var y = 0; y <= 1; y++)
                        for (var z = 0; z <= 1; z++)
                        {
                            var point = transform * (bounds.Position + new Vector3(
                                bounds.Size.X * x,
                                bounds.Size.Y * y,
                                bounds.Size.Z * z));
                            minimum = new Vector3(
                                MathF.Min(minimum.X, point.X),
                                MathF.Min(minimum.Y, point.Y),
                                MathF.Min(minimum.Z, point.Z));
                            maximum = new Vector3(
                                MathF.Max(maximum.X, point.X),
                                MathF.Max(maximum.Y, point.Y),
                                MathF.Max(maximum.Z, point.Z));
                            hasBounds = true;
                        }
            }

            foreach (var child in node.GetChildren()) Accumulate(child, transform);
        }
    }

    private void BuildAssistedLandmark(Node3D root, Node3D body, EntityInstance entity)
    {
        var semantic = new[] { "service", "objective", "social", "exploration", "rest", "encounter" }
            .FirstOrDefault(tag => entity.Tags.Contains(tag, StringComparer.Ordinal)) ?? "exploration";
        var primary = semantic switch
        {
            "service" => new Color("4db6ac"),
            "objective" => new Color("d5b35c"),
            "social" => new Color("9b633f"),
            "rest" => new Color("5576a8"),
            "encounter" => new Color("b74a3d"),
            _ => new Color("6f7884"),
        };
        switch (semantic)
        {
            case "service":
                Add(new CylinderMesh { TopRadius = 1.25f, BottomRadius = 1.45f, Height = .45f }, new(0, .22f, 0), primary.Darkened(.25f));
                Add(new CylinderMesh { TopRadius = .35f, BottomRadius = .5f, Height = 2.8f }, new(0, 1.55f, 0), primary);
                Add(new SphereMesh { Radius = .48f, Height = .96f }, new(0, 3.1f, 0), primary.Lightened(.32f), true);
                Light(primary.Lightened(.25f), 8, 2.2f);
                break;
            case "objective":
                Add(new BoxMesh { Size = new Vector3(2.4f, .5f, 2.4f) }, new(0, .25f, 0), primary.Darkened(.25f));
                Add(new CylinderMesh { TopRadius = .16f, BottomRadius = .72f, Height = 4.4f }, new(0, 2.45f, 0), primary);
                Add(new SphereMesh { Radius = .32f, Height = .64f }, new(0, 4.7f, 0), primary.Lightened(.35f), true);
                break;
            case "social":
                foreach (var (x, z) in new[] { (-1.45f, -1.25f), (1.45f, -1.25f), (-1.45f, 1.25f), (1.45f, 1.25f) })
                    Add(new CylinderMesh { TopRadius = .1f, BottomRadius = .13f, Height = 2.5f }, new(x, 1.25f, z), primary.Darkened(.22f));
                Add(new BoxMesh { Size = new Vector3(3.6f, .25f, 3.2f) }, new(0, 2.58f, 0), primary.Lightened(.12f));
                Add(new CylinderMesh { TopRadius = .38f, BottomRadius = .55f, Height = .7f }, new(0, .38f, 0), new Color("e4843f"), true);
                Light(new Color("ffb45f"), 7, 1.8f);
                break;
            case "rest":
                Add(new BoxMesh { Size = new Vector3(3.8f, .45f, 2.4f) }, new(0, .32f, 0), primary.Darkened(.2f));
                Add(new BoxMesh { Size = new Vector3(3.4f, .25f, 2f) }, new(0, .62f, 0), primary.Lightened(.15f));
                Add(new BoxMesh { Size = new Vector3(1.2f, .35f, 2f) }, new(-1.05f, .86f, 0), new Color("d7d0bd"));
                break;
            case "encounter":
                Add(new CylinderMesh { TopRadius = .75f, BottomRadius = 1.05f, Height = 1.1f }, new(0, .55f, 0), primary.Darkened(.3f));
                Add(new SphereMesh { Radius = .62f, Height = 1.15f }, new(0, 1.35f, 0), new Color("ff6538"), true);
                Light(new Color("ff6a3d"), 8, 2.8f);
                break;
            default:
                Add(new SphereMesh { Radius = 1.15f, Height = 2f }, new(-.65f, .75f, .15f), primary);
                Add(new SphereMesh { Radius = .8f, Height = 1.5f }, new(.75f, .48f, -.35f), primary.Lightened(.08f));
                Add(new SphereMesh { Radius = .58f, Height = 1.05f }, new(.4f, .35f, .8f), primary.Darkened(.1f));
                break;
        }
        CreateLabel(root, semantic.ToUpperInvariant(), primary.Lightened(.38f), 3.8f);

        void Add(PrimitiveMesh part, Vector3 position, Color partColor, bool glow = false) =>
            body.AddChild(new MeshInstance3D
            {
                Mesh = part,
                Position = position,
                MaterialOverride = CreateMaterial(partColor, glow),
            });

        void Light(Color lightColor, float range, float energy) => root.AddChild(new OmniLight3D
        {
            Position = new Vector3(0, 2.5f, 0),
            LightColor = lightColor,
            LightEnergy = energy,
            OmniRange = range,
            ShadowEnabled = false,
        });
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
        if (!TryAttachVisual(body, entity))
        {
            var effectiveColor = Color.FromString(GetOverride(entity, "visual.color") ?? string.Empty,
                new Color("665565"));
            body.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1 },
                MaterialOverride = CreateMaterial(effectiveColor),
            });
        }
        body.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = 0.5f, Height = 1 },
        });
        _collisionCount++;
        return root;
    }

    private Node3D CreateCreature(
        EntityInstance entity,
        string displayName,
        Color color,
        CreaturePresentationProfile? presentation)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);
        var species = GetOverride(entity, "runtime.species");
        if (!string.IsNullOrWhiteSpace(species))
        {
            BuildProceduralCreatureVisual(root, species, color);
        }
        else if (!TryAttachVisual(root, entity))
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.5f, Height = 1.7f },
                MaterialOverride = CreateMaterial(PresentationColor(presentation, color)),
            });
        }
        AddPresentationProxies(root, presentation);
        CreateLabel(root, displayName, Colors.White, 1.35f);
        return root;
    }

    private RuntimeFlyingMount CreateFlyingMount(EntityInstance entity, string displayName)
    {
        var mount = new RuntimeFlyingMount
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(mount);
        if (!TryAttachVisual(mount, entity))
            throw new InvalidOperationException($"Flying mount '{displayName}' has no governed animated visual.");
        mount.AddChild(new CollisionShape3D
        {
            Name = "MountCollision",
            Position = new Vector3(0, .85f, 0),
            Shape = new CapsuleShape3D { Radius = .8f, Height = 1.8f },
        });
        mount.Configure(_activeAreaHorizontalBounds ?? new Rect2(-64, -64, 128, 128), _camera);
        var prompt = new Label3D
        {
            Name = "MountPrompt",
            Text = $"{displayName} · E POUR MONTER",
            Position = new Vector3(0, 2.45f, 0),
            Modulate = new Color("d8e9cf"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 32,
            OutlineSize = 8,
        };
        mount.AddChild(prompt);
        mount.ConfigureInteractionPrompt(prompt);
        _collisionCount++;
        return mount;
    }

    private void BuildProceduralCreatureVisual(Node3D root, string species, Color fallback)
    {
        var gnoll = string.Equals(species, "gnoll", StringComparison.OrdinalIgnoreCase);
        var skin = gnoll ? new Color("8f7550") : new Color("71934a");
        var cloth = gnoll ? new Color("49352c") : new Color("553c2e");
        Add(new CapsuleMesh { Radius = gnoll ? .42f : .34f, Height = gnoll ? 1.25f : 1.05f },
            new Vector3(0, .72f, 0), Vector3.Zero, skin);
        Add(new SphereMesh { Radius = gnoll ? .34f : .3f, Height = gnoll ? .68f : .6f },
            new Vector3(0, gnoll ? 1.48f : 1.3f, 0), Vector3.Zero, skin);
        Add(new BoxMesh { Size = new Vector3(gnoll ? .32f : .22f, .18f, gnoll ? .48f : .28f) },
            new Vector3(0, gnoll ? 1.42f : 1.28f, -.3f), Vector3.Zero, skin.Lightened(.08f));
        Add(new CylinderMesh { TopRadius = .025f, BottomRadius = gnoll ? .16f : .2f, Height = .46f },
            new Vector3(-.28f, gnoll ? 1.68f : 1.5f, 0), new Vector3(0, 0, gnoll ? -22 : -48), skin);
        Add(new CylinderMesh { TopRadius = .025f, BottomRadius = gnoll ? .16f : .2f, Height = .46f },
            new Vector3(.28f, gnoll ? 1.68f : 1.5f, 0), new Vector3(0, 0, gnoll ? 22 : 48), skin);
        Add(new BoxMesh { Size = new Vector3(gnoll ? .82f : .68f, .48f, .42f) },
            new Vector3(0, .85f, .02f), Vector3.Zero, cloth);
        foreach (var x in new[] { -.24f, .24f })
        {
            Add(new CylinderMesh { TopRadius = .11f, BottomRadius = .13f, Height = .72f },
                new Vector3(x, .18f, 0), Vector3.Zero, skin.Darkened(.08f));
            Add(new CylinderMesh { TopRadius = .09f, BottomRadius = .12f, Height = .72f },
                new Vector3(x * 1.7f, .78f, 0), new Vector3(0, 0, x < 0 ? -22 : 22), skin);
        }
        if (gnoll)
        {
            Add(new CylinderMesh { TopRadius = .04f, BottomRadius = .12f, Height = .9f },
                new Vector3(0, .65f, .42f), new Vector3(62, 0, 0), skin.Darkened(.12f));
        }
        Add(new CylinderMesh { TopRadius = .055f, BottomRadius = .07f, Height = gnoll ? 1.5f : 1.05f },
            new Vector3(.62f, .72f, 0), new Vector3(0, 0, 12), fallback.Darkened(.28f));

        void Add(PrimitiveMesh mesh, Vector3 position, Vector3 rotation, Color partColor)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = mesh,
                Position = position,
                RotationDegrees = rotation,
                MaterialOverride = CreateMaterial(partColor),
            });
        }
    }

    private static Color PresentationColor(CreaturePresentationProfile? presentation, Color fallback)
    {
        var value = presentation?.MaterialColors.FirstOrDefault(item => item.ParameterId == "primary")?.HexColor;
        return string.IsNullOrWhiteSpace(value) ? fallback : Color.FromString(value, fallback);
    }

    private static string ProgressionDisplayName(BlueprintDefinition blueprint)
    {
        var level = ResolveProgression(blueprint).TotalLevel;
        return level > 0 ? $"{blueprint.DisplayName} · Niv. {level}" : blueprint.DisplayName;
    }

    private static CreatureProgressionRuntimeSnapshot ResolveProgression(BlueprintDefinition blueprint)
    {
        var abilities = (blueprint.Combat?.Abilities ?? []).Select(item => item.Slug)
            .ToHashSet(StringComparer.Ordinal);
        var result = CreatureProgressionRuntimeResolver.Resolve(
            blueprint.CreatureProgression,
            abilities,
            blueprint.CreatureProgression?.RequiredAtRuntime == true);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        }

        return result;
    }

    private void AddPresentationProxies(Node3D root, CreaturePresentationProfile? presentation)
    {
        if (presentation is null) return;
        var color = PresentationColor(presentation, new Color("d68a3a")).Lightened(0.14f);
        foreach (var attachment in presentation.Attachments)
        {
            switch (attachment.Kind)
            {
                case "wings":
                    Add(new BoxMesh { Size = new Vector3(1.15f, 0.6f, 0.08f) }, new Vector3(-0.62f, 0.68f, 0.22f), new Vector3(0, 0, 28));
                    Add(new BoxMesh { Size = new Vector3(1.15f, 0.6f, 0.08f) }, new Vector3(0.62f, 0.68f, 0.22f), new Vector3(0, 0, -28));
                    break;
                case "tail":
                    Add(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.14f, Height = 1.1f }, new Vector3(0, 0.15f, 0.48f), new Vector3(65, 0, 0));
                    break;
            }
        }

        void Add(PrimitiveMesh mesh, Vector3 position, Vector3 rotation)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = mesh,
                Position = position,
                RotationDegrees = rotation,
                MaterialOverride = CreateMaterial(color),
            });
        }
    }

    private Node3D CreateItem(EntityInstance entity, BlueprintDefinition blueprint)
    {
        var displayName = blueprint.DisplayName;
        var itemSlug = blueprint.Slug;
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);
        if (!TryAttachVisual(root, entity))
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f },
                MaterialOverride = CreateMaterial(new Color("ffca55"), emission: true),
            });
        }
        var clickTarget = new Area3D
        {
            Name = "ItemClickTarget",
            CollisionLayer = 1,
            CollisionMask = 1,
            InputRayPickable = true,
            Monitoring = false,
            Monitorable = true,
        };
        clickTarget.AddChild(new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = .65f },
        });
        clickTarget.MouseEntered += () => Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
        clickTarget.MouseExited += () => Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        root.AddChild(clickTarget);
        _questItemSlug = GetOverride(entity, "runtime.item") ?? itemSlug;
        _questItemDisplayName = GetOverride(entity, "runtime.displayName") ?? displayName;
        _keyItemBlueprint = blueprint;
        if (blueprint.ItemAdvanced is { } advanced)
        {
            var diagnostics = ItemAdvancedValidator.Validate(advanced, $"blueprints/{blueprint.Id}/itemAdvanced");
            if (diagnostics.Count > 0)
                throw new InvalidOperationException($"Advanced item '{blueprint.Slug}' is invalid: {diagnostics[0].Code}.");
            GD.Print($"OPENRPG_FORGE_RUNTIME_ITEM_ADVANCED_LOADED profile={advanced.Id:D} " +
                     $"weapon={(advanced.Weapon is not null).ToString().ToLowerInvariant()} " +
                     $"armor={(advanced.Armor is not null).ToString().ToLowerInvariant()} " +
                     $"container={(advanced.Container is not null).ToString().ToLowerInvariant()} " +
                     $"recipes={advanced.Recipes.Count} procedural={(advanced.Generator?.Enabled == true).ToString().ToLowerInvariant()}");
        }
        CreateLabel(root, displayName, new Color("ffe6a0"), 0.9f);
        return root;
    }

    private Node3D CreateExit(EntityInstance entity, bool isPortal)
    {
        var displayName = GetOverride(entity, "runtime.displayName") ?? "Passage";
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        root.AddToGroup("forge_exit_marker");
        AddChild(root);
        if (isPortal)
        {
            var innDoor = string.Equals(_activeArea?.Slug, "ember-inn", StringComparison.Ordinal);
            var clickTarget = new Area3D
            {
                Name = "PortalClickTarget",
                Position = innDoor ? new Vector3(-2, 2, 1) : new Vector3(0, 1.5f, 0),
                CollisionLayer = 1,
                CollisionMask = 1,
                InputRayPickable = true,
                Monitoring = false,
                Monitorable = true,
            };
            clickTarget.AddToGroup("forge_portal_click_target");
            clickTarget.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D
                {
                    Size = innDoor ? new Vector3(6, 4.2f, 1.6f) : new Vector3(3, 3, 3),
                },
            });
            clickTarget.MouseEntered += () => Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
            clickTarget.MouseExited += () => Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            root.AddChild(clickTarget);
            if (innDoor) return root;
        }
        TryAttachVisual(root, entity);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 0.1f },
            MaterialOverride = CreateMaterial(new Color("55efc4"), emission: true),
        });
        CreateLabel(root, displayName, new Color("b9fff0"), 1.1f);
        return root;
    }


}
