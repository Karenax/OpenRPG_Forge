"""Compare the actual Rust-assembled fragment library with the source raster oracle."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

import numpy as np


def digest(data):
    return hashlib.sha256(data).hexdigest()


def verify(oracle, paired, package, intended, reference, native):
    specification = importlib.util.spec_from_file_location(
        "oracle_verifier", Path(__file__).with_name("verify-fish-surface-reference.py"))
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    oracle_proof = module.verify(oracle, paired, package)
    oracle, intended, reference, native = map(Path, (oracle, intended, reference, native))
    baseline = json.loads((oracle / "reference.json").read_bytes())
    intended_descriptor = json.loads((intended / "reference.json").read_bytes())
    reference_descriptor = json.loads((reference / "reference.json").read_bytes())
    native_proof = json.loads((native / "result.json").read_bytes())
    assert native_proof["steps"] == {"compile": 0, "clippy": 0, "export": 0}
    assert native_proof["inputs_unchanged"] is True
    for name, pin in native_proof["inputs"].items():
        assert digest(Path(name).read_bytes()) == pin, "Adapter input changed after compilation"
    for descriptor, root, mode in [(intended_descriptor, intended, "intended"), (reference_descriptor, reference, "reference")]:
        assert descriptor["adapter_library_sha256"] == digest((native / (mode + ".wgsl")).read_bytes())
        assert descriptor["gpu_executed"] is True and descriptor["specular_mode"] == mode
        for key in ["source_commit", "package_sha256", "source_surface_sha256", "width", "height", "tile", "columns", "cases", "time"]:
            assert descriptor[key] == baseline[key], key
        for name in descriptor["files"]:
            module.checked(root, descriptor, name)
    names = ["albedo-roughness-f32.bin", "normal-metalness-f32.bin", "translucency-specular-f32.bin", "clearcoat-coverage-f32.bin"]
    changed_specular = 0
    for name in names:
        original = module.checked(oracle, baseline, name)
        actual = module.checked(intended, intended_descriptor, name)
        assert original == actual, "Rust intended surface differs from source: " + name
        alternate = module.checked(reference, reference_descriptor, name)
        if name != "translucency-specular-f32.bin":
            assert alternate == original, "Reference mode changed an unrelated surface output"
        else:
            left = np.frombuffer(original, dtype="<f4").reshape(-1, 4)
            right = np.frombuffer(alternate, dtype="<f4").reshape(-1, 4)
            assert np.array_equal(left[:, :3], right[:, :3]), "Translucency changed"
            assert np.all(right[:, 3] == 0), "Historical reference specular must be zero"
            changed_specular = int(np.count_nonzero(left[:, 3]))
    assert changed_specular == oracle_proof["covered_pixels"]
    return {"status": "passed", "rust_library_gpu_executed": True,
            "cases": len(baseline["cases"]), "intended_byte_identical_to_source": True,
            "reference_only_specular_changed": True, "changed_specular_pixels": changed_specular,
            "native_inputs": native_proof["inputs"],
            "limits": ["Planar procedural surface outputs only", "Bevy bindings, lighting, shadows, LOD, Studio and scene parity unverified"]}


if __name__ == "__main__":
    assert len(sys.argv) == 7, "ORACLE PAIRED PACKAGE INTENDED REFERENCE NATIVE required"
    print(json.dumps(verify(*sys.argv[1:]), indent=2))
