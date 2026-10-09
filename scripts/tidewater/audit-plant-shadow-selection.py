"""Inventory source plant caster limits; this is not a rendered-shadow verdict."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct


def audit(package, camera, streams):
    pins = {}

    def read(name):
        acquired = name.startswith("vegetation-") and name.endswith(".bin")
        root = streams if acquired else package
        data = (root / name).read_bytes()
        pins[name] = hashlib.sha256(data).hexdigest()
        if acquired and pins[name] != name.removeprefix("vegetation-").removesuffix(".bin"):
            raise ValueError("Acquired stream hash mismatch")
        return data

    description = json.loads(read("vegetation-scene.json"))
    result = []
    for group in description["groups"]:
        if group["name"] == "understory":
            continue  # Same exclusion as Studio's Source::from_vegetation.
        near = next(level for level in group["levels"] if level["near"])
        positions = list(struct.iter_unpack("<4f", read(group["streams"]["iPos"])))
        radii = [v[0] for v in struct.iter_unpack("<f", read(group["streams"]["qr2"]))]
        if len(positions) != len(radii) or len(positions) != group["count"]:
            raise ValueError("Source stream cardinality mismatch")
        radius, cell = group["query_radius"], group["cell_size"]
        selected = []
        for index, (x, y, z, _) in enumerate(positions):
            squared = (x - camera[0]) ** 2 + (z - camera[2]) ** 2
            if (math.floor((camera[0] - radius) / cell) <= math.floor(x / cell)
                    <= math.floor((camera[0] + radius) / cell)
                    and math.floor((camera[2] - radius) / cell) <= math.floor(z / cell)
                    <= math.floor((camera[2] + radius) / cell)
                    and squared < (radii[index] if radii[index] > 0 else radius ** 2)):
                selected.append((index, squared))
        # Stable source cell order followed by float32 distance ordering.
        selected.sort(key=lambda v: (math.floor(positions[v[0]][2] / cell),
                                     math.floor(positions[v[0]][0] / cell)))
        selected.sort(key=lambda v: struct.unpack("<f", struct.pack("<f", v[1]))[0])
        submitted = selected[:near["capacity"]]
        result.append({"group": group["name"], "population": len(positions),
                       "query_radius_m": radius, "shader_lod_range_m": near["range"],
                       "capacity": near["capacity"], "cpu_selected": len(selected),
                       "submitted": len(submitted), "capacity_dropped": max(0, len(selected) - len(submitted)),
                       "max_submitted_planar_distance_m": math.sqrt(max((v[1] for v in submitted), default=0)),
                       "note": "CPU selection only; shader masks, depth and receivers unobserved"})
    return {"camera": camera, "groups": result, "source_pins": pins,
            "scope": "Unedited acquired population, not the current Studio authored projection",
            "rendered_shadow_verified": False, "visual_parity": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--camera", type=float, nargs=3, required=True)
    parser.add_argument("--streams", type=Path, required=True)
    args = parser.parse_args()
    args.output.write_text(json.dumps(audit(args.package, args.camera, args.streams), indent=2) + "\n", encoding="utf-8")
