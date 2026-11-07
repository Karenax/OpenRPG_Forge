"""Offline admission tests for application dependency sources."""
import argparse
import copy
import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch

import application_sources as sources


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding="utf-8")


class Fixture:
    def __init__(self, root):
        self.root = root
        self.repo, self.cache = root / "repo", root / "registry"
        self.repo.mkdir()
        self.inventory = self.repo / "sources.json"
        self.name, self.version, self.key = "symphonia", "0.6.1", "test-registry"
        self.prefix = f"{self.name}-{self.version}"
        self.registry = "registry+https://github.com/rust-lang/crates.io-index"
        self.license = b"Mozilla Public License Version 2.0\nSynthetic test text, not a shipped license.\n"
        self.notice = b"// Original test contributors.\n// MPL-2.0 test source notice.\n"
        self.members = {
            "Cargo.toml": (
                '[package]\nname="symphonia"\nversion="0.6.1"\nlicense="MPL-2.0"\n'
                'repository="https://github.com/pdeljanov/Symphonia"\nauthors=["Test contributors"]\n'
            ).encode(),
            "LICENSE": self.license,
            "src/lib.rs": self.notice + b"pub fn original() {}\n",
        }
        self.extra = []
        self.refresh()

    def refresh(self):
        data = io.BytesIO()
        with tarfile.open(fileobj=data, mode="w:gz") as archive:
            for name, content in self.members.items():
                item = tarfile.TarInfo(f"{self.prefix}/{name}")
                item.size = len(content)
                archive.addfile(item, io.BytesIO(content))
                path = self.cache / "src" / self.key / self.prefix / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)
            for item, content in self.extra:
                archive.addfile(item, io.BytesIO(content))
        self.archive = data.getvalue()
        self.archive_path = self.cache / "cache" / self.key / f"{self.prefix}.crate"
        self.archive_path.parent.mkdir(parents=True, exist_ok=True)
        self.archive_path.write_bytes(self.archive)
        self.lock_package = dict(name=self.name, version=self.version, source=self.registry,
                                 checksum=sha(self.archive))
        self.write_lock()
        self.document = {
            "schema": "forge.delivery.application-source-inventory", "version": 1,
            "name_prefix": "symphonia", "registry_cache_directory": self.key,
            "components": [{
                "name": self.name, "version": self.version, "source": self.registry,
                "sha256": sha(self.archive), "bytes": len(self.archive),
                "license": "MPL-2.0", "repository": "https://github.com/pdeljanov/Symphonia",
                "authors": ["Test contributors"],
                "license_file": {"member": "LICENSE", "bytes": len(self.license),
                                 "sha256": sha(self.license)},
                "notices": [{"member": "src/lib.rs", "first_line": 1, "last_line": 2,
                             "bytes": len(self.notice), "sha256": sha(self.notice),
                             "output": "notices/symphonia-copyright.txt"}],
            }],
        }
        self.write_inventory()

    def write_lock(self):
        text = "version = 4\n[[package]]\n"
        text += "".join(f"{name} = {json.dumps(value)}\n" for name, value in self.lock_package.items())
        (self.repo / "Cargo.lock").write_text(text, encoding="utf-8")

    def write_inventory(self):
        write_json(self.inventory, self.document)

    def prepare(self):
        return sources.prepare_sources(self.repo, self.cache, self.inventory)


