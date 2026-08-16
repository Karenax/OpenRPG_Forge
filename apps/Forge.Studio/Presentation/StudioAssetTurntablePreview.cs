using Forge.Studio.Core.Assets;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAssetTurntablePreview : VBoxContainer
{
    private readonly SubViewport _viewport;
    private readonly Node3D _modelRoot;
    private readonly Label _state;
    private readonly Label _angle;
    private int _angleIndex;

    internal StudioAssetTurntablePreview()
    {
        AddThemeConstantOverride("separation", 5);
        var frame = StudioStyle.Panel("AssetPreview3D", StudioStyle.Background, 7);
        frame.CustomMinimumSize = new Vector2(0, 220);
        frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(frame);
        var container = new SubViewportContainer
        {
            Stretch = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        frame.AddChild(container);
        _viewport = new SubViewport
        {
            Size = new Vector2I(700, 520),
            OwnWorld3D = true,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        container.AddChild(_viewport);
        _modelRoot = new Node3D { Name = "AssetTurntableModel" };
        _viewport.AddChild(_modelRoot);
        _viewport.AddChild(new Camera3D
        {
            Position = new Vector3(0, .55f, 3.5f),
            Current = true,
            Fov = 48,
        });
        _viewport.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45, -35, 0),
            LightEnergy = 1.35f,
            ShadowEnabled = true,
        });
        _viewport.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-25, 145, 0),
            LightEnergy = .55f,
            LightColor = new Color("bfd8d1"),
        });
        _viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = StudioStyle.Background,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("a8b0a4"),
                AmbientLightEnergy = .72f,
            },
        });

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 4);
        AddChild(actions);
        var previous = StudioStyle.Button("↶ 45°", compact: true);
        previous.Pressed += () => Rotate(-1);
        actions.AddChild(previous);
        _angle = StudioStyle.Label("Aperçu 3D · 0°", 10, StudioStyle.Muted);
        _angle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _angle.HorizontalAlignment = HorizontalAlignment.Center;
        _angle.VerticalAlignment = VerticalAlignment.Center;
        actions.AddChild(_angle);
        var next = StudioStyle.Button("45° ↷", compact: true);
        next.Pressed += () => Rotate(1);
        actions.AddChild(next);
        _state = StudioStyle.Label("Sélectionnez un modèle.", 10, StudioStyle.Muted, wrap: true);
        AddChild(_state);
    }

    internal bool Load(StudioAssetLibraryItem item, string vaultRoot)
    {
        ClearModel();
        _angleIndex = 0;
        _modelRoot.Rotation = Vector3.Zero;
        _angle.Text = "Aperçu 3D · 0°";
        if (!item.AvailableLocally)
        {
            _state.Text = "Le fichier du modèle n'est pas présent dans le Vault local.";
            return false;
        }

        var candidates = item.Variants
            .Where(variant => variant.AvailableLocally && variant.Format is "glb" or "gltf")
            .OrderBy(variant => string.Equals(
                variant.RelativePath,
                item.RelativeModelPath,
                StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(variant => variant.Format == "glb" ? 0 : 1)
            .ThenBy(variant => variant.FileSize)
            .ToArray();
        if (candidates.Length == 0)
        {
            _state.Text = $"Aperçu 3D non disponible pour le format {item.Format.ToUpperInvariant()}.";
            return false;
        }

        var failures = new List<string>();
        var oversized = 0;
        foreach (var candidate in candidates)
        {
            if (candidate.FileSize > StudioAssetLibraryItem.MaximumInlinePreviewBytes)
            {
                oversized++;
                continue;
            }

            var absolute = ResolveContained(vaultRoot, candidate.RelativePath);
            if (absolute is null || !File.Exists(absolute))
            {
                failures.Add("chemin absent ou extérieur au Vault");
                continue;
            }

            Node3D? pivot = null;
            try
            {
                var document = new GltfDocument();
                var state = new GltfState();
                var error = document.AppendFromFile(absolute.Replace('\\', '/'), state);
                if (error != Error.Ok)
                {
                    failures.Add($"{candidate.Format.ToUpperInvariant()} illisible ({error})");
                    continue;
                }
                var model = document.GenerateScene(state) as Node3D;
                if (model is null)
                {
                    failures.Add($"{candidate.Format.ToUpperInvariant()} sans scène 3D");
                    continue;
                }

                pivot = new Node3D { Name = "PreviewPivot" };
                pivot.AddChild(model);
                _modelRoot.AddChild(pivot);
                if (!Fit(pivot))
                {
                    _modelRoot.RemoveChild(pivot);
                    pivot.QueueFree();
                    pivot = null;
                    failures.Add($"{candidate.Format.ToUpperInvariant()} sans géométrie visible");
                    continue;
                }

                var fallback = !string.Equals(
                    candidate.RelativePath,
                    item.RelativeModelPath,
                    StringComparison.OrdinalIgnoreCase);
                _state.Text = fallback
                    ? $"Modèle local · variante {candidate.Format.ToUpperInvariant()} de secours · tournez par pas de 45°."
                    : $"Modèle local · {candidate.Format.ToUpperInvariant()} · tournez par pas de 45°.";
                return true;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidDataException or
                    ArgumentException or InvalidOperationException)
            {
                if (pivot is not null)
                {
                    if (pivot.GetParent() == _modelRoot) _modelRoot.RemoveChild(pivot);
                    pivot.QueueFree();
                }
                failures.Add(exception.Message);
            }
        }

        if (oversized == candidates.Length)
        {
            _state.Text = "Aperçu 3D différé : toutes les variantes dépassent 128 Mio. La miniature reste disponible.";
        }
        else
        {
            _state.Text = failures.Count == 0
                ? "Aperçu 3D indisponible pour les variantes locales."
                : $"Aperçu 3D indisponible · {failures[0]}.";
        }
        return false;
    }

    private void Rotate(int delta)
    {
        _angleIndex = (_angleIndex + delta + 8) % 8;
        var degrees = _angleIndex * 45;
        _modelRoot.RotationDegrees = new Vector3(0, degrees, 0);
        _angle.Text = $"Aperçu 3D · {degrees}°";
    }

    private static bool Fit(Node3D pivot)
    {
        var meshes = FindMeshes(pivot).ToArray();
        if (meshes.Length == 0) return false;
        var inversePivot = pivot.GlobalTransform.AffineInverse();
        var firstTransform = inversePivot * meshes[0].GlobalTransform;
        var bounds = firstTransform * meshes[0].Mesh!.GetAabb();
        foreach (var mesh in meshes.Skip(1))
        {
            var relativeTransform = inversePivot * mesh.GlobalTransform;
            bounds = bounds.Merge(relativeTransform * mesh.Mesh!.GetAabb());
        }
        var longest = Math.Max(bounds.Size.X, Math.Max(bounds.Size.Y, bounds.Size.Z));
        if (!float.IsFinite(longest) || longest <= .0001f) return false;
        var scale = longest > .0001f ? 2.2f / longest : 1;
        pivot.Scale = Vector3.One * scale;
        pivot.Position = -(bounds.Position + bounds.Size / 2) * scale + new Vector3(0, .1f, 0);
        return true;
    }

    private static IEnumerable<MeshInstance3D> FindMeshes(Node root)
    {
        if (root is MeshInstance3D mesh && mesh.Mesh is not null) yield return mesh;
        foreach (Node child in root.GetChildren())
        {
            foreach (var nested in FindMeshes(child)) yield return nested;
        }
    }

    private void ClearModel()
    {
        foreach (Node child in _modelRoot.GetChildren())
        {
            _modelRoot.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string? ResolveContained(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) return null;
        var absoluteRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return candidate.StartsWith(absoluteRoot, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }
}
