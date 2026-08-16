using Godot;

namespace Forge.Studio.Presentation;

internal static class StudioStyle
{
    internal const int BaseFontSize = 12;
    internal const int CompactControlHeight = 28;
    internal const int ControlHeight = 32;

    internal static readonly Color Background = new("0d100e");
    internal static readonly Color Chrome = new("171a17");
    internal static readonly Color Surface = new("20241f");
    internal static readonly Color SurfaceRaised = new("292e27");
    internal static readonly Color Border = new("3d4439");
    internal static readonly Color Text = new("f2f0e9");
    internal static readonly Color Muted = new("aeb5a8");
    internal static readonly Color Accent = new("ef7b42");
    internal static readonly Color AccentDark = new("47271a");
    internal static readonly Color Selection = new("30422f");
    internal static readonly Color Success = new("7bd094");
    internal static readonly Color Warning = new("e2b85e");
    internal static readonly Color Danger = new("e56f6f");

    internal static PanelContainer Panel(
        string name,
        Color? background = null,
        int radius = 8,
        int borderWidth = 1)
    {
        var panel = new PanelContainer { Name = name };
        panel.AddThemeStyleboxOverride("panel", Box(background ?? Surface, Border, radius, borderWidth));
        return panel;
    }

    internal static StyleBoxFlat Box(
        Color background,
        Color border,
        int radius = 8,
        int borderWidth = 1)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 8,
            ContentMarginTop = 5,
            ContentMarginRight = 8,
            ContentMarginBottom = 5,
        };
    }

    internal static Button Button(
        string text,
        bool primary = false,
        bool compact = false)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, compact ? CompactControlHeight : ControlHeight),
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        var normal = primary ? Accent : SurfaceRaised;
        var normalText = primary ? Background : Text;
        button.AddThemeStyleboxOverride("normal", Box(normal, primary ? Accent : Border, 5));
        button.AddThemeStyleboxOverride("hover", Box(normal.Lightened(0.08f), Accent, 5));
        button.AddThemeStyleboxOverride("pressed", Box(normal.Darkened(0.08f), Accent, 5));
        button.AddThemeStyleboxOverride("focus", Box(normal, Accent, 5, 2));
        button.AddThemeStyleboxOverride("disabled", Box(Surface, Border.Darkened(0.18f), 5));
        button.AddThemeFontSizeOverride("font_size", BaseFontSize);
        button.AddThemeColorOverride("font_color", normalText);
        button.AddThemeColorOverride("font_hover_color", normalText);
        button.AddThemeColorOverride("font_pressed_color", normalText);
        button.AddThemeColorOverride("font_focus_color", normalText);
        button.AddThemeColorOverride("font_disabled_color", Muted.Darkened(0.28f));
        return button;
    }

    internal static Label Label(
        string text,
        int size = BaseFontSize,
        Color? color = null,
        bool wrap = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = wrap
                ? TextServer.AutowrapMode.WordSmart
                : TextServer.AutowrapMode.Off,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Text);
        return label;
    }

    internal static Label SectionLabel(string text)
    {
        var label = Label(text.ToUpperInvariant(), 10, Muted);
        label.AddThemeConstantOverride("outline_size", 0);
        return label;
    }

    internal static MarginContainer Margin(int left, int top, int right, int bottom)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        return margin;
    }

    internal static void ApplyLineEdit(LineEdit edit)
    {
        edit.AddThemeStyleboxOverride("normal", Box(Surface, Border, 5));
        edit.AddThemeStyleboxOverride("focus", Box(SurfaceRaised, Accent, 5, 2));
        edit.AddThemeColorOverride("font_color", Text);
        edit.AddThemeColorOverride("font_placeholder_color", Muted);
        edit.AddThemeColorOverride("caret_color", Accent);
        edit.AddThemeFontSizeOverride("font_size", BaseFontSize);
        edit.CustomMinimumSize = new Vector2(0, ControlHeight);
    }

    internal static void ApplyOptionButton(OptionButton option)
    {
        option.AddThemeStyleboxOverride("normal", Box(Surface, Border, 5));
        option.AddThemeStyleboxOverride("hover", Box(SurfaceRaised, Accent, 5));
        option.AddThemeStyleboxOverride("pressed", Box(SurfaceRaised, Accent, 5));
        option.AddThemeStyleboxOverride("focus", Box(SurfaceRaised, Accent, 5, 2));
        option.AddThemeColorOverride("font_color", Text);
        option.AddThemeFontSizeOverride("font_size", BaseFontSize);
        option.CustomMinimumSize = new Vector2(0, ControlHeight);
    }

    internal static void ApplySpinBox(SpinBox spinBox)
    {
        spinBox.CustomMinimumSize = new Vector2(0, ControlHeight);
        ApplyLineEdit(spinBox.GetLineEdit());
    }
}
