#if TOOLS
using Forge.Domain.Gameplay;
using Godot;

namespace Forge.Toolset.Editor;

public partial class GameplaySimulationPreview : Control
{
    private GameplaySimulationProfile? _profile;

    public GameplaySimulationPreview()
    {
        CustomMinimumSize = new Vector2(0, 210);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void ShowProfile(GameplaySimulationProfile profile)
    {
        _profile = profile;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(Panel(), bounds);
        if (_profile is null) return;
        var centerY = Size.Y * .56f;
        DrawString(ThemeDB.FallbackFont, new Vector2(22, 28), "APERÇU TEMPS RÉEL · VOL · PROJECTILE · PHYSIQUE · CIBLAGE",
            HorizontalAlignment.Left, -1, 15, new Color("ffad72"));
        var volume = new Rect2(28, 55, Size.X * .22f, 120);
        DrawRect(volume, new Color("34264a"), true);
        DrawRect(volume, new Color("8d6ac3"), false, 2);
        var ship = new Vector2(volume.Position.X + volume.Size.X * .58f, centerY);
        DrawCircle(ship, 8, new Color("79e7d0"));
        DrawLine(ship - new Vector2(25, 17), ship, new Color("79e7d0"), 3);
        DrawLine(ship, ship + new Vector2(31, -24), new Color("79e7d0"), 3);
        DrawString(ThemeDB.FallbackFont, volume.Position + new Vector2(10, 108),
            $"Vol {_profile.Flight.MaximumAltitude:0} m · inclinaison {_profile.Flight.BankingDegrees:0}°", 0, -1, 13, new Color("d5c7e4"));

        var projectileStart = new Vector2(Size.X * .3f, centerY + 25);
        var projectileEnd = new Vector2(Size.X * .49f, centerY - 28);
        var previous = projectileStart;
        for (var index = 1; index <= 16; index++)
        {
            var t = index / 16f;
            var point = projectileStart.Lerp(projectileEnd, t) + new Vector2(0, -MathF.Sin(t * MathF.PI) * 34);
            DrawLine(previous, point, new Color("ff875f"), 3);
            previous = point;
        }
        DrawCircle(projectileEnd, 10, new Color("ffd36b"));
        DrawString(ThemeDB.FallbackFont, new Vector2(Size.X * .3f, 175),
            $"{_profile.Projectiles.Mode} · {_profile.Projectiles.Speed:0} m/s · VFX", 0, -1, 13, new Color("d5c7e4"));

        var bodyA = new Rect2(Size.X * .55f, centerY - 28, 48, 48);
        var bodyB = new Rect2(Size.X * .66f, centerY + 8, 62, 26);
        DrawRect(bodyA, new Color("6f4aa5"), true);
        DrawRect(bodyB, new Color("4a3c5d"), true);
        DrawLine(bodyA.GetCenter(), bodyB.GetCenter(), new Color("ffb06f"), 4);
        DrawString(ThemeDB.FallbackFont, new Vector2(Size.X * .55f, 175),
            $"{_profile.Physics.Bodies.Count} corps · {_profile.Physics.Joints.Count} liaison", 0, -1, 13, new Color("d5c7e4"));

        var targetOrigin = new Vector2(Size.X * .79f, centerY + 35);
        var left = targetOrigin + new Vector2(145, -92);
        var right = targetOrigin + new Vector2(145, 5);
        DrawColoredPolygon([targetOrigin, left, right], new Color(0.33f, .84f, .74f, .14f));
        DrawPolyline([targetOrigin, left, right, targetOrigin], new Color("55d6be"), 3);
        DrawCircle(targetOrigin + new Vector2(105, -40), 9, new Color("ffdf82"));
        DrawString(ThemeDB.FallbackFont, new Vector2(Size.X * .79f, 175),
            $"{_profile.Targeting.Mode} · {_profile.Targeting.MaximumRange:0} m · {_profile.WorldTime.InitialClock.TimeScale:0.#}×", 0, -1, 13, new Color("d5c7e4"));
    }

    private static StyleBoxFlat Panel()
    {
        var panel = new StyleBoxFlat { BgColor = new Color("120d1c"), BorderColor = new Color("513a6d") };
        panel.SetBorderWidthAll(1); panel.SetCornerRadiusAll(12);
        return panel;
    }
}
#endif
