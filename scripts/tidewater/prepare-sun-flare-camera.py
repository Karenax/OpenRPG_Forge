"""Make an isolated, sun-facing copy of a Tidewater visual fixture.

Asset files remain hard-linked and byte-identical. Only the un-hashed camera
pose in fixture.json changes, so the normal fixture verifier still runs.
"""

from __future__ import annotations

import argparse
import json
import math
import os
from pathlib import Path


def normalize(vector: tuple[float, float, float]) -> tuple[float, float, float]:
    length = math.sqrt(sum(component * component for component in vector))
    if length < 1e-6:
        raise ValueError("sun direction is degenerate")
    return tuple(component / length for component in vector)


def cross(
    a: tuple[float, float, float], b: tuple[float, float, float]
) -> tuple[float, float, float]:
    return (
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    )


def look_rotation(direction: tuple[float, float, float]) -> tuple[float, ...]:
    """Quaternion for a Bevy camera whose local -Z points toward direction."""
    z = tuple(-component for component in normalize(direction))
    x = normalize(cross((0.0, 1.0, 0.0), z))
    y = cross(z, x)
    m00, m01, m02 = x[0], y[0], z[0]
    m10, m11, m12 = x[1], y[1], z[1]
    m20, m21, m22 = x[2], y[2], z[2]
    trace = m00 + m11 + m22
    if trace <= 0:
        raise ValueError("sun-facing pose needs another quaternion branch")
    scale = math.sqrt(trace + 1.0) * 2.0
    return (
        (m21 - m12) / scale,
        (m02 - m20) / scale,
        (m10 - m01) / scale,
        scale / 4.0,
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    source = args.source.resolve(strict=True)
    output = args.output.resolve(strict=False)
    if not source.is_dir() or output.exists() or source == output:
        raise ValueError("source must be a directory and output must be new")
    fixture = json.loads((source / "fixture.json").read_text(encoding="utf-8"))
    direction = tuple(fixture["environment_lighting_probe"]["sun_direction"])
    if len(direction) != 3:
        raise ValueError("sun direction must have three components")
    if any(item.is_dir() for item in source.iterdir()):
        raise ValueError("fixture unexpectedly contains a directory")
    fixture["camera_pose"]["rotation"] = look_rotation(direction)
    # Grass cell lists in this fixture were qualified for the village pose.
    # Leave their asset bytes untouched, but do not load that camera-specific
    # oracle for a sky-facing diagnostic.
    fixture["files"].pop("grass.json", None)
    fixture["camera_reference"] = {
        "view": "sun-facing lens-flare diagnostic",
        "source": str(source),
        "matched_to_upstream_capture": False,
    }
    fixture.setdefault("limitations", []).append(
        "diagnostic_only_camera_oriented_to_environment_sun"
    )
    fixture["limitations"].append("grass_omitted_for_sun_facing_camera")
    output.mkdir(parents=True)
    for item in source.iterdir():
        if item.name != "fixture.json":
            os.link(item, output / item.name)
    (output / "fixture.json").write_text(
        json.dumps(fixture, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"SUN_FLARE_CAMERA_READY output={output} files={len(fixture['files'])}")


if __name__ == "__main__":
    main()
