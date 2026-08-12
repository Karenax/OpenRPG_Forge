#if TOOLS
using Forge.Domain.Creatures;
using Godot;

namespace Forge.Toolset.Editor;

public partial class CreatureInteractionCompositionPreview : Control
{
    private CreatureInteractionCompositionProfile? _profile;
    private CreatureInteractionDialogueDecision? _decision;

    public CreatureInteractionCompositionPreview()
    {
        CustomMinimumSize = new Vector2(360, 430);
        MouseFilter = MouseFilterEnum.Ignore;
        TooltipText = "Aperçu des factions, sens, dialogues, protections et événement de conversation.";
        AccessibilityName = "Aperçu des interactions de la créature";
    }

    public void ShowProfile(
        CreatureInteractionCompositionProfile? profile,
        CreatureInteractionDialogueDecision? decision = null)
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
                "Aucun profil d’interaction valide", 0, -1, 14, new Color("aa99bb"));
            return;
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(18, 28), "APERÇU INTERACTIONS",
            0, -1, 15, new Color("ffad72"));
        DrawCircle(new Vector2(48, 70), 24, new Color("d85f48"));
        DrawString(ThemeDB.FallbackFont, new Vector2(84, 66), "FACTION PRINCIPALE",
            0, -1, 12, new Color("baa8ca"));
        DrawString(ThemeDB.FallbackFont, new Vector2(84, 86),
            _profile.Factions.PrimaryFactionId.ToString("D")[..18] + "…",
            0, -1, 14, new Color("f4e7fb"));
        DrawString(ThemeDB.FallbackFont, new Vector2(18, 112),
            $"{_profile.Factions.Memberships.Count} membership(s) pondéré(s)",
            0, -1, 12, new Color("d8c6e7"));

        DrawString(ThemeDB.FallbackFont, new Vector2(18, 146), "SENSE SYSTEM",
            0, -1, 13, new Color("ffad72"));
        var center = new Vector2(75, 215);
        foreach (var (sense, index) in _profile.Perception.Senses.Select((item, index) => (item, index)))
        {
            var radius = 25 + index * 16;
            DrawArc(center, radius, 0, Mathf.Tau, 40,
                index % 2 == 0 ? new Color("6a9fdf") : new Color("9d70d6"), 2);
        }
        DrawCircle(center, 8, new Color("ff9858"));
        foreach (var (sense, index) in _profile.Perception.Senses.Take(4).Select((item, index) => (item, index)))
            DrawString(ThemeDB.FallbackFont, new Vector2(155, 178 + index * 18),
                $"{sense.Kind} · {sense.Range:0.#}m · {(sense.UsesOcclusion ? "occlusion" : "direct")}",
                0, 185, 11, new Color("f4e7fb"));
        DrawString(ThemeDB.FallbackFont, new Vector2(155, 258),
            $"Mémoire {_profile.Perception.MemorySeconds:0.#}s · stimuli typés",
            0, 185, 11, new Color("baa8ca"));

        DrawString(ThemeDB.FallbackFont, new Vector2(18, 292), "DIALOGUE & PROTECTION",
            0, -1, 13, new Color("ffad72"));
        var protection = _profile.Protection;
        DrawString(ThemeDB.FallbackFont, new Vector2(18, 316),
            $"{_profile.DialogueBindings.Count} binding(s) · invulnérable {(protection.Invulnerable ? "oui" : "non")} · mort narrative {(protection.UsesNarrativeDeath ? "oui" : "non")}",
            0, -1, 11, new Color("f4e7fb"));
        DrawString(ThemeDB.FallbackFont, new Vector2(18, 338),
            $"Interruption {(protection.DialogueInterruptible ? "autorisée" : "protégée")} · {_profile.Protection.GameMasterPermissions.Count} permission(s) GM",
            0, -1, 11, new Color("baa8ca"));
        if (_decision is not null)
            DrawString(ThemeDB.FallbackFont, new Vector2(18, 367),
                _decision.Accepted
                    ? $"Simulation : dialogue {_decision.Binding!.DialogueId} sélectionné"
                    : $"Simulation refusée : {_decision.RefusalCode}",
                0, 325, 11, _decision.Accepted ? new Color("8ce0bd") : new Color("ff7777"));
        DrawString(ThemeDB.FallbackFont, new Vector2(18, Size.Y - 16),
            $"OnConversation → {_profile.OnConversation.Handler} · serveur · ForgeScript {_profile.OnConversation.ForgeScriptApiVersion}",
            0, -1, 11, new Color("d8c6e7"));
    }
}
#endif
