"""Checks captured bound state, not pixel execution, visual quality, or Gate 1."""
import hashlib
import json
import math
from pathlib import Path
import re
import struct


def require(condition, message):
    if not condition:
        raise ValueError(message)


def close(actual, expected):
    require(len(actual) == len(expected), "Wrong vector length")
    require(all(math.isfinite(a) and abs(a - e) < 0.00001 for a, e in zip(actual, expected)),
            "Unexpected bound values: {} != {}".format(actual, expected))


def block(draw, bind_set, binding, size):
    found = [b for b in draw["constant_blocks"] if (b["set"], b["binding"], b["size"]) == (bind_set, binding, size)]
    require(len(found) == 1, "Missing or duplicate constant block")
    result = found[0]
    require(len(bytes.fromhex(result["raw_hex"])) == size, "Wrong constant buffer length")
    require(result["bound"]["resource"] != "ResourceId::0", "Unbound constant buffer")
    return result


def floats(value, offset, count):
    return list(struct.unpack_from("<" + "f" * count, bytes.fromhex(value["raw_hex"]), offset))


def texture_binding(draw, bind_set, binding, array_index=0):
    matches = [r for r in draw["resources"] if (r["set"], r["binding"], r["array_index"]) == (bind_set, binding, array_index)]
    require(len(matches) == 1 and not matches[0]["statically_unused"], "Missing used texture binding")
    require(matches[0]["bound"]["resource"] != "ResourceId::0", "Null texture")
    return matches[0]["bound"]


