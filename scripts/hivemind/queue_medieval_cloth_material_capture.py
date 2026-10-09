"""Queue exact cloth material bindings in a separate startup-HDRP project."""
import json
from pathlib import Path
import re
import os
import subprocess
import sys

import queue_hdrp_snapshot_v4 as capture

ROOT = capture.W / "unity-medieval-cloth-material-source-v1"
TEMPLATE = capture.W / "unity-dungeon-original-shadergraph-hdrp-startup-v4"
PLAN = capture.W / "medieval-cloth-source-bindings-plan-v1/PLAN.v1.json"
SOURCE = capture.W / "hivemind-run/medieval-kingdom/source"
capture.ROOT = ROOT


def prepare():
    if ROOT.exists():
        raise ValueError("Previous material capture preparation preserved")
    plan = capture.read(PLAN)
    capture.check(plan["input_pins"])
    project = ROOT / "project"
    ROOT.mkdir()
    capture.save("RUN-RESULT.v1.json", {"status": "preparing_three_original_materials", "native_verified": False, "full_import": False})
    template_project = TEMPLATE / "project"
    pins = {str(PLAN): capture.sha(PLAN), str(Path(__file__).resolve()): capture.sha(__file__), str(Path(capture.__file__).resolve()): capture.sha(capture.__file__), **plan["input_pins"]}
    # Pipeline assets are generated probe context, never claimed as purchased
    # original project settings. Purchased sources are copied separately below.
    context_files = []
    for folder in ["Packages", "ProjectSettings", "Assets/OpenRPGProbe", "Assets/HDRPDefaultResources"]:
        context_files.extend(path for path in (template_project / folder).rglob("*") if path.is_file())
    context_files.extend(path for path in (template_project / "Assets").iterdir() if path.is_file())
    context_pins = {}
    for source in context_files:
        if source.suffix.lower() == ".cs" or source.is_symlink():
            raise ValueError("Context must not import scripts or links")
        target = project / source.relative_to(template_project)
        target.parent.mkdir(parents=True, exist_ok=True)
        expected = capture.sha(source)
        target.write_bytes(source.read_bytes())
        if capture.sha(source) != expected or capture.sha(target) != expected:
            raise ValueError("Unstable context copy")
        context_pins[str(target)] = expected
    files = set()
    requests = []
    for prefab in plan["prefabs"]:
        material = prefab["material_slots"][0]
        files.update([Path(prefab["path"]), Path(material["path"]), Path(material["shader"]["path"]), Path(plan["fbx_mesh_source"]["path"])])
        required = []
        for ref in material["serialized_texture_references"]:
            if ref["status"] == "source_path_resolved":
                files.add(Path(ref["path"]))
                required.append({"property": ref["property"], "guid": ref["guid"], "file_id": ref["file_id"]})
        requests.append({"material_guid": material["guid"], "asset_path": Path(material["path"]).relative_to(SOURCE).as_posix(), "expected_shader_guid": material["shader"]["guid"], "expected_shader_file_id": material["shader"]["file_id"], "required_textures": required})
    shader = Path(plan["prefabs"][0]["material_slots"][0]["shader"]["path"])
    # Retain any available graph texture defaults, even though the three exact
    # materials override the five declared texture properties.
    defaults = set(re.findall(r'\\"guid\\":\\"([a-f0-9]{32})\\"', shader.read_text()))
    default_resolution = {}
    for meta in SOURCE.rglob("*.meta"):
        match = re.search(r"^guid:\s*([a-f0-9]{32})\s*$", meta.read_text("utf-8-sig"), re.M)
        if match and match.group(1) in defaults:
            asset = Path(str(meta)[:-5])
            if asset.is_file():
                if match.group(1) in default_resolution:
                    raise ValueError("Ambiguous graph texture default")
                default_resolution[match.group(1)] = str(asset)
                files.add(asset)
    for source in sorted(files):
        if source.suffix.lower() in [".cs", ".dll"]:
            raise ValueError("Purchased executable dependency refused")
        for asset in [source, Path(str(source) + ".meta")]:
            expected = capture.sha(asset)
            target = project / asset.relative_to(SOURCE)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(asset.read_bytes())
            pins[str(asset)] = expected
            pins[str(target)] = expected
    helper_source = template_project / "Assets/Editor/OriginalGraphMaterialSnapshotHDRP.cs"
    helper = project / "Assets/Editor/OriginalGraphMaterialSnapshotHDRP.cs"
    helper.parent.mkdir(parents=True, exist_ok=True)
    helper.write_bytes(helper_source.read_bytes())
    pins[str(helper)] = capture.sha(helper_source)
    capture.save("manifest.v1.json", {"output_path": str(ROOT / "NATIVE-RESULT.v1.json"), "cases": requests})
    pins[str(ROOT / "manifest.v1.json")] = capture.sha(ROOT / "manifest.v1.json")
    # The exact same helper already passed the actual installed Unity API compile.
    api = capture.read(TEMPLATE / "API-COMPILE.v4.json")
    if api["actual_exit_code"] != 0 or pins[str(helper)] != api["helper_sha256"]:
        raise ValueError("Actual compiled helper parity absent")
    pins[str(TEMPLATE / "API-COMPILE.v4.json")] = capture.sha(TEMPLATE / "API-COMPILE.v4.json")
    capture.check(pins)
    capture.check(context_pins)
    capture.save("PREPARED.v1.json", {"project": str(project), "input_pins": pins, "context_pins": context_pins, "requested_materials": 3, "available_graph_defaults": default_resolution, "unresolved_graph_defaults": sorted(defaults - default_resolution.keys()), "helper_actual_api_compile_exit_code": 0, "original_project_pipeline_configuration": False})
    with (ROOT / "controller.log").open("xb") as log:
        child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "run"], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    capture.save("JOB.v1.json", {"controller_pid": child.pid, "created_utc": capture.now()})
    print(json.dumps({"queued_pid": child.pid, "requested_materials": 3, "root": str(ROOT)}))


