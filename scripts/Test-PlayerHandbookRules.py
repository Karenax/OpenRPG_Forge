#!/usr/bin/env python3
"""Vérifie les invariants numériques du Manuel du joueur OpenRPG Forge.

Le script n'utilise que la bibliothèque standard et peut être lancé depuis
n'importe quel répertoire :

    python scripts/Test-PlayerHandbookRules.py
"""

from __future__ import annotations

from collections import Counter
from decimal import Decimal, ROUND_HALF_UP
from itertools import product
from pathlib import Path
import re
import unittest


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
HANDBOOK_PATH = (
    REPOSITORY_ROOT / "docs" / "design" / "OPENRPG_FORGE_PLAYER_HANDBOOK_FR.md"
)


def learning_cost(score: int) -> int:
    """Nombre de traces requis pour passer de score à score + 1."""

    return 5 + score // 5 + score * score // 400


def level_threshold(level: int) -> int:
    """Nombre de jalons requis pour atteindre un niveau global."""

    return level * level + 3 * level - 4


def margin_class(margin: int) -> str:
    """Identifiant de résultat défini par les cinq bandes de marge."""

    if margin <= -10:
        return "net"
    if margin <= -1:
        return "constructif"
    if margin <= 4:
        return "fragile"
    if margin <= 9:
        return "propre"
    return "décisif"


def keep_two_highest(dice: tuple[int, int, int]) -> tuple[int, int]:
    """Sélection des deux dés d'un test avec avantage, du plus haut au plus bas."""

    ordered = sorted(dice, reverse=True)
    return ordered[0], ordered[1]


def keep_two_lowest(dice: tuple[int, int, int]) -> tuple[int, int]:
    """Sélection des deux dés d'un test avec désavantage, du plus bas au plus haut."""

    ordered = sorted(dice)
    return ordered[0], ordered[1]


def round_half_up(value: Decimal) -> int:
    """Arrondi arithmétique des valeurs positives utilisé pour le Trauma."""

    return int(value.quantize(Decimal("1"), rounding=ROUND_HALF_UP))


def trauma(
    impact: int,
    contact_quality: Decimal,
    protection: int,
    zone_coefficient: Decimal,
) -> int:
    raw = (
        Decimal(impact) * contact_quality
        - Decimal("0.65") * Decimal(protection)
    ) * zone_coefficient
    return max(0, round_half_up(raw))


def resources(
    strength: int,
    constitution: int,
    intelligence: int,
    willpower: int,
    perception: int,
) -> dict[str, int]:
    return {
        "Vitalité": 60 + 6 * constitution + 2 * strength,
        "Endurance": 50 + 5 * constitution + 3 * willpower,
        "Mana": 20 + 6 * willpower + 4 * intelligence,
        "Équilibre": 40 + 4 * strength + 4 * constitution,
        "Concentration": 40 + 4 * perception + 4 * willpower,
    }


def safe_magic_intensity(skill: int, intelligence: int, willpower: int) -> int:
    intensity = 1 + skill // 20 + (intelligence + willpower - 10) // 4
    return min(5, max(1, intensity))


class DiceRulesTests(unittest.TestCase):
    def test_2d10_triangular_distribution(self) -> None:
        distribution = Counter(sum(dice) for dice in product(range(1, 11), repeat=2))
        expected = {
            total: total - 1 if total <= 11 else 21 - total
            for total in range(2, 21)
        }
        self.assertEqual(sum(distribution.values()), 100)
        self.assertEqual(dict(sorted(distribution.items())), expected)

    def test_advantage_and_disadvantage_selection_examples(self) -> None:
        self.assertEqual(keep_two_highest((3, 7, 9)), (9, 7))
        self.assertEqual(keep_two_lowest((3, 7, 9)), (3, 7))

    def test_3d10_keep_two_distributions(self) -> None:
        advantage = Counter()
        disadvantage = Counter()
        for dice in product(range(1, 11), repeat=3):
            advantage[sum(keep_two_highest(dice))] += 1
            disadvantage[sum(keep_two_lowest(dice))] += 1

        expected_advantage = {
            2: 1, 3: 3, 4: 7, 5: 12, 6: 19, 7: 27, 8: 37, 9: 48,
            10: 61, 11: 75, 12: 88, 13: 96, 14: 100, 15: 99,
            16: 94, 17: 84, 18: 70, 19: 51, 20: 28,
        }
        expected_disadvantage = {
            2: 28, 3: 51, 4: 70, 5: 84, 6: 94, 7: 99, 8: 100,
            9: 96, 10: 88, 11: 75, 12: 61, 13: 48, 14: 37, 15: 27,
            16: 19, 17: 12, 18: 7, 19: 3, 20: 1,
        }
        self.assertEqual(sum(advantage.values()), 1_000)
        self.assertEqual(sum(disadvantage.values()), 1_000)
        self.assertEqual(dict(sorted(advantage.items())), expected_advantage)
        self.assertEqual(dict(sorted(disadvantage.items())), expected_disadvantage)


