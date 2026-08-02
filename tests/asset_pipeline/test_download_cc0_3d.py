from __future__ import annotations

import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile


SCRIPT = Path(__file__).resolve().parents[2] / "tools" / "asset_pipeline" / "download_cc0_3d.py"
SPEC = importlib.util.spec_from_file_location("download_cc0_3d", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class DownloadCc0AssetsTests(unittest.TestCase):
    def test_select_ids_is_deterministic_and_limited(self) -> None:
        records = [{"id": "z"}, {"id": "A"}, {"id": "m"}]
        self.assertEqual(["A", "m"], [item["id"] for item in MODULE.select_ids(records, 2)])

    def test_file_digest_matches_known_sha256(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "asset.glb"
            path.write_bytes(b"openrpg-forge")
            self.assertEqual(
                "afb6e6748e45e0fbf5b34b1046d5060016b52f3c99fd5add234c4d4245f5929b",
                MODULE.file_digest(path),
            )

    def test_relativize_vault_paths_keeps_urls(self) -> None:
        vault = Path("E:/OpenRPG_AssetVault")
        value = {
            "path": "E:/OpenRPG_AssetVault/originals/source/model.glb",
            "url": "https://example.test/OpenRPG_AssetVault/model.glb",
        }
        self.assertEqual(
            {
                "path": "originals/source/model.glb",
                "url": "https://example.test/OpenRPG_AssetVault/model.glb",
            },
            MODULE.relativize_vault_paths(value, vault),
        )

    def test_safe_extract_zip_rejects_parent_traversal(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            archive = root / "unsafe.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr("../escape.txt", "bad")
            with self.assertRaisesRegex(RuntimeError, "Unsafe archive path"):
                MODULE.safe_extract_zip(archive, root / "output")

    def test_safe_extract_zip_lists_supported_content(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            archive = root / "safe.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr("model/asset.gltf", "{}")
                bundle.writestr("model/textures/color.jpg", "data")
            extracted = MODULE.safe_extract_zip(archive, root / "output")
            self.assertEqual(["model/asset.gltf", "model/textures/color.jpg"], extracted)


if __name__ == "__main__":
    unittest.main()
