import hashlib
import io
from pathlib import Path
import tarfile
import tempfile
import unittest

from prepare_registry import REGISTRY, prepare


class RegistryPreparation(unittest.TestCase):
    def fixture(self, root, malicious=False, marker_alias=None):
        home = root / "cargo-home"
        archive = home / "registry" / "cache" / REGISTRY / "example-1.0.0.crate"
        archive.parent.mkdir(parents=True)
        with tarfile.open(archive, "w:gz") as output:
            for name, content in {
                "example-1.0.0/Cargo.toml": b'[package]\nname="example"\nversion="1.0.0"\n',
                "example-1.0.0/src/lib.rs": b"pub const VALUE: u8 = 7;\n",
                **({"example-1.0.0/../escaped": b"escape"} if malicious else {}),
                **({f"example-1.0.0/{marker_alias}": b"marker"} if marker_alias else {}),
            }.items():
                member = tarfile.TarInfo(name)
                member.size = len(content)
                member.mtime = 0
                output.addfile(member, io.BytesIO(content))
        checksum = hashlib.sha256(archive.read_bytes()).hexdigest()
        return home, archive, {"name": "example", "version": "1.0.0", "checksum": checksum}

    def test_epoch_archive_keeps_original_checksum_and_verifies_every_source_file(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            home, archive, package = self.fixture(root)
            result = prepare(package, home, root / "read-only")
            self.assertEqual(result["files"], 2)
            self.assertEqual(hashlib.sha256(archive.read_bytes()).hexdigest(), package["checksum"])
            repeated = prepare(package, home, root / "read-only")
            self.assertEqual(repeated["status"], "VERIFIED_EXISTING")
            source = home / "registry" / "src" / REGISTRY / "example-1.0.0" / "src" / "lib.rs"
            source.write_text("modified")
            with self.assertRaises(ValueError):
                prepare(package, home, root / "read-only")

    def test_checksum_mismatch_and_path_escape_never_mark_a_package_complete(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            home, _, package = self.fixture(root, malicious=True)
            with self.assertRaises(ValueError):
                prepare({**package, "checksum": "0" * 64}, home, root / "read-only")
            with self.assertRaises(ValueError):
                prepare(package, home, root / "read-only")
            self.assertFalse((root / "escaped").exists())
            marker = home / "registry" / "src" / REGISTRY / "example-1.0.0" / ".cargo-ok"
            self.assertFalse(marker.exists())

    def test_windows_marker_aliases_cannot_replace_the_completion_marker(self):
        for alias in (".CARGO-OK", ".cargo-ok.", ".cargo-ok "):
            with self.subTest(alias=alias), tempfile.TemporaryDirectory() as directory:
                root = Path(directory).resolve()
                home, _, package = self.fixture(root, marker_alias=alias)
                with self.assertRaises(ValueError):
                    prepare(package, home, root / "read-only")
                marker = home / "registry" / "src" / REGISTRY / "example-1.0.0" / ".cargo-ok"
                self.assertFalse(marker.exists())


if __name__ == "__main__":
    unittest.main()
