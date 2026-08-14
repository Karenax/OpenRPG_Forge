using Forge.Application.Areas;
using Forge.Domain.RuntimePolicies;
using Godot;

namespace Forge.Runtime;

public partial class RuntimeLoadingOverlay : CanvasLayer
{
    private readonly string _contentRoot;
    private TextureRect? _image;
    private VideoStreamPlayer? _video;
    private Label? _title;
    private Label? _tip;
    private Label? _accessible;
    private Label? _resource;
    private ProgressBar? _progress;

    public RuntimeLoadingOverlay(string contentRoot)
    {
        _contentRoot = contentRoot;
        Layer = 1000;
    }

    public override void _Ready()
    {
        var background = new LoadingBackdrop { MouseFilter = Control.MouseFilterEnum.Stop };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(720, 430) };
        panel.AddThemeStyleboxOverride("panel", RuntimeRpgUiTheme.Frame(
            new Color("120d0af4"), RuntimeRpgUiTheme.Border, 22, 20));
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 14);
        panel.AddChild(content);

        var ornament = new ColorRect
        {
            Color = RuntimeRpgUiTheme.Gold,
            CustomMinimumSize = new Vector2(0, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        content.AddChild(ornament);
        _title = new Label { Text = "Chargement", HorizontalAlignment = HorizontalAlignment.Center };
        RuntimeRpgUiTheme.StyleTitle(_title, 28, RuntimeRpgUiTheme.Bone);
        content.AddChild(_title);
        _image = new TextureRect
        {
            CustomMinimumSize = new Vector2(680, 260),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        _image.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Inset(RuntimeRpgUiTheme.Border));
        content.AddChild(_image);
        _video = new VideoStreamPlayer { CustomMinimumSize = new Vector2(680, 260) };
        _video.Expand = true;
        _video.Visible = false;
        content.AddChild(_video);
        _tip = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        RuntimeRpgUiTheme.StyleTitle(_tip, 14, RuntimeRpgUiTheme.Gold);
        _tip.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(_tip);
        _accessible = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        RuntimeRpgUiTheme.StyleBody(_accessible, 10, RuntimeRpgUiTheme.Muted);
        _accessible.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(_accessible);
        _progress = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = true };
        _progress.AddThemeStyleboxOverride("background", RuntimeRpgUiTheme.Bar(new Color("e5dac312"), 2));
        _progress.AddThemeStyleboxOverride("fill", RuntimeRpgUiTheme.Bar(RuntimeRpgUiTheme.Gold, 2));
        _progress.AddThemeFontOverride("font", RuntimeRpgUiTheme.BodyFont);
        _progress.AddThemeFontSizeOverride("font_size", 9);
        _progress.AddThemeColorOverride("font_color", RuntimeRpgUiTheme.Bone);
        content.AddChild(_progress);
        _resource = new Label { ClipText = true };
        RuntimeRpgUiTheme.StyleBody(_resource, 8, RuntimeRpgUiTheme.Quiet);
        _resource.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(_resource);
    }

    public void Apply(LoadingPresentationFrame frame, string currentResource = "")
    {
        if (_title is null) return;
        _title.Text = frame.Title;
        _tip!.Text = frame.Tip;
        _accessible!.Text = frame.AccessibleText;
        _progress!.Visible = frame.ShowProgress;
        _progress.Value = Math.Clamp(frame.Progress, 0, 1);
        _resource!.Text = currentResource;
        _image!.Visible = frame.MediaKind == LoadingMediaKind.Image;
        _video!.Visible = frame.MediaKind == LoadingMediaKind.Video;
        if (frame.MediaKind == LoadingMediaKind.Image)
            _image.Texture = LoadTexture(frame.MediaResource);
        else if (frame.MediaKind == LoadingMediaKind.Video && LoadVideo(frame.MediaResource) is { } video)
        {
            _video.Stream = video;
            _video.Play();
        }
    }

    public void Complete()
    {
        _video?.Stop();
        QueueFree();
    }

    private Texture2D? LoadTexture(string resourcePath)
    {
        var resolved = ResolveResource(resourcePath);
        if (resolved.StartsWith("res://", StringComparison.Ordinal) && ResourceLoader.Exists(resolved))
            return ResourceLoader.Load<Texture2D>(resolved);
        if (!Path.IsPathFullyQualified(resolved) || !File.Exists(resolved)) return null;
        var image = Image.LoadFromFile(resolved);
        return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }

    private VideoStream? LoadVideo(string resourcePath)
    {
        var resolved = ResolveResource(resourcePath);
        return resolved.StartsWith("res://", StringComparison.Ordinal) && ResourceLoader.Exists(resolved)
            ? ResourceLoader.Load<VideoStream>(resolved)
            : null;
    }

    private string ResolveResource(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return string.Empty;
        if (resourcePath.StartsWith("res://", StringComparison.Ordinal) || Path.IsPathFullyQualified(resourcePath))
            return resourcePath;
        var absolute = Path.Combine(_contentRoot, resourcePath.Replace('/', Path.DirectorySeparatorChar));
        var localized = ProjectSettings.LocalizePath(absolute);
        return localized.StartsWith("res://", StringComparison.Ordinal) ? localized : absolute;
    }

    private sealed partial class LoadingBackdrop : Control
    {
        public override void _Draw()
        {
            var bounds = new Rect2(Vector2.Zero, Size);
            DrawRect(bounds, new Color("070403"));
            DrawCircle(bounds.GetCenter() + new Vector2(-Size.X * .22f, 0), Size.Y * .62f,
                new Color("51382028"));
            DrawCircle(bounds.GetCenter() + new Vector2(Size.X * .28f, Size.Y * .08f), Size.Y * .72f,
                new Color("26180f32"));
            DrawLine(new Vector2(Size.X * .22f, Size.Y * .18f), new Vector2(Size.X * .78f, Size.Y * .18f),
                new Color("a9855733"), 1);
            DrawLine(new Vector2(Size.X * .22f, Size.Y * .82f), new Vector2(Size.X * .78f, Size.Y * .82f),
                new Color("a9855733"), 1);
        }
    }
}