def run():
    state = {"status": "waiting_for_exact_hdrp_v4_controller", "started_utc": capture.now(), "native_materials_verified": 0, "gpu_verified": False, "studio_verified": False, "full_import": False}
    capture.save("RUN-RESULT.v1.json", state)
    try:
        wait = "$ErrorActionPreference='Stop';while($true){$p=Get-Process -Id 3384 -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne 639269632496501426L){break};$p.WaitForExit(45000)|Out-Null};while(Get-Process -Name Unity -ErrorAction SilentlyContinue){Start-Sleep -Seconds 30}"
        subprocess.run([str(capture.PS), "-NoProfile", "-Command", wait], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        prep = capture.read(ROOT / "PREPARED.v1.json")
        capture.check(prep["input_pins"])
        capture.check(prep["context_pins"])
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(capture.OLD / "upm-cache"))
        args = [r"F:\Unity\6000.5.8f1\Editor\Unity.exe", "-batchmode", "-force-d3d11", "-quit", "-projectPath", prep["project"], "-executeMethod", "OpenRPGOriginalGraphHDRP.OriginalGraphMaterialSnapshotHDRP.Export", "--openrpg-manifest", str(ROOT / "manifest.v1.json"), "-logFile", str(ROOT / "unity.log")]
        with (ROOT / "unity.stdout.log").open("xb") as out, (ROOT / "unity.stderr.log").open("xb") as err:
            unity = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status="capturing_three_original_cloth_materials", unity_pid=unity.pid)
            capture.save("RUN-RESULT.v1.json", state)
            state["actual_unity_exit_code"] = unity.wait()
        capture.check({p: h for p, h in prep["input_pins"].items() if not p.startswith(prep["project"]) or not p.endswith(".meta")})
        native = capture.read(ROOT / "NATIVE-RESULT.v1.json")
        state.update(captured=native["captured"], failed=native["failed"], native_result_sha256=capture.sha(ROOT / "NATIVE-RESULT.v1.json"))
        if state["actual_unity_exit_code"] != 0 or native["requested"] != 3 or native["captured"] != 3 or native["failed"] or len(native["cases"]) != 3:
            raise ValueError("Complete native cloth material capture absent")
        if native["graphics_api"] == "Null" or native["selected_pipeline_type"] != "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset":
            raise ValueError("Actual native HDRP GPU compiler context absent")
        manifest = capture.read(ROOT / "manifest.v1.json")
        for request, row in zip(manifest["cases"], native["cases"]):
            if row["material_guid"] != request["material_guid"] or row["actual_material_file_id"] != "2100000" or row["shader_guid"] != request["expected_shader_guid"] or row["shader_file_id"] != request["expected_shader_file_id"] or row["active_render_pipeline_tag"] != "HDRenderPipeline":
                raise ValueError("Original HDRP identity differs")
            if row["error"] or row["shader_has_errors"] or not row["shader_supported"] or not row["compile_passes_executed"] or not row["passes"] or any(not p["compiled"] or not p["name"] or p["name"].startswith("<Unnamed") for p in row["passes"]):
                raise ValueError("Original native source passes unverified")
            properties = {p["name"]: p for p in row["properties"]}
            for texture in request["required_textures"]:
                actual = properties[texture["property"]]
                if actual["texture_null"] or not actual["texture_exact_identity"] or actual["texture_guid"] != texture["guid"] or actual["texture_file_id"] != texture["file_id"]:
                    raise ValueError("Original native texture binding differs")
        state.update(status="three_original_cloth_material_passes_and_textures_captured", native_materials_verified=3, independent_reopening_required=True)
    except Exception as error:
        state.update(status="refused", error=str(error))
    state["finished_utc"] = capture.now()
    capture.save("RUN-RESULT.v1.json", state)
    print(json.dumps(state))


if __name__ == "__main__":
    {"prepare": prepare, "run": run}[sys.argv[1]]()
