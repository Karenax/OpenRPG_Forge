#if TOOLS
using Forge.Domain.Creatures;
using Godot;

namespace Forge.Toolset.Editor;

public partial class CreatureEventAiCompositionPreview : Control
{
    private CreatureEventAiCompositionProfile? _profile;
    private CreatureEventAiDecision? _decision;

    public CreatureEventAiCompositionPreview()
    {
        CustomMinimumSize = new Vector2(370, 430);
        MouseFilter = MouseFilterEnum.Ignore;
        TooltipText = "Aperçu des événements de perception, combat, capacité, blocage et du contrôleur IA.";
        AccessibilityName = "Aperçu des événements et de l’intelligence artificielle de la créature";
    }

    public void ShowProfile(CreatureEventAiCompositionProfile? profile, CreatureEventAiDecision? decision = null)
    {
        _profile = profile;
        _decision = decision;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, new Color("0d0914"));
        DrawRect(bounds, new Color("553c70"), false, 1);
        if (_profile is null)
        {
            DrawString(ThemeDB.FallbackFont, bounds.GetCenter() - new Vector2(100, 0),
                "Aucun profil événement/IA valide", 0, -1, 14, new Color("aa99bb"));
            return;
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(18, 28), "ÉVÉNEMENTS & IA",
            0, -1, 15, new Color("ffad72"));
        DrawCircle(new Vector2(48, 74), 25, new Color("d85f48"));
        DrawString(ThemeDB.FallbackFont, new Vector2(38, 80), "IA", 0, -1, 13, Colors.White);
        DrawString(ThemeDB.FallbackFont, new Vector2(88, 67), _profile.Controller.Kind.ToString(),
            0, -1, 15, new Color("f4e7fb"));
        DrawString(ThemeDB.FallbackFont, new Vector2(88, 88), _profile.Controller.DefinitionId,
            0, 255, 12, new Color("baa8ca"));

        var rows = new[]
        {
            ("ONPERCEPTION", $"{_profile.Perception.Detected.Handler} / {_profile.Perception.Lost.Handler}"),
            ("ONPHYSICALATTACKED", $"{_profile.PhysicalAttack.Declared.Handler} / {_profile.PhysicalAttack.Resolved.Handler}"),
            ("ONSPELLCASTAT", $"{_profile.Ability.Targeted.Handler} / {_profile.Ability.Impacted.Handler}"),
            ("ONBLOCKED", _profile.Blocked.Blocked.Handler),
        };
        foreach (var (row, index) in rows.Select((item, index) => (item, index)))
        {
            var y = 130 + index * 52;
            DrawCircle(new Vector2(29, y - 4), 7, index < 3 ? new Color("6a9fdf") : new Color("9d70d6"));
            DrawString(ThemeDB.FallbackFont, new Vector2(48, y), row.Item1,
                0, -1, 12, new Color("ffad72"));
            DrawString(ThemeDB.FallbackFont, new Vector2(48, y + 20), row.Item2,
                0, 300, 11, new Color("f4e7fb"));
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(18, 350),
            $"Blackboard {_profile.Controller.Blackboard.Count} · actions {_profile.Controller.AllowedActions.Count}",
            0, -1, 12, new Color("d8c6e7"));
        DrawString(ThemeDB.FallbackFont, new Vector2(18, 372),
            $"Récupération : {string.Join(" → ", _profile.Blocked.RecoveryOrder)}", 0, 335, 11,
            new Color("baa8ca"));
        if (_decision is not null)
            DrawString(ThemeDB.FallbackFont, new Vector2(18, 402),
                $"Simulation : {_decision.Action} · score {_decision.Score:0.##}", 0, 335, 12,
                new Color("8ce0bd"));
    }
}
#endif
