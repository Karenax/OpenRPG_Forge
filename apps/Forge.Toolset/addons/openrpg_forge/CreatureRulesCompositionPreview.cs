#if TOOLS
using Forge.Domain.Creatures;
using Godot;

namespace Forge.Toolset.Editor;

public partial class CreatureRulesCompositionPreview : Control
{
    private CreatureRulesCompositionProfile? _profile;
    private double? _lastDamage;

    public CreatureRulesCompositionPreview()
    {
        CustomMinimumSize = new Vector2(360, 430);
        MouseFilter = MouseFilterEnum.Ignore;
        TooltipText = "Aperçu de l’état, des attributs, des défenses, capacités et emplacements d’inventaire.";
        AccessibilityName = "Aperçu des règles et de l’inventaire de la créature";
    }

    public void ShowProfile(CreatureRulesCompositionProfile? profile)
    {
        _profile = profile;
        _lastDamage = null;
        QueueRedraw();
    }

    public double SimulateDamage(string damageType = "glass", double incoming = 10)
    {
        _lastDamage = _profile is null
            ? incoming
            : CreatureRulesCompositionValidator.Mitigate(_profile.Defenses, damageType, incoming);
        QueueRedraw();
        return _lastDamage.Value;
    }

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, new Color("0d0914"));
        DrawRect(bounds, new Color("553c70"), false, 1);
        if (_profile is null)
        {
            DrawString(ThemeDB.FallbackFont, bounds.GetCenter() - new Vector2(90, 0),
                "Aucun profil de règles valide", 0, -1, 14, new Color("aa99bb"));
            return;
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(18, 28), "APERÇU RÈGLES & INVENTAIRE",
            0, -1, 15, new Color("ffad72"));
        var stateColor = _profile.InitialState.LifeState switch
        {
            CreatureRulesInitialLifeState.Alive => new Color("62d4aa"),
            CreatureRulesInitialLifeState.Dead => new Color("8d7d99"),
            CreatureRulesInitialLifeState.Frozen => new Color("70b9ff"),
            _ => new Color("ffb565"),
        };
        DrawCircle(new Vector2(54, 70), 24, stateColor);
        DrawString(ThemeDB.FallbackFont, new Vector2(88, 66), _profile.InitialState.LifeState.ToString(),
            0, -1, 17, new Color("f4e7fb"));
        DrawString(ThemeDB.FallbackFont, new Vector2(88, 86),
            $"Pose {_profile.InitialState.PoseId ?? "—"} · boucle {_profile.InitialState.AnimationLoopAssetId ?? "—"}",
            0, -1, 11, new Color("baa8ca"));

        var values = CreatureRulesCompositionValidator.EvaluateAttributes(_profile.Attributes)
            .OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal).Take(4).ToArray();
        DrawString(ThemeDB.FallbackFont, new Vector2(18, 120), "ATTRIBUTS CALCULÉS",
            0, -1, 13, new Color("ffad72"));
        for (var index = 0; index < values.Length; index++)
        {
            var y = 142 + index * 27;
            var width = (float)Math.Clamp(Math.Abs(values[index].Value) / 20d, .05, 1) * 205;
            DrawString(ThemeDB.FallbackFont, new Vector2(18, y + 13), values[index].Key, 0, 90, 11,
                new Color("d8c6e7"));
            DrawRect(new Rect2(112, y, 205, 16), new Color("24182f"));
            DrawRect(new Rect2(112, y, width, 16), new Color("7f5ab4"));
            DrawString(ThemeDB.FallbackFont, new Vector2(323, y + 13), values[index].Value.ToString("0.##"),
                0, -1, 11, new Color("fff4ec"));
        }

        var defenseY = 260f;
        DrawString(ThemeDB.FallbackFont, new Vector2(18, defenseY), "DÉFENSES & SAUVEGARDES",
            0, -1, 13, new Color("ffad72"));
        DrawColoredPolygon([
            new Vector2(33, defenseY + 16), new Vector2(77, defenseY + 16),
            new Vector2(72, defenseY + 63), new Vector2(55, defenseY + 78), new Vector2(38, defenseY + 63),
        ], new Color("c85b43"));
        DrawString(ThemeDB.FallbackFont, new Vector2(94, defenseY + 37),
            $"Esquive {_profile.Defenses.Avoidance:0.#} · Armure {_profile.Defenses.Armor:0.#} · Mitigation {_profile.Defenses.FlatMitigation:0.#}",
            0, -1, 12, new Color("f4e7fb"));
        DrawString(ThemeDB.FallbackFont, new Vector2(94, defenseY + 58),
            $"{_profile.Defenses.Resistances.Count} résistance(s) · {_profile.Defenses.Saves.Count} save(s) · {_profile.Defenses.Immunities.Count} immunité(s)",
            0, -1, 11, new Color("baa8ca"));
        if (_lastDamage is not null)
            DrawString(ThemeDB.FallbackFont, new Vector2(94, defenseY + 77),
                $"Simulation : 10 glass → {_lastDamage:0.##} dégâts", 0, -1, 11, new Color("8ce0bd"));

        var inventoryY = 358f;
        DrawString(ThemeDB.FallbackFont, new Vector2(18, inventoryY),
            $"CAPACITÉS {_profile.Abilities.Count}  ·  INVENTAIRE {_profile.Inventory.Containers.Sum(item => item.Items.Sum(entry => entry.Quantity))} objet(s)",
            0, -1, 13, new Color("ffad72"));
        var slotCount = Math.Clamp(_profile.Inventory.Containers.Sum(item => item.SlotCount), 1, 10);
        for (var index = 0; index < slotCount; index++)
        {
            var x = 18 + index * 31;
            DrawRect(new Rect2(x, inventoryY + 15, 25, 25), new Color("251a30"));
            DrawRect(new Rect2(x, inventoryY + 15, 25, 25), new Color("73518d"), false, 1);
        }
        var occupied = Math.Min(slotCount, _profile.Inventory.Containers.Sum(item => item.Items.Count));
        for (var index = 0; index < occupied; index++)
            DrawCircle(new Vector2(30 + index * 31, inventoryY + 27), 6, new Color("ff9858"));
        DrawString(ThemeDB.FallbackFont, new Vector2(18, Size.Y - 14),
            $"{_profile.Attributes.RulesetId} · owner {_profile.Inventory.OwnerId.Replace("creature.", string.Empty, StringComparison.Ordinal)} · " +
            $"{_profile.Inventory.EquipmentSlots.Count} équipé(s)",
            0, -1, 11, new Color("d8c6e7"));
    }
}
#endif