class ProgressionRulesTests(unittest.TestCase):
    def test_margin_boundaries(self) -> None:
        margins = (-10, -9, -1, 0, 4, 5, 9, 10)
        expected = (
            "net", "constructif", "constructif", "fragile",
            "fragile", "propre", "propre", "décisif",
        )
        self.assertEqual(tuple(map(margin_class, margins)), expected)

    def test_learning_cost_examples(self) -> None:
        scores = (0, 20, 40, 60, 80, 99)
        expected = (5, 10, 17, 26, 37, 48)
        self.assertEqual(tuple(map(learning_cost, scores)), expected)

    def test_level_thresholds_1_to_20(self) -> None:
        expected = (
            0, 6, 14, 24, 36, 50, 66, 84, 104, 126,
            150, 176, 204, 234, 266, 300, 336, 374, 414, 456,
        )
        actual = tuple(level_threshold(level) for level in range(1, 21))
        self.assertEqual(actual, expected)
        self.assertTrue(all(left < right for left, right in zip(actual, actual[1:])))


class WorkedExamplesTests(unittest.TestCase):
    def test_average_character_resources(self) -> None:
        self.assertEqual(
            resources(5, 5, 5, 5, 5),
            {
                "Vitalité": 100,
                "Endurance": 90,
                "Mana": 70,
                "Équilibre": 80,
                "Concentration": 80,
            },
        )

    def test_plate_armor_trauma_examples(self) -> None:
        self.assertEqual(trauma(24, Decimal("1"), 33, Decimal("1")), 3)
        self.assertEqual(trauma(25, Decimal("1"), 14, Decimal("1")), 16)

    def test_sava_magic_example(self) -> None:
        skill = 55
        intelligence = 7
        willpower = 6
        intensity = 2
        mana_maximum = 20 + 6 * willpower + 4 * intelligence
        immediate_mana_cost = 4 + 8  # Projectile + intensité 2.
        power = 10 + 6 * intensity + skill // 10 + intelligence

        self.assertEqual(mana_maximum, 84)
        self.assertEqual(immediate_mana_cost, 12)
        self.assertEqual(power, 34)
        self.assertEqual(safe_magic_intensity(skill, intelligence, willpower), 3)

    def test_magic_overcast_threshold(self) -> None:
        safe_intensity = 3
        cast_intensity = 5
        self.assertEqual(22 + 3 * (cast_intensity - safe_intensity), 28)


class HandbookCatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.handbook = HANDBOOK_PATH.read_text(encoding="utf-8")
        cls.skills_section = cls.handbook.split(
            "# Personnage, progression et compétences", 1
        )[1].split("# Talents : cent manières de devenir soi-même", 1)[0]
        cls.talents_section = cls.handbook.split(
            "# Talents : cent manières de devenir soi-même", 1
        )[1].split("# Jouer dans le monde", 1)[0]

    def test_42_universal_skills(self) -> None:
        skills = re.findall(
            r"^####\s+(\d+)\..*?\(`([a-z_]+)`\)\s*$",
            self.skills_section,
            flags=re.MULTILINE,
        )
        self.assertEqual([int(number) for number, _ in skills], list(range(1, 43)))
        self.assertEqual(len({skill_id for _, skill_id in skills}), 42)

    def test_210_automatic_acquisitions(self) -> None:
        acquisitions = [
            int(score)
            for score in re.findall(
                r"^- \*\*(10|25|45|65|85) —",
                self.skills_section,
                flags=re.MULTILINE,
            )
        ]
        self.assertEqual(acquisitions, [10, 25, 45, 65, 85] * 42)
        self.assertEqual(len(acquisitions), 210)

    def test_100_talents(self) -> None:
        talent_numbers = [
            int(number)
            for number in re.findall(
                r"^\|\s*(\d{1,3})\s*\|",
                self.talents_section,
                flags=re.MULTILINE,
            )
        ]
        self.assertEqual(talent_numbers, list(range(1, 101)))


if __name__ == "__main__":
    unittest.main(verbosity=2)
