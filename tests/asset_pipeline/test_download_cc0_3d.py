from __future__ import annotations

import importlib.util
import io
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import zipfile


SCRIPT = Path(__file__).resolve().parents[2] / "tools" / "asset_pipeline" / "download_cc0_3d.py"
SPEC = importlib.util.spec_from_file_location("download_cc0_3d", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class DownloadCc0AssetsTests(unittest.TestCase):
    def test_default_vault_matches_repository_and_runtime_convention(self) -> None:
        with mock.patch.dict("os.environ", {}, clear=True):
            self.assertEqual(
                Path("E:/OpenRPG_AssetVault"),
                MODULE.default_vault(Path("E:/OpenRPG_Forge/OpenRPG_Forge")),
            )

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

    def test_download_resumes_into_content_cache(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            vault = Path(folder) / "vault"
            destination = vault / "originals" / "source" / "asset" / "model.glb"
            destination.parent.mkdir(parents=True)
            partial = destination.parent / ".model.glb.part"
            partial.write_bytes(b"open")
            requests = []

            def open_request(request, timeout):
                requests.append((request, timeout))
                return FakeResponse(b"rpg-forge", status=206)

            with mock.patch.object(MODULE, "urlopen", side_effect=open_request):
                result = MODULE.download_file("https://example.invalid/model.glb", destination)

            self.assertEqual(b"openrpg-forge", destination.read_bytes())
            self.assertEqual("bytes=4-", requests[0][0].get_header("Range"))
            cache = (
                vault
                / "cache"
                / "downloads"
                / "sha256"
                / result["sha256"][:2]
                / result["sha256"]
            )
            self.assertTrue(cache.is_file())
            self.assertEqual(result["sha256"], MODULE.file_digest(cache))

    def test_refresh_preserves_changed_original_in_versioned_path(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            vault = Path(folder) / "vault"
            destination = vault / "originals" / "source" / "asset" / "model.glb"
            destination.parent.mkdir(parents=True)
            destination.write_bytes(b"old-original")

            with mock.patch.object(
                MODULE,
                "urlopen",
                return_value=FakeResponse(b"new-original", status=200),
            ):
                result = MODULE.download_file(
                    "https://example.invalid/model.glb",
                    destination,
                    refresh=True,
                )

            versioned = Path(result["path"])
            self.assertEqual(b"old-original", destination.read_bytes())
            self.assertEqual(b"new-original", versioned.read_bytes())
            self.assertIn("versions", versioned.parts)
            self.assertNotEqual(destination, versioned)

    def test_ambientcg_refresh_versions_archive_and_normalized_output(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            vault = Path(folder) / "vault"
            archive = vault / "originals/ambientcg/Tree/Tree_LQ-1K-JPG.zip"
            archive.parent.mkdir(parents=True)
            archive.write_bytes(b"immutable-old-archive")
            payload = io.BytesIO()
            with zipfile.ZipFile(payload, "w") as bundle:
                bundle.writestr("tree.obj", "o tree")
                bundle.writestr("tree.png", "preview")
            record = {
                "id": "Tree",
                "title": "Tree",
                "url": "https://example.invalid/tree",
                "tags": ["tree"],
                "thumbnail": None,
                "downloads": [
                    {
                        "attributes": "LQ-1K-JPG",
                        "url": "https://example.invalid/tree.zip",
                    }
                ],
            }

            with mock.patch.object(
                MODULE,
                "urlopen",
                return_value=FakeResponse(payload.getvalue(), status=200),
            ):
                result = MODULE.download_ambientcg_asset(
                    vault,
                    record,
                    "LQ-1K-JPG",
                    refresh=True,
                )

            self.assertEqual(b"immutable-old-archive", archive.read_bytes())
            self.assertIn("versions/", result["extracted_root"])
            self.assertTrue((vault / result["extracted_root"] / "tree.obj").is_file())
            self.assertIn("versions/", result["files"][0]["path"])


class FakeResponse(io.BytesIO):
    def __init__(self, value: bytes, status: int) -> None:
        super().__init__(value)
        self.status = status

    def __enter__(self):
        return self

    def __exit__(self, exception_type, exception, traceback) -> None:
        self.close()


if __name__ == "__main__":
    unittest.main()
