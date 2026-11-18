"""Generate periodic cloud-volume mips and an independently seeded trace dither."""

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np


VOLUME_SIZE = 64
SEED = 20261114


def periodic_field(rng, cells):
    """Interpolate a periodic lattice with a cubic smoothstep in fixed point."""
    lattice = rng.integers(0, 256, size=(cells,) * 3, dtype=np.int32)
    scale = VOLUME_SIZE // cells
    sample = np.arange(VOLUME_SIZE, dtype=np.int32)
    lower = sample // scale
    upper = (lower + 1) % cells
    fraction = sample % scale
    weight = 256 * fraction**2 * (3 * scale - 2 * fraction) // scale**3
    field = lattice
    for axis in range(3):
        shape = [1] * 3
        shape[axis] = VOLUME_SIZE
        blend = weight.reshape(shape)
        field = (
            np.take(field, lower, axis=axis) * (256 - blend)
            + np.take(field, upper, axis=axis) * blend + 128
        ) // 256
    return field


def cloud_mips(rng):
    rgbs = []
    for cells in (4, 8, 16):
        coarse = periodic_field(rng, cells)
        detail = periodic_field(rng, cells * 2)
        rgbs.append(((3 * coarse + detail + 2) // 4).astype(np.uint8))
    volume = np.stack((*rgbs, np.full_like(rgbs[0], 255)), axis=-1)
    for level in range(7):
        yield f"cloud-volume-m{level}.rgba8.bin", volume.tobytes()
        if level < 6:
            half = volume.shape[0] // 2
            sums = volume.reshape(half, 2, half, 2, half, 2, 4).sum(axis=(1, 3, 5))
            volume = ((sums + 4) // 8).astype(np.uint8)


def trace_dither(rng):
    values = rng.permutation(VOLUME_SIZE**2).reshape((VOLUME_SIZE,) * 2)
    # Rank a high-pass field to keep a uniform histogram and discourage nearby
    # samples from sharing similar values, with periodic edges.
    high_pass = 4 * values - sum(
        np.roll(values, shift, axis=axis) for axis in range(2) for shift in (-1, 1)
    )
    order = np.argsort(high_pass.ravel(), kind="stable")
    result = np.empty(VOLUME_SIZE**2, dtype=np.uint8)
    result[order] = np.arange(VOLUME_SIZE**2) // 16
    return result.tobytes()


def generate():
    rng = np.random.Generator(np.random.PCG64(SEED))
    return dict([*cloud_mips(rng), ("cloud-blueNoise.bin", trace_dither(rng))])


def replace_textures(source, expected_manifest, generated):
    """Prepare a separate package with updated pins for the generated textures."""
    manifest_bytes = (source / "package.json").read_bytes()
    if hashlib.sha256(manifest_bytes).hexdigest() != expected_manifest:
        raise ValueError("Environment manifest SHA-256 differs")
    manifest = json.loads(manifest_bytes)
    if manifest["schema"] != "forge.tidewater.environment-package":
        raise ValueError("Unsupported environment package")
    pins = manifest["files"]
    if not generated.keys() <= pins.keys() or "cloud-environment.json" not in pins:
        raise ValueError("Missing cloud texture dependencies")
    payloads = {}
    for name, pin in pins.items():
        if name in (".", "..", "package.json") or any(c in name for c in "/\\:"):
            raise ValueError(f"Invalid environment path: {name}")
        path = source / name
        if not path.is_file() or path.is_symlink() or path.stat().st_size != pin["bytes"]:
            raise ValueError(f"Invalid environment file: {name}")
        with path.open("rb") as stream:
            data = stream.read(pin["bytes"] + 1)
        if len(data) != pin["bytes"] or hashlib.sha256(data).hexdigest() != pin["sha256"]:
            raise ValueError(f"Environment pin differs: {name}")
        payloads[name] = data
    metadata = json.loads(payloads["cloud-environment.json"])
    for name, data in generated.items():
        if pins[name] != {"sha256": metadata["files"][name], "bytes": len(data)}:
            raise ValueError(f"Cloud texture metadata differs: {name}")
        metadata["files"][name] = hashlib.sha256(data).hexdigest()
    for name in ("/clouds/baseShape64.bin", "/clouds/blueNoise.bin"):
        metadata.get("assets", {}).pop(name, None)
    payloads.update(generated)
    payloads["cloud-environment.json"] = encode(metadata)
    for name in (*generated, "cloud-environment.json"):
        data = payloads[name]
        pins[name] = {"sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)}
    payloads["package.json"] = encode(manifest)
    return payloads


def encode(value):
    return (json.dumps(value, indent=2, ensure_ascii=False) + "\n").encode()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="New output directory")
    parser.add_argument("--environment", type=Path, help="Existing environment package to repackage")
    parser.add_argument("--manifest-sha256", help="Expected SHA-256 of the existing package manifest")
    args = parser.parse_args()
    if bool(args.environment) != bool(args.manifest_sha256):
        parser.error("--environment and --manifest-sha256 must be supplied together")
    files = generate()
    if args.environment:
        files = replace_textures(args.environment, args.manifest_sha256, files)
    args.output.mkdir()
    for name, data in files.items():
        (args.output / name).write_bytes(data)
