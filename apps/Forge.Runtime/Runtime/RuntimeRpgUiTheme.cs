using Godot;

namespace Forge.Runtime;

internal static class RuntimeRpgUiTheme
{
    // Grounded exploration palette: worn leather, dark iron, aged bronze and bone.
    // The deliberately restrained accents leave the 3D world as the brightest layer.
    internal static readonly Color Backdrop = new("090706e8");
    internal static readonly Color Leather = new("17110edf");
    internal static readonly Color LeatherSoft = new("241a14dc");
    internal static readonly Color Iron = new("3b342cdf");
    internal static readonly Color Bronze = new("9b794f");
    internal static readonly Color Bone = new("e5dac3");
    internal static readonly Color Burgundy = new("8f302f");
    internal static readonly Color FocusBlue = new("557b8d");
    internal static readonly Color Sage = new("66775b");

    internal static readonly Color Surface = Leather;
    internal static readonly Color SurfaceSoft = LeatherSoft;
    internal static readonly Color SurfaceBright = Iron;
    internal static readonly Color Border = new("a9855796");
    internal static readonly Color BorderMuted = new("806a504f");
    internal static readonly Color Ember = new("ad693d");
    internal static readonly Color Gold = new("c6a36a");
    internal static readonly Color Parchment = Bone;
    internal static readonly Color Muted = new("afa38f");
    internal static readonly Color Quiet = new("796f61");
    internal static readonly Color Health = new("a93a37");
    internal static readonly Color Player = FocusBlue;
    internal static readonly Color CyanSoft = new("91a7aa");
    internal static readonly Color Violet = new("755f7e");
    internal static readonly Color Green = Sage;
    internal static readonly Color Sky = new("6b8791");

    internal static Font TitleFont { get; } = new SystemFont
    {
        FontNames = ["Iowan Old Style", "Baskerville", "Georgia", "Cambria", "Times New Roman"],
    };

    internal static Font BodyFont { get; } = new SystemFont
    {
        FontNames = ["Geist", "Segoe UI", "Inter", "Arial"],
    };

