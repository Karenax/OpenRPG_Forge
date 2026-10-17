"""Offline admission tests plus the actual assemble branch with mocked binaries."""
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
import build_delivery as delivery
import build_classic_installer as classic


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


class AssembleBranch(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.args = self.inputs()

    def inputs(self):
        root = self.root
        binaries = root / "bin"
        binaries.mkdir()
        paths = {}
        for name in ["delivery_cli", "hub", "launcher", "studio", "runtime", "webview2"]:
            paths[name] = binaries / f"{name}.exe"
            paths[name].write_bytes(name.encode())
        playtest = root / "playtest"
        for name in ["data/scene/LICENSE-Tidewater.txt", "data/scene/CREDITS-Tidewater.md",
                     "licenses/old.txt", "content/old.txt"]:
            path = playtest / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("historical input", encoding="utf-8")
        write_json(playtest / "data/scene/fixture.json", {})
        write_json(playtest / "data/scene/static-assets.json", {})
        write_json(playtest / "data/buildings/package.json", {})
        write_json(playtest / "data/workspace/area/authoring.json", {"head":{"base":{"terrain":{"area_id":"area"}}}})
        proofs = root / "proofs"
        source = proofs / "source-inputs.json"
        write_json(source, {"head":"revision", "scope":["apps/forge_launcher"],
                            "files":{"apps/forge_launcher/Cargo.toml":{}}})
        common = dict(schema="forge.delivery.native-build.v1", status="BUILD_ONLY",
                      source_stable_during_build=True, source_manifest_sha256=delivery.digest(source))
        launcher_proof = proofs / "launcher.json"
        write_json(launcher_proof, {**common, "current_source_manifest_sha256":delivery.digest(source),
                                   "outputs":{f"forge-{name}.exe":delivery.digest(paths[key])
                                              for name, key in [("launcher","launcher"),("hub","hub"),("delivery","delivery_cli")]}})
        frontend = root / "frontend"
        static = root / "static"
        write_json(static / "frontend-static-inputs.json", {})
        write_json(frontend / "forge-studio-local-assets-manifest.json", {"asset_root":str(static)})
        studio_proof, runtime_proof = proofs / "studio.json", proofs / "game.json"
        write_json(studio_proof, {**common,"frontend":str(frontend),"executable_sha256":delivery.digest(paths["studio"])})
        write_json(runtime_proof, {**common,"executable_sha256":delivery.digest(paths["runtime"])})
        return argparse.Namespace(**paths, playtest=playtest, aube_proof=launcher_proof,
                                  launcher_proof=launcher_proof, studio_proof=studio_proof,
                                  runtime_proof=runtime_proof, source_manifest=source,
                                  output=root / "delivery", version="test", source_revision="revision",
                                  shared_working_tree=True, frozen_archive=False, area_id="area",
                                  cargo_cache=root / "cache")

    def assemble(self, shared, failure=None):
        self.args.shared_working_tree = shared
        self.args.frozen_archive = not shared
        before = {p.relative_to(self.args.playtest).as_posix():p.read_bytes()
                  for p in self.args.playtest.rglob("*") if p.is_file()}
        sentinel, captured = object(), {}
        receipt = {"schema":"test.source-receipt", "manifest_sha256":"1" * 64}

        def stage(prepared, core):
            self.assertIs(prepared, sentinel)
            for name in ["manifest.json", "NOTICE.txt", "sources/original.crate"]:
                path = core / "licenses/native-media" / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"source proof")
            return receipt

        def package(cli, staging, output, descriptor):
            captured[descriptor["kind"]] = (descriptor, {p.relative_to(staging).as_posix():p.read_bytes()
                                                        for p in staging.rglob("*") if p.is_file()})
            return {"id":descriptor["id"], "kind":descriptor["kind"], "manifest_sha256":"0" * 64}

        with patch.object(sources, "prepare_for_assembly", return_value=sentinel, side_effect=failure) as prepare, \
             patch.object(sources, "stage_sources", side_effect=stage) as staged, \
             patch.object(delivery, "validate_shared_applications"), \
             patch.object(delivery.subprocess, "run", return_value=argparse.Namespace(returncode=0, stdout="FORGE_LAUNCHER_AUBE_FRONTEND_READY")), \
             patch.object(delivery, "package", side_effect=package):
            if failure:
                with self.assertRaisesRegex(ValueError, "missing sources"):
                    delivery.assemble(self.args)
                self.assertFalse(self.args.output.exists())
            else:
                delivery.assemble(self.args)
            self.assertEqual(prepare.call_count, int(shared))
            self.assertEqual(staged.call_count, int(shared and not failure))
        after = {p.relative_to(self.args.playtest).as_posix():p.read_bytes()
                 for p in self.args.playtest.rglob("*") if p.is_file()}
        self.assertEqual(before, after)
        return captured, receipt

    def test_shared_real_assemble_stages_sources_only_in_applications_and_pins_proof(self):
        captured, receipt = self.assemble(True)
        descriptor, files = captured["applications"]
        self.assertIn("licenses/native-media/sources/original.crate", files)
        self.assertIn("licenses/native-media/NOTICE.txt", descriptor["license_paths"])
        self.assertIn("licenses/native-media/manifest.json", descriptor["license_paths"])
        for kind in ["assets", "module"]:
            self.assertFalse(any("native-media" in p for p in captured[kind][1]))
        provenance = json.loads((self.args.output / "input-provenance.json").read_bytes())
        self.assertEqual(provenance["application_sources"], receipt)

    def test_frozen_real_assemble_never_reads_or_stages_current_sources(self):
        captured, _ = self.assemble(False)
        self.assertFalse(any("native-media" in p for p in captured["applications"][1]))
        provenance = json.loads((self.args.output / "input-provenance.json").read_bytes())
        self.assertNotIn("application_sources", provenance)

    def test_shared_real_assemble_source_failure_precedes_output_creation(self):
        self.assemble(True, ValueError("missing sources"))

    def test_classic_shared_missing_registry_fails_before_any_build_or_receipt_work(self):
        args = self.args
        args.reuse_native_build, args.baseline = None, None
        args.native_evidence, args.evidence = self.root / "proofs", self.root / "installer-evidence"
        args.target, args.replace_output, args.expected_output_sha256 = self.root / "target", False, None
        args.webview2_sha256, args.cargo_cache = delivery.digest(args.webview2), None
        with patch.object(classic, "shared_native_receipts", side_effect=AssertionError("unexpected receipt/build work")):
            with self.assertRaisesRegex(ValueError, "cargo.cache|registry"):
                classic.build_shared_installer(args)
        self.assertFalse(args.evidence.exists())


if __name__ == "__main__":
    unittest.main()
