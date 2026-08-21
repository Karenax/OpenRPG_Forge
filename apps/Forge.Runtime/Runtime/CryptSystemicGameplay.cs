using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Forge.Runtime;

/// <summary>
/// Module-scoped systemic gameplay director for the Crypte des Cendres.
/// It deliberately reuses GameRoot combat and quest resolution instead of
/// maintaining a second environmental health model.
/// </summary>
public partial class CryptSystemicGameplay : Node3D
{
    private const string ManifestPath = "res://content/crypt-of-ashes/systemic-gameplay.json";
    private const float KickRange = 4.2f;
    private const float ForceRange = 13f;
    private const float MinimumAlignment = -.15f;

    private readonly Dictionary<string, InteractionSpec> _specs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Node3D> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MeshInstance3D> _meshes = new(StringComparer.Ordinal);
    private GameRoot? _root;
    private bool _active;
    private bool _oilSpilled;
    private bool _oilIgnited;
    private bool _waterFrozen;
    private bool _brazierReleased;
    private bool _pillarCollapsed;
    private bool _focusActive;
    private double _previousTimeScale = 1;
    private double _oilTickElapsed;
    private double _spikeTickElapsed;

    public override async void _Ready()
    {
        SetProcess(false);
        _root = GetParent() as GameRoot;
        if (_root is null)
        {
            GD.PushError("OPENRPG_FORGE_CRYPT_SYSTEMIC_FAILED reason=missing-game-root");
            return;
        }

        for (var frame = 0; frame < 600 && !_root.IsSystemicRuntimeInitialized; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!_root.IsCryptSystemicModule)
        {
            GD.Print("OPENRPG_FORGE_CRYPT_SYSTEMIC_SKIPPED reason=other-module");
            return;
        }

        if (!_root.IsCryptSystemicGameplayReady)
        {
            GD.PushWarning("OPENRPG_FORGE_CRYPT_SYSTEMIC_SKIPPED reason=runtime-timeout");
            return;
        }

        try
        {
            LoadManifest();
            RegisterControls();
            BuildInteractions();
            _active = true;
            SetProcess(true);
            _root.PresentSystemicMessage(
                "La Crypte réagit désormais au feu, au givre, à la force et aux poussées.",
                "ready");
            GD.Print(
                $"OPENRPG_FORGE_CRYPT_SYSTEMIC_READY ruleset=openrpg-forge.systemic-rpg.v1 " +
                $"interactions={_specs.Count} controls=5");
        }
        catch (Exception exception)
        {
            GD.PushError($"OPENRPG_FORGE_CRYPT_SYSTEMIC_FAILED {exception}");
        }
    }

    public override void _Process(double delta)
    {
        if (!_active || _root is null || _root.SystemicPlayer is null)
        {
            return;
        }

        UpdateTacticalFocus();
        if (Input.IsActionJustPressed("systemic_kick")) HandleKick();
        if (Input.IsActionJustPressed("systemic_fire")) HandleFire();
        if (Input.IsActionJustPressed("systemic_frost")) HandleFrost();
        if (Input.IsActionJustPressed("systemic_force")) HandleForce();

        TickOil(delta);
        TickSpikes(delta);
    }

    public override void _ExitTree() => RestoreTacticalFocus();

