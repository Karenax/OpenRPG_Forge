"""Focused, Cargo-free checks for Rust notice source selection."""

from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


COLLECTOR_PATH = Path(__file__).resolve().parents[1] / "collect_rust_notices.py"
SPEC = importlib.util.spec_from_file_location("collect_rust_notices", COLLECTOR_PATH)
assert SPEC is not None and SPEC.loader is not None
collector = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = collector
SPEC.loader.exec_module(collector)


class NoticeSourceSelectionTests(unittest.TestCase):
    def package(self, root: Path, *, source: str | None = None, license_file: str | None = None) -> dict:
        manifest = root / "Cargo.toml"
        manifest.write_text('[package]\nname = "sample"\n', encoding="utf-8")
        return {
            "manifest_path": str(manifest),
            "source": source,
            "license_file": license_file,
            "license": "MIT OR Apache-2.0",
        }

    def test_registry_sources_include_nested_notice_files(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            nested = root / "components" / "codec"
            nested.mkdir(parents=True)
            (nested / "LICENSE-MIT").write_text("verified fixture\n", encoding="utf-8")

            found = collector.notice_files(self.package(root, source="registry+https://crates.io"))

            self.assertEqual([path.relative_to(root).as_posix() for path in found], ["components/codec/LICENSE-MIT"])

    def test_path_package_does_not_scan_unrelated_nested_trees(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "LICENSE").write_text("package license\n", encoding="utf-8")
            assets = root / "frontend" / "assets"
            assets.mkdir(parents=True)
            (assets / "NOTICE.txt").write_text("unrelated asset notice\n", encoding="utf-8")

            found = collector.notice_files(self.package(root))

            self.assertEqual([path.relative_to(root).as_posix() for path in found], ["LICENSE"])

    def test_declared_license_file_is_included_even_with_nonstandard_name(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            declared = root / "legal" / "terms.txt"
            declared.parent.mkdir()
            declared.write_text("declared license text\n", encoding="utf-8")

            found = collector.notice_files(self.package(root, license_file="legal/terms.txt"))

            self.assertEqual([path.relative_to(root).as_posix() for path in found], ["legal/terms.txt"])

    def test_spdx_expression_alone_does_not_invent_a_text_file(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)

            found = collector.notice_files(self.package(root))

            self.assertEqual(found, [])


if __name__ == "__main__":
    unittest.main()
