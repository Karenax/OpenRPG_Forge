from __future__ import annotations

import importlib.util
from pathlib import Path
import tempfile
import unittest
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


if __name__ == "__main__":
    unittest.main()
