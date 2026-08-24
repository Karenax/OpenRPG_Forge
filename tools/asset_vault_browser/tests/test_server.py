from __future__ import annotations

import json
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from pathlib import Path

from tools.asset_vault_browser.server import (
    AssetBrowserServer,
    CatalogIndex,
    SelectionStore,
    safe_vault_path,
)


class AssetVaultBrowserTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.vault = self.root / "vault"
        self.vault.mkdir()

        ambient_root = self.vault / "normalized" / "ambientcg" / "3DTree001" / "lq"
        ambient_root.mkdir(parents=True)
        (ambient_root / "3DTree001.obj").write_text("o Tree\n", encoding="utf-8")
        (ambient_root / "3DTree001.png").write_bytes(b"png")

        poly_root = self.vault / "originals" / "polyhaven" / "rock" / "gltf-1k"
        poly_root.mkdir(parents=True)
        (poly_root / "rock.gltf").write_text("{}", encoding="utf-8")

        kenney_root = self.vault / "normalized" / "kenney" / "forest-pack"
        (kenney_root / "Models" / "GLB format").mkdir(parents=True)
        (kenney_root / "Models" / "OBJ format").mkdir(parents=True)
        (kenney_root / "Previews").mkdir(parents=True)
        (kenney_root / "Models" / "GLB format" / "tree.glb").write_bytes(b"glTFfixture")
        (kenney_root / "Models" / "OBJ format" / "tree.obj").write_text("o Tree\n", encoding="utf-8")
        (kenney_root / "Previews" / "tree.png").write_bytes(b"png")

        self.main_catalog = self.root / "main.json"
        self.main_catalog.write_text(
            json.dumps(
                {
                    "assets": [
                        {
                            "asset_id": "ambientcg:3DTree001",
                            "source": "ambientcg",
                            "source_asset_id": "3DTree001",
                            "name": "3D Tree 001",
                            "extracted_root": "normalized/ambientcg/3DTree001/lq",
                            "model_files": ["3DTree001.obj"],
                            "license": "CC0-1.0",
                            "tags": ["tree"],
                        },
                        {
                            "asset_id": "polyhaven:rock",
                            "source": "polyhaven",
                            "source_asset_id": "rock",
                            "name": "Rock",
                            "files": [
                                {
                                    "path": "originals/polyhaven/rock/gltf-1k/rock.gltf",
                                    "size": 2,
                                }
                            ],
                            "license": "CC0-1.0",
                            "tags": ["rock"],
                        },
                    ]
                }
            ),
            encoding="utf-8",
        )
        self.kenney_catalog = self.root / "kenney.json"
        self.kenney_catalog.write_text(
            json.dumps(
                {
                    "packs": [
                        {
                            "pack_id": "kenney:forest-pack",
                            "name": "Forest Pack",
                            "source_page": "https://example.invalid/forest",
                            "extracted_root": "normalized/kenney/forest-pack",
                            "model_files": [
                                "Models/OBJ format/tree.obj",
                                "Models/GLB format/tree.glb",
                            ],
                            "license": "CC0-1.0",
                        }
                    ]
                }
            ),
            encoding="utf-8",
        )
        collection_root = self.root / "packs"
        collection_root.mkdir()
        (collection_root / "medieval.json").write_text(
            json.dumps(
                {
                    "schema_version": 1,
                    "collection_type": "asset-vault-collection",
                    "pack_id": "medieval",
                    "display_name": "Médiéval",
                    "source_pack_ids": ["kenney:forest-pack"],
                    "tags": ["medieval"],
                }
            ),
            encoding="utf-8",
        )
        self.index = CatalogIndex(self.vault, self.main_catalog, self.kenney_catalog)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_index_expands_kenney_models_and_prefers_glb(self) -> None:
        self.assertEqual(3, len(self.index.items))
        tree = self.index.by_id["kenney:forest-pack:tree"]
        self.assertEqual("glb", tree.format)
        self.assertEqual("medieval", tree.pack_id)
        self.assertEqual("Médiéval", tree.pack_name)
        self.assertIn("medieval", tree.tags)
        self.assertTrue(tree.viewer_supported)
        self.assertEqual(2, len(tree.variants))
        self.assertEqual(1, self.index.query(query="forest tree")["total"])
        self.assertEqual(1, self.index.query(source="ambientCG")["total"])

    def test_selection_round_trip_is_versioned_and_rejects_unknown_ids(self) -> None:
        path = self.root / "selection.json"
        store = SelectionStore(path, self.index)
        result = store.save(["kenney:forest-pack:tree"], "Réalisme forestier")
        self.assertEqual(1, result["schemaVersion"])
        self.assertEqual(["kenney:forest-pack:tree"], result["assetIds"])
        self.assertEqual("Réalisme forestier", SelectionStore(path, self.index).payload()["note"])
        with self.assertRaises(ValueError):
            store.save(["missing:asset"], "")

    def test_additional_cc0_pack_catalog_is_expanded_and_prefers_fbx_over_obj(self) -> None:
        kaykit_root = self.vault / "normalized" / "kaykit" / "dungeon"
        kaykit_root.mkdir(parents=True)
        (kaykit_root / "door.obj").write_text("o Door\n", encoding="utf-8")
        (kaykit_root / "door.fbx").write_bytes(b"fbx")
        material_root = self.vault / "originals" / "polyhaven" / "materials" / "stone" / "2k"
        material_root.mkdir(parents=True)
        (material_root / "stone_diff_2k.jpg").write_bytes(b"jpg")
        (material_root / "stone.preview.png").write_bytes(b"png")
        catalog = self.root / "kaykit.json"
        catalog.write_text(
            json.dumps(
                {
                    "assets": [
                        {
                            "asset_id": "polyhaven-material:stone",
                            "source": "polyhaven",
                            "source_asset_id": "stone",
                            "name": "Stone",
                            "kind": "material",
                            "primary_file": "originals/polyhaven/materials/stone/2k/stone_diff_2k.jpg",
                            "preview_file": "originals/polyhaven/materials/stone/2k/stone.preview.png",
                            "files": [
                                {
                                    "path": "originals/polyhaven/materials/stone/2k/stone_diff_2k.jpg",
                                    "size": 3,
                                }
                            ],
                            "license": "CC0-1.0",
                            "categories": ["Materials"],
                            "tags": ["stone"],
                        }
                    ],
                    "packs": [
                        {
                            "pack_id": "kaykit:dungeon",
                            "source": "kaykit",
                            "name": "Dungeon Pack",
                            "source_page": "https://example.invalid/dungeon",
                            "extracted_root": "normalized/kaykit/dungeon",
                            "model_files": ["door.obj", "door.fbx"],
                            "license": "CC0-1.0",
                            "categories": ["Dungeon", "Architecture"],
                            "tags": ["modular"],
                        }
                    ]
                }
            ),
            encoding="utf-8",
        )

        index = CatalogIndex(self.vault, self.main_catalog, self.kenney_catalog, [catalog])

        door = index.by_id["kaykit:dungeon:door"]
        self.assertEqual("KayKit", door.source)
        self.assertEqual("fbx", door.format)
        self.assertIn("Dungeon", door.categories)
        self.assertEqual(1, index.query(query="kaykit modular dungeon")['total'])
        material = index.by_id["polyhaven-material:stone"]
        self.assertEqual("material", material.kind)
        self.assertEqual("jpg", material.format)
        self.assertFalse(material.viewer_supported)
        self.assertTrue(material.preview_url.endswith("stone.preview.png"))

    def test_http_api_and_range_requests_work_on_loopback(self) -> None:
        store = SelectionStore(self.root / "selection.json", self.index)
        server = AssetBrowserServer(("127.0.0.1", 0), self.index, store)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        base = f"http://127.0.0.1:{server.server_address[1]}"
        try:
            with urllib.request.urlopen(f"{base}/api/meta", timeout=3) as response:
                meta = json.load(response)
            self.assertEqual(3, meta["logicalModelCount"])

            model_url = self.index.by_id["kenney:forest-pack:tree"].model_url
            request = urllib.request.Request(f"{base}{model_url}", headers={"Range": "bytes=0-3"})
            with urllib.request.urlopen(request, timeout=3) as response:
                self.assertEqual(206, response.status)
                self.assertEqual(b"glTF", response.read())

            selection_request = urllib.request.Request(
                f"{base}/api/selection",
                data=json.dumps({"assetIds": ["kenney:forest-pack:tree"], "note": "Forest"}).encode("utf-8"),
                headers={"Content-Type": "application/json"},
                method="POST",
            )
            with urllib.request.urlopen(selection_request, timeout=3) as response:
                saved = json.load(response)
            self.assertEqual(["kenney:forest-pack:tree"], saved["assetIds"])

            with self.assertRaises(urllib.error.HTTPError) as context:
                urllib.request.urlopen(f"{base}/vault/../main.json", timeout=3)
            self.assertIn(context.exception.code, {400, 404})
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=3)

    def test_safe_vault_path_rejects_traversal(self) -> None:
        with self.assertRaises(ValueError):
            safe_vault_path(self.vault, "../outside.txt")


if __name__ == "__main__":
    unittest.main()
