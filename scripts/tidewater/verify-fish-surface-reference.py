"""Verify paired complete-source raster oracles without claiming scene parity."""
import hashlib
import json
import pathlib
import sys

import numpy as np


def digest(data):
    return hashlib.sha256(data).hexdigest()


def checked(root, descriptor, name):
    data = (root / name).read_bytes()
    pin = descriptor["files"][name]
    assert len(data) == pin["bytes"] and digest(data) == pin["sha256"], name
    return data


def verify(first, second, package):
    root, paired, package = map(pathlib.Path, (first, second, package))
    a = (root / "reference.json").read_bytes()
    b = (paired / "reference.json").read_bytes()
    assert a == b, "Paired descriptors differ"
    descriptor = json.loads(a)
    manifest_bytes = (package / "package.json").read_bytes()
    assert digest(manifest_bytes) == descriptor["package_sha256"]
    assert descriptor["package_sha256"] == "e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a"
    manifest = json.loads(manifest_bytes)

    def payload(name):
        data = (package / name).read_bytes()
        pin = manifest["files"][name]
        assert len(data) == pin["bytes"] and digest(data) == pin["sha256"], name
        return data

    for name in manifest["files"]:
        payload(name)
    assert descriptor["schema"] == "forge.tidewater.fish-surface-reference"
    assert descriptor["version"] == 1 and descriptor["gpu_executed"] is True
    assert descriptor["complete_source_surface_body"] is True
    assert descriptor["source_commit"] == "1438b1abfcaee3267092b75573014f4d9b4a983c"
    shader = checked(root, descriptor, "surface.wgsl")
    source_surface = payload("material/main-surface.wgsl")
    assert digest(source_surface) == descriptor["source_surface_sha256"]
    assert shader.count(source_surface) == 1, "Complete original surface body must occur once"
    assert payload("material/module-2-fish.wgsl") in shader
    material = json.loads(payload(manifest["material"]["file"]))
    props = json.loads(payload(manifest["props"]["file"]))
    assert descriptor["specular_mode"] == material["specular_mode"] == "intended"
    assert checked(root, descriptor, "records-f32.bin") == payload("geometry/" + props["instance_stream"]["file"])
    assert checked(root, descriptor, "skin-f32.bin") == payload("material/" + material["skin_table"]["file"])
    assert len(props["instances"]) == 78 and len(descriptor["cases"]) == 1560
    for name in descriptor["files"]:
        assert checked(root, descriptor, name) == checked(paired, descriptor, name), name
    height, width = descriptor["height"], descriptor["width"]
    tile, columns = descriptor["tile"], descriptor["columns"]
    assert (height, width, tile, columns) == (1248, 1280, 32, 40)
    names = ["albedo-roughness-f32.bin", "normal-metalness-f32.bin", "translucency-specular-f32.bin", "clearcoat-coverage-f32.bin"]
    planes = [np.frombuffer(checked(root, descriptor, name), dtype="<f4").reshape(height, width, 4) for name in names]
    assert all(np.isfinite(plane).all() for plane in planes)
    covered_total = 0
    discarded_total = 0
    for index, case in enumerate(descriptor["cases"]):
        ordinal, part = divmod(index, 20)
        instance = props["instances"][ordinal]
        assert case["ordinal"] == ordinal and case["part"] == part
        assert case["source_id"] == instance["source_id"]
        assert case["pattern"] == int(instance["record_f32"][8])
        x, y = (index % columns) * tile, (index // columns) * tile
        samples = [plane[y:y + tile, x:x + tile] for plane in planes]
        mask = samples[3][..., 3] == 1
        assert int(mask.sum()) == case["covered_pixels"] and mask.any()
        assert np.all(samples[3][..., 3][~mask] == 0)
        assert all(np.all(sample[~mask] == 0) for sample in samples), "Discard masks must match across both render passes"
        normals = samples[1][..., :3][mask]
        assert np.allclose(np.linalg.norm(normals, axis=1), 1, atol=2e-5, rtol=0)
        wet = instance["record_f32"][13]
        coat = wet * (0.6 if part == 0 else 0.25 if 0 < part < 8 else 0)
        assert np.allclose(samples[3][..., 0][mask], coat, atol=1e-6, rtol=0)
        assert np.allclose(samples[3][..., 1][mask], 0.2, atol=1e-6, rtol=0)
        spec = samples[2][..., 3][mask]
        assert np.all(np.isclose(spec, 0.5, atol=1e-6) | np.isclose(spec, 1, atol=1e-6))
        if not 0 < part < 7:
            assert mask.all(), "Only the source translucent fin mask may discard"
        covered_total += int(mask.sum())
        discarded_total += int((~mask).sum())
    assert discarded_total > 0, "The stochastic fin discard must actually execute"
    return {"status": "passed", "reference_sha256": digest(a), "paired_byte_identical": True,
            "complete_source_surface_body": True, "cases": 1560,
            "covered_pixels": covered_total, "discarded_fin_pixels": discarded_total,
            "scope": "Planar material oracle; scene lighting, real geometry and visual parity unverified"}


if __name__ == "__main__":
    assert len(sys.argv) == 4, "FIRST SECOND PACKAGE required"
    print(json.dumps(verify(*sys.argv[1:]), indent=2))