class SourcesAdmission(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.f = Fixture(self.root)

    def test_exact_sources_notices_and_closed_inventory_travel_offline(self):
        prepared = self.f.prepare()
        output = self.root / "applications"
        output.mkdir()
        receipt = sources.stage_sources(prepared, output)
        prefix = output / "licenses/native-media"
        self.assertEqual((prefix / "sources/symphonia-0.6.1.crate").read_bytes(), self.f.archive)
        self.assertEqual((prefix / "licenses/symphonia-0.6.1.txt").read_bytes(), self.f.license)
        self.assertEqual((prefix / "notices/symphonia-copyright.txt").read_bytes(), self.f.notice)
        manifest = json.loads((prefix / "manifest.json").read_bytes())
        actual = {p.relative_to(output).as_posix() for p in prefix.rglob("*") if p.is_file()}
        self.assertEqual(actual, set(receipt["files"]))
        self.assertEqual(actual - {"licenses/native-media/manifest.json"}, set(manifest["files"]))
        for name, pin in receipt["files"].items():
            data = (output / name).read_bytes()
            self.assertEqual(pin, {"bytes": len(data), "sha256": sha(data)})
        notice = (prefix / "NOTICE.txt").read_text(encoding="utf-8")
        self.assertIn("sources/symphonia-0.6.1.crate", notice)
        self.assertIn("MPL-2.0", notice)
        self.assertNotIn(str(self.f.cache), json.dumps(manifest))

    def test_missing_or_changed_archive_refused_without_output(self):
        self.f.archive_path.unlink()
        with self.assertRaisesRegex(ValueError, "archive|source|missing"):
            self.f.prepare()
        self.f.archive_path.write_bytes(self.f.archive + b"tamper")
        with self.assertRaisesRegex(ValueError, "archive|SHA|size"):
            self.f.prepare()
        self.assertFalse((self.root / "applications").exists())

    def test_lock_version_checksum_and_registry_must_match_inventory(self):
        for field, value in [("version", "0.6.2"), ("checksum", "0" * 64),
                             ("source", "registry+https://untrusted.invalid/index")]:
            with self.subTest(field=field):
                original = self.f.lock_package[field]
                self.f.lock_package[field] = value
                self.f.write_lock()
                with self.assertRaisesRegex(ValueError, "lock|inventory|component"):
                    self.f.prepare()
                self.f.lock_package[field] = original
        self.f.write_lock()

    def test_missing_or_changed_notices_and_license_are_refused(self):
        for field in ["license_file", "notices"]:
            with self.subTest(field=field):
                original = copy.deepcopy(self.f.document)
                pin = self.f.document["components"][0][field]
                if isinstance(pin, list):
                    pin = pin[0]
                pin["sha256"] = "0" * 64
                self.f.write_inventory()
                with self.assertRaisesRegex(ValueError, "notice|LICENSE|license|pin"):
                    self.f.prepare()
                self.f.document = original
        self.f.write_inventory()
        self.f.members.pop("LICENSE")
        self.f.refresh()
        (self.f.cache / "src" / self.f.key / self.f.prefix / "LICENSE").unlink()
        with self.assertRaisesRegex(ValueError, "LICENSE|license|member"):
            self.f.prepare()

    def test_unsafe_tar_paths_links_and_duplicate_members_are_refused(self):
        for kind in ["parent", "absolute", "link", "duplicate"]:
            with self.subTest(kind=kind):
                name = {"parent": f"{self.f.prefix}/../escaped", "absolute": "/escaped",
                        "link": f"{self.f.prefix}/linked", "duplicate": f"{self.f.prefix}/LICENSE"}[kind]
                item = tarfile.TarInfo(name)
                content = b"bad"
                item.size = len(content)
                if kind == "link":
                    item.type, item.linkname, item.size = tarfile.SYMTYPE, "outside", 0
                    content = b""
                self.f.extra = [(item, content)]
                self.f.refresh()
                with self.assertRaisesRegex(ValueError, "path|member|link|duplicate"):
                    self.f.prepare()
        self.assertFalse((self.root / "escaped").exists())

    def test_modified_cache_source_and_missing_new_lock_component_are_refused(self):
        source = self.f.cache / "src" / self.f.key / self.f.prefix / "src/lib.rs"
        source.write_bytes(source.read_bytes() + b"modified")
        with self.assertRaisesRegex(ValueError, "source|cache"):
            self.f.prepare()
        self.f.refresh()
        with (self.f.repo / "Cargo.lock").open("a", encoding="utf-8") as lock:
            lock.write('\n[[package]]\nname="symphonia-new-codec"\nversion="0.6.1"\n'
                       f'source="{self.f.registry}"\nchecksum="{"0" * 64}"\n')
        with self.assertRaisesRegex(ValueError, "lock|inventory|component"):
            self.f.prepare()

    def test_staging_never_overwrites_an_existing_bundle(self):
        prepared = self.f.prepare()
        output = self.root / "applications"
        existing = output / "licenses/native-media/keep.txt"
        existing.parent.mkdir(parents=True)
        existing.write_bytes(b"keep")
        with self.assertRaisesRegex(ValueError, "exist|destination"):
            sources.stage_sources(prepared, output)
        self.assertEqual(existing.read_bytes(), b"keep")
        self.assertEqual(list(existing.parent.iterdir()), [existing])

    def test_frozen_assembly_needs_no_cache_shared_requires_explicit_cache(self):
        self.assertIsNone(sources.prepare_for_assembly(argparse.Namespace(shared_working_tree=False), self.f.repo))
        with self.assertRaisesRegex(ValueError, "cargo.cache|cache|registry"):
            sources.prepare_for_assembly(argparse.Namespace(shared_working_tree=True, cargo_cache=None), self.f.repo)

    def test_cache_reparse_marker_is_refused_before_reading_archive(self):
        original = Path.lstat

        def stat(path, *args, **kwargs):
            if path == self.f.archive_path:
                return argparse.Namespace(st_file_attributes=0x400, st_mode=original(path).st_mode)
            return original(path, *args, **kwargs)

        with patch.object(Path, "lstat", stat):
            with self.assertRaisesRegex(ValueError, "reparse|Link"):
                self.f.prepare()

    def test_decompression_budget_is_checked_before_tar_parsing(self):
        with patch.object(sources, "MAX_UNPACKED", 256):
            with self.assertRaisesRegex(ValueError, "decompression budget"):
                self.f.prepare()


if __name__ == "__main__":
    unittest.main()
