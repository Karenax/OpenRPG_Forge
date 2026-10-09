"""Compare fixed-framing GPU views; image equivalence cannot prove draw event counts."""
from pathlib import Path
import json, hashlib
from datetime import datetime, timezone
import numpy as np
from PIL import Image

W = Path("F:/OpenRPG_ImportWork")
ROOT = W / "townsmith-explicit-slot-controls-v1"
REF = W / "townsmith-native-draw-slots-v3"
OUT = W / "townsmith-explicit-slot-controls-reopen-v1"

def read(p):
    return json.loads(p.read_text(encoding="utf-8-sig"))

def sha(p):
    with Path(p).open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()

def main():
    assert not OUT.exists()
    run = read(ROOT / "RUN-RESULT.v1.json")
    assert run["actual_exit_code"] == 0
    assert run["status"] == "source_townsmith_explicit_triangle_slot_controls_captured_comparison_pending"
    prepared = read(ROOT / "PREPARED.v1.json")
    pins = dict(prepared["input_pins"])
    for p, h in {**pins, **prepared["private_before_pins"]}.items():
        assert sha(p) == h, p
    context = W / "townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json"
    reference = read(context)
    # Verify retained reference views before using them, plus the scoped source receipt.
    for p, h in reference["input_pins"].items():
        assert sha(p) == h, p
    pins[str(context)] = sha(context)
    request = read(ROOT / "REQUEST.v1.json")
    report = read(ROOT / "NATIVE-RESULT.v1.json")
    assert len(request["cases"]) == len(report["rows"]) == 16
    assert report["backend"] == "Direct3D11" and report["source_objects_unchanged"]
    rows = []
    for case, actual in zip(request["cases"], report["rows"]):
        assert case["id"] == actual["id"]
        assert actual["original_material_runtime_states_unchanged"]
        assert actual["material_identities"] == [m["guid"] + ":" + m["file_id"] for m in case["materials"]]
        expected_count = case["expected_submeshes"] if case["control_mode"] == "submesh_clamp" else len(case["materials"])
        mappings = ROOT / (case["id"] + ".explicit-mapping.txt")
        observed = mappings.read_text().splitlines()
        assert len(observed) == expected_count
        for index, mapping in enumerate(observed):
            submesh = index if case["control_mode"] == "submesh_clamp" else min(index, case["expected_submeshes"] - 1)
            slot = min(index, len(case["materials"]) - 1) if case["control_mode"] == "submesh_clamp" else index
            assert mapping.startswith(f"submesh:{submesh};slot:{slot};indices:")
            assert int(mapping.rsplit(":", 1)[1]) > 0
        raw = Path(actual["raw_image"])
        source = REF / (case["id"][:7] + ".rgba16f")
        image = Path(actual["image"])
        assert raw.stat().st_size == source.stat().st_size == 512 * 512 * 8
        a = np.fromfile(raw, dtype="<f2").reshape(512, 512, 4).astype(np.float32)
        b = np.fromfile(source, dtype="<f2").reshape(512, 512, 4).astype(np.float32)
        assert np.isfinite(a).all() and np.isfinite(b).all()
        with Image.open(image) as png:
            png.load()
            assert png.size == (512, 512)
        diff = np.abs(a[:, :, :3] - b[:, :, :3])
        rows.append({
            "id": case["id"], "control_mode": case["control_mode"],
            "explicit_groups": expected_count, "mappings": observed,
            "reference_bytes_identical": sha(raw) == sha(source),
            "rgb_changed_pixels": int(np.any(diff != 0, axis=2).sum()),
            "rgb_max_absolute_error": float(diff.max()),
            "rgb_mean_absolute_error": float(diff.mean()),
            "actual_image": str(image), "source_image": str(REF / (case["id"][:7] + ".png")),
            "draw_event_count_qualified": False,
        })
        for p in [raw, source, image, mappings]:
            pins[str(p)] = sha(p)
    for p in [ROOT / n for n in ["RUN-RESULT.v1.json", "PREPARED.v1.json", "REQUEST.v1.json", "NATIVE-RESULT.v1.json", "Unity.log"]] + [Path(__file__)]:
        pins[str(p)] = sha(p)
    result = {
        "status": "sixteen_explicit_triangle_slot_gpu_controls_reopened",
        "sub_lot": "L03.TOWNSMITH.DESSINS.CONTROLES_EXPLICITES",
        "finished_utc": datetime.now(timezone.utc).isoformat(),
        "source_inputs_unchanged": True, "cases_reopened": 16,
        "comparisons": rows,
        "byte_identical_controls": sum(r["reference_bytes_identical"] for r in rows),
        "scope": "Isolated original triangle groups and copied original materials; fixed camera and HDRP settings. No full prefab transforms. Repeated identical materials may be visually indistinguishable.",
        "native_draw_event_mapping_qualified": False,
        "forge_conversion_qualified": False, "forge_editor_verified": False,
        "full_import": False, "complete_assets_delivered_increment": 0,
        "next_action": "Review GPU differences per source configuration; retain unresolved event multiplicity, then verify converted material assignment in Forge",
        "input_pins": pins,
    }
    OUT.mkdir()
    (OUT / "RESULT.v1.json").write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"status": result["status"], "controls": 16, "byte_identical": result["byte_identical_controls"], "rows": [{k:r[k] for k in ["id", "rgb_changed_pixels", "rgb_max_absolute_error"]} for r in rows]}))

if __name__ == "__main__":
    main()
