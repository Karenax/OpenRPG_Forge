from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest


TOOLS = Path(__file__).resolve().parents[2] / "tools" / "asset_pipeline"
sys.path.insert(0, str(TOOLS))
SCRIPT = TOOLS / "asset_vault_lifecycle.py"
SPEC = importlib.util.spec_from_file_location("asset_vault_lifecycle", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class AssetVaultLifecycleTests(unittest.TestCase):
    def test_update_plan_detects_added_updated_removed_and_unchanged(self) -> None:
        current = catalog(
            entry("source:removed", sha="1"),
            entry("source:updated", sha="2"),
            entry("source:unchanged", sha="3"),
        )
        incoming = catalog(
            entry("source:added", sha="4"),
            entry("source:updated", sha="changed"),
            entry("source:unchanged", sha="3"),
        )

        result = MODULE.plan_catalog_update(current, incoming)

        self.assertEqual(["source:added"], result["added"])
        self.assertEqual(["source:updated"], result["updated"])
        self.assertEqual(["source:removed"], result["removed"])
        self.assertEqual(["source:unchanged"], result["unchanged"])
        self.assertEqual(["source:added", "source:updated"], result["requires_refresh"])

    def test_uninstall_plan_is_dry_and_apply_moves_content_to_recoverable_cache(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            vault = Path(folder) / "vault"
            original = write(vault, "originals/source/tree/tree.zip", b"original")
            normalized = write(vault, "normalized/source/tree/model.glb", b"model")
            metadata = write(vault, "metadata/source/tree.json", b"{}")
            source_catalog = catalog(
                entry(
                    "source:tree",
                    path="originals/source/tree/tree.zip",
                    extracted_root="normalized/source/tree",
                )
            )

            plan = MODULE.plan_uninstall(vault, source_catalog, "source:tree")

            self.assertTrue(original.exists())
            self.assertTrue(normalized.exists())
            self.assertTrue(metadata.exists())
            self.assertEqual(3, len(plan["paths"]))

            result = MODULE.apply_uninstall(vault, plan)

            self.assertFalse(original.exists())
            self.assertFalse(normalized.exists())
            self.assertFalse(metadata.exists())
            backup = vault / result["backup_root"]
            self.assertEqual(b"original", (backup / "originals/source/tree/tree.zip").read_bytes())
            self.assertEqual(b"model", (backup / "normalized/source/tree/model.glb").read_bytes())
            self.assertTrue((vault / result["receipt"]).is_file())
            registry = json.loads((vault / "metadata/uninstalled-assets.v1.json").read_text(encoding="utf-8"))
            self.assertEqual("source:tree", registry["assets"][0]["asset_id"])

    def test_uninstall_refuses_changed_content_without_moving_any_path(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            vault = Path(folder) / "vault"
            original = write(vault, "originals/source/tree/tree.zip", b"original")
            source_catalog = catalog(entry("source:tree", path="originals/source/tree/tree.zip"))
            plan = MODULE.plan_uninstall(vault, source_catalog, "source:tree")
            original.write_bytes(b"changed")

            with self.assertRaisesRegex(RuntimeError, "changed after planning"):
                MODULE.apply_uninstall(vault, plan)

            self.assertEqual(b"changed", original.read_bytes())

    def test_unsafe_catalog_path_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            vault = Path(folder) / "vault"
            with self.assertRaisesRegex(RuntimeError, "Unsafe Asset Vault path"):
                MODULE.plan_uninstall(
                    vault,
                    catalog(entry("source:tree", path="../escape.zip")),
                    "source:tree",
                )

    def test_uninstall_rejects_tampered_plan_fingerprint(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            vault = Path(folder) / "vault"
            original = write(vault, "originals/source/tree/tree.zip", b"original")
            plan = MODULE.plan_uninstall(
                vault,
                catalog(entry("source:tree", path="originals/source/tree/tree.zip")),
                "source:tree",
            )
            plan["paths"][0]["path"] = "originals/source/other"

            with self.assertRaisesRegex(RuntimeError, "fingerprint is invalid"):
                MODULE.apply_uninstall(vault, plan)

            self.assertTrue(original.exists())

    def test_cache_report_counts_objects_partials_and_uninstalled_assets(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            vault = Path(folder) / "vault"
            write(vault, "cache/downloads/sha256/aa/hash", b"cache")
            write(vault, "originals/source/tree/.tree.zip.part", b"part")
            registry = {"schema_version": 1, "assets": [{"asset_id": "source:tree"}]}
            path = vault / "metadata/uninstalled-assets.v1.json"
            path.parent.mkdir(parents=True)
            path.write_text(json.dumps(registry), encoding="utf-8")

            result = MODULE.cache_report(vault)

            self.assertEqual(1, result["cached_objects"])
            self.assertEqual(5, result["cached_bytes"])
            self.assertEqual(1, result["resumable_partials"])
            self.assertEqual(1, result["uninstalled_assets"])


def catalog(*entries):
    return {"schema_version": 1, "assets": list(entries)}


def entry(
    asset_id: str,
    *,
    sha: str = "hash",
    path: str | None = None,
    extracted_root: str | None = None,
):
    value = {
        "asset_id": asset_id,
        "source": "source",
        "source_asset_id": asset_id.split(":")[-1],
        "variant": "1k",
        "files": [
            {
                "url": f"https://example.invalid/{asset_id}",
                "sha256": sha,
                "size": 1,
            }
        ],
    }
    if path:
        value["files"][0]["path"] = path
    if extracted_root:
        value["extracted_root"] = extracted_root
    return value


def write(root: Path, relative: str, content: bytes) -> Path:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)
    return path


if __name__ == "__main__":
    unittest.main()
