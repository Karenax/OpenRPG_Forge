"""Focused, Cargo-free checks for Rust notice source selection."""

from __future__ import annotations

import importlib.util
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
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

    def test_component_prefixed_license_and_copyright_are_preserved(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "FiraMono-LICENSE").write_text("font grant\n", encoding="utf-8")
            (root / "COPYRIGHT").write_text("authors\n", encoding="utf-8")
            found = collector.notice_files(self.package(root, source="registry+https://crates.io"))
            self.assertEqual({path.name for path in found}, {"FiraMono-LICENSE", "COPYRIGHT"})


class ShippingClosureTests(unittest.TestCase):
    def graph(self) -> dict:
        def dependency(name, kind=None):
            return {"pkg": name, "dep_kinds": [{"kind": kind}]}
        edges = {
            "game": [dependency("normal"), dependency("example", "dev"), {"pkg": "unrelated", "dep_kinds": []}],
            "normal": [dependency("grandchild"), dependency("dependency_tests", "dev")],
            "example": [dependency("build_tool", "build")],
            "grandchild": [], "dependency_tests": [], "build_tool": [], "unrelated": [],
        }
        return {
            "packages": [{"id": name, "name": name} for name in edges],
            "resolve": {"nodes": [{"id": name, "deps": deps} for name, deps in edges.items()]},
        }

    def test_shipped_example_adds_only_the_root_dev_closure(self) -> None:
        document = self.graph()
        self.assertEqual(set(collector.dependencies(document, "game")), {"game", "normal", "grandchild"})
        self.assertEqual(
            set(collector.dependencies(document, "game", include_root_dev=True)),
            {"game", "normal", "grandchild", "example", "build_tool"},
        )

    def test_metadata_uses_explicit_platform_and_feature_policy(self) -> None:
        with patch.object(collector.subprocess, "run") as run:
            run.return_value.stdout = b'{"packages":[]}'
            collector.metadata(Path("Cargo.toml"), target="wasm32-unknown-unknown", features=("web",), no_default_features=True)
            command = run.call_args.args[0]
            self.assertEqual(command[command.index("--filter-platform") + 1], "wasm32-unknown-unknown")
            self.assertEqual(command[command.index("--features") + 1], "web")
            self.assertIn("--no-default-features", command)
            self.assertIn("--locked", command)
            self.assertIn("--offline", command)

    def test_same_name_version_from_different_origins_keeps_both_texts(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packages = []
            for name, package_id, notice in [("game", "game", None), ("util", "registry:util", "first"), ("util", "git:util", "second")]:
                folder = root / package_id.replace(":", "-")
                folder.mkdir()
                manifest = folder / "Cargo.toml"
                manifest.write_text("[package]\n", encoding="utf-8")
                if notice:
                    (folder / "LICENSE").write_text(notice, encoding="utf-8")
                packages.append({"id": package_id, "name": name, "version": "1.0", "manifest_path": str(manifest), "source": "registry:test" if notice else None})
            document = {"packages": packages, "resolve": {"nodes": [
                {"id": "game", "deps": [
                    {"pkg": "registry:util", "dep_kinds": [{"kind": None}]},
                    {"pkg": "git:util", "dep_kinds": [{"kind": "dev"}]},
                ]},
                {"id": "registry:util", "deps": []}, {"id": "git:util", "deps": []},
            ]}}
            output = root / "notices"
            with patch.object(collector, "metadata", return_value=document):
                index = collector.collect_scopes([{
                    "manifest": packages[0]["manifest_path"], "package": "game",
                    "target": "x86_64-unknown-linux-gnu", "include_root_dev": True,
                }], output)
            self.assertEqual(index["package_count"], 3)
            self.assertEqual(index["packages_without_bundled_notice"], 1)
            self.assertEqual({(output / notice).read_text() for entry in index["packages"] for notice in entry["notices"]}, {"first", "second"})
            self.assertEqual(index["targets"], ["x86_64-unknown-linux-gnu"])
            self.assertTrue(index["requests"][0]["include_root_dev"])

    def test_identical_texts_share_storage_and_keep_both_package_origins(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packages = {}
            for name in ("first", "second"):
                folder = root / name
                folder.mkdir()
                (folder / "LICENSE").write_bytes(b"same original grant\r\n")
                packages[name] = {"id": name, "name": name, "version": "1", "source": None, "manifest_path": str(folder / "Cargo.toml")}
            output = root / "notices"
            index = collector.write_notices(packages, output, {"schema": "test"}, deduplicate=True)
            self.assertEqual(len(list((output / "texts").iterdir())), 1)
            self.assertEqual(index["package_count"], 2)
            for entry in index["packages"]:
                self.assertEqual((output / entry["notices"][0]).read_bytes(), b"same original grant\r\n")
                self.assertEqual(entry["notice_sources"][0]["source_relative_path"], "LICENSE")


class SupplementIntegrationTests(unittest.TestCase):
    def fixture(self, root: Path) -> tuple[dict, Path, Path, Path, dict]:
        source, output, stage = root / "crate", root / "output", root / "stage"
        source.mkdir()
        output.mkdir()
        (stage / "texts").mkdir(parents=True)
        (source / "Cargo.toml").write_text("[package]\n", encoding="utf8")
        vcs = json.dumps({"git": {"sha1": "1" * 40}, "path_in_vcs": "crate"}).encode()
        (source / ".cargo_vcs_info.json").write_bytes(vcs)
        package = {"id": "registry:example", "name": "example", "version": "1.0",
                   "license": "MIT", "repository": "https://example.invalid/project",
                   "source": "registry:test", "manifest_path": str(source / "Cargo.toml")}
        base = {"packages": [{"package_id": package["id"], "notices": []}]}
        base_bytes = json.dumps(base).encode()
        (output / "INDEX.json").write_bytes(base_bytes)
        (stage / "INDEX.json").write_bytes(base_bytes)
        notice = b"fixture copyright and permission\n"
        digest = hashlib.sha256(notice).hexdigest()
        (stage / "texts" / (digest + ".txt")).write_bytes(notice)
        entry = {"package_id": package["id"], "name": "example", "version": "1.0",
                 "declared_license": "MIT", "repository": package["repository"],
                 "commit": "1" * 40, "path_in_vcs": "crate", "notices": [{
                     "path": f"texts/{digest}.txt", "sha256": digest, "bytes": len(notice),
                     "source_url": "https://example.invalid/project/LICENSE"}]}
        upstream = {"schema": "forge.upstream-notice-supplements.v1",
                    "base_index_sha256": hashlib.sha256(base_bytes).hexdigest(),
                    "source_vcs_hashes": {"original-location": hashlib.sha256(vcs).hexdigest()},
                    "packages": [entry]}
        up_bytes = json.dumps(upstream).encode()
        (stage / "UPSTREAM-NOTICES.json").write_bytes(up_bytes)
        standard = {"schema": "forge.standard-notice-supplements.v1", "packages": [],
                    "base_index_sha256": hashlib.sha256(base_bytes).hexdigest(),
                    "upstream_index_sha256": hashlib.sha256(up_bytes).hexdigest()}
        standard_bytes = json.dumps(standard).encode()
        (stage / "STANDARD-NOTICES.json").write_bytes(standard_bytes)
        gitlab = {"schema": "forge.gitlab-notice-supplements.v1", "packages": [],
                  "base_index_sha256": hashlib.sha256(base_bytes).hexdigest(),
                  "upstream_index_sha256": hashlib.sha256(up_bytes).hexdigest(),
                  "standard_index_sha256": hashlib.sha256(standard_bytes).hexdigest()}
        (stage / "GITLAB-NOTICES.json").write_text(json.dumps(gitlab), encoding="utf8")
        lock = root / "lock.json"
        lock.write_text(json.dumps({"schema": "forge.gate7.notice-supplement-cross-check.v1",
            "supplement_texts_verified": True, "index_sha256": {
                name: hashlib.sha256((stage / name).read_bytes()).hexdigest() for name in
                ("INDEX.json", "UPSTREAM-NOTICES.json", "STANDARD-NOTICES.json", "GITLAB-NOTICES.json")}}), encoding="utf8")
        return {package["id"]: package}, output, stage, lock, entry

    def test_verified_notice_is_portable_and_reuses_existing_text(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, stage, lock, entry = self.fixture(Path(temporary))
            existing = output / entry["notices"][0]["path"]
            existing.parent.mkdir()
            existing.write_bytes((stage / entry["notices"][0]["path"]).read_bytes())
            result = collector.include_supplements(packages, output, stage, lock)
            index = json.loads((output / "SUPPLEMENT-NOTICES.json").read_text(encoding="utf8"))
            self.assertEqual(result, {"supplemented_packages": 1, "packages_without_available_notice": 0})
            self.assertNotIn("unresolved_transitive_references", index)
            self.assertEqual(len(list((output / "texts").iterdir())), 1)
            self.assertEqual(index["packages"][0]["notices"][0]["path"], entry["notices"][0]["path"])
            self.assertNotIn(str(stage), json.dumps(index))
            self.assertFalse(index["distribution_qualified"])

    def test_rejects_changed_text_and_does_not_publish_index(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, stage, lock, entry = self.fixture(Path(temporary))
            (stage / entry["notices"][0]["path"]).write_bytes(b"altered")
            with self.assertRaisesRegex(ValueError, "differs from its receipt"):
                collector.include_supplements(packages, output, stage, lock)
            self.assertFalse((output / "SUPPLEMENT-NOTICES.json").exists())

    def test_rejects_changed_source_revision_and_inventory(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, stage, lock, _ = self.fixture(Path(temporary))
            package = next(iter(packages.values()))
            vcs = Path(package["manifest_path"]).parent / ".cargo_vcs_info.json"
            vcs.write_text(json.dumps({"git": {"sha1": "2" * 40}, "path_in_vcs": "crate"}), encoding="utf8")
            with self.assertRaisesRegex(ValueError, "source revision differs"):
                collector.include_supplements(packages, output, stage, lock)
            (output / "INDEX.json").write_bytes(b"different")
            with self.assertRaisesRegex(ValueError, "different Cargo inventory"):
                collector.include_supplements(packages, output, stage, lock)
            self.assertFalse((output / "SUPPLEMENT-NOTICES.json").exists())

    def test_rejects_notice_for_a_different_package_and_unsafe_paths(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, stage, lock, _ = self.fixture(Path(temporary))
            next(iter(packages.values()))["repository"] = "https://example.invalid/other"
            with self.assertRaisesRegex(ValueError, "package metadata differs"):
                collector.include_supplements(packages, output, stage, lock)
            with self.assertRaisesRegex(ValueError, "Unsafe supplement text path"):
                collector._supplement_text(stage, "../outside.txt", "0" * 64, None)
            self.assertFalse((output / "SUPPLEMENT-NOTICES.json").exists())

    def test_rejects_replaced_index_even_if_its_internal_hash_chain_is_repaired(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, stage, lock, _ = self.fixture(Path(temporary))
            upstream = stage / "UPSTREAM-NOTICES.json"
            document = json.loads(upstream.read_text(encoding="utf8"))
            document["packages"][0]["notices"][0]["source_url"] = "https://example.invalid/replaced"
            upstream.write_text(json.dumps(document), encoding="utf8")
            standard = stage / "STANDARD-NOTICES.json"
            standard_doc = json.loads(standard.read_text(encoding="utf8"))
            standard_doc["upstream_index_sha256"] = hashlib.sha256(upstream.read_bytes()).hexdigest()
            standard.write_text(json.dumps(standard_doc), encoding="utf8")
            gitlab = stage / "GITLAB-NOTICES.json"
            gitlab_doc = json.loads(gitlab.read_text(encoding="utf8"))
            gitlab_doc["upstream_index_sha256"] = hashlib.sha256(upstream.read_bytes()).hexdigest()
            gitlab_doc["standard_index_sha256"] = hashlib.sha256(standard.read_bytes()).hexdigest()
            gitlab.write_text(json.dumps(gitlab_doc), encoding="utf8")
            # A separately verified lock still catches a self-consistent replacement.
            with self.assertRaisesRegex(ValueError, "verification lock"):
                collector.include_supplements(packages, output, stage, lock)
            self.assertFalse((output / "SUPPLEMENT-NOTICES.json").exists())

    def test_upstream_source_checks_refuse_changed_published_source(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, stage, lock, _ = self.fixture(Path(temporary))
            source = Path(next(iter(packages.values()))["manifest_path"]).parent
            (source / "Cargo.toml.orig").write_bytes(b"original manifest")
            (source / "src").mkdir()
            (source / "src" / "lib.rs").write_bytes(b"original source")
            upstream_path = stage / "UPSTREAM-NOTICES.json"
            upstream = json.loads(upstream_path.read_text(encoding="utf8"))
            upstream["packages"][0]["source_checks"] = {
                name: hashlib.sha256((source / name).read_bytes()).hexdigest()
                for name in ("Cargo.toml.orig", "src/lib.rs")}
            upstream_path.write_text(json.dumps(upstream), encoding="utf8")
            standard_path = stage / "STANDARD-NOTICES.json"
            standard = json.loads(standard_path.read_text(encoding="utf8"))
            standard["upstream_index_sha256"] = hashlib.sha256(upstream_path.read_bytes()).hexdigest()
            standard_path.write_text(json.dumps(standard), encoding="utf8")
            gitlab_path = stage / "GITLAB-NOTICES.json"
            gitlab = json.loads(gitlab_path.read_text(encoding="utf8"))
            gitlab["upstream_index_sha256"] = hashlib.sha256(upstream_path.read_bytes()).hexdigest()
            gitlab["standard_index_sha256"] = hashlib.sha256(standard_path.read_bytes()).hexdigest()
            gitlab_path.write_text(json.dumps(gitlab), encoding="utf8")
            lock_doc = json.loads(lock.read_text(encoding="utf8"))
            lock_doc["index_sha256"] = {name: hashlib.sha256((stage / name).read_bytes()).hexdigest()
                                        for name in lock_doc["index_sha256"]}
            lock.write_text(json.dumps(lock_doc), encoding="utf8")
            (source / "src" / "lib.rs").write_bytes(b"changed source")
            with self.assertRaisesRegex(ValueError, "Supplement source file differs"):
                collector.include_supplements(packages, output, stage, lock)
            self.assertFalse((output / "SUPPLEMENT-NOTICES.json").exists())

    def test_alternate_revision_retains_provenance_and_requires_source_checks(self) -> None:
        for mode in ("complete", "missing", "omitted"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as temporary:
                packages, output, stage, lock, _ = self.fixture(Path(temporary))
                source = Path(next(iter(packages.values()))["manifest_path"]).parent
                (source / "Cargo.toml.orig").write_bytes(b"original manifest")
                (source / "src").mkdir()
                (source / "src" / "lib.rs").write_bytes(b"original source")
                (source / "src" / "nested").mkdir()
                (source / "src" / "nested" / "other.rs").write_bytes(b"another source")
                upstream_path = stage / "UPSTREAM-NOTICES.json"
                upstream = json.loads(upstream_path.read_text(encoding="utf8"))
                entry = upstream["packages"][0]
                if mode != "missing":
                    source_files = ["Cargo.toml.orig", "src/lib.rs"]
                    if mode == "complete":
                        source_files.append("src/nested/other.rs")
                    entry["source_checks"] = {
                        name: hashlib.sha256((source / name).read_bytes()).hexdigest()
                        for name in source_files}
                entry["reference_commit"] = "2" * 40
                entry["reference_basis"] = "identical_published_manifest_and_source"
                entry["notices"][0]["source_url"] = "https://example.invalid/project/blob/" + "2" * 40 + "/LICENSE"
                upstream_path.write_text(json.dumps(upstream), encoding="utf8")
                standard_path = stage / "STANDARD-NOTICES.json"
                standard = json.loads(standard_path.read_text(encoding="utf8"))
                standard["upstream_index_sha256"] = hashlib.sha256(upstream_path.read_bytes()).hexdigest()
                standard_path.write_text(json.dumps(standard), encoding="utf8")
                gitlab_path = stage / "GITLAB-NOTICES.json"
                gitlab = json.loads(gitlab_path.read_text(encoding="utf8"))
                gitlab["upstream_index_sha256"] = hashlib.sha256(upstream_path.read_bytes()).hexdigest()
                gitlab["standard_index_sha256"] = hashlib.sha256(standard_path.read_bytes()).hexdigest()
                gitlab_path.write_text(json.dumps(gitlab), encoding="utf8")
                lock_doc = json.loads(lock.read_text(encoding="utf8"))
                lock_doc["index_sha256"] = {name: hashlib.sha256((stage / name).read_bytes()).hexdigest()
                                            for name in lock_doc["index_sha256"]}
                lock.write_text(json.dumps(lock_doc), encoding="utf8")
                if mode == "complete":
                    collector.include_supplements(packages, output, stage, lock)
                    entry = json.loads((output / "SUPPLEMENT-NOTICES.json").read_text(encoding="utf8"))["packages"][0]
                    self.assertEqual(entry["commit"], "1" * 40)
                    self.assertEqual(entry["reference_commit"], "2" * 40)
                    self.assertTrue(entry["applicability_review_required"])
                else:
                    expected = "reference revision differs" if mode == "missing" else "source checks differ"
                    with self.assertRaisesRegex(ValueError, expected):
                        collector.include_supplements(packages, output, stage, lock)
                    self.assertFalse((output / "SUPPLEMENT-NOTICES.json").exists())


    def _transitive_fixture(self, root: Path):
        packages, output, stage, lock, _ = self.fixture(root)
        package = next(iter(packages.values()))
        source = Path(package["manifest_path"]).parent
        (source / "Cargo.toml.orig").write_bytes(b"[package]\nname = 'example'\n")
        (source / "src").mkdir()
        (source / "src/copied.rs").write_bytes(b"package source bytes\n")
        evidence = b"synthetic reference only; original origin unverified\n"
        document = b"synthetic ancestor document, not a package notice\n"
        def stage_text(data):
            digest = hashlib.sha256(data).hexdigest()
            (stage / "texts" / (digest + ".txt")).write_bytes(data)
            return {"path": "texts/" + digest + ".txt", "sha256": digest, "bytes": len(data)}
        document_pin = {**stage_text(document), "source_repository": "https://github.com/example/ancestor",
                        "source_revision": "2" * 40, "source_path": "COPYRIGHT"}
        ref = {"package_id": package["id"], "package_revision": "1" * 40,
               "package_manifest_sha256": hashlib.sha256((source / "Cargo.toml.orig").read_bytes()).hexdigest(),
               "package_source_path": "src/copied.rs",
               "package_source_sha256": hashlib.sha256((source / "src/copied.rs").read_bytes()).hexdigest(),
               "evidence": stage_text(evidence), "documents": [document_pin], "exact_source_confirmed": False}
        upstream_path = stage / "UPSTREAM-NOTICES.json"
        upstream = json.loads(upstream_path.read_bytes())
        upstream["packages"] = []
        upstream["unresolved_transitive_references"] = [ref]
        self._rewrite_transitive_chain(stage, lock, upstream)
        return packages, output, stage, lock, upstream, source

    def _rewrite_transitive_chain(self, stage: Path, lock: Path, upstream: dict) -> None:
        upstream_path = stage / "UPSTREAM-NOTICES.json"
        upstream_path.write_text(json.dumps(upstream), encoding="utf8")
        standard_path = stage / "STANDARD-NOTICES.json"
        standard = json.loads(standard_path.read_bytes())
        standard["upstream_index_sha256"] = hashlib.sha256(upstream_path.read_bytes()).hexdigest()
        standard_path.write_text(json.dumps(standard), encoding="utf8")
        gitlab_path = stage / "GITLAB-NOTICES.json"
        gitlab = json.loads(gitlab_path.read_bytes())
        gitlab["upstream_index_sha256"] = hashlib.sha256(upstream_path.read_bytes()).hexdigest()
        gitlab["standard_index_sha256"] = hashlib.sha256(standard_path.read_bytes()).hexdigest()
        gitlab_path.write_text(json.dumps(gitlab), encoding="utf8")
        lock_doc = json.loads(lock.read_bytes())
        lock_doc["index_sha256"] = {name: hashlib.sha256((stage / name).read_bytes()).hexdigest()
                                    for name in lock_doc["index_sha256"]}
        lock.write_text(json.dumps(lock_doc), encoding="utf8")

    def test_transitive_reference_is_portable_and_does_not_supply_notice(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, stage, lock, upstream, _ = self._transitive_fixture(Path(temporary))
            result = collector.include_supplements(packages, output, stage, lock)
            index = json.loads((output / "SUPPLEMENT-NOTICES.json").read_bytes())
            self.assertEqual(result, {"supplemented_packages": 0, "packages_without_available_notice": 1})
            self.assertEqual(index["packages"], [])
            self.assertEqual(index["packages_without_available_notice"], ["registry:example"])
            ref = index["unresolved_transitive_references"][0]
            self.assertEqual(ref["reference_type"], "transitive_source_reference")
            self.assertFalse(ref["exact_source_confirmed"])
            self.assertFalse(ref["comparison_verified_by_collector"])
            self.assertFalse(ref["document_origin_verified_by_collector"])
            self.assertFalse(ref["package_notice_supplied"])
            self.assertTrue(ref["applicability_review_required"])
            self.assertFalse(ref["distribution_qualified"])
            self.assertEqual(ref["documents"][0]["source_url"],
                             "https://github.com/example/ancestor/blob/" + "2" * 40 + "/COPYRIGHT")
            for value in (ref["evidence"], *ref["documents"]):
                data = (output / value["path"]).read_bytes()
                self.assertEqual(hashlib.sha256(data).hexdigest(), value["sha256"])
            self.assertNotIn(str(stage), json.dumps(index))
            self.assertEqual(len(upstream["unresolved_transitive_references"]), 1)

    def test_transitive_reference_rejects_bad_claims_and_pins_before_publication(self) -> None:
        for mode in ("promoted", "unknown_claim", "duplicate", "foreign_package", "wrong_revision",
                     "wrong_manifest", "changed_source", "unsafe_source_path", "wrong_document_hash",
                     "bad_document_origin", "duplicate_document", "missing_document", "bad_evidence",
                     "oversized_evidence", "too_many_documents"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as temporary:
                packages, output, stage, lock, upstream, source = self._transitive_fixture(Path(temporary))
                ref = upstream["unresolved_transitive_references"][0]
                doc = ref["documents"][0]
                if mode == "promoted":
                    ref["exact_source_confirmed"] = True
                elif mode == "unknown_claim":
                    ref["package_notice_supplied"] = True
                elif mode == "duplicate":
                    upstream["unresolved_transitive_references"].append(dict(ref))
                elif mode == "foreign_package":
                    ref["package_id"] = "registry:foreign"
                elif mode == "wrong_revision":
                    ref["package_revision"] = "3" * 40
                elif mode == "wrong_manifest":
                    (source / "Cargo.toml.orig").write_bytes(b"different manifest")
                elif mode == "changed_source":
                    (source / "src/copied.rs").write_bytes(b"different source")
                elif mode == "unsafe_source_path":
                    ref["package_source_path"] = "src/../Cargo.toml.orig"
                elif mode == "wrong_document_hash":
                    (stage / doc["path"]).write_bytes(b"altered document")
                elif mode == "bad_document_origin":
                    doc["source_repository"] = "https://github.com/../foreign"
                elif mode == "duplicate_document":
                    ref["documents"].append(dict(doc))
                elif mode == "missing_document":
                    doc["path"] = "texts/" + "0" * 64 + ".txt"
                    doc["sha256"] = "0" * 64
                elif mode == "bad_evidence":
                    ref["evidence"]["bytes"] = True
                elif mode == "oversized_evidence":
                    ref["evidence"]["bytes"] = 1024 * 1024 + 1
                elif mode == "too_many_documents":
                    ref["documents"] = [dict(doc) for _ in range(33)]
                self._rewrite_transitive_chain(stage, lock, upstream)
                with self.assertRaises((ValueError, FileNotFoundError)):
                    collector.include_supplements(packages, output, stage, lock)
                self.assertFalse((output / "SUPPLEMENT-NOTICES.json").exists())
                self.assertFalse((output / "texts").exists())

    def test_transitive_reference_without_lock_pin_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, stage, lock, upstream, _ = self._transitive_fixture(Path(temporary))
            upstream["unresolved_transitive_references"][0]["package_revision"] = "3" * 40
            (stage / "UPSTREAM-NOTICES.json").write_text(json.dumps(upstream), encoding="utf8")
            with self.assertRaisesRegex(ValueError, "verification lock"):
                collector.include_supplements(packages, output, stage, lock)
            self.assertFalse((output / "SUPPLEMENT-NOTICES.json").exists())

    def test_transitive_sources_reject_a_linked_ancestor(self) -> None:
        for linked in ("package", "stage", "before-parent-traversal"):
            with self.subTest(linked=linked), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                physical = root / "physical"
                physical.mkdir()
                packages, output, stage, lock, _, _ = self._transitive_fixture(physical)
                alias = root / "alias"
                try:
                    alias.symlink_to(physical, target_is_directory=True)
                except OSError as error:
                    if os.name != "nt":
                        self.skipTest(f"Cannot create a directory link: {error}")
                    result = subprocess.run(
                        ["cmd.exe", "/d", "/c", "mklink", "/J", str(alias), str(physical)],
                        capture_output=True,
                        check=False,
                    )
                    if result.returncode:
                        self.skipTest("Cannot create a directory junction")
                if linked == "package":
                    package = next(iter(packages.values()))
                    package["manifest_path"] = str(alias / "crate" / "Cargo.toml")
                elif linked == "stage":
                    stage = alias / "stage"
                else:
                    stage = alias / ".." / "physical" / "stage"
                with self.assertRaisesRegex(ValueError, "directory is not physical"):
                    collector.include_supplements(packages, output, stage, lock)
                self.assertFalse((output / "SUPPLEMENT-NOTICES.json").exists())
                self.assertFalse((output / "texts").exists())

    def test_transitive_stage_allows_physical_parent_traversal(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packages, output, stage, lock, _, _ = self._transitive_fixture(root)
            (root / "physical").mkdir()
            stage = root / "physical" / ".." / stage.name
            result = collector.include_supplements(packages, output, stage, lock)
            self.assertEqual(result, {"supplemented_packages": 0, "packages_without_available_notice": 1})


class ForgeOriginalLicenseTests(unittest.TestCase):
    def fixture(self, root: Path) -> tuple[dict, Path, Path, Path, Path, Path]:
        project, output = root / "project", root / "output"
        crate = project / "crates" / "forge_example"
        crate.mkdir(parents=True)
        output.mkdir()
        manifest = crate / "Cargo.toml"
        manifest.write_text('[package]\nname = "forge_example"\n', encoding="utf8")
        package = {"id": "path:forge_example", "name": "forge_example", "version": "1.0",
                   "source": None, "license": "MIT OR Apache-2.0", "manifest_path": str(manifest)}
        dependency = {"id": "registry:other", "name": "other", "version": "2.0",
                      "source": "registry:test", "license": "MIT", "manifest_path": str(manifest)}
        packages = {item["id"]: item for item in (package, dependency)}
        base = {"packages": [{"package_id": item["id"], "name": item["name"], "version": item["version"],
                              "source": item["source"], "license": item["license"], "notices": []}
                             for item in (package, dependency)]}
        base_bytes = json.dumps(base).encode()
        (output / "INDEX.json").write_bytes(base_bytes)
        supplement_bytes = json.dumps({"packages_without_available_notice": list(packages)}).encode()
        (output / "SUPPLEMENT-NOTICES.json").write_bytes(supplement_bytes)
        files = {}
        for name in ("LICENSE-MIT", "LICENSE-APACHE", "LICENSE.md"):
            text = (name + " fixture\n").encode()
            (project / name).write_bytes(text)
            files[name] = {"sha256": hashlib.sha256(text).hexdigest(), "bytes": len(text)}
        receipt_bytes = json.dumps({"schema": "forge.gate7.owner-code-license.v1",
                                    "owner_confirmation": "MIT OR Apache-2.0", "files": files}).encode()
        receipt = root / "receipt.json"
        receipt.write_bytes(receipt_bytes)
        spec = root / "spec.json"
        specification = {"schema": "forge.gate7.original-code-license-spec.v1",
                         "base_index_sha256": hashlib.sha256(base_bytes).hexdigest(),
                         "supplement_index_sha256": hashlib.sha256(supplement_bytes).hexdigest(),
                         "owner_receipt_sha256": hashlib.sha256(receipt_bytes).hexdigest(),
                         "packages": [{"package_id": package["id"], "name": package["name"],
                                       "version": package["version"], "manifest": "crates/forge_example/Cargo.toml",
                                       "manifest_sha256": hashlib.sha256(manifest.read_bytes()).hexdigest()}]}
        spec.write_text(json.dumps(specification), encoding="utf8")
        return packages, output, project, receipt, spec, manifest

    def test_only_explicit_original_package_receives_owner_licence(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, project, receipt, spec, _ = self.fixture(Path(temporary))
            result = collector.include_forge_licenses(packages, output, project, receipt, spec)
            index = json.loads((output / "FORGE-ORIGINAL-NOTICES.json").read_text(encoding="utf8"))
            self.assertEqual(result, {"original_packages": 1, "packages_without_available_notice": 1})
            self.assertEqual(index["packages_without_available_notice"], ["registry:other"])
            self.assertEqual(index["packages"][0]["scope"], "original Forge contributions only")
            self.assertEqual(len(list((output / "texts").iterdir())), 3)
            self.assertFalse(index["distribution_qualified"])

    def test_rejects_changed_licence_or_manifest_without_publishing_index(self) -> None:
        for target in ("licence", "manifest"):
            with self.subTest(target=target), tempfile.TemporaryDirectory() as temporary:
                packages, output, project, receipt, spec, manifest = self.fixture(Path(temporary))
                (project / "LICENSE-MIT" if target == "licence" else manifest).write_text("altered", encoding="utf8")
                with self.assertRaises(ValueError):
                    collector.include_forge_licenses(packages, output, project, receipt, spec)
                self.assertFalse((output / "FORGE-ORIGINAL-NOTICES.json").exists())

    def test_rejects_external_source_and_manifest_escape(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            packages, output, project, receipt, spec, _ = self.fixture(Path(temporary))
            packages["path:forge_example"]["source"] = "registry:test"
            with self.assertRaisesRegex(ValueError, "metadata or source differs"):
                collector.include_forge_licenses(packages, output, project, receipt, spec)
            packages["path:forge_example"]["source"] = None
            document = json.loads(spec.read_text(encoding="utf8"))
            document["packages"][0]["manifest"] = "../outside/Cargo.toml"
            spec.write_text(json.dumps(document), encoding="utf8")
            with self.assertRaisesRegex(ValueError, "Unsafe Forge package manifest"):
                collector.include_forge_licenses(packages, output, project, receipt, spec)
            self.assertFalse((output / "FORGE-ORIGINAL-NOTICES.json").exists())


if __name__ == "__main__":
    unittest.main()
