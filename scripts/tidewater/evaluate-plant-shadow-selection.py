"""Check real Studio CPU caster observations without claiming GPU coverage."""
import argparse
import hashlib
import json
import math
from pathlib import Path


def finite_vector(value, size):
    return isinstance(value, list) and len(value) == size and all(
        type(n) in (int, float) and math.isfinite(n) for n in value)


def checks(sample):
    if not isinstance(sample, dict):
        return {"selection_present": False}
    selection = sample.get("plant_caster_selection")
    if not isinstance(selection, dict):
        return {"selection_present": False}
    source = selection.get("source")
    groups = selection.get("groups")
    camera = selection.get("camera")
    pose = (sample.get("camera_signature") or {}).get("world_from_view")
    result = {
        "selection_present": True,
        "source_identified": isinstance(source, list) and len(source) == 2
        and isinstance(source[0], str) and len(source[0]) == 64
        and all(c in "0123456789abcdef" for c in source[0])
        and type(source[1]) is int and source[1] > 0,
        "current_camera": finite_vector(camera, 4) and finite_vector(pose, 16)
        and all(abs(camera[i] - pose[12 + i]) <= 0.001 for i in range(3)),
        "five_groups": isinstance(groups, list) and len(groups) == 5
        and all(isinstance(group, dict) for group in groups)
        and {g.get("group") for g in groups}
        == {"palms", "bananas", "monsteras", "broadleaf", "canopy"},
    }
    if not result["five_groups"]:
        return result
    for group in groups:
        count_names = ("candidates", "submitted", "capacity_dropped", "submitted_beyond_lod_end")
        valid_counts = all(type(group.get(name)) is int and group[name] >= 0 for name in count_names)
        count = group.get("submitted")
        maximum = group.get("max_base_distance")
        result[group["group"] + ".counts_consistent"] = valid_counts and (
            group["candidates"] == count + group["capacity_dropped"]
            and group["submitted_beyond_lod_end"] <= count)
        result[group["group"] + ".distance_consistent"] = (
            maximum is None if count == 0 else type(maximum) in (int, float)
            and math.isfinite(maximum) and maximum >= 0)
        lod = group.get("lod_range")
        result[group["group"] + ".lod_identified"] = finite_vector(lod, 3) and 0 <= lod[0] < lod[1] < lod[2]
    result["casters_present"] = any(type(g.get("submitted")) is int and g["submitted"] > 0 for g in groups)
    return result


def evaluate(value):
    result = {}
    for phase in ("before", "after"):
        result.update({phase + "." + key: passed for key, passed in checks(value.get(phase)).items()})
    first = (value.get("before") or {}).get("plant_caster_selection") or {}
    last = (value.get("after") or {}).get("plant_caster_selection") or {}
    result["same_source"] = first.get("source") is not None and first.get("source") == last.get("source")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("observation", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError("Fresh evaluation receipt required")
    raw = args.observation.read_bytes()
    result = evaluate(json.loads(raw))
    passed = all(result.values())
    args.output.write_text(json.dumps({"checks": result, "status": "PASS" if passed else "FAIL",
        "observation_sha256": hashlib.sha256(raw).hexdigest(),
        "scope": "CPU caster stream selection for animation upload",
        "gpu_depth_verified": False, "visual_parity": False}, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": "PASS" if passed else "FAIL", "failed_checks": [k for k, v in result.items() if not v]}))
    raise SystemExit(0 if passed else 1)
