using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorldViewport
{
    private const float HumanPickingScreenPadding = 10f;

    private sealed record HumanHitProxy(
        EntityId EntityId,
        BlueprintKind Kind,
        string DisplayName,
        Aabb Bounds,
        bool BackgroundSurface,
        bool Ghosted);

    private readonly record struct HumanHitCandidate(
        EntityId EntityId,
        BlueprintKind Kind,
        bool BackgroundSurface,
        float Distance);

    private readonly Dictionary<EntityId, HumanHitProxy> _humanHitProxies = [];
    private MeshInstance3D _humanHoverBounds = null!;
    private MeshInstance3D _humanSelectionBounds = null!;
    private PanelContainer _humanHoverCard = null!;
    private Label _humanHoverLabel = null!;
    private PanelContainer _humanGhostBanner = null!;
    private Label _humanGhostLabel = null!;
    private EntityId? _humanHoveredEntityId;

    internal event Action? DisplayFilterResetRequested;

    private void InitializeHumanPicking()
    {
        _humanSelectionBounds = BuildHumanBoundsMarker("SelectionBounds");
        _world.AddChild(_humanSelectionBounds);
        _humanHoverBounds = BuildHumanBoundsMarker("HoverBounds");
        _world.AddChild(_humanHoverBounds);

        _humanHoverCard = StudioStyle.Panel("ViewportHoverCard", new Color(StudioStyle.Chrome, .97f), 6, 1);
        _humanHoverCard.Visible = false;
        _humanHoverCard.ZIndex = 95;
        _humanHoverCard.MouseFilter = MouseFilterEnum.Ignore;
        _humanHoverCard.CustomMinimumSize = new Vector2(270, 30);
        _humanHoverLabel = StudioStyle.Label(string.Empty, 11, StudioStyle.Text);
        _humanHoverCard.AddChild(_humanHoverLabel);
        AddChild(_humanHoverCard);

        _humanGhostBanner = StudioStyle.Panel("FilteredObjectsBanner", new Color(StudioStyle.Chrome, .97f), 7, 1);
        _humanGhostBanner.Visible = false;
        _humanGhostBanner.ZIndex = 84;
        _humanGhostBanner.MouseFilter = MouseFilterEnum.Stop;
        _humanGhostBanner.AnchorLeft = 1;
        _humanGhostBanner.AnchorRight = 1;
        _humanGhostBanner.OffsetLeft = -330;
        _humanGhostBanner.OffsetTop = 48;
        _humanGhostBanner.OffsetRight = -8;
        _humanGhostBanner.OffsetBottom = 84;
        var ghostRow = new HBoxContainer();
        ghostRow.AddThemeConstantOverride("separation", 7);
        _humanGhostLabel = StudioStyle.Label(string.Empty, 10, StudioStyle.Warning);
        _humanGhostLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ghostRow.AddChild(_humanGhostLabel);
        var showAll = StudioStyle.Button("Tout afficher", compact: true);
        showAll.TooltipText = "Réinitialiser les filtres qui rendent certains objets fantômes.";
        showAll.Pressed += () => DisplayFilterResetRequested?.Invoke();
        ghostRow.AddChild(showAll);
        _humanGhostBanner.AddChild(ghostRow);
        AddChild(_humanGhostBanner);

        MouseExited += ClearHumanPickingHover;
    }

    private static bool IsHumanPickable(bool matches, bool ghostHidden, bool readOnly)
        => !readOnly && (matches || ghostHidden);

    private void RefreshHumanPickingProxies()
    {
        _humanHitProxies.Clear();
        var project = _session?.Project;
        var area = ViewArea;
        if (project is null || area is null)
        {
            RefreshHumanGhostBanner();
            return;
        }

        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        foreach (var entity in area.Entities)
        {
            if (!_editableEntityIds.Contains(entity.Id) ||
                !_entityNodes.TryGetValue(entity.Id, out var node) ||
                !blueprints.TryGetValue(entity.BlueprintId, out var blueprint))
            {
                continue;
            }

            var bounds = ResolveHumanHitBounds(node, blueprint.Kind);
            _humanHitProxies[entity.Id] = new HumanHitProxy(
                entity.Id,
                blueprint.Kind,
                blueprint.DisplayName,
                bounds,
                IsHumanBackgroundSurface(blueprint.Kind, entity.Tags, bounds),
                IsHumanGhosted(node));
        }

        if (_humanHoveredEntityId is not null && !_humanHitProxies.ContainsKey(_humanHoveredEntityId))
        {
            ClearHumanPickingHover();
        }
        RefreshHumanGhostBanner();
    }

    private void ClearHumanPickingArea()
    {
        _humanHitProxies.Clear();
        _humanSelectionBounds.Visible = false;
        ClearHumanPickingHover();
        _humanGhostBanner.Visible = false;
    }

    private EntityId? FindPickedEntity(Vector2 pointer)
    {
        if (Size.X <= 0 || Size.Y <= 0)
        {
            return null;
        }

        var screen = ToViewportPointer(pointer);
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen).Normalized();
        var rayCandidates = new List<HumanHitCandidate>();
        if (direction.LengthSquared() > .000001f)
        {
            foreach (var proxy in _humanHitProxies.Values)
            {
                if (TryIntersectHumanRay(proxy.Bounds, origin, direction, out var distance) &&
                    distance <= _camera.Far)
                {
                    rayCandidates.Add(new HumanHitCandidate(
                        proxy.EntityId,
                        proxy.Kind,
                        proxy.BackgroundSurface,
                        distance));
                }
            }
        }

        return ChooseHumanHit(rayCandidates) ??
               FindHumanScreenBoundsEntity(screen) ??
               FindNearestEntity(pointer);
    }

    private EntityId? FindHumanScreenBoundsEntity(Vector2 screen)
    {
        var scale = _viewport.Size.X / Math.Max(Size.X, 1);
        var padding = Math.Clamp(HumanPickingScreenPadding * scale, 6, 18);
        var candidates = new List<HumanHitCandidate>();
        foreach (var proxy in _humanHitProxies.Values)
        {
            if (!TryProjectHumanBounds(proxy.Bounds, out var projected) ||
                !IsHumanScreenFallbackHit(projected, screen, padding))
            {
                continue;
            }

            candidates.Add(new HumanHitCandidate(
                proxy.EntityId,
                proxy.Kind,
                proxy.BackgroundSurface,
                projected.GetCenter().DistanceSquaredTo(screen)));
        }
        return ChooseHumanHit(candidates);
    }

    private static EntityId? ChooseHumanHit(IEnumerable<HumanHitCandidate> candidates)
        => candidates
            .OrderBy(candidate => candidate.BackgroundSurface)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => HumanKindPriority(candidate.Kind))
            .ThenBy(candidate => candidate.EntityId.Value)
            .Select(candidate => candidate.EntityId)
            .FirstOrDefault();

    private static int HumanKindPriority(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Creature or BlueprintKind.PlayerSpawn => 0,
        BlueprintKind.Door or BlueprintKind.Item => 1,
        BlueprintKind.Trigger => 2,
        _ => 3,
    };

    private bool TryProjectHumanBounds(Aabb bounds, out Rect2 projected)
    {
        var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var count = 0;
        foreach (var corner in HumanBoundsCorners(bounds))
        {
            if (_camera.IsPositionBehind(corner))
            {
                continue;
            }
            var point = _camera.UnprojectPosition(corner);
            minimum = new Vector2(Math.Min(minimum.X, point.X), Math.Min(minimum.Y, point.Y));
            maximum = new Vector2(Math.Max(maximum.X, point.X), Math.Max(maximum.Y, point.Y));
            count++;
        }

        projected = count == 0
            ? default
            : new Rect2(minimum, maximum - minimum);
        return count > 0;
    }

    private static bool IsHumanScreenFallbackHit(Rect2 projected, Vector2 pointer, float padding)
    {
        var padded = new Rect2(
            projected.Position - Vector2.One * padding,
            projected.Size + Vector2.One * padding * 2);
        return padded.HasPoint(pointer);
    }

    private static bool TryIntersectHumanRay(
        Aabb bounds,
        Vector3 origin,
        Vector3 direction,
        out float distance)
    {
        var minimum = bounds.Position;
        var maximum = bounds.Position + bounds.Size;
        var near = 0f;
        var far = float.PositiveInfinity;
        for (var axis = 0; axis < 3; axis++)
        {
            var rayOrigin = HumanComponent(origin, axis);
            var rayDirection = HumanComponent(direction, axis);
            var slabMinimum = HumanComponent(minimum, axis);
            var slabMaximum = HumanComponent(maximum, axis);
            if (MathF.Abs(rayDirection) < .000001f)
            {
                if (rayOrigin < slabMinimum || rayOrigin > slabMaximum)
                {
                    distance = 0;
                    return false;
                }
                continue;
            }

            var first = (slabMinimum - rayOrigin) / rayDirection;
            var second = (slabMaximum - rayOrigin) / rayDirection;
            if (first > second)
            {
                (first, second) = (second, first);
            }
            near = Math.Max(near, first);
            far = Math.Min(far, second);
            if (near > far)
            {
                distance = 0;
                return false;
            }
        }

        distance = near;
        return far >= 0 && float.IsFinite(distance);
    }

    private static float HumanComponent(Vector3 value, int axis) => axis switch
    {
        0 => value.X,
        1 => value.Y,
        _ => value.Z,
    };

    private static Aabb ResolveHumanHitBounds(Node3D root, BlueprintKind kind)
    {
        Aabb? merged = null;
        foreach (var mesh in EnumerateHumanMeshes(root))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }
            var candidate = ResolveHumanNodeTransform(mesh) * mesh.Mesh.GetAabb();
            if (!IsFiniteHumanBounds(candidate))
            {
                continue;
            }
            merged = merged is null ? candidate : merged.Value.Merge(candidate);
        }

        var minimum = HumanMinimumHitSize(kind);
        var rootPosition = ResolveHumanNodeTransform(root).Origin;
        var source = merged ?? new Aabb(rootPosition - minimum * .5f, minimum);
        var center = source.Position + source.Size * .5f;
        var size = new Vector3(
            Math.Max(MathF.Abs(source.Size.X), minimum.X),
            Math.Max(MathF.Abs(source.Size.Y), minimum.Y),
            Math.Max(MathF.Abs(source.Size.Z), minimum.Z));
        return new Aabb(center - size * .5f, size);
    }

    private static IEnumerable<MeshInstance3D> EnumerateHumanMeshes(Node root)
    {
        if (root is MeshInstance3D mesh)
        {
            // Invisible fallback previews remain valid picking geometry when an art-pass mesh replaces them.
            yield return mesh;
        }
        foreach (Node child in root.GetChildren())
        {
            foreach (var nested in EnumerateHumanMeshes(child))
            {
                yield return nested;
            }
        }
    }

    private static Transform3D ResolveHumanNodeTransform(Node3D node)
    {
        var transform = node.Transform;
        var parent = node.GetParent() as Node3D;
        while (parent is not null)
        {
            transform = parent.Transform * transform;
            parent = parent.GetParent() as Node3D;
        }
        return transform;
    }

    private static bool IsFiniteHumanBounds(Aabb bounds)
        => float.IsFinite(bounds.Position.X) && float.IsFinite(bounds.Position.Y) &&
           float.IsFinite(bounds.Position.Z) && float.IsFinite(bounds.Size.X) &&
           float.IsFinite(bounds.Size.Y) && float.IsFinite(bounds.Size.Z);

    private static Vector3 HumanMinimumHitSize(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Creature or BlueprintKind.PlayerSpawn => new Vector3(1.25f, 2f, 1.25f),
        BlueprintKind.Door => new Vector3(1.35f, 2.5f, .65f),
        BlueprintKind.Item => new Vector3(1f, 1f, 1f),
        BlueprintKind.Trigger => new Vector3(1.2f, .2f, 1.2f),
        _ => new Vector3(.75f, .35f, .75f),
    };

    private static bool IsHumanBackgroundSurface(
        BlueprintKind kind,
        IReadOnlyList<string> tags,
        Aabb bounds)
    {
        if (kind != BlueprintKind.Placeable)
        {
            return false;
        }
        if (tags.Any(tag => tag.Equals("floor", StringComparison.OrdinalIgnoreCase) ||
                            tag.Equals("ground", StringComparison.OrdinalIgnoreCase) ||
                            tag.Equals("terrain", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return bounds.Size.X * bounds.Size.Z >= 25 &&
               Math.Max(bounds.Size.X, bounds.Size.Z) >= 6 &&
               bounds.Size.Y <= 2;
    }

    private static bool IsHumanGhosted(Node node)
    {
        if (node is GeometryInstance3D { Transparency: > .01f })
        {
            return true;
        }
        foreach (Node child in node.GetChildren())
        {
            if (IsHumanGhosted(child))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<Vector3> HumanBoundsCorners(Aabb bounds)
    {
        var start = bounds.Position;
        var end = bounds.Position + bounds.Size;
        yield return new Vector3(start.X, start.Y, start.Z);
        yield return new Vector3(end.X, start.Y, start.Z);
        yield return new Vector3(end.X, start.Y, end.Z);
        yield return new Vector3(start.X, start.Y, end.Z);
        yield return new Vector3(start.X, end.Y, start.Z);
        yield return new Vector3(end.X, end.Y, start.Z);
        yield return new Vector3(end.X, end.Y, end.Z);
        yield return new Vector3(start.X, end.Y, end.Z);
    }

    private void UpdateHumanPickingHover(Vector2 pointer)
    {
        if (_readOnly || GizmoDragActive)
        {
            ClearHumanPickingHover();
            return;
        }

        var entityId = FindPickedEntity(pointer);
        if (entityId is null || !_humanHitProxies.TryGetValue(entityId, out var proxy))
        {
            ClearHumanPickingHover();
            return;
        }

        if (_humanHoveredEntityId != entityId)
        {
            _humanHoveredEntityId = entityId;
            var filtered = proxy.Ghosted ? " · filtré, sélectionnable" : string.Empty;
            _humanHoverLabel.Text = $"{HumanKindName(proxy.Kind)} · {proxy.DisplayName}{filtered}";
            _humanHoverLabel.AddThemeColorOverride(
                "font_color",
                proxy.Ghosted ? StudioStyle.Warning : StudioStyle.Text);
            ShowHumanBounds(
                _humanHoverBounds,
                HumanDisplayBounds(proxy),
                proxy.Ghosted ? StudioStyle.Warning : StudioStyle.Success,
                .9f);
            _humanHoverCard.Visible = true;
        }
        PositionHumanHoverCard(pointer);
    }

    private void PositionHumanHoverCard(Vector2 pointer)
    {
        const float width = 300;
        const float height = 32;
        _humanHoverCard.Size = new Vector2(width, height);
        _humanHoverCard.Position = new Vector2(
            Math.Clamp(pointer.X + 15, 8, Math.Max(8, Size.X - width - 8)),
            Math.Clamp(pointer.Y + 17, 48, Math.Max(48, Size.Y - height - 8)));
    }

    private void ClearHumanPickingHover()
    {
        _humanHoveredEntityId = null;
        if (_humanHoverBounds is not null)
        {
            _humanHoverBounds.Visible = false;
        }
        if (_humanHoverCard is not null)
        {
            _humanHoverCard.Visible = false;
        }
    }

    private void RefreshHumanGhostBanner()
    {
        if (_humanGhostBanner is null)
        {
            return;
        }
        _humanGhostBanner.Visible = !_readOnly && GhostedEntityCount > 0;
        _humanGhostLabel.Text = GhostedEntityCount == 1
            ? "1 objet est filtré"
            : $"{GhostedEntityCount} objets sont filtrés";
    }

    private static string HumanKindName(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Creature => "Créature",
        BlueprintKind.PlayerSpawn => "Personnage",
        BlueprintKind.Door => "Porte",
        BlueprintKind.Item => "Objet",
        BlueprintKind.Trigger => "Déclencheur",
        _ => "Décor",
    };

    private static MeshInstance3D BuildHumanBoundsMarker(string name) => new()
    {
        Name = name,
        Visible = false,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    private static void ShowHumanBounds(
        MeshInstance3D marker,
        Aabb source,
        Color color,
        float alpha)
    {
        const float margin = .035f;
        var bounds = new Aabb(
            source.Position - Vector3.One * margin,
            source.Size + Vector3.One * margin * 2);
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(color, alpha),
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = 1.45f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            NoDepthTest = true,
        };
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        AddOverlayBox(
            mesh,
            new Vector3(
                bounds.Position.X + bounds.Size.X * .5f,
                bounds.Position.Y,
                bounds.Position.Z + bounds.Size.Z * .5f),
            bounds.Size,
            new Color(color, alpha));
        mesh.SurfaceEnd();
        marker.Mesh = mesh;
        marker.Visible = true;
    }

    private void PlaceHumanSelectionMarker(
        MeshInstance3D marker,
        EntityId entityId,
        Node3D fallbackNode,
        bool primary)
    {
        if (!_humanHitProxies.TryGetValue(entityId, out var proxy))
        {
            marker.Position = ResolveHumanNodeTransform(fallbackNode).Origin +
                              new Vector3(0, primary ? .035f : .03f, 0);
            return;
        }

        var radius = HumanMarkerRadius(proxy, primary);
        if (marker.Mesh is CylinderMesh cylinder)
        {
            cylinder.TopRadius = radius;
            cylinder.BottomRadius = radius;
        }
        if (marker.MaterialOverride is StandardMaterial3D material)
        {
            material.NoDepthTest = true;
        }
        marker.Position = new Vector3(
            proxy.Bounds.Position.X + proxy.Bounds.Size.X * .5f,
            HumanMarkerHeight(proxy, ResolveHumanNodeTransform(fallbackNode).Origin.Y) +
            (primary ? .035f : .03f),
            proxy.Bounds.Position.Z + proxy.Bounds.Size.Z * .5f);
    }

    private static float HumanMarkerRadius(HumanHitProxy proxy, bool primary)
    {
        var footprint = MathF.Sqrt(Math.Max(.01f, proxy.Bounds.Size.X * proxy.Bounds.Size.Z));
        var radius = proxy.BackgroundSurface
            ? Math.Clamp(footprint * .12f, .9f, 4f)
            : proxy.Kind switch
            {
                BlueprintKind.Creature or BlueprintKind.PlayerSpawn => .72f,
                BlueprintKind.Door => .9f,
                BlueprintKind.Item => .55f,
                _ => Math.Clamp(Math.Max(proxy.Bounds.Size.X, proxy.Bounds.Size.Z) * .42f, .55f, 1.4f),
            };
        return primary ? radius : radius * .82f;
    }

    private static Aabb HumanDisplayBounds(HumanHitProxy proxy)
    {
        var maximum = proxy.Kind switch
        {
            BlueprintKind.Creature or BlueprintKind.PlayerSpawn => new Vector3(2.4f, 3.4f, 2.4f),
            BlueprintKind.Door => new Vector3(3f, 4.5f, 1.5f),
            BlueprintKind.Item => new Vector3(1.8f, 1.8f, 1.8f),
            _ => new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
        };
        var size = new Vector3(
            Math.Min(proxy.Bounds.Size.X, maximum.X),
            Math.Min(proxy.Bounds.Size.Y, maximum.Y),
            Math.Min(proxy.Bounds.Size.Z, maximum.Z));
        var center = proxy.Bounds.Position + proxy.Bounds.Size * .5f;
        return new Aabb(center - size * .5f, size);
    }

    private static float HumanMarkerHeight(HumanHitProxy proxy, float entityRootHeight)
        => proxy.BackgroundSurface
            ? proxy.Bounds.Position.Y + proxy.Bounds.Size.Y
            : entityRootHeight;

    private void RefreshHumanSelectionBounds(EntityId entityId)
    {
        if (_humanHitProxies.TryGetValue(entityId, out var proxy))
        {
            ShowHumanBounds(_humanSelectionBounds, HumanDisplayBounds(proxy), StudioStyle.Accent, .95f);
        }
        else
        {
            _humanSelectionBounds.Visible = false;
        }
    }

    private void HideHumanSelectionBounds()
        => _humanSelectionBounds.Visible = false;

    internal bool RunHumanPickingRegressionEvidence()
    {
        var floorId = EntityId.From(Guid.Parse("0f000000-0000-4000-8000-000000000001"));
        var creatureId = EntityId.From(Guid.Parse("0f000000-0000-4000-8000-000000000002"));
        var priority = ChooseHumanHit(
            [
                new HumanHitCandidate(floorId, BlueprintKind.Placeable, true, 1),
                new HumanHitCandidate(creatureId, BlueprintKind.Creature, false, 12),
            ]) == creatureId;
        var rayAabb = TryIntersectHumanRay(
            new Aabb(new Vector3(-.6f, 0, -.6f), new Vector3(1.2f, 2f, 1.2f)),
            new Vector3(0, 1, 5),
            Vector3.Forward,
            out var rayDistance) && rayDistance > 0;
        var screenFallback = IsHumanScreenFallbackHit(
            new Rect2(new Vector2(100, 100), new Vector2(4, 4)),
            new Vector2(95, 102),
            HumanPickingScreenPadding);
        var ghostPickable = IsHumanPickable(matches: false, ghostHidden: true, readOnly: false) &&
                            !IsHumanPickable(matches: false, ghostHidden: false, readOnly: false);
        var floorProxy = new HumanHitProxy(
            floorId,
            BlueprintKind.Placeable,
            "Sol test",
            new Aabb(new Vector3(-20, -.5f, -6), new Vector3(40, 1, 12)),
            BackgroundSurface: true,
            Ghosted: false);
        var boundsAware = HumanMarkerRadius(floorProxy, primary: true) > .82f &&
                          HumanMarkerHeight(floorProxy, 0) > floorProxy.Bounds.Position.Y;
        var proxyCoverage = _editableEntityIds.All(_humanHitProxies.ContainsKey);
        var valid = priority && rayAabb && screenFallback && ghostPickable && boundsAware && proxyCoverage;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_PICKER_{(valid ? "PASS" : "FAILED")} " +
            $"aabb={rayAabb.ToString().ToLowerInvariant()} " +
            $"interactive_priority={priority.ToString().ToLowerInvariant()} " +
            $"screen_fallback={screenFallback.ToString().ToLowerInvariant()} " +
            $"ghost_pickable={ghostPickable.ToString().ToLowerInvariant()} " +
            $"bounds_marker={boundsAware.ToString().ToLowerInvariant()} " +
            $"proxy_coverage={proxyCoverage.ToString().ToLowerInvariant()} proxies={_humanHitProxies.Count}");
        return valid;
    }
}
