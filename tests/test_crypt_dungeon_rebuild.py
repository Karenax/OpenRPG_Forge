from __future__ import annotations

import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CRYPT = ROOT / "apps" / "Forge.Runtime" / "content" / "crypt-of-ashes"
AREA = CRYPT / "areas" / "93b6f81b-49eb-4e3d-a156-08ad78529525-f07a9bf666090558.json"
MANIFEST = CRYPT / "systemic-gameplay.json"
ART = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "CryptArtKitDecorator.cs"
CREDITS = ROOT / "apps" / "Forge.Runtime" / "art" / "crypt-kaykit" / "CREDITS.md"


class CryptDungeonRebuildTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.area = json.loads(AREA.read_text(encoding="utf-8"))["area"]
        cls.manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        cls.art = ART.read_text(encoding="utf-8")

    def test_area_is_no_longer_the_legacy_box(self) -> None:
        self.assertEqual({"width": 48, "depth": 76}, self.area["dimensions"])
        self.assertGreaterEqual(len(self.area["entities"]), 38)

    def test_critical_path_entities_are_preserved_and_repositioned(self) -> None:
        entities = {item["id"]: item for item in self.area["entities"]}
        expected = {
            "20000000-0000-4000-8000-000000000006": 6,   # sealed gate
            "20000000-0000-4000-8000-000000000007": 25,  # Elyra
            "20000000-0000-4000-8000-000000000008": 1,   # Sentinel
            "20000000-0000-4000-8000-000000000009": -31, # Warden
            "20000000-0000-4000-8000-000000000010": 34,  # spawn
            "20000000-0000-4000-8000-000000000011": 15,  # Ember key
            "20000000-0000-4000-8000-000000000012": -36, # exit
        }
        for entity_id, expected_z in expected.items():
            with self.subTest(entity_id=entity_id):
                self.assertIn(entity_id, entities)
                self.assertEqual(expected_z, entities[entity_id]["transform"]["position"]["z"])

    def test_area_declares_distinct_dungeon_spaces(self) -> None:
        tags = {tag for entity in self.area["entities"] for tag in entity.get("tags", [])}
        expected = {
            "zone:ember-threshold",
            "zone:elyra-sanctuary",
            "zone:catacombs",
            "zone:reliquary",
            "zone:sentinel-hall",
            "zone:flooded-ossuary",
            "zone:grand-nave",
            "zone:warden-sanctum",
        }
        self.assertTrue(expected <= tags)

    def test_sealed_gate_cannot_be_walked_around_on_the_main_axis(self) -> None:
        gate = next(item for item in self.area["entities"] if item["id"].endswith("000006"))
        self.assertIn("locked", gate["tags"])
        gate_walls = [
            item for item in self.area["entities"]
            if "sealed-gate" in item.get("tags", []) and "wall" in item.get("tags", [])
        ]
        self.assertEqual(2, len(gate_walls))
        self.assertEqual({-12, 12}, {item["transform"]["position"]["x"] for item in gate_walls})

    def test_systemic_manifest_follows_the_new_rooms(self) -> None:
        self.assertEqual(48, self.manifest["mapStructure"]["widthMeters"])
        self.assertEqual(76, self.manifest["mapStructure"]["depthMeters"])
        interactions = {item["kind"]: item for item in self.manifest["interactions"]}
        self.assertEqual(-10.0, interactions["oil"]["position"]["z"])
        self.assertEqual(-9.0, interactions["water"]["position"]["z"])
        self.assertEqual(-29.0, interactions["brazier"]["position"]["z"])
        self.assertEqual(-29.0, interactions["pillar"]["position"]["z"])
        self.assertEqual(-31.0, interactions["spikes"]["position"]["z"])

    def test_cc0_art_pass_builds_seven_zones_and_real_room_silhouettes(self) -> None:
        self.assertIn("return new CryptArtKitResult(root, instances, floors, walls, props, 7);", self.art)
        self.assertGreaterEqual(self.art.count("FillFloor(root"), 7)
        self.assertGreaterEqual(self.art.count("WallLineZ(root"), 14)
        self.assertIn("wall_doorway.glb", self.art)
        self.assertIn("column.gltf.glb", self.art)
        self.assertIn("stairs.gltf.glb", self.art)
        self.assertIn("chest.glb", self.art)
        self.assertIn("barrel_large_decorated.gltf.glb", self.art)
        self.assertIn("torch_lit.gltf.glb", self.art)
        self.assertIn("FloodedOssuaryColdLight", self.art)
        self.assertIn("AddSafetyWall", self.art)

    def test_cc0_provenance_remains_documented(self) -> None:
        credits = CREDITS.read_text(encoding="utf-8")
        self.assertIn("KayKit Dungeon Remastered", credits)
        self.assertIn("CC0-1.0", credits)


if __name__ == "__main__":
    unittest.main()
