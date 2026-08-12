#if TOOLS
using Forge.Domain.Creatures;
using Godot;

namespace Forge.Toolset.Editor;

public partial class CreatureAuthoringCompositionPreview : Control
{
    private CreatureAuthoringCompositionProfile? _profile;
    private int _simulatedState;

    public CreatureAuthoringCompositionPreview()
    {
        CustomMinimumSize = new Vector2(360, 430);
        MouseFilter = MouseFilterEnum.Ignore;
        TooltipText = "Aperçu du modèle, du rôle et de la mini-carte du graphe d’animation.";
        AccessibilityName = "Aperçu visuel de la créature et mini-carte des animations";
    }

    public void ShowProfile(CreatureAuthoringCompositionProfile? profile)
    {
        _profile = profile;
        _simulatedState = 0;
        QueueRedraw();
    }

    public string SimulateNextState()
    {
        if (_profile?.AnimationGraph.States.Count > 0)
            _simulatedState = (_simulatedState + 1) % _profile.AnimationGraph.States.Count;
        QueueRedraw();
        return _profile?.AnimationGraph.States.ElementAtOrDefault(_simulatedState)?.StateId ?? string.Empty;
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, new Color("0d0914"));
        DrawRect(bounds, new Color("553c70"), false, 1);
        if (_profile is null)
        {
            DrawString(ThemeDB.FallbackFont, bounds.GetCenter() - new Vector2(95, 0),
                "Aucun profil valide à prévisualiser", HorizontalAlignment.Left, -1, 14, new Color("aa99bb"));
            return;
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(18, 28), "APERÇU MODÈLE & RÈGLES",
            HorizontalAlignment.Left, -1, 15, new Color("ffad72"));
        var roleColor = _profile.EncounterRole switch
        {
            CreatureAuthoringEncounterRole.Boss => new Color("ff6b5c"),
            CreatureAuthoringEncounterRole.Support => new Color("65d9bd"),
            CreatureAuthoringEncounterRole.Controller => new Color("a58bff"),
            _ => new Color("ff9858"),
        };
        var center = new Vector2(Size.X * .5f, 145);
        DrawCircle(center - new Vector2(0, 58), 25, new Color("e9c8ae"));
        DrawColoredPolygon([
            center + new Vector2(-45, -32), center + new Vector2(45, -32),
            center + new Vector2(60, 78), center + new Vector2(-60, 78),
        ], new Color(roleColor, .82f));
        DrawLine(center + new Vector2(-28, 2), center + new Vector2(-72, 55), roleColor, 10, true);
        DrawLine(center + new Vector2(28, 2), center + new Vector2(72, 55), roleColor, 10, true);
        DrawLine(center + new Vector2(-25, 75), center + new Vector2(-35, 118), roleColor, 12, true);
        DrawLine(center + new Vector2(25, 75), center + new Vector2(35, 118), roleColor, 12, true);
        DrawString(ThemeDB.FallbackFont, new Vector2(18, 286),
            $"{_profile.EncounterRole} · menace {_profile.ThreatRating:0.#}", 0, -1, 14, new Color("f4e7fb"));
        DrawString(ThemeDB.FallbackFont, new Vector2(18, 307),
            $"{_profile.Species.SpeciesId} · {_profile.Appearance.MeshAssetId}", 0, -1, 12,
            new Color("baa8ca"));

        var states = _profile.AnimationGraph.States;
        DrawString(ThemeDB.FallbackFont, new Vector2(18, 325), "MINI-CARTE ANIMATIONGRAPH",
            0, -1, 13, new Color("ffad72"));
        if (states.Count == 0) return;
        var startX = 24f;
        var available = Math.Max(1, Size.X - 48);
        var step = available / Math.Max(1, states.Count - 1);
        var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        for (var index = 0; index < states.Count; index++)
            positions[states[index].StateId] = new Vector2(startX + step * index, 360 + index % 2 * 22);
        foreach (var transition in _profile.AnimationGraph.Transitions)
            if (positions.TryGetValue(transition.FromStateId, out var from) &&
                positions.TryGetValue(transition.ToStateId, out var to))
                DrawLine(from, to, new Color("7b5ba0"), 2, true);
        for (var index = 0; index < states.Count; index++)
        {
            var state = states[index];
            var selected = index == _simulatedState;
            var color = selected ? new Color("ffce72") : state.Category switch
            {
                "locomotion" => new Color("67d7bd"),
                "combat" => new Color("ff796b"),
                "emote" => new Color("a58bff"),
                _ => new Color("70a7ff"),
            };
            DrawCircle(positions[state.StateId], selected ? 9 : 7, color);
        }
        var current = states[Math.Clamp(_simulatedState, 0, states.Count - 1)];
        DrawString(ThemeDB.FallbackFont, new Vector2(18, Size.Y - 15),
            $"Simulation : {current.StateId} · {current.Category} · retarget {_profile.AnimationGraph.RetargetProfileId}",
            0, -1, 12, new Color("d8c6e7"));
    }
}
#endif
