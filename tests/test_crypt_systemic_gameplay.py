from __future__ import annotations

import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RULESET = ROOT / "docs" / "design" / "OPENRPG_FORGE_RULESET_FR.md"
CRYPT_ROOT = ROOT / "apps" / "Forge.Runtime" / "content" / "crypt-of-ashes"
MANIFEST = CRYPT_ROOT / "systemic-gameplay.json"
MODULE_README = CRYPT_ROOT / "README.md"
DIRECTOR = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "CryptSystemicGameplay.cs"
BRIDGE = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "GameRoot.SystemicGameplay.cs"
MAIN_SCENE = ROOT / "apps" / "Forge.Runtime" / "Main.tscn"


class CryptSystemicGameplayContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))

    def test_ruleset_is_canonical_and_substantial(self) -> None:
        content = RULESET.read_text(encoding="utf-8")
        self.assertGreater(len(content), 25_000)
        self.assertIn("openrpg-forge.systemic-rpg.v1", content)
        self.assertIn("Le monde comme arme", content)
        self.assertIn("Causalité", content)
        self.assertIn("compagnons", content)
        self.assertIn("Quêtes systémiques", content)
        self.assertIn("Application à la Crypte des Cendres", content)

    def test_manifest_targets_the_crypt_and_enables_world_as_weapon(self) -> None:
        self.assertEqual(1, self.manifest["schemaVersion"])
        self.assertEqual("crypt-of-ashes", self.manifest["moduleSlug"])
        self.assertEqual("openrpg-forge.systemic-rpg.v1", self.manifest["rulesetId"])
        self.assertTrue(self.manifest["worldAsWeapon"])
        self.assertEqual(
            ["observer", "comprendre", "préparer", "agir", "improviser", "assumer"],
            self.manifest["designLoop"],
        )
        self.assertEqual(48, self.manifest["mapStructure"]["widthMeters"])
        self.assertEqual(76, self.manifest["mapStructure"]["depthMeters"])

    def test_manifest_declares_required_controls(self) -> None:
        controls = {item["action"]: item["physicalKey"] for item in self.manifest["controls"]}
        self.assertEqual(
            {
                "systemic_kick": "X",
                "systemic_fire": "F",
                "systemic_frost": "G",
                "systemic_force": "B",
                "systemic_focus": "Y",
            },
            controls,
        )
        self.assertEqual(0.2, self.manifest["tacticalFocus"]["timeScale"])

    def test_manifest_declares_five_distinct_environmental_interactions(self) -> None:
        interactions = self.manifest["interactions"]
        self.assertEqual(5, len(interactions))
        self.assertEqual(
            {"oil", "water", "brazier", "pillar", "spikes"},
            {item["kind"] for item in interactions},
        )
        self.assertEqual(len(interactions), len({item["id"] for item in interactions}))
        for interaction in interactions:
            with self.subTest(interaction=interaction["id"]):
                self.assertGreater(interaction["radius"], 0)
                self.assertTrue(interaction["displayName"])
                self.assertTrue(interaction["verbs"])
                self.assertTrue(interaction["consequence"])
                position = interaction["position"]
                self.assertLessEqual(abs(position["x"]), 21)
                self.assertLessEqual(abs(position["z"]), 38)

    def test_manifest_declares_required_reaction_chains_and_approaches(self) -> None:
        chain_ids = {chain["id"] for chain in self.manifest["reactionChains"]}
        self.assertTrue(
            {"oil-fire", "water-frost-force", "force-brazier", "force-pillar", "kick-spikes"}
            <= chain_ids
        )
        approaches = {approach["id"] for approach in self.manifest["approaches"]}
        self.assertTrue(
            {"dialogue", "exploration", "direct-combat", "environmental-combat", "tactical-control"}
            <= approaches
        )

    def test_runtime_director_is_attached_and_scoped_to_the_crypt(self) -> None:
        scene = MAIN_SCENE.read_text(encoding="utf-8")
        director = DIRECTOR.read_text(encoding="utf-8")
        self.assertIn('res://Runtime/CryptSystemicGameplay.cs', scene)
        self.assertIn('name="CryptSystemicGameplay"', scene)
        self.assertIn("IsCryptSystemicModule", director)
        self.assertIn("OPENRPG_FORGE_CRYPT_SYSTEMIC_READY", director)
        self.assertIn("ManifestPath", director)
        for action in (
            "systemic_kick",
            "systemic_fire",
            "systemic_frost",
            "systemic_force",
            "systemic_focus",
        ):
            self.assertIn(action, director)

    def test_environmental_damage_reuses_the_existing_combat_state(self) -> None:
        bridge = BRIDGE.read_text(encoding="utf-8")
        director = DIRECTOR.read_text(encoding="utf-8")
        self.assertIn("DamageHostile(hostile)", bridge)
        self.assertIn("_hostiles", bridge)
        self.assertIn("DefeatSystemicHostile", bridge)
        self.assertIn("DamageSystemicHostile", director)
        self.assertNotIn("environmentalHealth", director)

    def test_module_documentation_links_rules_controls_and_limits(self) -> None:
        content = MODULE_README.read_text(encoding="utf-8")
        self.assertIn("OPENRPG_FORGE_RULESET_FR.md", content)
        self.assertIn("systemic-gameplay.json", content)
        self.assertIn("Limites explicites", content)
        self.assertIn("python -m unittest tests.test_crypt_systemic_gameplay", content)


if __name__ == "__main__":
    unittest.main()
