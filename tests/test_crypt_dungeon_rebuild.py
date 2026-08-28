from __future__ import annotations

import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CRYPT = ROOT / "apps" / "Forge.Runtime" / "content" / "crypt-of-ashes"
PROJECT = CRYPT / "project.json"
MANIFEST = CRYPT / "systemic-gameplay.json"
ART = ROOT / "src" / "Forge.EngineAdapter" / "Rendering" / "GodotArtKitBuilder.cs"
ART_LAYOUT = CRYPT / "art-kits" / "kaykit-dungeon-remastered.layout.json"
ART_DECORATOR = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "BundledArtKitResolver.cs"
GAME_ROOT_FILES = sorted((ROOT / "apps" / "Forge.Runtime" / "Runtime").glob("GameRoot*.cs"))
CAMERA_RIG = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "RuntimeCameraRig.cs"
HUD = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "RuntimeHud.cs"
RUNTIME_COMBAT = ROOT / "apps" / "Forge.Runtime" / "Runtime" / "GameRoot.RuntimeUiCombat.cs"
CREDITS = ROOT / "apps" / "Forge.Runtime" / "art" / "kaykit-dungeon-remastered" / "CREDITS.md"
CHARACTER_MANIFESTS = CRYPT / "assets" / "asset-manifests.json"


class CryptDungeonRebuildTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        project = json.loads(PROJECT.read_text(encoding="utf-8"))["project"]
        cls.project = project
        area_path = project["areas"][0]["documentPath"]
        cls.area = json.loads((CRYPT / area_path).read_text(encoding="utf-8"))["area"]
        cls.manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        cls.art_layout = json.loads(ART_LAYOUT.read_text(encoding="utf-8"))
        cls.art = ART.read_text(encoding="utf-8")
        cls.art_decorator = ART_DECORATOR.read_text(encoding="utf-8")
        cls.game_root = "\n".join(path.read_text(encoding="utf-8") for path in GAME_ROOT_FILES)
        cls.camera_rig = CAMERA_RIG.read_text(encoding="utf-8")
        cls.hud = HUD.read_text(encoding="utf-8")
        cls.runtime_combat = RUNTIME_COMBAT.read_text(encoding="utf-8")

    def test_module_contract_drives_boss_event_without_slug_branch(self) -> None:
        boss = next(item for item in self.project["blueprints"] if item["slug"] == "ash-warden-boss")
        events = boss["creatureEvents"]
        event_name = events["overrides"]["runtime.phase_two.custom_event"]

        self.assertEqual("crypt.braziers_lit", event_name)
        self.assertIn(event_name, {item["qualifiedName"] for item in events["customEvents"]})
        self.assertIn('"runtime.phase_two.custom_event"', self.runtime_combat)
        self.assertNotIn('"crypt.braziers_lit"', self.runtime_combat)
        self.assertNotIn('string.Equals(_moduleSlug, "crypt-of-ashes"', self.game_root)

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
        self.assertEqual("kaykit-dungeon-remastered", self.art_layout["id"])
        self.assertEqual(7, self.art_layout["zones"])
        self.assertEqual(7, len(self.art_layout["floorFills"]))
        self.assertGreaterEqual(len(self.art_layout["wallLines"]), 24)
        self.assertEqual(33, len(self.art_layout["instances"]))
        models = {
            item["model"]
            for category in ("floorFills", "wallLines", "instances")
            for item in self.art_layout[category]
        }
        self.assertTrue(
            {
                "wall.gltf.glb",
                "column.gltf.glb",
                "stairs.gltf.glb",
                "chest.glb",
                "barrel_large_decorated.gltf.glb",
                "torch_lit.gltf.glb",
            }
            <= models
        )
        self.assertEqual(10, len(self.art_layout["collisionBoxes"]))
        self.assertIn("FloodedOssuaryColdLight", {item["name"] for item in self.art_layout["lights"]})
        self.assertIn("GodotArtKitLayoutJson.Deserialize", self.art_decorator)
        self.assertIn("GodotArtKitBuilder.Build", self.art_decorator)
        self.assertNotIn("GodotKayKitDungeonArtBuilder", self.art_decorator + self.art)

    def test_lateral_reliquaries_have_matching_visual_and_physical_doorways(self) -> None:
        doorway_lines = [
            line for line in self.art_layout["wallLines"]
            if line["axis"] == "z" and abs(line["fixed"]) == 8 and line.get("doorwayAt") == 15
        ]
        self.assertEqual({-8, 8}, {line["fixed"] for line in doorway_lines})
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
        character_tags = {"player", "elyra", "sentinel", "warden"}
        characters = [
            entity for entity in self.area["entities"]
            if character_tags.intersection(entity.get("tags", []))
        ]
        self.assertEqual(4, len(characters))
        for character in characters:
            properties = {
                item["key"]: item["value"]
                for item in character["overrides"]["properties"]
            }
            self.assertIn("visual.asset", properties)
            self.assertEqual("kaykit-adventurer", properties["runtime.animationProfile"])
        self.assertNotIn("ResolveCryptCharacterVisual", self.game_root)
        self.assertIn("OPENRPG_FORGE_CRYPT_GROUNDED_MOVEMENT_PASS", self.game_root)

    def test_camera_is_slower_and_releases_wall_collision_smoothly(self) -> None:
        self.assertIn("MouseYawSensitivity = .0032f", self.camera_rig)
        self.assertIn("MousePitchSensitivity = .0028f", self.camera_rig)
        self.assertIn("CollisionReleaseSharpness = 3.25f", self.camera_rig)
        self.assertIn("SmoothCollisionDistance", self.camera_rig)
        self.assertIn("OPENRPG_FORGE_CRYPT_CAMERA_RESPONSE_PASS", self.game_root)
        self.assertIn("_moduleExperience.ConstrainCamera ? ConstrainCameraPosition : null", self.game_root)
        self.assertNotIn("_isCryptPrototype", self.game_root)
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
