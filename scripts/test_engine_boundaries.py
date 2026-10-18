"""Architecture regressions: product build inputs must be owned by the product.

Run from any directory with Python's unittest runner. No build or GPU is started.
"""
import hashlib
import importlib.util
from pathlib import Path
import re
import tempfile
import tomllib
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
EXAMPLES = ROOT / "crates/forge_tidewater/examples"


def rust_body(path):
    # Only standalone comments/blank lines differ in the legacy copies covered
    # here. The actual Rust/WGSL tokens must still be reviewed before factoring.
    return "\n".join(
        line.strip() for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.lstrip().startswith("//")
    )


class EngineBoundaries(unittest.TestCase):
    def test_orchestration_candidates_are_not_compiled_inventory_inputs(self):
        spec = importlib.util.spec_from_file_location("build_validator", ROOT / "scripts/Validate-Gate3WorkingTree.py")
        validator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(validator)
        for extension in ("py", "rs", "wgsl", "cs", "json"):
            self.assertFalse(validator.is_build_input(f"scripts/hivemind/candidates/proof.{extension}"))
        self.assertTrue(validator.is_build_input("crates/forge_tidewater/examples/support/proof.rs"))
        self.assertTrue(validator.is_build_input("scripts/hivemind-other/build.py"))
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "crates/probe/src/lib.rs"
            source.parent.mkdir(parents=True)
            source.write_text('include!(concat!(env!("CARGO_MANIFEST_DIR"), "/../../scripts/hivemind/candidates/proof.rs"));')
            with patch.object(validator, "SOURCE", root):
                with self.assertRaisesRegex(ValueError, "orchestration"):
                    validator.source_inventories()
                source.write_text("// Product-owned source.\n")
                (root / "Cargo.toml").write_text('[dependencies.probe]\npath = "scripts/hivemind/candidates"\n')
                with self.assertRaisesRegex(ValueError, "orchestration"):
                    validator.source_inventories()

    def test_extracted_renderer_inputs_do_not_depend_on_a_named_map(self):
        for crate in ("forge_engine", "forge_assets"):
            for path in (ROOT / "crates" / crate).rglob("*"):
                if not path.is_file():
                    continue
                self.assertNotIn("tidewater", str(path.relative_to(ROOT)).lower())
                if path.suffix in (".rs", ".wgsl", ".toml", ".json", ".md"):
                    source = path.read_text(encoding="utf-8").lower()
                    self.assertNotIn("tidewater", source, str(path))
                    self.assertNotIn("drg software solutions", source, str(path))
        for directory in ("apps", "crates"):
            for path in (ROOT / directory).rglob("*.rs"):
                source = rust_body(path)
                self.assertNotRegex(source, r"\btidewater_(?:water_query|exposure)\b", str(path))

    def test_animation_reference_is_owned_by_content_not_the_engine(self):
        root = ROOT / "crates/forge_tidewater/src/scene_wind"
        for path in root.rglob("*.rs"):
            production = rust_body(path).split("#[cfg(test)]", 1)[0]
            self.assertNotIn("tidewater", production.lower())
            self.assertNotIn("include_str!", production)
        flare = rust_body(ROOT / "crates/forge_tidewater/src/lens_flare.rs")
        self.assertNotIn("VillageMaterial", flare)
        self.assertNotIn("Assets<", flare)

    def test_engine_local_dependency_closure_excludes_map_components(self):
        workspace = tomllib.loads((ROOT / "Cargo.toml").read_text(encoding="utf-8"))
        dependencies = workspace["workspace"]["dependencies"]
        pending = ["forge_engine"]
        visited = set()
        while pending:
            name = pending.pop()
            if name in visited:
                continue
            visited.add(name)
            self.assertNotIn("tidewater", name)
            package = dependencies[name]
            for source_file in (ROOT / package["path"] / "src").rglob("*"):
                if source_file.is_file() and source_file.suffix in (".rs", ".wgsl"):
                    source = source_file.read_text(encoding="utf-8").lower()
                    self.assertNotIn("tidewater", source, str(source_file))
                    self.assertNotIn("drg software solutions", source, str(source_file))
            manifest = tomllib.loads((ROOT / package["path"] / "Cargo.toml").read_text(encoding="utf-8"))
            tables = [manifest.get("dependencies", {})]
            tables.extend(target.get("dependencies", {}) for target in manifest.get("target", {}).values())
            for table in tables:
                for dep, spec in table.items():
                    if isinstance(spec, dict) and spec.get("workspace") and dep in dependencies and "path" in dependencies[dep]:
                        pending.append(dep)
                    elif isinstance(spec, dict) and "path" in spec:
                        self.fail(f"Audit direct path dependency in {name}: {dep}")
        self.assertIn("forge_assets", visited)
        adapter = tomllib.loads((ROOT / "crates/forge_tidewater/Cargo.toml").read_text(encoding="utf-8"))
        self.assertIn("forge_engine", adapter["dependencies"])
        adapter_source = rust_body(ROOT / "crates/forge_tidewater/src/lib.rs")
        self.assertNotIn("pub use forge_engine::*", adapter_source)
        data_source = rust_body(ROOT / "crates/forge_tidewater_assets/src/lib.rs")
        self.assertNotIn("pub use forge_assets::*", data_source)

    def test_rust_build_inputs_do_not_include_orchestration_candidates(self):
        violations = []
        for directory in ("apps", "crates", "tools"):
            for path in (ROOT / directory).rglob("*.rs"):
                if "vendor" in path.parts:
                    continue
                source = path.read_text(encoding="utf-8")
                for target in re.findall(
                    r'(?:include(?:_str|_bytes)?!\s*\(\s*|#\[path\s*=\s*)"([^"]+)"',
                    source,
                ):
                    if "hivemind" in target.replace("\\", "/").split("/"):
                        violations.append(f"{path.relative_to(ROOT)} -> {target}")
        self.assertEqual(violations, [], "Move compiled inputs under product ownership")

    def test_large_identical_example_implementations_are_shared(self):
        seen = {}
        copies = []
        for path in sorted(EXAMPLES.glob("*.rs")):
            body = rust_body(path)
            if len(body.splitlines()) < 100:
                continue
            digest = hashlib.sha256(body.encode()).hexdigest()
            if digest in seen:
                copies.append(f"{path.name} = {seen[digest]}")
            else:
                seen[digest] = path.name
        self.assertEqual(copies, [], "Use thin example entry points with a shared harness")


if __name__ == "__main__":
    unittest.main()
