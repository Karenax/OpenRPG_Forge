#if TOOLS
using Forge.Domain.Gameplay;
using Godot;

namespace Forge.Toolset.Editor;

public partial class GameplaySessionPreview : Control
{
    private GameplaySessionProfile? _profile;

    public GameplaySessionPreview()
    {
        CustomMinimumSize = new Vector2(0, 185);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void ShowProfile(GameplaySessionProfile profile)
    {
        _profile = profile;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawStyleBox(Panel(), new Rect2(Vector2.Zero, Size));
        if (_profile is null) return;
        DrawString(ThemeDB.FallbackFont, new Vector2(22, 27),
            "PARCOURS DE SESSION · SAUVEGARDE → CHECKPOINT → UI → CONTRÔLES → REPLAY",
            HorizontalAlignment.Left, -1, 15, new Color("ffad72"));
        var y = Size.Y * .58f;
        var start = 45f;
        var end = Size.X - 45f;
        DrawLine(new Vector2(start, y), new Vector2(end, y), new Color("5e476f"), 5);
        var labels = new[]
        {
            $"{_profile.Persistence.Scope}\n{_profile.Persistence.Authority}",
            $"{_profile.Checkpoints.Count} checkpoint\nspawn sûr",
            $"{_profile.Shell.KitId}\n{_profile.Shell.Scale:0.##}×",
            $"{_profile.ControlLocks.Contexts.Count} contextes\nrestauration",
            $"Replay\n{_profile.Replay.MaximumFrames:N0} frames",
        };
        var colors = new[] { "8d6ac3", "ff875f", "55d6be", "ffd36b", "7bb7ff" };
        for (var index = 0; index < labels.Length; index++)
        {
            var x = Mathf.Lerp(start + 35, end - 35, index / 4f);
            DrawCircle(new Vector2(x, y), 15, new Color(colors[index]));
            var lines = labels[index].Split('\n');
            DrawString(ThemeDB.FallbackFont, new Vector2(x - 54, y + 39), lines[0], 0, 108, 13,
                new Color("e7dbef"));
            DrawString(ThemeDB.FallbackFont, new Vector2(x - 54, y + 58), lines[1], 0, 108, 12,
                new Color("a996bb"));
        }
    }

    private static StyleBoxFlat Panel()
    {
        var panel = new StyleBoxFlat { BgColor = new Color("120d1c"), BorderColor = new Color("513a6d") };
        panel.SetBorderWidthAll(1); panel.SetCornerRadiusAll(12);
        return panel;
    }
}
#endif