def validate_document(document):
    require(document["schema"] == "forge-gate1-material-replay/v1", "Unsupported schema")
    capture = document["capture"]
    require(capture["quality"] == "High", "Only native High is covered")
    for key, length in (("sha256", 64), ("parent_receipt_sha256", 64), ("runtime_ledger_sha256", 64), ("runtime_source_commit", 40)):
        require(re.fullmatch("[0-9a-f]{" + str(length) + "}", capture[key]) is not None, "Invalid source identity")
    require((capture["window"]["physical_width"], capture["window"]["physical_height"]) == (1920, 1080), "Wrong capture resolution")
    require(capture["pipeline"] == "Vulkan" and capture["degraded"] is False, "Invalid replay")
    require(capture["weather"] in ("clear", "rain"), "Unsupported weather")
    q = document["qualification"]
    require(q == {"pixel_debug_performed": False, "human_review_performed": False,
                  "same_process_weather_reversal": False, "browser_observed": False,
                  "routes_closed": [], "gate1_transition_authorized": False}, "Scope was overstated")
    draws = document["draws"]
    require(len({d["event_id"] for d in draws}) == len(draws), "Duplicate draw event")
    require(all(d["indices"] > 0 and d["instances"] > 0 for d in draws), "Empty draw")
    selected = {}
    for role in ("water", "terrain", "vegetation", "tonemapping"):
        matches = [d for d in draws if d["role"] == role]
        require(len(matches) == 1, "Expected one representative " + role + " draw")
        selected[role] = matches[0]
    bloom = [d for d in draws if d["role"] == "bloom"]
    require(len(draws) == 20 and len(bloom) == 16, "Unexpected selected draw count")
    require([d["entry"] for d in bloom] == ["downsample_first"] + ["downsample"] * 7 + ["upsample"] * 8, "Wrong Bloom chain")
    require(all(d["indices"] == 3 and d["instances"] == 1 for d in bloom), "Bloom is not fullscreen triangles")
    water = selected["water"]
    water_ext = block(water, 3, 13, 80)
    close(floats(water_ext, 64, 4), [0.84, 0.16, 1.33, 0.72])
    close(floats(water_ext, 32, 4), [0.12, 5.5, 0.72, 0.62])
    close(floats(water_ext, 12, 1), [0])
    material = block(water, 3, 0, 192)
    close(floats(material, 120, 3), [0.18, 3.2, 1.33])
    wetness = 0.0 if capture["weather"] == "clear" else 0.78
    wet_values = {}
    for role, size, offset in (("terrain", 112, 96), ("vegetation", 160, 128)):
        bound = block(selected[role], 3, 13, size)
        wet_values[role] = floats(bound, offset, 4)
        close(wet_values[role], [wetness, 0.32, 0.72, 0])
    grade = block(selected["tonemapping"], 0, 0, 784)
    rain = capture["weather"] == "rain"
    close(floats(grade, 672, 3), [1.08 if rain else 1.06] * 3)
    close(floats(grade, 688, 3), [1] * 3)
    close(floats(grade, 744, 1), [-0.35 if rain else 0])
    close(floats(grade, 752, 1), [0.86 if rain else 1.04])
    # GetOutputTargets includes the MSAA attachment and its resolve target.
    # Start from the actual Bloom input and require the exact resolve event,
    # rather than assuming attachment zero is the single-sampled HDR image.
    textures = {t["resource"]: t for t in document["textures"]}
    hdr = texture_binding(bloom[0], 0, 0)["resource"]
    require(hdr in {t["resource"] for t in water["outputs"]}, "Bloom input is not a water pass target")
    color_target = water["outputs"][0]["resource"]
    if color_target != hdr:
        source_resolves = {u["event_id"] for u in textures[color_target]["usage"] if u["usage"] == "ResourceUsage.ResolveSrc"}
        dest_resolves = {u["event_id"] for u in textures[hdr]["usage"] if u["usage"] == "ResourceUsage.ResolveDst"}
        require(any(water["event_id"] < event < bloom[0]["event_id"] for event in source_resolves & dest_resolves), "No exact water MSAA resolve before Bloom")
    previous = None
    pyramid = bloom[0]["outputs"][0]["resource"]
    for i, draw in enumerate(bloom):
        source = texture_binding(draw, 0, 0)
        target = draw["outputs"][0]
        require(source["resource"] == (hdr if i == 0 else pyramid), "Broken Bloom input chain")
        require(target["resource"] == (hdr if i == 15 else pyramid), "Broken Bloom output chain")
        require(source["first_mip"] == (max(i - 1, 0) if i < 8 else 15 - i), "Wrong Bloom input mip")
        require(target["first_mip"] == (i if i < 8 else max(14 - i, 0)), "Wrong Bloom output mip")
        if previous is not None:
            require(draw["event_id"] > previous, "Unordered Bloom events")
        previous = draw["event_id"]
        if i == 0 or i >= 8:
            block(draw, 0, 2, 48)
        else:
            require(not draw["constant_blocks"], "Ordinary downsample should not use constants")
        if i >= 8:
            factor = draw["blend"]["factor"]
            require(len(factor) == 4 and all(math.isfinite(x) and 0 <= x <= 1 for x in factor), "Unbounded Bloom blend")
            require(draw["blend"]["attachments"][0]["state"]["enabled"] is True, "Bloom blend disabled")
    close(bloom[-1]["blend"]["factor"], [0.08, 0.08, 0.08, 1])
    tone = selected["tonemapping"]
    require(water["event_id"] < bloom[0]["event_id"] < bloom[-1]["event_id"] < tone["event_id"], "Wrong pass order")
    require(texture_binding(tone, 0, 1)["resource"] == hdr, "Tonemapping does not read composed HDR")
    transmission = texture_binding(water, 0, 15)
    transmission_texture = textures[transmission["resource"]]
    require((transmission_texture["width"], transmission_texture["height"]) == (1920, 1080), "Wrong transmission extent")
    require(any(u["event_id"] < water["event_id"] and u["usage"] == "ResourceUsage.CopyDst" for u in transmission_texture["usage"]), "No transmission copy before water")
    ibl = texture_binding(water, 1, 1)
    ibl_texture = textures[ibl["resource"]]
    require(ibl_texture["cubemap"] is True and ibl_texture["mips"] > 1, "Missing mipmapped IBL cubemap")
    return {
        "status": "PASS_BOUND_STATE_ONLY", "run": capture["run"], "capture_sha256": capture["sha256"],
        "events": {role: draw["event_id"] for role, draw in selected.items()},
        "water_optical": floats(water_ext, 64, 4), "water_transmission_thickness_ior": floats(material, 120, 3),
        "wetness": wet_values, "grading_contrast": floats(grade, 672, 3),
        "grading_exposure": floats(grade, 744, 1)[0], "grading_post_saturation": floats(grade, 752, 1)[0],
        "bloom_events": [d["event_id"] for d in bloom], "bloom_blend_factors": [d["blend"]["factor"] for d in bloom[8:]],
        "transmission_texture": transmission, "ibl_texture": ibl, "qualification": q,
    }