    internal static void StyleTitle(Label label, int size, Color? color = null)
    {
        label.AddThemeFontOverride("font", TitleFont);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Gold);
        label.AddThemeColorOverride("font_shadow_color", new Color("000000b8"));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
    }

    internal static void StyleBody(Label label, int size, Color? color = null)
    {
        label.AddThemeFontOverride("font", BodyFont);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Parchment);
    }

    internal static void StyleButton(Button button, bool quiet = false)
    {
        button.AddThemeFontOverride("font", BodyFont);
        button.AddThemeFontSizeOverride("font_size", 9);
        button.AddThemeColorOverride("font_color", quiet ? Muted : Parchment);
        button.AddThemeColorOverride("font_hover_color", Bone);
        button.AddThemeColorOverride("font_pressed_color", Gold.Lightened(.12f));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(quiet ? new Color("17110e9c") : SurfaceSoft,
            quiet ? BorderMuted : Border));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color("35271dde"), Gold));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color("4a3423e8"), Bone));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(new Color("110e0caa"), new Color("544a3d")));
    }

    internal static void StyleIconButton(Button button, bool pause = false)
    {
        button.AddThemeFontOverride("font", BodyFont);
        button.AddThemeFontSizeOverride("font_size", pause ? 11 : 14);
        button.AddThemeColorOverride("font_color", pause ? Parchment : Muted);
        button.AddThemeColorOverride("font_hover_color", Bone);
        button.AddThemeColorOverride("font_pressed_color", Gold.Lightened(.12f));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color("17110eb8"), Border));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color("35271dde"), Gold));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color("4a3423e8"), Bone));
    }

    internal static StyleBoxFlat Frame(
        Color? background = null,
        Color? border = null,
        int marginHorizontal = 10,
        int marginVertical = 7,
        int borderWidth = 1) => new()
        {
            BgColor = background ?? Surface,
            BorderColor = border ?? Border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = marginHorizontal,
            ContentMarginTop = marginVertical,
            ContentMarginRight = marginHorizontal,
            ContentMarginBottom = marginVertical,
            ShadowColor = new Color("00000078"),
            ShadowSize = 12,
            ShadowOffset = new Vector2(0, 4),
        };

    internal static StyleBoxFlat ButtonStyle(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        ContentMarginLeft = 4,
        ContentMarginTop = 3,
        ContentMarginRight = 4,
        ContentMarginBottom = 3,
    };

    internal static StyleBoxFlat Circle(Color background, Color border, int radius) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ShadowColor = new Color("00000078"),
        ShadowSize = 8,
        ShadowOffset = new Vector2(0, 2),
    };

    internal static StyleBoxFlat Slot(Color color, bool disabled = false) => new()
    {
        BgColor = new Color("16110ee8"),
        BorderColor = disabled ? new Color("51483d") : color.Darkened(.24f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        ContentMarginLeft = 2,
        ContentMarginTop = 2,
        ContentMarginRight = 2,
        ContentMarginBottom = 2,
    };

    internal static StyleBoxFlat Bar(Color color, int radius = 1) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };

    internal static Control Portrait(
        string? path,
        int size,
        Color? rim = null,
        bool selected = false,
        string? fallbackText = null)
    {
        var root = new Control
        {
            CustomMinimumSize = new Vector2(size, size),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var shadow = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        shadow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shadowStyle = Circle(new Color("0d0907"), new Color("3f3226"), size / 2);
        shadowStyle.ShadowColor = new Color("0000009a");
        shadowStyle.ShadowSize = selected ? 7 : 4;
        shadow.AddThemeStyleboxOverride("panel", shadowStyle);
        root.AddChild(shadow);

        var texture = LoadPortraitTexture(path);
        if (texture is not null)
        {
            var portrait = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = PortraitMaskMaterial(),
            };
            portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            portrait.OffsetLeft = 3;
            portrait.OffsetTop = 3;
            portrait.OffsetRight = -3;
            portrait.OffsetBottom = -3;
            root.AddChild(portrait);
        }
        else
        {
            var fallback = new Label
            {
                Text = string.IsNullOrWhiteSpace(fallbackText) ? "◇" : fallbackText.Trim().ToUpperInvariant(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            fallback.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            StyleTitle(fallback, Math.Max(11, size / 3), rim ?? Bone);
            root.AddChild(fallback);
        }

        var ring = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        ring.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var ringStyle = Circle(new Color("00000000"), rim ?? Bronze, size / 2);
        ringStyle.BorderWidthLeft = selected ? 2 : 1;
        ringStyle.BorderWidthTop = selected ? 2 : 1;
        ringStyle.BorderWidthRight = selected ? 2 : 1;
        ringStyle.BorderWidthBottom = selected ? 2 : 1;
        ringStyle.ShadowColor = selected ? new Color("ad693d45") : new Color("00000000");
        ringStyle.ShadowSize = selected ? 5 : 0;
        ring.AddThemeStyleboxOverride("panel", ringStyle);
        root.AddChild(ring);
        return root;
    }

    private static Texture2D? LoadPortraitTexture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path)) return null;
        var image = Image.LoadFromFile(path);
        return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }

    internal static StyleBoxFlat SearchField() => Frame(
        new Color("21170fe8"), new Color("9b794f72"), 11, 7);

    internal static StyleBoxFlat Inset(Color? border = null) => Frame(
        new Color("1d140ee8"), border ?? BorderMuted, 8, 7);

    private static ShaderMaterial PortraitMaskMaterial() => new()
    {
        Shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                void fragment() {
                    vec2 p = UV * 2.0 - vec2(1.0);
                    float alpha = 1.0 - smoothstep(0.94, 1.0, length(p));
                    vec4 source = texture(TEXTURE, UV);
                    vec3 lifted = source.rgb * 1.30 + vec3(0.020, 0.013, 0.009);
                    COLOR = vec4(lifted, source.a * alpha);
                }
                """,
        },
    };
}
