from __future__ import annotations

import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CRYPT = ROOT / "apps" / "Forge.Runtime" / "content" / "crypt-of-ashes"
PROJECT = CRYPT / "project.json"
MANIFEST = CRYPT / "systemic-gameplay.json"
ART = ROOT / "src" / "Forge.EngineAdapter" / "Rendering" / "GodotCryptArtKitBuilder.cs"
ART_DECORATOR = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "CryptArtKitDecorator.cs"
GAME_ROOT_FILES = sorted((ROOT / "apps" / "Forge.Runtime" / "Runtime").glob("GameRoot*.cs"))
CAMERA_RIG = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "RuntimeCameraRig.cs"
HUD = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "RuntimeHud.cs"
CREDITS = ROOT / "apps" / "Forge.Runtime" / "art" / "crypt-kaykit" / "CREDITS.md"
CHARACTER_MANIFESTS = CRYPT / "assets" / "asset-manifests.json"


class CryptDungeonRebuildTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        project = json.loads(PROJECT.read_text(encoding="utf-8"))["project"]
        area_path = project["areas"][0]["documentPath"]
        cls.area = json.loads((CRYPT / area_path).read_text(encoding="utf-8"))["area"]
        cls.manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        cls.art = ART.read_text(encoding="utf-8")
        cls.art_decorator = ART_DECORATOR.read_text(encoding="utf-8")
        cls.game_root = "\n".join(path.read_text(encoding="utf-8") for path in GAME_ROOT_FILES)
        cls.camera_rig = CAMERA_RIG.read_text(encoding="utf-8")
        cls.hud = HUD.read_text(encoding="utf-8")

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
        self.assertIn(
            "return new GodotCryptArtKitResult(root, instances, floors, walls, props, ZoneCount);",
            self.art,
        )
        self.assertIn("GodotCryptArtKitBuilder.Build(InstantiateModel)", self.art_decorator)
        self.assertGreaterEqual(self.art.count("FillFloor(root"), 7)
        self.assertGreaterEqual(self.art.count("WallLineZ(root"), 14)
        self.assertIn("wall_doorway.glb", self.art)
        self.assertIn("column.gltf.glb", self.art)
        self.assertIn("stairs.gltf.glb", self.art)
        self.assertNotIn('"stairs.gltf.glb", new Vector3(0, 0, 35), 180', self.art)
        self.assertIn("chest.glb", self.art)
        self.assertIn("barrel_large_decorated.gltf.glb", self.art)
        self.assertIn("torch_lit.gltf.glb", self.art)
        self.assertIn("FloodedOssuaryColdLight", self.art)
        self.assertIn("AddSafetyWall", self.art)

    def test_lateral_reliquaries_have_matching_visual_and_physical_doorways(self) -> None:
        self.assertIn(
            "WallLineZ(root, -8, 11, 19, 90, instantiateModel, ref walls, doorwayZ: 15)",
            self.art,
        )
        self.assertIn(
            "WallLineZ(root, 8, 11, 19, -90, instantiateModel, ref walls, doorwayZ: 15)",
            self.art,
        )
        dividers = [
            entity for entity in self.area["entities"]
            if "wall" in entity["tags"]
            and "cover" in entity["tags"]
            and abs(abs(entity["transform"]["position"]["x"]) - 8) < 0.01
        ]
        self.assertEqual(4, len(dividers))
        self.assertEqual({12, 18}, {entity["transform"]["position"]["z"] for entity in dividers})
        self.assertEqual({2}, {entity["transform"]["scale"]["z"] for entity in dividers})
        self.assertIn("OPENRPG_FORGE_CRYPT_CRITICAL_PATH_PASS", self.game_root)

    def test_cc0_provenance_remains_documented(self) -> None:
        credits = CREDITS.read_text(encoding="utf-8")
        self.assertIn("KayKit Dungeon Remastered", credits)
        self.assertIn("CC0-1.0", credits)

    def test_continuous_interior_floor_does_not_duplicate_tile_colliders(self) -> None:
        self.assertIn("var usesInteriorFloorShell = isFloor && _interiorWalkableBounds is not null;", self.game_root)
        self.assertIn("if (!usesInteriorFloorShell &&", self.game_root)

    def test_crypt_uses_four_governed_animated_character_models(self) -> None:
        manifests = json.loads(CHARACTER_MANIFESTS.read_text(encoding="utf-8"))["manifests"]
        self.assertEqual(4, len(manifests))
        self.assertEqual({"CC0-1.0"}, {item["licenseSpdx"] for item in manifests})
        for item in manifests:
            model = CRYPT / item["chunks"][0]["relativePath"]
            self.assertTrue(model.is_file(), model)
            self.assertGreater(model.stat().st_size, 3_000_000)
        self.assertIn("ResolveCryptCharacterVisual", self.game_root)
        self.assertIn("OPENRPG_FORGE_CRYPT_GROUNDED_MOVEMENT_PASS", self.game_root)

    def test_camera_is_slower_and_releases_wall_collision_smoothly(self) -> None:
        self.assertIn("MouseYawSensitivity = .0032f", self.camera_rig)
        self.assertIn("MousePitchSensitivity = .0028f", self.camera_rig)
        self.assertIn("CollisionReleaseSharpness = 3.25f", self.camera_rig)
        self.assertIn("SmoothCollisionDistance", self.camera_rig)
        self.assertIn("OPENRPG_FORGE_CRYPT_CAMERA_RESPONSE_PASS", self.game_root)
        self.assertIn("_isCryptPrototype ? null : ConstrainCameraPosition", self.game_root)
        self.assertIn("OPENRPG_FORGE_CRYPT_FREE_CAMERA_PASS", self.game_root)
        self.assertIn("FirstPersonFieldOfView = 75f", self.camera_rig)
        self.assertIn("FirstPersonActive", self.camera_rig)
        self.assertIn("SyncLocalPlayerCameraVisibility", self.game_root)
        self.assertIn("OPENRPG_FORGE_CRYPT_FIRST_PERSON_ZOOM_PASS", self.game_root)

    def test_objective_and_interaction_guidance_remain_readable(self) -> None:
        self.assertIn("OffsetLeft = LargeText ? -590 : -410", self.hud)
        self.assertIn("OffsetBottom = LargeText ? 328 : 292", self.hud)
        self.assertIn("UpdateInteractionPrompt", self.hud)
        self.assertIn("reliquaire latéral", self.game_root)


if __name__ == "__main__":
    unittest.main()
