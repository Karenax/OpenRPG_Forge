from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import zipfile


SCRIPT = Path(__file__).resolve().parents[2] / "tools" / "asset_pipeline" / "import_manual_cc0_packs.py"
SPEC = importlib.util.spec_from_file_location("import_manual_cc0_packs", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class ImportManualCc0PacksTests(unittest.TestCase):
    def test_audit_rejects_hash_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            archive = Path(folder) / "pack.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr("License.txt", "Creative Commons Zero https://creativecommons.org/publicdomain/zero/1.0/")
            pack = {"sha256": "0" * 64, "license_path": "License.txt"}
            with self.assertRaisesRegex(RuntimeError, "SHA-256 mismatch"):
                MODULE.audit_archive(archive, pack)

    def test_audit_rejects_executable_content(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            archive = Path(folder) / "pack.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr("License.txt", "Creative Commons Zero https://creativecommons.org/publicdomain/zero/1.0/")
                bundle.writestr("setup.exe", b"not allowed")
            pack = {"sha256": MODULE.file_digest(archive), "license_path": "License.txt"}
            with self.assertRaisesRegex(RuntimeError, "Executable content"):
                MODULE.audit_archive(archive, pack)

    def test_audit_accepts_cc0_universal_wording(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            archive = Path(folder) / "pack.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr("License.txt", "CC0 1.0 Universal https://creativecommons.org/publicdomain/zero/1.0/")
            pack = {"sha256": MODULE.file_digest(archive), "license_path": "License.txt"}
            result = MODULE.audit_archive(archive, pack)
            self.assertEqual(["License.txt"], result["license_evidence_files"])

    def test_import_preserves_original_and_catalogs_supported_models(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            vault = root / "vault"
            inbox = vault / "inbox" / "manual"
            inbox.mkdir(parents=True)
            archive = inbox / "pack.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr("Pack/License.txt", "Creative Commons Zero https://creativecommons.org/publicdomain/zero/1.0/")
                bundle.writestr("Pack/Models/tree.gltf", '{"asset":{"version":"2.0"}}')
                bundle.writestr("Pack/Models/tree.obj", "o tree")
                bundle.writestr("Pack/Models/source.blend", "source")
                bundle.writestr("Pack/Preview.png", "preview")
            pack = {
                "source": "test",
                "slug": "pack",
                "name": "Pack",
                "author": "Author",
                "version": "1.0",
                "archive": archive.name,
                "sha256": MODULE.file_digest(archive),
                "source_page": "https://example.invalid/pack",
                "license_path": "Pack/License.txt",
                "categories": ["Nature"],
                "tags": ["tree"],
            }
            result = MODULE.import_pack(
                vault,
                inbox,
                pack,
                catalog_date="2026-08-21",
                retrieved_at="2026-08-21T00:00:00Z",
            )
            self.assertTrue(archive.is_file())
            self.assertTrue((vault / result["files"][0]["path"]).is_file())
            self.assertEqual(["Pack/Models/tree.gltf", "Pack/Models/tree.obj"], result["model_files"])
            self.assertEqual(["Pack/Preview.png"], result["preview_files"])
            self.assertEqual("downloaded-and-extracted", result["status"])

    def test_gltf_uri_repair_requires_and_uses_local_dependency(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            model = root / "model.gltf"
            model.write_text('{"images":[{"uri":"normal_png.png"}]}', encoding="utf-8")
            (root / "normal.png").write_bytes(b"texture")
            repairs = MODULE.apply_gltf_uri_repairs(root, ["model.gltf"], {"normal_png.png": "normal.png"})
            self.assertEqual([{"file": "model.gltf", "from": "normal_png.png", "to": "normal.png"}], repairs)
            self.assertIn('"uri":"normal.png"', model.read_text(encoding="utf-8"))

    def test_merge_existing_preserves_unselected_catalog_entries(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            vault = root / "vault"
            inbox = root / "inbox"
            inbox.mkdir(parents=True)
            archive = inbox / "new.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr(
                    "New/License.txt",
                    "CC0 1.0 Universal https://creativecommons.org/publicdomain/zero/1.0/",
                )
                bundle.writestr("New/model.glb", b"glTF")
            pack = {
                "source": "test",
                "slug": "new",
                "name": "New",
                "author": "Author",
                "version": "1.0",
                "archive": archive.name,
                "sha256": MODULE.file_digest(archive),
                "source_page": "https://example.invalid/new",
                "license_path": "New/License.txt",
                "categories": ["Props"],
                "tags": ["new"],
            }
            output = root / "catalog.json"
            output.write_text(json.dumps({
                "schema_version": 1,
                "license_policy": "cc0-only",
                "error_count": 0,
                "packs": [{
                    "pack_id": "test:preserved",
                    "model_file_count": 1,
                    "unique_model_name_count": 1,
                    "total_bytes": 5,
                }],
            }), encoding="utf-8")

            with mock.patch.object(MODULE, "PACKS", (pack,)):
                result = MODULE.main([
                    "--vault", str(vault),
                    "--inbox", str(inbox),
                    "--catalog-output", str(output),
                    "--pack", "new",
                    "--merge-existing",
                ])

            self.assertEqual(0, result)
            catalog = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual(2, catalog["pack_count"])
            self.assertEqual(["test:new", "test:preserved"], [item["pack_id"] for item in catalog["packs"]])

    def test_medieval_collection_pins_five_cataloged_archives(self) -> None:
        repository = SCRIPT.parents[2]
        manifest = json.loads(
            (repository / "asset_manifests" / "packs" / "medieval.json").read_text(encoding="utf-8")
        )
        catalog_packs = {}
        for relative in manifest["source_catalogs"]:
            catalog = json.loads((repository / relative).read_text(encoding="utf-8"))
            catalog_packs.update({item["pack_id"]: item for item in catalog["packs"]})

        self.assertEqual("Médiéval", manifest["display_name"])
        self.assertEqual(5, manifest["archive_count"])
        self.assertEqual(5, len(manifest["source_pack_ids"]))
        self.assertEqual(manifest["source_pack_ids"], [item["pack_id"] for item in manifest["archives"]])
        for archive in manifest["archives"]:
            catalog_pack = catalog_packs[archive["pack_id"]]
            self.assertEqual(archive["sha256"], catalog_pack["archive_sha256"])
            self.assertEqual(archive["model_file_count"], catalog_pack["model_file_count"])
            self.assertEqual(archive["logical_model_count"], catalog_pack["unique_model_name_count"])
        self.assertEqual(manifest["archive_bytes"], sum(item["bytes"] for item in manifest["archives"]))
        self.assertEqual(manifest["model_file_count"], sum(item["model_file_count"] for item in manifest["archives"]))
        self.assertEqual(manifest["logical_model_count"], sum(item["logical_model_count"] for item in manifest["archives"]))


if __name__ == "__main__":
    unittest.main()
