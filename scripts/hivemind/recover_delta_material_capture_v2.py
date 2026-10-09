"""Recover both material packs in new startup-HDRP projects.

A failed pack keeps its diagnostics and does not prevent examining the other
independent pack. An original source hash mismatch still stops the whole job.
"""
import os
from pathlib import Path
import subprocess
import sys

import queue_hdrp_snapshot_v4 as common

ROOT = common.W / "unity-delta-null-material-defaults-v2"
OLD = common.W / "unity-delta-null-material-defaults-v1"
TEMPLATE = common.W / "unity-dungeon-original-shadergraph-hdrp-startup-v4"
common.ROOT = ROOT


def prepare():
    if ROOT.exists():
        raise ValueError("Previous recovery preserved")
    old_path = OLD / "PREPARED.v1.json"
    old = common.read(old_path)
    if old["requested_materials"] != 13 or len(old["projects"]) != 2:
        raise ValueError("Exact thirteen-material scope required")
    api = common.read(TEMPLATE / "API-COMPILE.v4.json")
    template = TEMPLATE / "project"
    helper_source = template / "Assets/Editor/OriginalGraphMaterialSnapshotHDRP.cs"
    if api["actual_exit_code"] != 0 or common.sha(helper_source) != api["helper_sha256"]:
        raise ValueError("Actual installed API compile required")
    ROOT.mkdir()
    common.save("RUN-RESULT.v1.json", {"status": "preparing_two_recovery_projects", "native_materials_verified": 0, "full_import": False})
    pins = {str(old_path): common.sha(old_path), str(Path(__file__).resolve()): common.sha(__file__), str(Path(common.__file__).resolve()): common.sha(common.__file__), str(TEMPLATE / "API-COMPILE.v4.json"): common.sha(TEMPLATE / "API-COMPILE.v4.json")}
    contexts = []
    for folder in ["Packages", "ProjectSettings", "Assets/OpenRPGProbe", "Assets/HDRPDefaultResources"]:
        contexts.extend(path for path in (template / folder).rglob("*") if path.is_file())
    contexts.extend(path for path in (template / "Assets").iterdir() if path.is_file())
    projects = []
    for item in old["projects"]:
        pack_root = ROOT / item["pack_id"].removeprefix("hivemind.")
        project = pack_root / "project"
        project.mkdir(parents=True)
        (pack_root / "temp").mkdir()
        context_pins = {}
        private_pins = {}
        for source in contexts:
            if source.is_symlink() or source.suffix.lower() == ".cs":
                raise ValueError("Context executable/link excluded")
            expected = common.sha(source)
            target = project / source.relative_to(template)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())
            if common.sha(source) != expected or common.sha(target) != expected:
                raise ValueError("Context changed while copying")
            context_pins[str(target)] = expected
        for copied in old["copies"]:
            if copied["pack_id"] != item["pack_id"]:
                continue
            source = Path(copied["original"])
            if source.suffix.lower() in [".cs", ".dll", ".exe", ".ps1", ".js", ".bat", ".cmd"]:
                raise ValueError("Purchased executable input excluded")
            if common.sha(source) != copied["sha256"]:
                raise ValueError("Original dependency changed")
            target = project / Path(copied["copy"]).relative_to(Path(item["project"]))
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())
            pins[str(source)] = copied["sha256"]
            private_pins[str(target)] = copied["sha256"]
        helper = project / "Assets/Editor/OriginalGraphMaterialSnapshotHDRP.cs"
        helper.parent.mkdir(parents=True, exist_ok=True)
        helper.write_bytes(helper_source.read_bytes())
        private_pins[str(helper)] = api["helper_sha256"]
        manifest = common.read(item["manifest"])
        pins[item["manifest"]] = common.sha(item["manifest"])
        output = pack_root / "NATIVE-RESULT.v1.json"
        manifest["output_path"] = str(output)
        path = pack_root / "manifest.v1.json"
        with path.open("x", encoding="utf-8") as file:
            import json
            json.dump(manifest, file, indent=2)
        pins[str(path)] = common.sha(path)
        common.check(context_pins)
        common.check(private_pins)
        projects.append({"pack_id": item["pack_id"], "project": str(project), "manifest": str(path), "native_result": str(output), "expected": len(manifest["cases"]), "context_pins": context_pins, "private_pins": private_pins})
    if sum(p["expected"] for p in projects) != 13:
        raise ValueError("Recovery coverage changed")
    common.check(pins)
    common.save("PREPARED.v1.json", {"requested_materials": 13, "projects": projects, "source_pins": pins, "previous_refusal": str(OLD / "RUN-RESULT.v1.json"), "original_project_pipeline_configuration": False, "full_import": False})
    with (ROOT / "controller.log").open("xb") as log:
        process = subprocess.Popen([sys.executable, "-B", str(Path(__file__).resolve()), "run"], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    common.save("JOB.v1.json", {"controller_pid": process.pid, "created_utc": common.now()})
    print({"queued_pid": process.pid, "requested_materials": 13, "projects": 2})


def verify(item):
    native = common.read(item["native_result"])
    requests = common.read(item["manifest"])["cases"]
    if native["requested"] != item["expected"] or native["captured"] != item["expected"] or native["failed"] or len(native["cases"]) != item["expected"] or native["graphics_api"] == "Null" or native["selected_pipeline_type"] != "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset":
        raise ValueError("Complete actual HDRP material coverage absent")
    for request, row in zip(requests, native["cases"]):
        if row["material_guid"] != request["material_guid"] or row["actual_material_file_id"] != "2100000" or row["shader_guid"] != request["expected_shader_guid"] or row["shader_file_id"] != request["expected_shader_file_id"] or row["active_render_pipeline_tag"] != "HDRenderPipeline":
            raise ValueError("Original material/shader identity differs")
        if row["error"] or row["shader_has_errors"] or not row["shader_supported"] or not row["compile_passes_executed"] or not row["passes"] or any(not p["compiled"] or not p["name"] or p["name"].startswith("<Unnamed") for p in row["passes"]):
            raise ValueError("Original native passes unverified")


def run():
    state = {"status": "waiting_for_exact_villa_api_controller", "native_materials_verified": 0, "projects": [], "gpu_render_parity_qualified": False, "studio_verified": False, "full_import": False}
    common.save("RUN-RESULT.v1.json", state)
    try:
        wait = "$ErrorActionPreference='Stop';while($true){$p=Get-Process -Id 2432 -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne 639269653290947925L){break};$p.WaitForExit(45000)|Out-Null};while(Get-Process -Name Unity -ErrorAction SilentlyContinue){Start-Sleep -Seconds 30}"
        subprocess.run([str(common.PS), "-NoProfile", "-Command", wait], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        prep = common.read(ROOT / "PREPARED.v1.json")
        common.check(prep["source_pins"])
        for item in prep["projects"]:
            common.check(prep["source_pins"])
            common.check(item["context_pins"])
            common.check(item["private_pins"])
            result = {"pack_id": item["pack_id"], "expected_materials": item["expected"], "verified_materials": 0}
            folder = Path(item["native_result"]).parent
            try:
                env = os.environ.copy()
                env.update(TEMP=str(folder / "temp"), TMP=str(folder / "temp"), UPM_CACHE_ROOT=str(common.OLD / "upm-cache"))
                args = [r"F:\Unity\6000.5.8f1\Editor\Unity.exe", "-batchmode", "-force-d3d11", "-quit", "-projectPath", item["project"], "-executeMethod", "OpenRPGOriginalGraphHDRP.OriginalGraphMaterialSnapshotHDRP.Export", "--openrpg-manifest", item["manifest"], "-logFile", str(folder / "unity.log")]
                with (folder / "unity.stdout.log").open("xb") as out, (folder / "unity.stderr.log").open("xb") as err:
                    unity = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
                    state.update(status="capturing_recovery_material_pack", current_pack=item["pack_id"], unity_pid=unity.pid)
                    common.save("RUN-RESULT.v1.json", state)
                    result["actual_unity_exit_code"] = unity.wait()
                if result["actual_unity_exit_code"] != 0:
                    raise ValueError("Native Unity refused; preserve project logs")
                verify(item)
                result.update(status="native_original_material_passes_captured", verified_materials=item["expected"], native_receipt=item["native_result"], native_receipt_sha256=common.sha(item["native_result"]))
                state["native_materials_verified"] += item["expected"]
            except Exception as error:
                result.update(status="refused", error=str(error))
            common.check(prep["source_pins"])
            state["projects"].append(result)
            common.save("RUN-RESULT.v1.json", state)
        state["status"] = "13_original_material_passes_captured_reopening_required" if state["native_materials_verified"] == 13 else "partial_or_refused_material_capture_retained"
    except Exception as error:
        state.update(status="refused", error=str(error))
    state["finished_utc"] = common.now()
    common.save("RUN-RESULT.v1.json", state)
    print(state)


if __name__ == "__main__":
    {"prepare": prepare, "run": run}[sys.argv[1]]()
