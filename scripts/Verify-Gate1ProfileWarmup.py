"""Recheck native profile preparation artifacts; never grants artistic acceptance."""
import argparse
import hashlib
import json
import math
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def verify(root):
    result = {"gate1_pass": False, "studio_verified": False, "cycles": []}
    for name in ("native-clear", "native-clear-cycle", "native-rain-cycle"):
        directory = root / name
        receipt = read(directory / "receipt.json")
        require(receipt["exit_code"] == 0 and receipt["process_exited"]
                and receipt["inputs_unchanged"] and receipt["interrupted"] is None, name)
        for pin in receipt["pins"]:
            digest = hashlib.sha256(Path(pin["Path"]).read_bytes()).hexdigest()
            require(digest == pin["Hash"].lower(), f"changed input: {pin['Path']}")
        events = [json.loads(line) for line in (directory / "stderr.log").read_text(
            encoding="utf-8-sig").splitlines() if line.strip()]
        require(not any(e.get("level") == "ERROR" for e in events), f"runtime error: {name}")
        if name == "native-clear":
            metrics = read(directory / "metrics.json")
            warmup = metrics["profile_warmup"]
            require(warmup["phase"] == "ready" and warmup["error"] is None, "not ready")
            require([v["tier"] for v in warmup["completed"]] ==
                    ["low", "medium", "high", "ultra", "high"], "incomplete preparation")
            require((directory / "capture.png").stat().st_size > 1000, "missing capture")
            result["native_preparation_seconds"] = warmup["elapsed_seconds"]
            continue
        prepared = [e for e in events if e.get("event_kind") == "quality_variant_prepared"]
        observed = [e for e in events if e.get("event_kind") == "global_quality_cycle_observed"]
        require([e["tier"] for e in prepared] == ["Low", "Medium", "High", "Ultra", "High"], name)
        require([e["tier"] for e in observed] == ["High", "Low", "Medium", "Ultra", "High"], name)
        require(prepared[-1]["timestamp"] < observed[0]["timestamp"], "cycle preceded preparation")
        require(any(e.get("event_kind") == "quality_cycle_completed" for e in events), "incomplete cycle")
        bindings, counts = [], []
        for index, event in enumerate(observed):
            serialized = event["observation"]
            require(serialized.startswith("Ok(") and serialized.endswith(")"), "invalid observation")
            state = json.loads(json.loads(serialized[3:-1]))
            app, gpu = state["application"], state["gpu_submission"]
            require(app["gpu_ready"] and app["error"] is None and all(app["projected_domains"]), "not applied")
            require(app["identity"] == gpu["identity"] and gpu["queue_work_completed"], "stale GPU receipt")
            require(gpu["pipelines_pending"] == 0 and gpu["pipelines_failed"] == 0, "cache not ready")
            require((directory / "cycle" / f"stage-{index}.png").stat().st_size > 1000, "missing stage")
            counts.append(gpu["pipelines_total"])
            bindings.append(state["effective"]["plan"]["source_bindings"])
        require(all(b == bindings[0] for b in bindings), "author sources changed")
        result["cycles"].append({"name": name, "prepared_seconds": prepared[-1]["elapsed_seconds"],
            "prepared_pipelines": prepared[-1]["pipelines"], "observed_pipelines": counts,
            "new_pipelines_at_observed_stages": max(counts) - prepared[-1]["pipelines"],
            "hitch_free_qualified": False})
    result["native_verified"] = True
    if (root / "studio-clear.json").exists() or (root / "studio-rain.json").exists():
        sources, studio = [], []
        for weather in ("clear", "rain"):
            observations = read(root / f"studio-{weather}.json")["observations"]
            warmup, quality = observations["profile_warmup"], observations["quality"]
            require(warmup["phase"] == "ready" and warmup["error"] is None, "Studio not ready")
            require([v["tier"] for v in warmup["completed"]] ==
                    ["low", "medium", "high", "ultra", "high"], "Studio preparation incomplete")
            elapsed = warmup["elapsed_seconds"]
            require(math.isfinite(elapsed) and 0 < elapsed < 120, "invalid Studio duration")
            app, gpu = quality["application"], quality["gpu_submission"]
            require(quality["requested_tier"] == "high" and app["gpu_ready"]
                    and app["error"] is None and all(app["projected_domains"]), "Studio High not restored")
            require(app["identity"] == gpu["identity"] and gpu["queue_work_completed"]
                    and gpu["pipelines_pending"] == 0 and gpu["pipelines_failed"] == 0, "Studio GPU not ready")
            require(observations["weather"]["projection"]["target_state"] == weather, "wrong Studio weather")
            scene = observations["scene"]
            require(scene["parity_status"] == "source_manifest_matched", "Studio parity failed")
            sources.append(scene["runtime_manifest"]["source_hashes"])
            require((root / f"studio-{weather}.png").stat().st_size > 1000, "missing Studio capture")
            studio.append({"weather": weather, "prepared_seconds": elapsed,
                           "pipelines": warmup["completed"][-1]["pipelines"]})
        require(sources[0] == sources[1], "Studio author sources changed")
        require(read(root / "studio-cleanup.json")["closed"], "Studio processes not closed")
        result.update(studio_verified=True, studio=studio)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    print(json.dumps(verify(parser.parse_args().root), indent=2))
