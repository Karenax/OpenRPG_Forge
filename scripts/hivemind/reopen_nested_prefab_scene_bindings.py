"""Reopen selected hierarchy source pins and record the exact prefab assembly stage."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "nested-prefab-scene-bindings-v1"
T = W / "SUIVI_LOTS_HIVEMIND"

def read(p):
    return json.loads(p.read_text("utf-8-sig"))

def sha(p):
    digest = hashlib.sha256()
    with Path(p).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()

def main():
    destination = ROOT / "SOURCE-REOPEN.v1.json"
    if destination.exists():
        raise RuntimeError("Prior reopening preserved")
    plan_path = ROOT / "RESULT.v1.json"
    plan_sha = sha(plan_path)
    plan = read(plan_path)
    if plan["errors"] or not plan["source_and_derivative_pins_unchanged"]:
        raise RuntimeError("Scene binding assembly refused")
    helper = Path(__file__).with_name("assemble_nested_prefab_bindings.py")
    helper_sha = sha(helper)
    result = subprocess.run([sys.executable, str(helper), "self-test"], capture_output=True, text=True,
                            check=True, creationflags=subprocess.CREATE_NO_WINDOW)
    controls = json.loads(result.stdout)["assembly_controls_passed"]
    if len(controls) != 7 or sha(helper) != helper_sha:
        raise RuntimeError("Seven actual assembly controls absent or helper changed")
    hierarchy = read(W / "hivemind-prefab-instance-source-closure-v1/HIERARCHY-REOPEN.v1.json")
    slugs = {row["pack_id"].removeprefix("hivemind.") for row in plan["rows"]}
    selected = {}
    for path, expected in hierarchy["proof_pins"].items():
        normalized = path.replace("\\", "/")
        if any(f"/{slug}/" in normalized or f"/{slug}." in normalized for slug in slugs):
            if sha(path) != expected:
                raise RuntimeError(f"Original hierarchy dependency changed: {path}")
            selected[path] = expected
    if not selected:
        raise RuntimeError("No original hierarchy dependencies selected")
    for path, expected in plan["source_pins"].items():
        if sha(path) != expected:
            raise RuntimeError(f"Assembly input changed: {path}")
    if sha(plan_path) != plan_sha:
        raise RuntimeError("Assembly receipt changed")
    proof = {"status": "selected_prefab_hierarchy_and_geometry_bindings_reopened", "checked_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
             "plan_sha256": plan_sha, "prefabs": plan["prefabs"], "static_renderer_bindings": plan["static_renderer_bindings"],
             "deferred_nonstatic_renderers": plan["deferred_nonstatic_renderers"], "source_dependency_pins": selected,
             "actual_controls_exit_code": result.returncode, "assembly_controls_passed": controls,
             "helper_sha256": helper_sha, "source_and_assembly_pins_unchanged": True,
             "material_programs_qualified": False, "glb_created": False, "studio_verified": False, "full_import": False}
    destination.write_text(json.dumps(proof, indent=2, ensure_ascii=False), "utf-8")
    state_path = T / "ETAT.v1.json"
    before = state_path.read_bytes()
    state = json.loads(before)
    lot = next(row for row in state["batches"] if row["id"] == "L03")
    lot["native_prefab_scene_bindings"] = {k: v for k, v in proof.items() if k != "source_dependency_pins"}
    lot["native_prefab_scene_bindings"]["receipt"] = str(destination)
    state["updated_utc"] = proof["checked_utc"]
    temporary = state_path.with_name(state_path.name + f".{os.getpid()}.tmp")
    temporary.write_text(json.dumps(state, indent=2, ensure_ascii=False), "utf-8")
    if state_path.read_bytes() != before:
        raise RuntimeError("Concurrent state update preserved")
    os.replace(temporary, state_path)
    text = (f"\n## L03 — assemblage des prefabs — {proof['checked_utc']}\n"
            f"{proof['prefabs']} prefabs : {proof['static_renderer_bindings']} correspondances renderer/mesh exactes, hiérarchies, LOD et ordre des matériaux conservés. "
            f"{proof['deferred_nonstatic_renderers']} renderers non statiques conservés pour leurs adaptateurs L05/L06. "
            f"Sept contrôles d'assemblage réussis ; {len(selected)} dépendances source des hiérarchies réouvertes. Reçu {destination}. "
            "Matériaux, GLB finaux et Studio encore non qualifiés.\n")
    for path in [T / "PLAN_ET_SUIVI.md", T / "JOURNAL.md", W / "RUN_STATUS.md"]:
        with path.open("a", encoding="utf-8") as output:
            output.write(text)
    print(json.dumps({k: v for k, v in proof.items() if k != "source_dependency_pins"}))

if __name__ == "__main__":
    main()
