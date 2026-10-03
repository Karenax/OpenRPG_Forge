"""Create a verified, isolated Tidewater shader A/B fixture for one shadow term."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path

TERMS = {
    "hill": "*terrainSunShadowAt(P)",
    "cloud": "*cloudsShadow(P.xz)",
    "object": "*sunShadow(P,N,pixel)",
}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("term", choices=TERMS)
    args = parser.parse_args()
    source = args.source.resolve(strict=True)
    output = args.output.resolve(strict=False)
    if not source.is_dir() or output.exists() or source == output:
        raise ValueError("source must be a directory and output must be new")
    fixture = json.loads((source / "fixture.json").read_text(encoding="utf-8"))
    original = (source / "terrain.wgsl").read_bytes()
    expected = fixture["files"]["terrain.wgsl"]
    if hashlib.sha256(original).hexdigest() != expected:
        raise ValueError("terrain shader does not match verified fixture")
    needle = TERMS[args.term].encode("ascii")
    if original.count(needle) != 1:
        raise ValueError("shadow term is missing or ambiguous")
    modified = original.replace(needle, b"*1.0", 1)
    if any(item.is_dir() for item in source.iterdir()):
        raise ValueError("fixture unexpectedly contains a directory")
    output.mkdir(parents=True)
    for item in source.iterdir():
        if item.name in {"terrain.wgsl", "fixture.json"}:
            continue
        os.link(item, output / item.name)
    (output / "terrain.wgsl").write_bytes(modified)
    fixture["files"]["terrain.wgsl"] = hashlib.sha256(modified).hexdigest()
    fixture.setdefault("limitations", []).append(
        f"diagnostic_only_terrain_{args.term}_sun_shadow_neutralized"
    )
    (output / "fixture.json").write_text(
        json.dumps(fixture, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    if hashlib.sha256(original).hexdigest() != expected:
        raise AssertionError("source shader changed")
    print(f"LIGHTING_AB_READY term={args.term} files={len(fixture['files'])} output={output}")


if __name__ == "__main__":
    main()
