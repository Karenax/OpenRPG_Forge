"""Pair a verified Tidewater scene fixture with the default cloud acquisition."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path

OLD_OVERCAST = "0406213b070741e79713a190783e9e1027f5362eccde7d44835f0c711b97d40e"
SOURCE_COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scene", type=Path)
    parser.add_argument("cloud_environment", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    scene = args.scene.resolve(strict=True)
    clouds = args.cloud_environment.resolve(strict=True)
    output = args.output.resolve(strict=False)
    if not scene.is_dir() or output.exists():
        raise ValueError("scene must be a directory and output must be new")
    weather = json.loads(clouds.read_text(encoding="utf-8"))
    if (
        weather.get("schema") != "forge.tidewater.environment-probe"
        or weather.get("source_commit") != SOURCE_COMMIT
        or weather.get("cloud_coverage", 0.45) > 0.6
    ):
        raise ValueError("expected the pinned default-weather acquisition")
    fixture_path = scene / "fixture.json"
    fixture = json.loads(fixture_path.read_text(encoding="utf-8"))
    if (
        fixture.get("source", {}).get("commit") != SOURCE_COMMIT
        or fixture.get("cloud_acquisition_sha256") != OLD_OVERCAST
        or sha256(scene / "terrain.wgsl") != fixture["files"]["terrain.wgsl"]
    ):
        raise ValueError("source scene is not the expected verified overcast fixture")
    if any(not item.is_file() or item.is_symlink() for item in scene.iterdir()):
        raise ValueError("scene must contain only regular files")
    extras = {item.name for item in scene.iterdir()} - set(fixture["files"]) - {"fixture.json"}
    if extras != {"package-only-proof.json"}:
        raise ValueError("source scene file set differs from its manifest")
    for name, expected in fixture["files"].items():
        if sha256(scene / name) != expected:
            raise ValueError(f"source scene file digest differs: {name}")
    fixture["cloud_acquisition_sha256"] = sha256(clouds)
    fixture.setdefault("limitations", []).append("default_weather_acquisition_paired")
    output.mkdir(parents=True)
    for item in scene.iterdir():
        if item.name != "fixture.json":
            os.link(item, output / item.name)
    (output / "fixture.json").write_text(
        json.dumps(fixture, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    if sha256(fixture_path) == sha256(output / "fixture.json"):
        raise AssertionError("scene provenance did not change")
    print(
        json.dumps(
            {
                "scene": str(output),
                "cloud_acquisition_sha256": fixture["cloud_acquisition_sha256"],
                "files": len(fixture["files"]),
            }
        )
    )


if __name__ == "__main__":
    main()