def validate_files(root):
    root = Path(root)
    path = root / "material-replay.json"
    document = json.loads(path.read_text(encoding="utf-8"))
    for draw in document["draws"]:
        artifact = draw["shader"]
        require(Path(artifact["file"]).name == artifact["file"] and ":" not in artifact["file"]
                and "\\" not in artifact["file"], "Unsafe shader path")
        require(hashlib.sha256((root / artifact["file"]).read_bytes()).hexdigest() == artifact["sha256"], "Shader hash mismatch")
    result = validate_document(document)
    result["extraction_sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
    return result


def validate_matrix(root):
    root = Path(root)
    launcher_path = root / "material-replay-launcher.json"
    launcher = json.loads(launcher_path.read_text(encoding="utf-8-sig"))
    require(launcher["status"] == "PASS", "Replay launcher did not pass")
    safety = launcher["safety"]
    for field in ("git_unchanged", "captures_unchanged", "input_receipt_scripts_manifest_unchanged",
                  "real_profile_unchanged", "disposable_profile_removed", "execution_mirror_removed"):
        require(safety[field] is True, "Replay safety check failed: " + field)
    for field in ("host_registry_modified", "runtime_launched", "build_performed"):
        require(safety[field] is False, "Replay scope changed: " + field)
    names = ["clear-01", "clear-02", "rain-01", "rain-02"]
    require([r["run"] for r in launcher["results"]] == names, "Incomplete or reordered matrix")
    results = []
    for run in launcher["results"]:
        result = validate_files(root / run["run"])
        require(result["extraction_sha256"] == run["receipt_sha256"], "Extraction changed since launch")
        require(result["capture_sha256"] == run["capture_sha256"], "Capture identity drift")
        results.append(result)
    repeat_fields = ("water_optical", "water_transmission_thickness_ior", "wetness", "grading_contrast",
                     "grading_exposure", "grading_post_saturation", "bloom_blend_factors")
    for first, second in ((results[0], results[1]), (results[2], results[3])):
        require(all(first[k] == second[k] for k in repeat_fields), "Bound values differ across repetitions")
    artifacts = []
    for name in names:
        for path in sorted((root / name).iterdir()):
            require(path.is_file() and not path.is_symlink(), "Unexpected replay directory/link")
            artifacts.append({"file": path.relative_to(root).as_posix(), "bytes": path.stat().st_size,
                              "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    return {"schema": "forge-gate1-material-bound-state-validation/v1", "status": "PASS_BOUND_STATE_ONLY",
            "evidence_root": str(root.resolve()), "launcher_sha256": hashlib.sha256(launcher_path.read_bytes()).hexdigest(),
            "validator_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            "parent_receipt_sha256": launcher["parent_receipt_sha256"], "safety": safety,
            "bound_values_repeatable_per_weather": True, "results": results, "artifacts": artifacts,
            "qualification": results[0]["qualification"]}


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = validate_matrix(args.root)
    if args.output:
        with args.output.open("x", encoding="utf-8") as handle:
            json.dump(result, handle, indent=2, allow_nan=False)
    print("PASS_BOUND_STATE_ONLY: 4 captures; no route qualification")
