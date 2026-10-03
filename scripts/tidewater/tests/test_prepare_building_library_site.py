import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).parents[1] / "prepare-building-library-site.py"
SPEC = importlib.util.spec_from_file_location("building_site", SCRIPT)
SITE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SITE)


def digest(data):
    return hashlib.sha256(data).hexdigest()


class FishStandSiteTest(unittest.TestCase):
    def test_selects_exact_reader_inputs_from_acquired_manifest(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            bundle = root / "bundle"
            entries = {}

            def add(name, data=b"x"):
                path = bundle / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
                entries[name] = {"bytes": len(data), "sha256": digest(data)}

            for group in ("scene", "workspace", "buildings", "ocean", "shore", "spray", "clouds"):
                add(f"{group}/marker.bin")
            add("buildings/package.json", b"{}")
            attributes = {}
            for name in ("position", "normal", "uv", "color", "aLayer", "aUV2"):
                filename = f"{name}.bin"
                add(f"fish-stand/{filename}")
                attributes[name] = {"file": filename}
            add("fish-stand/indices.bin")
            textures = {}
            for name in ("surf_a", "surf_n", "surf_r", "prop_a", "prop_n", "prop_r", "signs"):
                filename = f"{name}.bin"
                add(f"fish-stand/{filename}")
                textures[name] = {"file": filename}
            for name in SITE.FISH_STAND_PROVENANCE:
                add(name)
            descriptor = json.dumps({"geometry": {"attributes": attributes, "indices": {"file": "indices.bin"}}, "textures": textures}).encode()
            add("fish-stand/fish-stand.json", descriptor)
            content_sources = {}
            for relative in (
                "source/LICENSE",
                "source/public/models/characters/LICENSE-Rocketbox.md",
                "source/public/models/characters/joe.glb",
                "source/src/game/CatchDisplay.js",
                "source/src/game/FishStand.js",
                "source/src/game/FishTable.js",
                "source/src/game/Vendor.js",
                "source/src/world/fish/FishGeometry.js",
                "source/src/world/fish/FishMaterial.js",
                "source/src/world/fish/FishProps.js",
                "source/src/world/fish/FishSpecies.js",
            ):
                name = "fish-stand-content/" + relative
                add(name, b"glTF original" if relative.endswith("joe.glb") else b"MIT source")
                content_sources[relative] = entries[name]
            species = ("jack", "redSnapper", "yellowtail", "grunt", "mullet")
            fish = []
            display = []
            for index, name in enumerate(species):
                geometry = {}
                for key, filename in (
                    ("position", "position.f32le.bin"),
                    ("normal", "normal.f32le.bin"),
                    ("a_data", "a-data.f32le.bin"),
                    ("indices", "indices.u32le.bin"),
                ):
                    path = f"fish-stand-fish/{name}/{filename}"
                    add(path, b"\0" * 12)
                    geometry[key] = {"file": filename, **entries[path], "item_size": 4, "count": 3}
                geometry["triangles"] = 1
                fish.append({"species": name, "geometry": geometry, "index": index})
                display.append({"species": name})
            source_commit = "1438b1abfcaee3267092b75573014f4d9b4a983c"
            add("fish-stand-content/content.json", json.dumps({
                "schema": "forge.tidewater.fish-stand-content-source-export",
                "source": {"commit": source_commit}, "source_files": content_sources,
                "display": {"fish_on_ice": display},
            }).encode())
            add("fish-stand-fish/fish.json", json.dumps({
                "schema": "forge.tidewater.fish-stand-fish-source-export",
                "source": {"commit": source_commit}, "fish": fish,
            }).encode())
            add("scene/joe.glb", b"glTF derivative")
            manifest = {"schema": "forge.tidewater.preview-scene", "version": 1,
                        "source_commit": source_commit,
                        "area_id": "00000000-0000-0000-0000-000000000001",
                        "building_package_sha256": digest(b"{}"), "files": entries, "scope": "test"}
            raw = json.dumps(manifest).encode()
            (bundle / "preview-scene.json").write_bytes(raw)
            selected_raw, selected = SITE.fish_stand_entries(bundle, digest(raw))
            self.assertEqual(selected_raw, raw)
            self.assertEqual(len(selected), 53)
            with self.assertRaisesRegex(AssertionError, "different building package"):
                SITE.fish_stand_entries(bundle, digest(raw), digest(b"wrong building"))
            self.assertEqual({name for name, _ in selected}, {
                "fish-stand/fish-stand.json", "fish-stand/indices.bin", *SITE.FISH_STAND_PROVENANCE,
                *(f"fish-stand/{name}.bin" for name in ("position", "normal", "uv", "color", "aLayer", "aUV2", "surf_a", "surf_n", "surf_r", "prop_a", "prop_n", "prop_r", "signs")),
                "fish-stand-content/content.json", "fish-stand-fish/fish.json", "scene/joe.glb",
                *("fish-stand-content/" + name for name in content_sources),
                *(f"fish-stand-fish/{name}/{filename}" for name in species for filename in ("position.f32le.bin", "normal.f32le.bin", "a-data.f32le.bin", "indices.u32le.bin")),
            })
            (bundle / "fish-stand-fish" / "jack" / "position.f32le.bin").write_bytes(b"tampered")
            with self.assertRaises((AssertionError, ValueError)):
                SITE.fish_stand_entries(bundle, digest(raw))


if __name__ == "__main__":
    unittest.main()
