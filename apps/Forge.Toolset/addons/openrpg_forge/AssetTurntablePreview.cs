#if TOOLS
using System.Globalization;
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssetTurntablePreview : VBoxContainer
{
    private readonly SubViewport _viewport;
    private readonly Node3D _modelRoot;
    private readonly Label _angle;
    private readonly Label _state;
    private AssetPreviewPlan? _plan;
    private string? _workspaceRoot;
    private int _angleIndex;

    public AssetTurntablePreview()
    {
        AddThemeConstantOverride("separation", 6);
        var container = new SubViewportContainer
        {
            Stretch = true,
            CustomMinimumSize = new Vector2(0, 190),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        AddChild(container);
        _viewport = new SubViewport
        {
            Size = new Vector2I(760, 380),
            OwnWorld3D = true,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        container.AddChild(_viewport);
        _modelRoot = new Node3D { Name = "AssetTurntableModel" };
        _viewport.AddChild(_modelRoot);
        _viewport.AddChild(new Camera3D { Position = new Vector3(0, 0.65f, 3.4f), Current = true });
        _viewport.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-42, -32, 0),
            LightEnergy = 1.4f,
            ShadowEnabled = true,
        });
        _viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("0c0a13"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("9a84b5"),
                AmbientLightEnergy = 0.75f,
            },
        });
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 6);
        AddChild(actions);
        var previous = new Button { Text = "↶ 45°" };
        previous.Pressed += () => Rotate(-1);
        actions.AddChild(previous);
        _angle = new Label
        {
            Text = "Turntable 0°",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        actions.AddChild(_angle);
        var next = new Button { Text = "45° ↷" };
        next.Pressed += () => Rotate(1);
        actions.AddChild(next);
        var thumbnail = new Button { Text = "Générer miniature" };
        thumbnail.Pressed += SaveThumbnail;
        actions.AddChild(thumbnail);
        _state = new Label { Text = "Aucun modèle 3D", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _state.AddThemeColorOverride("font_color", new Color("aa9bc7"));
        AddChild(_state);
    }

    public bool Load(AssetCatalogItem item, string vaultRoot, string? workspaceRoot)
    {
        ClearModel();
        _plan = AssetPreviewPlanBuilder.Build(item);
        _workspaceRoot = workspaceRoot;
        _angleIndex = 0;
        _angle.Text = "Turntable 0°";
        if (!_plan.CanRender3D || string.IsNullOrWhiteSpace(item.RelativeModelPath) ||
            !AssetCatalogValidator.IsSafeRelativePath(item.RelativeModelPath))
        {
            _state.Text = "Turntable optionnel · modèle local absent, miniature catalogue conservée.";
            Visible = false;
            return false;
        }

        var absolute = Path.GetFullPath(Path.Combine(vaultRoot,
            item.RelativeModelPath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.GetFullPath(vaultRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolute))
        {
            _state.Text = "Turntable refusé · chemin de modèle hors Vault ou absent.";
            Visible = false;
            return false;
        }

        try
        {
            var model = Path.GetExtension(absolute).ToLowerInvariant() switch
            {
                ".obj" => LoadObj(absolute, ResolvePreviewTexturePath(item, vaultRoot)),
                ".glb" or ".gltf" => LoadGltf(absolute),
                _ => throw new InvalidDataException("Format de turntable non pris en charge."),
            };
            _modelRoot.AddChild(model);
            Fit(model);
            Visible = true;
            _state.Text = $"3D locale · 8 angles · variantes {_plan.Variants.Count} · animations {_plan.Animations.Count}";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ClearModel();
            _state.Text = $"Turntable indisponible · {exception.Message}";
            Visible = false;
            return false;
        }
    }

    private static Node3D LoadGltf(string path)
    {
        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromFile(path, state);
        if (error != Error.Ok) throw new InvalidDataException($"Import GLTF impossible ({error}).");
        return document.GenerateScene(state) as Node3D ??
            throw new InvalidDataException("Le GLTF ne contient aucune scène 3D.");
    }

    private static string? ResolvePreviewTexturePath(AssetCatalogItem item, string vaultRoot)
    {
        if (string.IsNullOrWhiteSpace(item.RelativePreviewPath) ||
            !AssetCatalogValidator.IsSafeRelativePath(item.RelativePreviewPath)) return null;
        var absolute = Path.GetFullPath(Path.Combine(vaultRoot,
            item.RelativePreviewPath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.GetFullPath(vaultRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && File.Exists(absolute) ? absolute : null;
    }

    private static Node3D LoadObj(string path, string? texturePath)
    {
        var positions = new List<Vector3>();
        var textureCoordinates = new List<Vector2>();
        var triangles = new List<(Vector3 Position, Vector2? Uv)>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                var values = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length >= 3 && TryFloat(values[0], out var x) && TryFloat(values[1], out var y) &&
                    TryFloat(values[2], out var z)) positions.Add(new Vector3(x, y, z));
            }
            else if (line.StartsWith("vt ", StringComparison.Ordinal))
            {
                var values = line[3..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (values.Length >= 2 && TryFloat(values[0], out var u) && TryFloat(values[1], out var v))
                    textureCoordinates.Add(new Vector2(u, 1f - v));
            }
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                var values = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var vertices = values.Select(value => ParseObjVertex(value, positions.Count, textureCoordinates.Count))
                    .ToArray();
                for (var i = 1; i + 1 < vertices.Length; i++)
                {
                    AddVertex(vertices[0]);
                    AddVertex(vertices[i]);
                    AddVertex(vertices[i + 1]);
                }
            }
            if (triangles.Count > 3_000_000)
                throw new InvalidDataException("Le modèle dépasse la limite d’aperçu de 1 000 000 triangles.");
        }
        if (triangles.Count == 0) throw new InvalidDataException("Le OBJ ne contient aucune face exploitable.");
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var vertex in triangles)
        {
            if (vertex.Uv.HasValue) surface.SetUV(vertex.Uv.Value);
            surface.AddVertex(vertex.Position);
        }
        surface.GenerateNormals();
        if (!string.IsNullOrWhiteSpace(texturePath))
        {
            var image = Image.LoadFromFile(texturePath);
            if (!image.IsEmpty())
                surface.SetMaterial(new StandardMaterial3D
                {
                    AlbedoTexture = ImageTexture.CreateFromImage(image),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                });
        }
        var mesh = surface.Commit() ?? throw new InvalidDataException("La géométrie OBJ n’a pas pu être générée.");
        return new MeshInstance3D { Mesh = mesh };

        void AddVertex((int Position, int? Uv) vertex)
        {
            triangles.Add((positions[vertex.Position],
                vertex.Uv.HasValue ? textureCoordinates[vertex.Uv.Value] : null));
        }
    }

    private static bool TryFloat(string value, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && float.IsFinite(result);

    private static (int Position, int? Uv) ParseObjVertex(string value, int positionCount, int uvCount)
    {
        var tokens = value.Split('/');
        var position = ParseObjIndex(tokens[0], positionCount);
        int? uv = tokens.Length > 1 && !string.IsNullOrWhiteSpace(tokens[1])
            ? ParseObjIndex(tokens[1], uvCount)
            : null;
        return (position, uv);
    }

    private static int ParseObjIndex(string token, int count)
    {
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index == 0)
            throw new InvalidDataException($"Indice OBJ invalide '{token}'.");
        index = index > 0 ? index - 1 : count + index;
        if (index < 0 || index >= count) throw new InvalidDataException($"Indice OBJ hors limites '{token}'.");
        return index;
    }

    private void Fit(Node3D model)
    {
        var meshes = FindMeshes(model).ToArray();
        if (meshes.Length == 0) return;
        var bounds = meshes[0].Mesh!.GetAabb();
        foreach (var mesh in meshes.Skip(1)) bounds = bounds.Merge(mesh.Mesh!.GetAabb());
        var longest = Math.Max(bounds.Size.X, Math.Max(bounds.Size.Y, bounds.Size.Z));
        var scale = longest > 0.0001f ? 1.8f / longest : 1f;
        model.Scale = Vector3.One * scale;
        model.Position = -(bounds.Position + bounds.Size / 2f) * scale + new Vector3(0, 0.15f, 0);
    }

    private static IEnumerable<MeshInstance3D> FindMeshes(Node root)
    {
        if (root is MeshInstance3D mesh && mesh.Mesh is not null) yield return mesh;
        foreach (Node child in root.GetChildren())
            foreach (var nested in FindMeshes(child)) yield return nested;
    }

    private void Rotate(int delta)
    {
        if (_plan is null || _plan.TurntableAngles.Count == 0) return;
        _angleIndex = (_angleIndex + delta + _plan.TurntableAngles.Count) % _plan.TurntableAngles.Count;
        var degrees = _plan.TurntableAngles[_angleIndex];
        _modelRoot.RotationDegrees = new Vector3(0, degrees, 0);
        _angle.Text = $"Turntable {degrees}°";
    }

    private void SaveThumbnail()
    {
        if (_plan is null || string.IsNullOrWhiteSpace(_workspaceRoot) || !Visible) return;
        var path = Path.GetFullPath(Path.Combine(_workspaceRoot,
            _plan.ThumbnailRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.GetFullPath(_workspaceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var error = _viewport.GetTexture().GetImage().SavePng(path);
        _state.Text = error == Error.Ok ? $"Miniature dérivée · {_plan.ThumbnailRelativePath}" :
            $"Miniature refusée · {error}";
    }

    private void ClearModel()
    {
        foreach (Node child in _modelRoot.GetChildren()) child.QueueFree();
    }
}
#endif
