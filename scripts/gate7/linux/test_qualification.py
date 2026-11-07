"""Transport checks are runnable on the Windows authoring host without Docker."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from run_qualification import extract, resume, retain_source_manifest, verify


class TransportTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def archive(self, name="crates/source.rs", *, symlink=False, bad_hash=False):
        path = self.root / "input.zip"
        data = b"immutable authoring source\n"
        expected = {"bytes": len(data), "sha256": "0" * 64 if bad_hash else hashlib.sha256(data).hexdigest()}
        with zipfile.ZipFile(path, "x") as bundle:
            info = zipfile.ZipInfo(name)
            if symlink:
                info.create_system = 3
                info.external_attr = 0o120777 << 16
            bundle.writestr(info, data)
            bundle.writestr("__forge_vm_source_manifest__.json", json.dumps({"files": {name: expected}}))
        return path

    def test_preserved_source_and_change_detection(self):
        destination = self.root / "source"
        manifest = extract(self.archive(), destination)
        self.assertEqual(verify(destination, manifest["files"]), [])
        (destination / "crates/source.rs").write_bytes(b"changed\n")
        self.assertEqual(verify(destination, manifest["files"]), ["crates/source.rs"])

    def test_source_manifest_is_retained_exactly_and_changed_metadata_is_refused(self):
        archive = self.archive()
        manifest = extract(archive, self.root / "source")
        output = self.root / "evidence"
        output.mkdir()
        recorded_hash = retain_source_manifest(archive, output, manifest)
        with zipfile.ZipFile(archive) as bundle:
            expected = bundle.read("__forge_vm_source_manifest__.json")
        self.assertEqual((output / "source-manifest.json").read_bytes(), expected)
        self.assertEqual(recorded_hash, hashlib.sha256(expected).hexdigest())
        altered = {"files": {}}
        other = self.root / "other"
        other.mkdir()
        with self.assertRaisesRegex(ValueError, "manifest changed"):
            retain_source_manifest(archive, other, altered)
        self.assertFalse((other / "source-manifest.json").exists())
        archive.unlink()
        self.assertEqual((output / "source-manifest.json").read_bytes(), expected)

    def test_parent_path_is_rejected_before_any_write(self):
        destination = self.root / "source"
        with self.assertRaisesRegex(ValueError, "Unsafe"):
            extract(self.archive("../escape"), destination)
        self.assertFalse(destination.exists())
        self.assertFalse((self.root / "escape").exists())

    def test_symlink_is_rejected_before_any_write(self):
        destination = self.root / "source"
        with self.assertRaisesRegex(ValueError, "Unsafe"):
            extract(self.archive(symlink=True), destination)
        self.assertFalse(destination.exists())

    def test_changed_payload_does_not_get_installed(self):
        destination = self.root / "source"
        with self.assertRaisesRegex(ValueError, "digest differs"):
            extract(self.archive(bad_hash=True), destination)
        self.assertFalse((destination / "crates/source.rs").exists())

    def test_retry_rejects_changed_source_or_successful_log(self):
        archive = self.archive()
        source = self.root / "sources" / "first"
        source.parent.mkdir()
        extract(archive, source)
        previous = self.root / "results" / "first"
        previous.mkdir(parents=True)
        log = previous / "01-test.log"
        log.write_bytes(b"passed")
        steps = [("test", ["cargo", "test"], "."), ("build", ["cargo", "build"], ".")]
        receipt = {"succeeded": False, "source_unchanged": True, "source_sha256": "a" * 64,
                   "checks": [{"name": "test", "command": ["cargo", "test"], "cwd": ".", "exit_code": 0,
                               "log": log.name, "sha256": hashlib.sha256(b"passed").hexdigest()},
                              {"name": "build", "command": ["cargo", "build"], "cwd": ".", "exit_code": 101}]}
        (previous / "receipt.json").write_text(json.dumps(receipt))
        _, original, _, inherited = resume(self.root, "first", archive, "a" * 64, steps)
        self.assertEqual(original, "first")
        self.assertEqual(len(inherited), 1)
        log.write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "log changed"):
            resume(self.root, "first", archive, "a" * 64, steps)
        log.write_bytes(b"passed")
        (source / "crates/source.rs").write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "sources changed"):
            resume(self.root, "first", archive, "a" * 64, steps)


if __name__ == "__main__":
    unittest.main()