    private void LoadManifest()
    {
        if (!Godot.FileAccess.FileExists(ManifestPath))
        {
            throw new InvalidOperationException($"Missing systemic manifest: {ManifestPath}.");
        }

        using var document = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(ManifestPath));
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
            root.GetProperty("moduleSlug").GetString() != "crypt-of-ashes" ||
            root.GetProperty("rulesetId").GetString() != "openrpg-forge.systemic-rpg.v1" ||
            !root.GetProperty("worldAsWeapon").GetBoolean())
        {
            throw new InvalidOperationException("The systemic manifest does not match the Crypt ruleset contract.");
        }

        foreach (var element in root.GetProperty("interactions").EnumerateArray())
        {
            var position = element.GetProperty("position");
            var spec = new InteractionSpec(
                element.GetProperty("id").GetString() ?? string.Empty,
                element.GetProperty("kind").GetString() ?? string.Empty,
                element.GetProperty("displayName").GetString() ?? string.Empty,
                new Vector3(
                    position.GetProperty("x").GetSingle(),
                    position.GetProperty("y").GetSingle(),
                    position.GetProperty("z").GetSingle()),
                element.GetProperty("radius").GetSingle(),
                element.GetProperty("damageStrikes").GetInt32(),
                element.GetProperty("force").GetSingle(),
                element.GetProperty("verbs").EnumerateArray()
                    .Select(value => value.GetString() ?? string.Empty)
                    .Where(value => value.Length > 0)
                    .ToArray());
            spec.Validate();
            if (!_specs.TryAdd(spec.Id, spec))
            {
                throw new InvalidOperationException($"Duplicate systemic interaction '{spec.Id}'.");
            }
        }

        var expectedKinds = new HashSet<string>(["oil", "water", "brazier", "pillar", "spikes"],
            StringComparer.Ordinal);
        if (_specs.Count != 5 || !expectedKinds.SetEquals(_specs.Values.Select(spec => spec.Kind)))
        {
            throw new InvalidOperationException("The Crypt must declare oil, water, brazier, pillar and spikes.");
        }
    }

    private static void RegisterControls()
    {
        RegisterAction("systemic_kick", Key.X);
        RegisterAction("systemic_fire", Key.F);
        RegisterAction("systemic_frost", Key.G);
        RegisterAction("systemic_force", Key.B);
        RegisterAction("systemic_focus", Key.Y);
    }

    private static void RegisterAction(string action, Key key)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        if (InputMap.ActionGetEvents(action).OfType<InputEventKey>()
            .Any(input => input.PhysicalKeycode == key)) return;
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private void BuildInteractions()
    {
        foreach (var spec in _specs.Values)
        {
            var node = new Node3D { Name = $"Systemic_{spec.Id}" };
            AddChild(node);
            node.Position = spec.Position;

            var mesh = new MeshInstance3D
            {
                Name = "Visual",
                Mesh = CreateMesh(spec.Kind),
                MaterialOverride = CreateMaterial(BaseColor(spec.Kind)),
            };
            node.AddChild(mesh);
            node.AddChild(new Label3D
            {
                Name = "Prompt",
                Text = Prompt(spec.Kind),
                Position = new Vector3(0, PromptHeight(spec.Kind), 0),
                Modulate = new Color("e8d8a8"),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 28,
                OutlineSize = 7,
                NoDepthTest = true,
            });

            _nodes.Add(spec.Id, node);
            _meshes.Add(spec.Id, mesh);
        }
    }

    private static PrimitiveMesh CreateMesh(string kind) => kind switch
    {
        "oil" => new CylinderMesh { TopRadius = .3f, BottomRadius = .48f, Height = .9f, RadialSegments = 18 },
        "water" => new BoxMesh { Size = new Vector3(3.8f, .08f, 1.8f) },
        "brazier" => new SphereMesh { Radius = .5f, Height = 1f, RadialSegments = 16, Rings = 8 },
        "pillar" => new BoxMesh { Size = new Vector3(.8f, 3f, .8f) },
        "spikes" => new BoxMesh { Size = new Vector3(2.6f, .12f, 2.2f) },
        _ => new BoxMesh { Size = Vector3.One },
    };

    private static Color BaseColor(string kind) => kind switch
    {
        "oil" => new Color("4d3514"),
        "water" => new Color("315a72"),
        "brazier" => new Color("d75b20"),
        "pillar" => new Color("665f58"),
        "spikes" => new Color("6f241d"),
        _ => Colors.White,
    };

    private static string Prompt(string kind) => kind switch
    {
        "oil" => "X · RENVERSER  |  F · ENFLAMMER",
        "water" => "G · GELER  |  X/B · POUSSER",
        "brazier" => "B · DÉTACHER LE BRASERO",
        "pillar" => "B · RENVERSER LE PILIER",
        "spikes" => "X/B · PROJETER DANS LES POINTES",
        _ => "INTERACTION",
    };

    private static float PromptHeight(string kind) => kind switch
    {
        "brazier" => 1.1f,
        "pillar" => 2.1f,
        _ => 1.25f,
    };

    private static StandardMaterial3D CreateMaterial(Color color, bool emission = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = .72f,
            Metallic = .08f,
        };
        if (emission)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 2.2f;
        }
        return material;
    }

    private void HandleKick()
    {
        var player = _root!.SystemicPlayer!;
        var oil = Find("oil");
        if (!_oilSpilled && player.GlobalPosition.DistanceTo(oil.Position) <= KickRange)
        {
            _oilSpilled = true;
            oil.Position += new Vector3(.55f, -.43f, -.25f);
            oil.Rotation = new Vector3(0, 0, 1.15f);
            _meshes[oil.Id].Scale = new Vector3(1.8f, .16f, 1.8f);
            _root.PresentSystemicMessage(
                "L'urne se brise : l'huile funéraire se répand sur les dalles.",
                "oil-spilled");
            return;
        }

        var hostile = _root.FindFocusedSystemicHostile(KickRange, MinimumAlignment);
        if (hostile is null)
        {
            _root.PresentSystemicMessage("Aucune cible ni urne à portée de poussée.", "kick-miss");
            return;
        }

        var direction = hostile.GlobalPosition - player.GlobalPosition;
        var force = _waterFrozen && IsInside(hostile.GlobalPosition, Find("water")) ? 8f : 4.6f;
        _root.PushSystemicHostile(hostile, direction, force);
    }

    private void HandleFire()
    {
        var oil = Find("oil");
        if (!_oilSpilled)
        {
            _root!.PresentSystemicMessage(
                "Le feu lèche l'urne, mais l'huile doit d'abord être répandue.",
                "fire-no-spill");
            return;
        }
        if (_oilIgnited) return;

        _oilIgnited = true;
        _meshes[oil.Id].MaterialOverride = CreateMaterial(new Color("ff6a1c"), emission: true);
        _root!.PresentSystemicMessage(
            "L'huile s'embrase et transforme le sol en arme.",
            "oil-ignited");
    }

    private void HandleFrost()
    {
        if (_waterFrozen) return;
        var water = Find("water");
        var player = _root!.SystemicPlayer!;
        if (player.GlobalPosition.DistanceTo(water.Position) > ForceRange)
        {
            _root.PresentSystemicMessage("La rigole est hors de portée du givre.", "frost-out-of-range");
            return;
        }

        _waterFrozen = true;
        _meshes[water.Id].MaterialOverride = CreateMaterial(new Color("a8e7ff"), emission: true);
        _root.PresentSystemicMessage(
            "La rigole gèle : les cibles qui la traversent perdent leur stabilité.",
            "water-frozen");
    }

    private void HandleForce()
    {
        var player = _root!.SystemicPlayer!;
        var brazier = Find("brazier");
        var pillar = Find("pillar");

        if (!_brazierReleased && player.GlobalPosition.DistanceTo(brazier.Position) <= ForceRange)
        {
            _brazierReleased = true;
            brazier.Position = new Vector3(brazier.Position.X, .55f, brazier.Position.Z);
            foreach (var hostile in _root.FindLiveSystemicHostiles(brazier.Position, brazier.Radius))
            {
                _root.DamageSystemicHostile(hostile, brazier.DamageStrikes, "la chute du brasero");
            }
            _root.PresentSystemicMessage(
                "La télékinésie rompt l'ancrage : le brasero s'écrase dans l'arène.",
                "brazier-released");
            return;
        }

        if (!_pillarCollapsed && player.GlobalPosition.DistanceTo(pillar.Position) <= ForceRange)
        {
            _pillarCollapsed = true;
            pillar.Position += new Vector3(1.4f, -1.05f, .3f);
            pillar.Rotation = new Vector3(0, 0, 1.35f);
            foreach (var hostile in _root.FindLiveSystemicHostiles(pillar.Position, pillar.Radius))
            {
                _root.DamageSystemicHostile(hostile, pillar.DamageStrikes, "l'effondrement du pilier");
            }
            _root.PresentSystemicMessage(
                "Le pilier fracturé s'effondre et coupe une partie de l'arène.",
                "pillar-collapsed");
            return;
        }

        var target = _root.FindFocusedSystemicHostile(ForceRange, MinimumAlignment);
        if (target is null)
        {
            _root.PresentSystemicMessage("Aucune cible systémique n'est alignée.", "force-miss");
            return;
        }

        var direction = target.GlobalPosition - player.GlobalPosition;
        var force = _waterFrozen && IsInside(target.GlobalPosition, Find("water")) ? 12f : 7f;
        _root.PushSystemicHostile(target, direction, force);
    }

    private void TickOil(double delta)
    {
        if (!_oilIgnited) return;
        _oilTickElapsed += delta;
        if (_oilTickElapsed < .65) return;
        _oilTickElapsed = 0;
        var oil = Find("oil");
        foreach (var hostile in _root!.FindLiveSystemicHostiles(oil.Position, oil.Radius))
        {
            _root.DamageSystemicHostile(hostile, Math.Max(1, oil.DamageStrikes), "l'huile enflammée");
        }
    }

    private void TickSpikes(double delta)
    {
        _spikeTickElapsed += delta;
        if (_spikeTickElapsed < .12) return;
        _spikeTickElapsed = 0;
        var spikes = Find("spikes");
        foreach (var hostile in _root!.FindLiveSystemicHostiles(spikes.Position, spikes.Radius))
        {
            _root.DefeatSystemicHostile(hostile, "une projection dans les pointes de cendre");
        }
    }

    private void UpdateTacticalFocus()
    {
        var requested = Input.IsActionPressed("systemic_focus");
        if (requested == _focusActive) return;

        if (requested)
        {
            if (Engine.TimeScale <= 0) return;
            _previousTimeScale = Engine.TimeScale;
            Engine.TimeScale = Math.Min(_previousTimeScale, .2);
            _focusActive = true;
            _root!.PresentSystemicMessage(
                "Concentration tactique : le temps ralentit pour lire l'arène.",
                "focus-enter");
        }
        else
        {
            RestoreTacticalFocus();
            _root!.PresentSystemicMessage(
                "Le temps reprend son rythme précédent.",
                "focus-exit");
        }
    }

    private void RestoreTacticalFocus()
    {
        if (!_focusActive) return;
        Engine.TimeScale = _previousTimeScale;
        _focusActive = false;
    }

    private InteractionSpec Find(string kind) => _specs.Values.Single(spec => spec.Kind == kind);

    private static bool IsInside(Vector3 position, InteractionSpec spec)
    {
        var offset = position - spec.Position;
        offset.Y = 0;
        return offset.Length() <= spec.Radius;
    }

    private sealed record InteractionSpec(
        string Id,
        string Kind,
        string DisplayName,
        Vector3 Position,
        float Radius,
        int DamageStrikes,
        float Force,
        IReadOnlyList<string> Verbs)
    {
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) ||
                string.IsNullOrWhiteSpace(Kind) ||
                string.IsNullOrWhiteSpace(DisplayName) ||
                !float.IsFinite(Radius) || Radius <= 0 ||
                !float.IsFinite(Force) || Force < 0 ||
                DamageStrikes < 0 ||
                Verbs.Count == 0)
            {
                throw new InvalidOperationException($"Invalid systemic interaction '{Id}'.");
            }
        }
    }
}
